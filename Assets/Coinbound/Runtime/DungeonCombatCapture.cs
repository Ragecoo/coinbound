#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProceduralGeneration;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Coinbound
{
    /// <summary>Explicit development-only rendering of the real player, physics, shader and UI.</summary>
    [DefaultExecutionOrder(10000)]
    internal sealed class DungeonCombatCapture : MonoBehaviour
    {
        private const int CaptureWidth = 1280;
        private const int CaptureHeight = 720;
        private const int FramesPerSecond = 24;
        private static readonly int GroundLightId = Shader.PropertyToID("_GroundLightPosition");
        private static readonly string[] Directions = { "down", "left", "right", "up" };
        private static readonly Vector2[] DirectionVectors = { Vector2.down, Vector2.left, Vector2.right, Vector2.up };
        private static readonly Key[] DirectionKeys = { Key.S, Key.A, Key.D, Key.W };
        public static bool IsRunning { get; private set; }

        private string output;
        private bool effectsOnly;
        private bool lightPairCaptured;
        private DungeonGame game;
        private DungeonPlayer player;
        private Camera camera;
        private Keyboard keyboard;
        private Mouse mouse;
        private RenderTexture target;
        private Texture2D pixels;
        private RenderTexture previousTarget;
        private float previousCameraSize;
        private Vector3 previousCameraPosition;
        private bool previousGameEnabled;
        private bool previousBackground;
        private int previousFrameRate;
        private int previousCaptureRate;
        private InputSettings.BackgroundBehavior previousInputBackground;
        private bool prepared;
        private string pendingFrame;
        private Exception renderError;
        private readonly List<InputDevice> disabledDevices = new List<InputDevice>();
        private readonly List<CanvasState> canvases = new List<CanvasState>();
        private readonly List<DungeonEnemy> stagedEnemies = new List<DungeonEnemy>();
        private readonly Dictionary<DungeonEnemy, EnemyState> stagedBodies = new Dictionary<DungeonEnemy, EnemyState>();
        private readonly Report report = new Report();
        private Clip currentClip;
        private readonly HashSet<string> clipSprites = new HashSet<string>();

        [Serializable] private sealed class Report
        {
            public int width = CaptureWidth;
            public int height = CaptureHeight;
            public int framesPerSecond = FramesPerSecond;
            public int seed = 42;
            public string mode = "Development-only real runtime capture; enemies hidden during isolated animation clips; following review camera at exactly 3x native world pixels.";
            public List<string> resources = new List<string>();
            public List<Clip> clips = new List<Clip>();
            public List<FrameRecord> frames = new List<FrameRecord>();
            public string lightComparisonSource;
            public int lightComparisonCount;
        }
        [Serializable] private sealed class Clip
        {
            public string path;
            public string hero;
            public int frames;
            public int distinctSpriteFrames;
            public List<string> visibleEffects = new List<string>();
            public int maximumEffectPixels;
            public int maximumProjectiles;
            public int maximumStuckProjectiles;
            public bool bloodObserved;
        }
        [Serializable] private sealed class FrameRecord
        {
            public string path;
            public string hero;
            public string pose;
            public Vector2 feet;
            public Vector2 frameCell;
            public Vector4 groundLight;
            public int sortingOrder;
            public float health;
            public List<string> visibleEffects;
            public int effectPixels;
            public int projectiles;
            public bool blood;
            public float blockCooldown;
            public float rollCooldown;
            public List<ArrowRecord> arrows;
        }
        [Serializable] private sealed class ArrowRecord
        {
            public Vector2 feet;
            public Vector2 visibleTip;
            public Vector2 direction;
            public bool stuck;
        }
        private sealed class EnemyState
        {
            public Rigidbody2D body;
            public bool simulated;
            public RigidbodyConstraints2D constraints;
            public Vector2 position;
        }
        private sealed class CanvasState
        {
            public Canvas canvas;
            public RenderMode mode;
            public Camera camera;
            public float planeDistance;
            public bool overrideSorting;
            public int sortingOrder;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int fxFlag = Array.IndexOf(args, "-coinbound-fx-capture");
            int flag = fxFlag >= 0 ? fxFlag : Array.IndexOf(args, "-coinbound-combat-capture");
            if (flag < 0 || flag + 1 >= args.Length || IsRunning) return;
            IsRunning = true;
            var capture = new GameObject("Combat visual capture").AddComponent<DungeonCombatCapture>();
            capture.output = Path.GetFullPath(args[flag + 1]);
            capture.effectsOnly = fxFlag >= 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCaptureState() => IsRunning = false;

        private IEnumerator Start()
        {
            // Flatten nested iterators so an error in a clip also reaches cleanup and a nonzero exit code.
            var work = new Stack<IEnumerator>();
            work.Push(Run());
            Exception failure = null;
            while (work.Count > 0 && failure == null)
            {
                object next = null;
                try
                {
                    if (renderError != null) throw renderError;
                    if (!work.Peek().MoveNext())
                    {
                        (work.Pop() as IDisposable)?.Dispose();
                        continue;
                    }
                    next = work.Peek().Current;
                    if (next is IEnumerator nested) { work.Push(nested); continue; }
                }
                catch (Exception error) { failure = error; }
                if (failure == null) yield return next;
            }
            while (work.Count > 0) (work.Pop() as IDisposable)?.Dispose();
            if (failure == null)
            {
                try
                {
                    File.WriteAllText(Path.Combine(output, "capture-manifest.json"), JsonUtility.ToJson(report, true));
                    Debug.Log($"Coinbound combat capture completed: {report.frames.Count} gameplay frames, {report.clips.Count} clips at {output}");
                }
                catch (Exception error) { failure = error; }
            }
            if (failure != null)
            {
                Debug.LogException(failure);
                try { File.WriteAllText(Path.Combine(output, "capture-failure.txt"), failure.ToString()); }
                catch (Exception writeError) { Debug.LogError(writeError.Message); }
            }
            Cleanup();
            Application.Quit(failure == null ? 0 : 1);
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(output);
            Prepare();
            ValidateResources();
            if (effectsOnly)
                report.mode = "Development-only retained combat effects: white sword trail, warm hit/block/stone sparks, four wall contacts and embedded arrows; no ambient/audio capture or cancelled effects enabled. Real combat and following camera at 3x native pixels.";
            JsonUtility.FromJsonOverwrite("{\"randomSeed\":false,\"seed\":42,\"minimumStatusSeconds\":0.01}", game);
            yield return Settle(4);
            yield return Capture("00-menu");
            for (int heroIndex = 0; heroIndex < DungeonHero.All.Count; heroIndex++)
            {
                DungeonHero hero = DungeonHero.All[heroIndex];
                game.enabled = previousGameEnabled;
                game.ShowHeroSelection();
                yield return Settle(2);
                RequireButton("Hero " + heroIndex).onClick.Invoke();
                yield return Settle(2);
                yield return Capture(hero.ArtFolder + "-selection");
                RequireButton("Create dungeon").onClick.Invoke();
                yield return Settle(2);
                yield return Capture(hero.ArtFolder + "-loading");
                float timeout = Time.realtimeSinceStartup + 60;
                while (!game.IsReady && Time.realtimeSinceStartup < timeout) yield return null;
                if (!game.IsReady) throw new InvalidOperationException("Dungeon generation did not complete for " + hero.DisplayName);
                player = FindAnyObjectByType<DungeonPlayer>();
                if (player == null || player.Hero != hero) throw new InvalidOperationException("Hero selection did not reach the spawned player.");
                foreach (DungeonEnemy enemy in FindObjectsByType<DungeonEnemy>())
                {
                    stagedEnemies.Add(enemy);
                    enemy.gameObject.SetActive(false);
                }
                game.enabled = false;
                SetReviewCamera(player.transform.position);
                QueueMouse(Vector2.down);
                yield return Settle(3);
                ValidatePlayer();
                yield return Capture(hero.ArtFolder + "-entrance");

                DungeonRoom room = FindReviewRoom(game.Map);
                Vector2 arena = RoomCenter(room);
                if (effectsOnly)
                {
                    yield return CaptureEffects(arena, room);
                    game.ShowMenu();
                    yield return Settle(2);
                    player = null;
                    continue;
                }
                foreach (string pose in new[] { "idle", "run", "attack", "roll" })
                for (int direction = 0; direction < Directions.Length; direction++)
                {
                    yield return ResetActor(arena, DirectionVectors[direction]);
                    string path = hero.ArtFolder + "/" + pose + "-" + Directions[direction];
                    int frames = pose == "idle" ? 32 : pose == "run" ? 16 : pose == "attack" ? 12 : 10;
                    if (pose == "run")
                    {
                        QueueKeyboard(DirectionKeys[direction]);
                        yield return Settle(2);
                    }
                    else if (pose == "attack" && !player.TryAttack(DirectionVectors[direction]))
                        throw new InvalidOperationException("Capture could not start attack: " + path);
                    else if (pose == "roll" && !player.TryRoll(DirectionVectors[direction]))
                        throw new InvalidOperationException("Capture could not start roll: " + path);
                    yield return CaptureClip(path, frames);
                    QueueKeyboard();
                }
                Vector2 diagonalAim = new Vector2(1, 0.65f).normalized;
                yield return ResetActor(arena, diagonalAim);
                if (!player.TryAttack(diagonalAim)) throw new InvalidOperationException("Could not capture a diagonal cursor-directed attack.");
                yield return CaptureClip(hero.ArtFolder + "/attack-diagonal", 12);
                if (!hero.IsArcher)
                {
                    yield return ResetActor(arena, Vector2.down);
                    QueueMouse(Vector2.down, true);
                    yield return Settle(2);
                    if (!player.IsBlocking) throw new InvalidOperationException("RMB did not activate the swordsman's block.");
                    yield return CaptureClip(hero.ArtFolder + "/block-down", 26);
                    QueueMouse(Vector2.down);
                }

                // Feet are adjacent to the north wall while the tall head overlaps its tile row.
                Vector2 besideWall = new Vector2(arena.x, 1 - room.TileTop - 0.3f);
                yield return ResetActor(besideWall, Vector2.up);
                yield return Capture(hero.ArtFolder + "-foot-lighting-north-wall");
                SpriteRenderer fountain = GameObject.Find("object.fountain").GetComponent<SpriteRenderer>();
                Collider2D fountainBase = fountain.GetComponent<Collider2D>();
                foreach (bool behind in new[] { true, false })
                {
                    Vector2 point = new Vector2(fountain.transform.position.x,
                        behind ? fountainBase.bounds.max.y + 0.24f : fountainBase.bounds.min.y - 0.24f);
                    yield return ResetActor(point, behind ? Vector2.down : Vector2.up);
                    SpriteRenderer heroVisual = player.GetComponentInChildren<SpriteRenderer>();
                    if (behind && heroVisual.sortingOrder >= fountain.sortingOrder ||
                        !behind && heroVisual.sortingOrder <= fountain.sortingOrder)
                        throw new InvalidOperationException("Incorrect fountain/hero depth ordering.");
                    yield return Capture(hero.ArtFolder + (behind ? "-behind-fountain" : "-in-front-of-fountain"));
                }
                game.ShowMenu();
                yield return Settle(2);
                player = null;
            }
            if (effectsOnly && !lightPairCaptured)
                throw new InvalidOperationException("No lit parry frame was available for the same-frame light-on/light-off comparison.");
        }

        private void Prepare()
        {
            game = FindAnyObjectByType<DungeonGame>();
            camera = Camera.main;
            if (game == null || camera == null) throw new InvalidOperationException("The SampleScene game and main camera are required.");
            previousGameEnabled = game.enabled;
            previousBackground = Application.runInBackground;
            previousFrameRate = Application.targetFrameRate;
            previousCaptureRate = Time.captureFramerate;
            previousInputBackground = InputSystem.settings.backgroundBehavior;
            previousTarget = camera.targetTexture;
            previousCameraSize = camera.orthographicSize;
            previousCameraPosition = camera.transform.position;
            prepared = true;
            Application.quitting += Cleanup;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            Time.captureFramerate = FramesPerSecond;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            foreach (InputDevice device in InputSystem.devices.ToArray())
            {
                if (!device.enabled || !(device is Keyboard || device is Mouse || device is Gamepad)) continue;
                disabledDevices.Add(device);
                InputSystem.DisableDevice(device);
            }
            keyboard = InputSystem.AddDevice<Keyboard>("CoinboundCaptureKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("CoinboundCaptureMouse");
            keyboard.MakeCurrent();
            mouse.MakeCurrent();
            target = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            pixels = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
            target.Create();
            camera.targetTexture = target;
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                canvases.Add(new CanvasState
                {
                    canvas = canvas, mode = canvas.renderMode, camera = canvas.worldCamera,
                    planeDistance = canvas.planeDistance, overrideSorting = canvas.overrideSorting, sortingOrder = canvas.sortingOrder
                });
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 30000 + canvas.sortingOrder;
            }
        }

        private void ValidateResources()
        {
            foreach (DungeonHero hero in DungeonHero.All)
            foreach (string direction in Directions)
            {
                string resource = "HeroArt/" + hero.ArtFolder + "/" + direction;
                Texture2D sheet = Resources.Load<Texture2D>(resource);
                if (sheet == null || sheet.width != 256 || sheet.height != 320 || sheet.filterMode != FilterMode.Point)
                    throw new InvalidOperationException("Expected a Point-filtered 256 x 320 hero sheet: " + resource);
                report.resources.Add(resource + " (256x320, Point)");
            }
        }

        private void ValidatePlayer()
        {
            BoxCollider2D feet = player.GetComponent<BoxCollider2D>();
            SpriteRenderer visual = player.GetComponentInChildren<SpriteRenderer>();
            if (feet == null || Vector2.Distance(feet.size, Vector2.one * 0.36f) > 0.001f || feet.offset.sqrMagnitude > 0.0001f)
                throw new InvalidOperationException("Hero must have the centered 0.36 x 0.36 square feet collider.");
            if (visual == null || visual.sprite == null || visual.sprite.rect.size != new Vector2(32, 64))
                throw new InvalidOperationException("Hero visual must use a native 32 x 64 animation frame.");
            if (visual.sprite.pivot != new Vector2(16, 8))
                throw new InvalidOperationException("Hero frame pivot moved from the common feet anchor.");
            var block = new MaterialPropertyBlock();
            visual.GetPropertyBlock(block);
            Vector4 ground = block.GetVector(GroundLightId);
            if (ground.w < 0.5f || Vector2.Distance(ground, player.transform.position) > 0.01f)
                throw new InvalidOperationException("Tall hero lighting must sample its feet position.");
        }

        private static DungeonRoom FindReviewRoom(DungeonMap map)
        {
            foreach (DungeonRoom room in map.Rooms.OrderByDescending(room => room.TileWidth * room.TileHeight))
            {
                Vector2 center = RoomCenter(room);
                if (!DungeonCombatPhysics.FreeFeet(center, 0.25f)) continue;
                if (DirectionVectors.All(direction => DungeonCombatPhysics.ClearLine(center, center + direction * 4.2f, 0.25f))) return room;
            }
            throw new InvalidOperationException("Seed 42 has no room with a clear four-direction animation path.");
        }

        private static Vector2 RoomCenter(DungeonRoom room) => new Vector2(room.TileLeft + room.TileWidth * 0.5f,
            1 - room.TileTop - room.TileHeight * 0.5f);

        private Vector2 FindWallReviewPoint(DungeonRoom room, Vector2 direction, float clearance)
        {
            bool horizontalWall = Mathf.Abs(direction.y) > 0.5f;
            int length = horizontalWall ? room.TileWidth : room.TileHeight;
            for (int step = 0; step < length; step++)
            {
                int offset = step == 0 ? 0 : (step + 1) / 2 * (step % 2 == 0 ? -1 : 1);
                int along = length / 2 + offset;
                if (along < 1 || along >= length - 1) continue;
                int x = horizontalWall ? room.TileLeft + along : direction.x > 0 ? room.TileLeft + room.TileWidth - 1 : room.TileLeft;
                int y = !horizontalWall ? room.TileTop + along : direction.y > 0 ? room.TileTop : room.TileTop + room.TileHeight - 1;
                var floorTile = new TilePosition(x, y);
                var wallTile = new TilePosition(x + Mathf.RoundToInt(direction.x), y - Mathf.RoundToInt(direction.y));
                if (!game.Map.IsTileFloor(floorTile) || game.Map.IsTileFloor(wallTile)) continue;
                Vector2 face = (Vector2)DungeonView.WorldPosition(floorTile) + direction * 0.5f;
                Vector2 point = face - direction * clearance;
                if (DungeonCombatPhysics.FreeFeet(point, 0.22f) && DungeonCombatPhysics.ClearLine(point, face - direction * 0.15f)) return point;
            }
            throw new InvalidOperationException("No solid room wall with clear review footing in direction " + direction);
        }

        private IEnumerator ResetActor(Vector2 position, Vector2 aim)
        {
            QueueKeyboard();
            QueueMouse(aim);
            yield return Settle(28); // finish attacks and the one-second roll cooldown through the real controller
            if (!DungeonCombatPhysics.FreeFeet(position, 0.22f)) throw new InvalidOperationException("Capture staging point intersects stone: " + position);
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            body.linearVelocity = Vector2.zero;
            body.position = position;
            player.transform.position = position;
            Physics2D.SyncTransforms();
            SetReviewCamera(position);
            QueueMouse(aim);
            player.SetAimDirection(aim);
            yield return Settle(3);
            ValidatePlayer();
        }

        private void SetReviewCamera(Vector2 center)
        {
            // At 720px high, 7.5 world units give exactly three screen pixels per native texture pixel.
            camera.orthographicSize = 3.75f;
            float halfWidth = camera.orthographicSize * CaptureWidth / CaptureHeight;
            camera.transform.position = new Vector3(
                Mathf.Clamp(center.x, halfWidth, game.Map.TileWidth - halfWidth),
                Mathf.Clamp(center.y + 0.65f, 1 - game.Map.TileHeight + 3.75f, 1 - 3.75f), -10);
        }

        private void QueueKeyboard(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        private void QueueMouse(Vector2 aim, bool block = false)
        {
            Vector2 position = player != null ? (Vector2)player.transform.position : Vector2.zero;
            Vector2 screen = camera.WorldToScreenPoint(position + aim * 2.5f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen, buttons = block ? (ushort)2 : (ushort)0 });
        }

        private static IEnumerator Settle(int frames)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }

        private IEnumerator CaptureEffects(Vector2 arena, DungeonRoom room)
        {
            string prefix = player.Hero.ArtFolder + "/fx-";
            Vector2 diagonal = new Vector2(1, 0.65f).normalized;
            yield return ResetActor(arena, diagonal);
            if (!player.TryAttack(diagonal)) throw new InvalidOperationException("Could not stage the attack effect.");
            yield return CaptureClip(prefix + "attack", 24, player.Hero.IsArcher ? null : "Slash", false, player.Hero.IsArcher);

            yield return ResetActor(arena, Vector2.right);
            DungeonEnemy enemy = StageEnemy(arena + Vector2.right * 1.35f);
            float enemyHealth = enemy.Health;
            if (!player.TryAttack(Vector2.right)) throw new InvalidOperationException("Could not stage an enemy hit.");
            yield return CaptureClip(prefix + "enemy-hit", 24, "Hit", false);
            if (enemy == null || enemy.Health >= enemyHealth) throw new InvalidOperationException("The staged attack did not damage its enemy.");
            ReleaseEnemy(enemy);

            yield return ResetActor(arena, Vector2.right);
            if (!player.TakeDamage(1, arena + Vector2.left)) throw new InvalidOperationException("Could not stage a player hit.");
            yield return CaptureClip(prefix + "player-hit", 24, "Hit", false);

            if (!player.Hero.IsArcher)
            {
                yield return ResetActor(arena, Vector2.right);
                QueueMouse(Vector2.right, true);
                yield return Settle(2);
                if (!player.IsBlocking) throw new InvalidOperationException("RMB did not activate block for the parry capture.");
                float health = player.Health;
                enemy = StageEnemy(arena + Vector2.right * 0.95f);
                // The real enemy windup/strike drives the actual directional block, sparks and stun.
                yield return CaptureClip(prefix + "parry", 32, "Parry", false);
                if (player.Health != health || player.BlockCooldownRemaining <= 0 ||
                    !report.clips[report.clips.Count - 1].visibleEffects.Contains("Stun"))
                    throw new InvalidOperationException("The staged melee strike did not produce a damage-free block and enemy stun.");
                QueueMouse(Vector2.right);
                ReleaseEnemy(enemy);
            }

            yield return ResetActor(arena, Vector2.right);
            DungeonCombatFx.Impact(player, arena + Vector2.right * 0.85f, new Color32(143, 156, 187, 255));
            yield return CaptureClip(prefix + "metal-impact", 24, "Impact", false);

            if (!player.Hero.IsArcher)
            {
                yield return ResetActor(FindWallReviewPoint(room, Vector2.up, 0.42f), Vector2.up);
                if (!player.TryAttack(Vector2.up)) throw new InvalidOperationException("Could not start the slash beside a wall.");
                yield return CaptureClip(prefix + "slash-north-wall", 24, "Slash", false);
            }
            else
            {
                for (int direction = 0; direction < DirectionVectors.Length; direction++)
                {
                    Vector2 heading = DirectionVectors[direction];
                    yield return ResetActor(FindWallReviewPoint(room, heading, 1.25f), heading);
                    if (!player.TryAttack(heading)) throw new InvalidOperationException("Could not shoot at the " + Directions[direction] + " wall.");
                    yield return CaptureClip(prefix + "arrow-wall-" + Directions[direction], 40, "WallHit", false, true, true);
                }
            }
        }

        private DungeonEnemy StageEnemy(Vector2 position)
        {
            DungeonEnemy enemy = stagedEnemies.FirstOrDefault(candidate => candidate != null && !candidate.IsDead &&
                candidate.transform.parent == player.transform.parent && !candidate.gameObject.activeSelf);
            if (enemy == null) throw new InvalidOperationException("No existing enemy is available for VFX staging.");
            Rigidbody2D body = enemy.GetComponent<Rigidbody2D>();
            stagedBodies[enemy] = new EnemyState
            {
                body = body, simulated = body.simulated, constraints = body.constraints, position = body.position
            };
            // Keep the real collider active for sword/arrow contact, but stop AI locomotion in this opt-in capture.
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            body.simulated = true;
            body.position = position;
            enemy.transform.position = position;
            body.linearVelocity = Vector2.zero;
            enemy.gameObject.SetActive(true);
            Physics2D.SyncTransforms();
            return enemy;
        }

        private void ReleaseEnemy(DungeonEnemy enemy)
        {
            if (enemy == null) return;
            enemy.gameObject.SetActive(false);
            if (!stagedBodies.TryGetValue(enemy, out EnemyState state)) return;
            state.body.linearVelocity = Vector2.zero;
            state.body.constraints = state.constraints;
            state.body.simulated = state.simulated;
            state.body.position = state.position;
            enemy.transform.position = state.position;
            stagedBodies.Remove(enemy);
        }

        private IEnumerator CaptureClip(string path, int frames, string requiredEffect = null, bool? blood = null, bool projectile = false, bool stuck = false)
        {
            Directory.CreateDirectory(Path.Combine(output, path));
            currentClip = new Clip { path = path, hero = player.Hero.DisplayName, frames = frames };
            clipSprites.Clear();
            for (int i = 0; i < frames; i++) yield return Capture(path + "/frame-" + i.ToString("D3"));
            currentClip.distinctSpriteFrames = clipSprites.Count;
            if (currentClip.distinctSpriteFrames < 3) throw new InvalidOperationException("Animation did not advance in " + path);
            if (requiredEffect != null && !currentClip.visibleEffects.Contains(requiredEffect))
                throw new InvalidOperationException("No visible " + requiredEffect + " pixels were captured in " + path);
            if (blood.HasValue && currentClip.bloodObserved != blood.Value)
                throw new InvalidOperationException("Incorrect blood/no-blood result in " + path);
            if (projectile && currentClip.maximumProjectiles == 0)
                throw new InvalidOperationException("No live projectile was captured in " + path);
            if (stuck && currentClip.maximumStuckProjectiles == 0)
                throw new InvalidOperationException("No arrow embedded in the wall was captured in " + path);
            report.clips.Add(currentClip);
            currentClip = null;
        }

        private IEnumerator Capture(string name)
        {
            pendingFrame = name;
            while (pendingFrame != null && renderError == null) yield return null;
            if (renderError != null) throw renderError;
        }

        private void LateUpdate()
        {
            // Follow actual motion at the fixed review scale, so a full upward run cycle cannot leave the frame.
            if (player != null && game != null && game.Map != null) SetReviewCamera(player.transform.position);
            if (pendingFrame == null || target == null) return;
            string name = pendingFrame;
            try
            {
                foreach (CanvasState state in canvases)
                    if (state.canvas != null)
                        foreach (Graphic graphic in state.canvas.GetComponentsInChildren<Graphic>()) graphic.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture active = RenderTexture.active;
                try
                {
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                    pixels.Apply(false);
                    File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
                }
                finally { RenderTexture.active = active; }
                SaveLightComparison(name);
                if (player != null)
                {
                    SpriteRenderer visual = player.GetComponentInChildren<SpriteRenderer>();
                    var block = new MaterialPropertyBlock();
                    visual.GetPropertyBlock(block);
                    DungeonCombatFx[] effects = FindObjectsByType<DungeonCombatFx>().Where(effect =>
                        effect.ActivePixelCount > 0 && effect.GetComponentsInChildren<Renderer>().Any(renderer => renderer.enabled)).ToArray();
                    List<string> visibleEffects = effects.Select(effect => effect.EffectKind).Distinct().OrderBy(kind => kind).ToList();
                    int effectPixels = effects.Sum(effect => effect.ActivePixelCount);
                    DungeonArrow[] arrows = FindObjectsByType<DungeonArrow>();
                    int projectiles = arrows.Length;
                    int stuckProjectiles = arrows.Count(arrow => arrow.IsStuck);
                    var arrowRecords = new List<ArrowRecord>(projectiles);
                    foreach (DungeonArrow arrow in arrows)
                    {
                        Vector2 pixelTip = arrow.VisibleTip * 32;
                        if (Vector2.Distance(pixelTip, new Vector2(Mathf.Round(pixelTip.x), Mathf.Round(pixelTip.y))) > 0.001f)
                            throw new InvalidOperationException("The arrow tip left the native pixel grid.");
                        arrowRecords.Add(new ArrowRecord { feet = arrow.transform.position, visibleTip = arrow.VisibleTip,
                            direction = arrow.Direction, stuck = arrow.IsStuck });
                    }
                    bool blood = effects.Any(effect => effect.ProducesBlood);
                    report.frames.Add(new FrameRecord
                    {
                        path = name + ".png", hero = player.Hero.DisplayName,
                        pose = player.IsRolling ? "roll" : player.IsAttacking ? "attack" : player.IsBlocking ? "block"
                            : player.Velocity.sqrMagnitude > 0.01f ? "run" : "idle",
                        feet = player.transform.position, frameCell = visual.sprite.rect.position,
                        groundLight = block.GetVector(GroundLightId), sortingOrder = visual.sortingOrder, health = player.Health,
                        visibleEffects = visibleEffects, effectPixels = effectPixels, projectiles = projectiles, blood = blood,
                        blockCooldown = player.BlockCooldownRemaining, rollCooldown = player.RollCooldownRemaining, arrows = arrowRecords
                    });
                    if (currentClip != null)
                    {
                        clipSprites.Add(visual.sprite.name + ":" + visual.sprite.rect.position);
                        foreach (string kind in visibleEffects)
                            if (!currentClip.visibleEffects.Contains(kind)) currentClip.visibleEffects.Add(kind);
                        currentClip.maximumEffectPixels = Mathf.Max(currentClip.maximumEffectPixels, effectPixels);
                        currentClip.maximumProjectiles = Mathf.Max(currentClip.maximumProjectiles, projectiles);
                        currentClip.maximumStuckProjectiles = Mathf.Max(currentClip.maximumStuckProjectiles, stuckProjectiles);
                        currentClip.bloodObserved |= blood;
                    }
                }
            }
            catch (Exception error) { renderError = error; }
            pendingFrame = null;
        }

        private void SaveLightComparison(string sourceFrame)
        {
            if (lightPairCaptured || player == null || !sourceFrame.Contains("/fx-parry/")) return;
            Material litMaterial = player.EffectMaterial;
            int count = litMaterial != null ? litMaterial.GetInt("_EffectLightCount") : 0;
            if (count <= 0 || !FindObjectsByType<DungeonCombatFx>().Any(effect => effect.EffectKind == "Parry" && effect.ActivePixelCount > 0)) return;
            // The normal render was just read. No coroutine, Update, clock or pose advances between this exact A/B pair.
            File.WriteAllBytes(Path.Combine(output, "light-on.png"), pixels.EncodeToPNG());
            RenderTexture previous = RenderTexture.active;
            try
            {
                litMaterial.SetInt("_EffectLightCount", 0);
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                pixels.Apply(false);
                File.WriteAllBytes(Path.Combine(output, "light-off.png"), pixels.EncodeToPNG());
            }
            finally
            {
                litMaterial.SetInt("_EffectLightCount", count);
                RenderTexture.active = previous;
            }
            report.lightComparisonSource = sourceFrame + ".png";
            report.lightComparisonCount = count;
            lightPairCaptured = true;
        }

        private static Button RequireButton(string name)
        {
            Button button = GameObject.Find(name)?.GetComponent<Button>();
            if (button == null) throw new InvalidOperationException("Capture UI button was not available: " + name);
            return button;
        }

        private void Cleanup()
        {
            pendingFrame = null;
            IsRunning = false;
            if (!prepared) return;
            prepared = false;
            Application.quitting -= Cleanup;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            foreach (InputDevice device in disabledDevices) if (device.added) InputSystem.EnableDevice(device);
            InputSystem.settings.backgroundBehavior = previousInputBackground;
            Application.runInBackground = previousBackground;
            Application.targetFrameRate = previousFrameRate;
            Time.captureFramerate = previousCaptureRate;
            foreach (CanvasState state in canvases)
            {
                if (state.canvas == null) continue;
                state.canvas.renderMode = state.mode;
                state.canvas.worldCamera = state.camera;
                state.canvas.planeDistance = state.planeDistance;
                state.canvas.overrideSorting = state.overrideSorting;
                state.canvas.sortingOrder = state.sortingOrder;
            }
            if (camera != null)
            {
                camera.targetTexture = previousTarget;
                camera.orthographicSize = previousCameraSize;
                camera.transform.position = previousCameraPosition;
            }
            if (target != null) { target.Release(); Destroy(target); }
            if (pixels != null) Destroy(pixels);
            foreach (DungeonEnemy enemy in stagedBodies.Keys.ToArray()) ReleaseEnemy(enemy);
            stagedBodies.Clear();
            foreach (DungeonEnemy enemy in stagedEnemies) if (enemy != null) enemy.gameObject.SetActive(true);
            if (game != null) { game.enabled = previousGameEnabled; game.ShowMenu(); }
            Cursor.visible = true;
        }

        private void OnDestroy() => Cleanup();
        private void OnDisable() { StopAllCoroutines(); Cleanup(); }
    }
}
#endif
