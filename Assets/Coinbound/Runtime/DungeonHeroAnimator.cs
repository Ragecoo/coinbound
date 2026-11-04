using System;
using System.Collections.Generic;
using UnityEngine;

namespace Coinbound
{
    /// <summary>Plays native pixel frames around the same foot anchor in every pose.</summary>
    [DisallowMultipleComponent]
    public sealed class DungeonHeroAnimator : MonoBehaviour
    {
        public const int FrameWidth = 32;
        public const int FrameHeight = 64;
        public const int FramesPerAnimation = 8;
        public const int AnimationRows = 5;
        public const float PixelsPerUnit = 32;
        public const float VisualScale = 0.8f;
        public const float HandHeight = 0.85f * VisualScale;
        public const float PaletteSaturation = 0.68f;
        public const float PaletteBrightness = 0.96f;
        public static readonly Color PaletteTint = new Color(0.88f, 0.94f, 1.08f);
        public static readonly Color32 SilhouetteInk = new Color32(24, 26, 43, 255);

        // Art is authored with its ground line eight pixels above each cell's bottom.
        // Using a fixed cell/pivot (rather than each pose's alpha bounds) prevents foot jumps.
        private static readonly Vector2 FootPivot = new Vector2(0.5f, 0.125f);
        private static readonly Dictionary<string, HeroFrames> Cache = new Dictionary<string, HeroFrames>(StringComparer.Ordinal);
        private static readonly string[] DirectionNames = { "down", "left", "right", "up" };

        private enum Pose { Idle, Run, Attack, Roll, Block }
        private enum Facing { Down, Left, Right, Up }

        private DungeonPlayer player;
        private SpriteRenderer visual;
        private HeroFrames frames;
        private Pose pose;
        private Facing facing = Facing.Down;
        private float phase;
        private float activeTime;
        private bool capeSettling;
        private int observedBowShot;
        private float bowRecoilRemaining;
        private Vector2 bowRecoilDirection;
        private Color baseColor = Color.white;

        private const float BowRecoilDuration = 0.12f;
        private const float BowRecoilPixels = 2;

