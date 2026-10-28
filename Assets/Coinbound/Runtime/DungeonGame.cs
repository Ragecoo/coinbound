using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using ProceduralGeneration;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace Coinbound
{
    /// <summary>Temporary start screen and the runtime generation lifecycle.</summary>
    public sealed class DungeonGame : MonoBehaviour
    {
        [Header("Original generator settings (sections of 3 x 3 tiles)")]
        [SerializeField] private int width = 39;
        [SerializeField] private int height = 19;
        [SerializeField] private bool randomSeed = true;
        [SerializeField] private int seed = 42;
        [SerializeField, Range(0, 1)] private double loopChance = 0.08;
        [Header("Placeholder presentation")]
        [SerializeField, Min(0)] private float minimumStatusSeconds = 0.12f;
        [SerializeField, Min(1)] private float cameraSize = 6.5f;
        [SerializeField, Min(1)] private float minimumCameraSize = 3.5f;
        [SerializeField, Min(1)] private float maximumCameraSize = 12;
        [Header("Landmarks")]
        [Tooltip("Масштаб входа; коллизия увеличивается вместе со спрайтом.")]
        [SerializeField, Range(1, 2)] private float entranceScale = 1.5f;
        [Tooltip("Масштаб фонтана; коллизия увеличивается вместе со спрайтом.")]
        [SerializeField, Range(1, 2)] private float fountainScale = 1.25f;
        [Tooltip("Смещение входа вверх в тайлах. При текущих размерах 1 оставляет примерно тайл между входом и фонтаном.")]
        [SerializeField, Min(0)] private float entranceLift = 1;
        [Tooltip("Максимальная длина небольшой тени входа и фонтана за их краем, в тайлах. Обзор не перекрывает.")]
        [SerializeField, Range(0, 1.5f)] private float landmarkShadowLength = 0.65f;
        [Tooltip("Видимая плотность коротких теней от входа и фонтана. Учитывает цветовое пространство; не ослабляется повторно с расстоянием.")]
        [SerializeField, Range(0, 1)] private float landmarkShadowOpacity = 0.35f;
        [Tooltip("Мягкость края тени объектов, в тайлах. Край остаётся на пиксельной сетке игры.")]
        [SerializeField, Range(0.03125f, 0.25f)] private float landmarkShadowSoftness = 0.08f;
        [Header("Player torch")]
        [Tooltip("Радиус освещения в тайлах. На дальность слабой видимости не влияет.")]
        [SerializeField, Min(1)] private float torchRadius = 8;
        [Tooltip("Яркость света возле персонажа.")]
        [SerializeField, Min(0)] private float torchIntensity = 1.15f;
        [Tooltip("Оттенок света; близкий к белому сохраняет исходную палитру текстур.")]
        [SerializeField] private Color torchColor = new Color(1, 0.97f, 0.94f, 1);
        [Tooltip("Мягкость краёв теней в тайлах. Освещение остаётся на пиксельной сетке текстур.")]
        [SerializeField, Range(0.05f, 1)] private float shadowSoftness = 0.35f;
        [Tooltip("Усиление падающего на стены света относительно пола.")]
        [SerializeField, Min(1)] private float wallLightMultiplier = 2.5f;
        [Tooltip("Дополнительная подсветка стен, у которых пол находится непосредственно снизу. 1.5 = ещё на 50% ярче.")]
        [SerializeField, Min(1)] private float upperWallLightMultiplier = 1.5f;
        [Tooltip("Глубина затухания света за краем стены вдоль луча, в тайлах. Меньше — видно меньше стены.")]
        [SerializeField, Range(0.1f, 3)] private float wallLightDepth = 0.8f;
        [Header("Darkness / distant visibility")]
        [Tooltip("Цвет полной темноты и фона за пределами видимости.")]
        [SerializeField] private Color darknessColor = new Color32(16, 16, 16, 255);
        [Tooltip("Яркость текстур вне радиуса света, пока их не закрывают стены.")]
        [SerializeField, Range(0, 0.15f)] private float unlitVisibility = 0.035f;

        private DungeonScreen screen;
        private Camera gameCamera;
        private DungeonView dungeon;
        private DungeonPlayer player;
        private Coroutine loading;
        private bool menuOpen = true;
        private bool choosingHero;
        private bool generating;
        private float targetCameraSize;
        private float displayedCameraSize;

        public DungeonMap Map { get; private set; }
        public SpawnPlan Spawns { get; private set; }
        public int CurrentSeed { get; private set; }
        public DungeonHero SelectedHero { get; private set; } = DungeonHero.First;
        public bool IsReady => player != null && !generating;

        private void Awake()
        {
            targetCameraSize = displayedCameraSize = Mathf.Clamp(cameraSize, minimumCameraSize, maximumCameraSize);
            gameCamera = Camera.main;
            if (gameCamera == null)
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                gameCamera = cameraObject.GetComponent<Camera>();
            }
            gameCamera.orthographic = true;
            gameCamera.orthographicSize = displayedCameraSize;
            gameCamera.clearFlags = CameraClearFlags.SolidColor;
            gameCamera.backgroundColor = darknessColor;
            gameCamera.transform.position = new Vector3(0, 0, -10);
            screen = new GameObject("Placeholder UI").AddComponent<DungeonScreen>();
            screen.transform.SetParent(transform, false);
            screen.Initialize(ShowHeroSelection, Resume, ShowMenu, CancelGeneration, StartWithHero);
            screen.ShowMenu(false);
        }

        private void Update()
        {
            if (IsReady && !menuOpen && Keyboard.current != null && Mouse.current != null &&
                (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed))
            {
                float scroll = Mouse.current.scroll.y.ReadValue();
                // Accept both the Input System's normalized ticks and native 120-unit wheel deltas.
                float ticks = Mathf.Abs(scroll) >= 120 ? scroll / 120 : scroll;
                targetCameraSize = Mathf.Clamp(targetCameraSize * Mathf.Exp(-Mathf.Clamp(ticks, -4, 4) * 0.12f),
                    minimumCameraSize, maximumCameraSize);
            }
            if (Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
            if (generating) CancelGeneration();
            else if (choosingHero) ShowMenu();
            else if (menuOpen && IsReady) Resume();
            else ShowMenu();
        }

        private void LateUpdate()
        {
            if (player == null) return;
            // Restrict the entire viewport, including on resize / ultrawide displays.
            displayedCameraSize = Mathf.Lerp(displayedCameraSize, targetCameraSize, 1 - Mathf.Exp(-14 * Time.unscaledDeltaTime));
            float aspect = Mathf.Max(0.01f, gameCamera.aspect);
            float halfHeight = Mathf.Min(displayedCameraSize, Map.TileHeight * 0.5f, Map.TileWidth / (2 * aspect));
            float halfWidth = halfHeight * aspect;
            gameCamera.orthographicSize = halfHeight;
            Vector3 position = player.transform.position;
            gameCamera.transform.position = new Vector3(
                Mathf.Clamp(Mathf.Round(position.x * 32) / 32, halfWidth, Map.TileWidth - halfWidth),
                Mathf.Clamp(Mathf.Round(position.y * 32) / 32, 1 - Map.TileHeight + halfHeight, 1 - halfHeight), -10);
        }

        public void ShowHeroSelection()
        {
            if (generating) return;
            choosingHero = true;
            menuOpen = true;
            if (player != null) player.SetInputEnabled(false);
            screen.ShowHeroSelection(SelectedHero);
        }

        private void StartWithHero(DungeonHero hero)
        {
            if (!choosingHero || generating) return;
            SelectedHero = hero ?? DungeonHero.First;
            StartNewDungeon();
        }

        // Also used by development helpers to generate directly with the current hero.
        public void StartNewDungeon()
        {
            if (generating) return;
            ClearDungeon();
            CurrentSeed = randomSeed ? Guid.NewGuid().GetHashCode() : seed;
            menuOpen = false;
            generating = true;
            screen.ShowLoading(CurrentSeed);
            loading = StartCoroutine(GuardGeneration());
        }

        private IEnumerator GuardGeneration()
        {
            // Catch errors from every iterator step, including nested rendering work,
            // so a missing asset never leaves the player stuck on the loading screen.
            using var work = GenerateDungeon();
            while (true)
            {
                object next;
                try
                {
                    if (!work.MoveNext()) break;
                    next = work.Current;
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                    generating = false;
                    loading = null;
                    ClearDungeon();
                    menuOpen = true;
                    screen.ShowError("Не удалось создать подземелье. Попробуйте ещё раз.");
                    yield break;
                }
                yield return next;
            }
            generating = false;
            loading = null;
            player.SetInputEnabled(true);
            screen.ShowGame(CurrentSeed, Spawns.Enemies.Count, Spawns.Chests.Count);
        }

        private IEnumerator<object> GenerateDungeon()
        {
            yield return null; // Present the loading screen before doing any work.
            var generator = new DungeonGenerator(new GenerationSettings(width, height, CurrentSeed,
                LoopChance: loopChance));
            string previousStatus = null;
            var budget = Stopwatch.StartNew();
            foreach (GenerationStep step in generator.Generate())
            {
                if (step.Stage == GenerationStage.Complete) break;
                // Reserve the final 18% for rendering and the separate SpawnPlanner.
                screen.SetProgress(step.Progress * 0.0082f, step.Status);
                if (step.Status != previousStatus)
                {
                    previousStatus = step.Status;
                    yield return new WaitForSecondsRealtime(minimumStatusSeconds);
                    budget.Restart();
                }
                else if (budget.ElapsedMilliseconds >= 3)
                {
                    yield return null;
                    budget.Restart();
                }
            }

            Map = generator.Map;
            screen.SetProgress(0.82f, "Подготовка тайлов подземелья...");
            yield return null;
            TileSpriteMap tiles = TileSpriteMap.Create(Map, CurrentSeed);
            dungeon = new GameObject("Generated Dungeon").AddComponent<DungeonView>();
            dungeon.transform.SetParent(transform, false);
            using (var render = dungeon.Build(Map, tiles, CurrentSeed, entranceScale, fountainScale, entranceLift,
                       fraction => screen.SetProgress(0.82f + fraction * 0.1f, "Подготовка тайлов подземелья...")))
                while (render.MoveNext()) yield return render.Current;

            screen.SetProgress(0.92f, "Планирование спавна врагов и предметов...");
            yield return new WaitForSecondsRealtime(minimumStatusSeconds);
            // Smaller packs and fewer corridor encounters; keep the original planner unchanged.
            var spawnSettings = new SpawnSettings
            {
                RoomMin = 4, RoomMax = 5,
                CorridorMin = 2, CorridorMax = 3,
                CorridorPacks = 5, Singles = 2
            };
            Spawns = new SpawnPlanner(Map, spawnSettings, CurrentSeed).Generate();
            using (var spawn = dungeon.PlaceSpawns(Spawns))
                while (spawn.MoveNext()) yield return spawn.Current;
            screen.SetProgress(0.98f, "Размещение персонажа...");
            player = dungeon.CreatePlayer(dungeon.FindPlayerSpawn(Map), SelectedHero);
            player.SetInputEnabled(false);
            dungeon.AddPlayerLight(Map, player.transform, torchRadius, torchIntensity, torchColor,
                darknessColor, unlitVisibility, shadowSoftness, wallLightMultiplier, upperWallLightMultiplier, wallLightDepth,
                landmarkShadowLength, landmarkShadowOpacity, landmarkShadowSoftness);
            Physics2D.SyncTransforms();
            foreach (string note in Spawns.Notes) Debug.Log(note);
            screen.SetProgress(1, "Подземелье готово!");
            yield return new WaitForSecondsRealtime(0.25f);
        }

        public void ShowMenu()
        {
            if (generating) return;
            choosingHero = false;
            menuOpen = true;
            if (player != null) player.SetInputEnabled(false);
            screen.ShowMenu(IsReady);
        }

        public void Resume()
        {
            if (!IsReady) return;
            choosingHero = false;
            menuOpen = false;
            player.SetInputEnabled(true);
            screen.ShowGame(CurrentSeed, Spawns.Enemies.Count, Spawns.Chests.Count);
        }

        public void CancelGeneration()
        {
            if (loading != null) StopCoroutine(loading);
            loading = null;
            generating = false;
            ClearDungeon();
            ShowMenu();
        }

        private void ClearDungeon()
        {
            if (dungeon != null)
            {
                dungeon.gameObject.SetActive(false);
                Destroy(dungeon.gameObject);
            }
            dungeon = null;
            player = null;
            Map = null;
            Spawns = null;
            choosingHero = false;
        }
    }
}