        /// <summary>
        /// The visual is a child placed at the physics root's foot position, at unit scale.
        /// Sprite frames and their derived textures are shared for the play session.
        /// </summary>
        public void Initialize(DungeonPlayer owner, SpriteRenderer renderer)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));
            player = owner;
            visual = renderer;
            frames = GetFrames(owner.Hero);
            baseColor = visual.color;
            pose = Pose.Idle;
            facing = Facing.Down;
            phase = activeTime = 0;
            capeSettling = false;
            observedBowShot = owner.BowShotSequence;
            ResetBowRecoil();
            visual.flipX = false;
            visual.flipY = false;
            ShowFrame(0);
        }

        /// <summary>
        /// Returns a borrowed, cached down-facing idle sprite. UI callers must not destroy
        /// this sprite or its texture. Created sprites/textures are released at play-session reset/quit.
        /// </summary>
        public static Sprite GetPortrait(DungeonHero hero) => GetFrames(hero).Get(Facing.Down, Pose.Idle, 0);
        public static Sprite GetLoadingFrame(DungeonHero hero, int frame) => GetFrames(hero).Get(Facing.Right, Pose.Run, frame % FramesPerAnimation);

        private void LateUpdate()
        {
            if (player == null || visual == null || frames == null) return;
            // Menus pause combat independently of timeScale. Do not advance idle, aim,
            // invulnerability flicker or committed animation while that input gate is closed.
            if (!player.CanAct && !player.IsDead) return;
            if (player.IsDead)
            {
                // Retain the last authored pose, rather than moving the feet on death.
                ResetBowRecoil();
                visual.color = baseColor * new Color(0.6f, 0.62f, 0.7f, 0.82f);
                return;
            }
            // Combat hit-stop is local to this actor; retain the displayed frame and
            // recoil offset without changing global time or consuming animation phase.
            if (player.IsHitStopped) return;

            float delta = Time.deltaTime;
            if (delta <= 0) return;
            activeTime += delta;

            Pose next = player.IsRolling ? Pose.Roll
                : player.IsAttacking ? Pose.Attack
                : player.IsBlocking && !player.Hero.IsArcher ? Pose.Block
                : player.Velocity.sqrMagnitude > 0.01f ? Pose.Run : Pose.Idle;

            if (next != pose)
            {
                // The latter half of the authored idle loop brings the cloak back to
                // its resting pose. Never keep playing running legs after the body stops.
                capeSettling = next == Pose.Idle && pose == Pose.Run && DungeonFxSettings.Current.capeMotion;
                pose = next;
                phase = capeSettling ? 4 : 0;
            }
            else if (capeSettling && !DungeonFxSettings.Current.capeMotion)
                capeSettling = false;

            Vector2 look = pose == Pose.Attack || pose == Pose.Roll ? player.ActionDirection
                : pose == Pose.Run ? player.Velocity : player.AimDirection;
            facing = ResolveFacing(look, facing);

            int frame;
            if (pose == Pose.Attack)
                frame = ProgressFrame(player.AttackProgress);
            else if (pose == Pose.Roll)
                frame = ProgressFrame(player.RollProgress);
            else
            {
                float framesPerSecond = pose == Pose.Run ? 12 : pose == Pose.Block ? 8 : capeSettling ? 10 : 6;
                if (pose == Pose.Run)
                    framesPerSecond *= Mathf.Clamp(player.Velocity.magnitude / Mathf.Max(0.01f, player.MoveSpeed), 0.2f, 1.5f);
                float advancedPhase = phase + delta * framesPerSecond;
                if (capeSettling && advancedPhase >= FramesPerAnimation) capeSettling = false;
                phase = Mathf.Repeat(advancedPhase, FramesPerAnimation);
                frame = Mathf.FloorToInt(phase);
            }

            ShowFrame(frame);
            AdvanceBowRecoil(delta);
            Color hit = Color.Lerp(Color.white, new Color(1, 0.4f, 0.4f), Mathf.Clamp01(player.HitFlash));
            // Keep every roll pose readable; brief post-hit immunity uses a restrained pulse.
            float opacity = player.IsInvulnerable && !player.IsRolling
                ? 0.72f + 0.28f * (0.5f + 0.5f * Mathf.Cos(activeTime * Mathf.PI * 16)) : 1;
            hit.a = opacity;
            visual.color = baseColor * hit;
        }

        private void ShowFrame(int index)
        {
            Sprite sprite = frames.Get(facing, pose, index);
            if (visual.sprite != sprite) visual.sprite = sprite;
            // MaterialPropertyBlocks remain owned by DungeonView's foot-based lighting.
        }

        private void AdvanceBowRecoil(float delta)
        {
            // A release counter follows actual projectiles, including consecutive held
            // shots; animation progress alone would repeat or miss interrupted attacks.
            bool released = observedBowShot != player.BowShotSequence;
            observedBowShot = player.BowShotSequence;
            if (!DungeonFxSettings.Current.bowRecoil || !player.Hero.IsArcher || player.IsRolling)
            {
                ResetBowRecoil();
                return;
            }

            if (released)
            {
                bowRecoilRemaining = BowRecoilDuration;
                bowRecoilDirection = -player.ActionDirection.normalized;
            }
            else
                bowRecoilRemaining = Mathf.Max(0, bowRecoilRemaining - delta);

            float pixels = BowRecoilPixels * (bowRecoilRemaining / BowRecoilDuration);
            // Move only the child art in whole native pixels. The foot collider, depth
            // sorting and light anchor keep using the unchanged physics root.
            visual.transform.localPosition = new Vector3(
                Mathf.Round(bowRecoilDirection.x * pixels) / PixelsPerUnit,
                Mathf.Round(bowRecoilDirection.y * pixels) / PixelsPerUnit, 0);
        }

        private void ResetBowRecoil()
        {
            bowRecoilRemaining = 0;
            bowRecoilDirection = Vector2.zero;
            if (visual != null) visual.transform.localPosition = Vector3.zero;
        }

        private void OnDisable()
        {
            ResetBowRecoil();
            capeSettling = false;
            if (player != null) observedBowShot = player.BowShotSequence;
        }

        private static int ProgressFrame(float progress) =>
            Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(progress) * FramesPerAnimation), 0, FramesPerAnimation - 1);

        private static Facing ResolveFacing(Vector2 direction, Facing previous)
        {
            if (direction.sqrMagnitude < 0.0001f) return previous;
            float x = Mathf.Abs(direction.x), y = Mathf.Abs(direction.y);
            // A narrow hysteresis around diagonals prevents alternating left/up sprites
            // when the cursor or analog stick moves by a fraction of a degree.
            const float hysteresis = 0.86f;
            if ((previous == Facing.Left && direction.x < 0 || previous == Facing.Right && direction.x > 0)
                && x >= y * hysteresis) return previous;
            if ((previous == Facing.Down && direction.y < 0 || previous == Facing.Up && direction.y > 0)
                && y >= x * hysteresis) return previous;
            return x >= y ? direction.x < 0 ? Facing.Left : Facing.Right
                : direction.y < 0 ? Facing.Down : Facing.Up;
        }

        private static HeroFrames GetFrames(DungeonHero hero)
        {
            hero = hero ?? DungeonHero.First;
            if (!Cache.TryGetValue(hero.ArtFolder, out HeroFrames result))
            {
                result = new HeroFrames(hero.ArtFolder);
                Cache.Add(hero.ArtFolder, result);
            }
            return result;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            ClearCache();
            // Also works when the Editor's Enter Play Mode options disable domain reload.
            Application.quitting -= ClearCache;
            Application.quitting += ClearCache;
        }

        private static void ClearCache()
        {
            foreach (HeroFrames value in Cache.Values) value.Dispose();
            Cache.Clear();
        }

        private sealed class HeroFrames : IDisposable
        {
            private readonly Sprite[,,] sprites = new Sprite[4, AnimationRows, FramesPerAnimation];
            private readonly Texture2D[] sheets = new Texture2D[4];

            public HeroFrames(string folder)
            {
                try
                {
                    for (int direction = 0; direction < DirectionNames.Length; direction++)
                    {
                        string path = "HeroArt/" + folder + "/" + DirectionNames[direction];
                        Texture2D source = Resources.Load<Texture2D>(path);
                        if (source == null)
                            throw new InvalidOperationException("Missing hero animation sheet: Resources/" + path + ".png");
                        if (source.width != FrameWidth * FramesPerAnimation || source.height != FrameHeight * AnimationRows)
                            throw new InvalidOperationException("Hero animation sheet must be 256 x 320 pixels: " + path);
                        if (!source.isReadable)
                            throw new InvalidOperationException("Hero animation sheet must have Read/Write enabled: " + path);

                        Texture2D sheet = PrepareSheet(source, path);
                        sheets[direction] = sheet;

                        for (int row = 0; row < AnimationRows; row++)
                        for (int column = 0; column < FramesPerAnimation; column++)
                        {
                            // Sheet rows are documented top-to-bottom; Unity rects start at the bottom.
                            var rect = new Rect(column * FrameWidth, (AnimationRows - row - 1) * FrameHeight,
                                FrameWidth, FrameHeight);
                            Sprite sprite = Sprite.Create(sheet, rect, FootPivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
                            sprite.name = folder + "/" + DirectionNames[direction] + "/" + (Pose)row + "/" + column;
                            sprite.hideFlags = HideFlags.DontSave;
                            sprites[direction, row, column] = sprite;
                        }
                    }
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public Sprite Get(Facing direction, Pose animation, int frame) => sprites[(int)direction, (int)animation, frame];

            private static Texture2D PrepareSheet(Texture2D source, string path)
            {
                Color32[] original = source.GetPixels32();
                var reduced = new Color32[original.Length];
                var palette = new Dictionary<uint, Color32>();
                Vector2 pivot = new Vector2(FootPivot.x * FrameWidth, FootPivot.y * FrameHeight);
                for (int row = 0; row < AnimationRows; row++)
                for (int column = 0; column < FramesPerAnimation; column++)
                for (int y = 0; y < FrameHeight; y++)
                for (int x = 0; x < FrameWidth; x++)
                {
                    // Shrink the native pixel content, not the world geometry. Each visible
                    // pixel stays 1/32 world units, with the same feet pivot in every pose.
                    int sampleX = Mathf.FloorToInt((x + 0.5f - pivot.x) / VisualScale + pivot.x);
                    int sampleY = Mathf.FloorToInt((y + 0.5f - pivot.y) / VisualScale + pivot.y);
                    if (sampleX < 0 || sampleX >= FrameWidth || sampleY < 0 || sampleY >= FrameHeight) continue;
                    Color32 color = original[(row * FrameHeight + sampleY) * source.width + column * FrameWidth + sampleX];
                    if (color.a == 0) continue;
                    color = HarmonizeColor(color);
                    uint key = unchecked((uint)(color.r | color.g << 8 | color.b << 16 | color.a << 24));
                    if (!palette.TryGetValue(key, out Color32 shaded))
                    {
                        // These are sRGB bytes: grade the shared art once so UI portraits
                        // and the lit world use exactly the same restrained indigo palette.
                        Vector3 rgb = new Vector3(color.r, color.g, color.b) / 255;
                        float gray = Vector3.Dot(rgb, new Vector3(0.2126f, 0.7152f, 0.0722f));
                        rgb = Vector3.Lerp(Vector3.one * gray, rgb, PaletteSaturation);
                        shaded = new Color(Mathf.Clamp01(rgb.x * PaletteTint.r * PaletteBrightness),
                            Mathf.Clamp01(rgb.y * PaletteTint.g * PaletteBrightness),
                            Mathf.Clamp01(rgb.z * PaletteTint.b * PaletteBrightness), color.a / 255f);
                        palette.Add(key, shaded);
                    }
                    reduced[(row * FrameHeight + y) * source.width + column * FrameWidth + x] = shaded;
                }
                CleanClustersAndEdges(reduced, source.width);
                var sheet = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
                {
                    name = path + " native 80%",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave
                };
                sheet.SetPixels32(reduced);
                sheet.Apply(false, false);
                return sheet;
            }

            private static Color32 HarmonizeColor(Color32 color)
            {
                // Merge only near-duplicate swatches from the original shared palette.
                // Fixed choices keep cloak/leather colors identical across all directions.
                int rgb = color.r << 16 | color.g << 8 | color.b;
                switch (rgb)
                {
                    case 0x542E41:
                    case 0x56333C: return new Color32(0x49, 0x34, 0x3D, color.a);
                    case 0x705037: return new Color32(0x75, 0x58, 0x3E, color.a);
                    case 0x46354D: return new Color32(0x51, 0x41, 0x57, color.a);
                    case 0x796483: return new Color32(0x81, 0x71, 0x8B, color.a);
                    case 0x844258: return new Color32(0x71, 0x37, 0x4C, color.a);
                    case 0x3C5153:
                    case 0x486167: return new Color32(0x4C, 0x53, 0x5E, color.a);
                    case 0x52696A: return new Color32(0x5C, 0x65, 0x71, color.a);
                    default: return color;
                }
            }

            private static void CleanClustersAndEdges(Color32[] pixels, int width)
            {
                var before = (Color32[])pixels.Clone();
                int height = pixels.Length / width;
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    Color32 color = before[index];
                    if (color.a == 0) continue;
                    // Never sample the neighbouring frame in a packed sprite sheet.
                    Color32 left = x % FrameWidth > 0 ? before[index - 1] : default;
                    Color32 right = x % FrameWidth < FrameWidth - 1 ? before[index + 1] : default;
                    Color32 down = y % FrameHeight > 0 ? before[index - width] : default;
                    Color32 up = y % FrameHeight < FrameHeight - 1 ? before[index + width] : default;
                    bool l = left.a != 0, r = right.a != 0, d = down.a != 0, u = up.a != 0;
                    bool brightDetail = color.r >= 180 || color.g >= 180 || color.b >= 180;
                    if (brightDetail) continue; // Keep eyes, faces, and one-pixel steel highlights.
                    if (!(l && r && d && u))
                    {
                        // An inside-only contour cannot enlarge the body or move its feet.
                        // Preserve thin bow/string/weapon lines instead of turning them solid ink.
                        int neighbours = (l ? 1 : 0) + (r ? 1 : 0) + (d ? 1 : 0) + (u ? 1 : 0);
                        bool thinLine = neighbours <= 1 || neighbours == 2 && (l && r || d && u);
                        if (!thinLine) pixels[index] = new Color32(SilhouetteInk.r, SilhouetteInk.g, SilhouetteInk.b, color.a);
                        continue;
                    }
                    // Remove a weak, isolated speck only when three direct neighbours
                    // already agree. No averaging, blur, or changes to opaque coverage.
                    Color32 majority = Same(left, right) && (Same(left, down) || Same(left, up)) ? left
                        : Same(down, up) && (Same(down, left) || Same(down, right)) ? down : color;
                    int dr = color.r - majority.r, dg = color.g - majority.g, db = color.b - majority.b;
                    if (dr * dr + dg * dg + db * db <= 24 * 24) pixels[index] = majority;
                }
            }

            private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

            public void Dispose()
            {
                foreach (Sprite sprite in sprites)
                {
                    if (sprite == null) continue;
                    if (Application.isPlaying) UnityEngine.Object.Destroy(sprite);
                    else UnityEngine.Object.DestroyImmediate(sprite);
                }
                foreach (Texture2D sheet in sheets)
                {
                    if (sheet == null) continue;
                    if (Application.isPlaying) UnityEngine.Object.Destroy(sheet);
                    else UnityEngine.Object.DestroyImmediate(sheet);
                }
                // Only derived textures are ours. Source textures loaded by Resources stay Unity-owned.
            }
        }
    }
}
