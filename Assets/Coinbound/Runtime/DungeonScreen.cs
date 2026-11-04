using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Coinbound
{
    /// <summary>Coinbound's front end and in-game interface. Gameplay remains in DungeonGame.</summary>
    public sealed partial class DungeonScreen : MonoBehaviour
    {
        private static readonly Color Ink = new Color32(9, 10, 15, 255);
        private static readonly Color Panel = new Color32(21, 21, 27, 255);
        private static readonly Color Gold = new Color32(181, 153, 111, 255);
        private static readonly Color Pale = new Color32(229, 218, 194, 255);
        private static readonly Color Muted = new Color32(140, 136, 132, 255);
        private static readonly Color Red = new Color32(155, 66, 78, 255);
        private static readonly Color Green = new Color32(105, 128, 106, 255);
        private Font font, displayFont;
        private Canvas uiCanvas;
        private GameObject menu, heroSelection, loading, hud, pause, death, settings, confirmation, current;
        private Action startAction, resumeAction, menuAction, cancelAction, titleAction;
        private Action<DungeonHero> heroAction;
        private Button startButton, resumeButton, createDungeonButton, heroBackButton;
        private Button pauseResumeButton, deathRetryButton;
        private readonly List<Button> heroButtons = new List<Button>();
        private readonly List<DungeonUiDivider> heroFrames = new List<DungeonUiDivider>();
        private readonly List<Text> heroMarkers = new List<Text>();
        private Text menuNote, status, percent, hudLabel, controlLegend, deathStats, pauseStats;
        private Text interactionPrompt;
        private RectTransform progressFill;
        private Image loadingActor;
        private float displayedScore;
        private DungeonHero selectedHero = DungeonHero.First;
        private DungeonPlayer gameplayPlayer;
        private Func<int> aliveEnemies;
        private int hudSeed, hudChests, initialEnemies, previousHealth;
        private float runSeconds, loadingProgress, targetProgress;
        private DungeonDamageBorder damageBorder;
        private Text voiceStatus;
        private Image curtainLeft, curtainRight;
        private CanvasGroup curtain;
        private Coroutine transition;
        private Selectable settingsReturnFocus, confirmReturnFocus;
        private Action confirmAction;
        private Text confirmTitle, confirmMessage;
        private Button confirmButton, confirmCancel;
        private AudioSource uiAudio;
        private AudioClip uiClick;

        public string CurrentPage => settings != null && settings.activeSelf ? "Settings" : current != null ? current.name : "None";
        public bool IsTransitioning => transition != null;
        public bool IsModalOpen => settings != null && settings.activeSelf || confirmation != null && confirmation.activeSelf;

        public void Initialize(Action start, Action resume, Action openMenu, Action cancel, Action<DungeonHero> startHero, Action title = null)
        {
            startAction = start; resumeAction = resume; menuAction = openMenu; cancelAction = cancel;
            heroAction = startHero; titleAction = title ?? (() => ShowMenu(gameplayPlayer != null && !gameplayPlayer.IsDead));
            font = Resources.Load<Font>("InterfaceFonts/OpirusOpik");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            displayFont = font;
            Font.textureRebuilt += KeepFontPixels;
            KeepFontPixels(font);
            uiCanvas = gameObject.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            uiCanvas.pixelPerfect = true; uiCanvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();
            if (EventSystem.current == null)
            {
                var events = new GameObject("Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            BuildMenu(); BuildHeroes(); BuildLoading(); BuildHud(); BuildPause(); BuildDeath(); BuildSettings(); BuildConfirmation();
            var cover = Layer("Transition", Color.clear);
            cover.SetActive(true);
            curtain = cover.AddComponent<CanvasGroup>();
            curtainLeft = Solid("Left curtain", cover.transform, Vector2.zero, Vector2.zero, Ink);
            curtainRight = Solid("Right curtain", cover.transform, Vector2.zero, Vector2.zero, Ink);
            curtain.alpha = 0; curtain.blocksRaycasts = false;
            SetupSound();
            DungeonSettings.Changed += ApplyPreferences;
            ApplyPreferences();
        }

        public void ShowMenu(bool canResume)
        {
            resumeButton.gameObject.SetActive(canResume);
            startButton.GetComponentInChildren<Text>().text = "НОВАЯ ИГРА";
            menuNote.text = "";
            Switch(menu, canResume ? resumeButton : startButton);
        }
        public void ShowPause()
        {
            pauseStats.text = $"{selectedHero.DisplayName}   ·   {TimeText(runSeconds)}\nОчки: {(gameplayPlayer != null ? gameplayPlayer.Score : 0):D9}";
            Switch(pause, pauseResumeButton);
        }
        public void ShowHeroSelection(DungeonHero hero)
        {
            ChooseHero(hero ?? DungeonHero.First);
            Switch(heroSelection, heroButtons[selectedHero.IsArcher ? 1 : 0]);
        }
        public void ShowLoading(int seed)
        {
            hudSeed = seed;
            loadingActor.sprite = DungeonHeroAnimator.GetLoadingFrame(selectedHero, 0);
            loadingProgress = targetProgress = 0;
            progressFill.anchorMax = new Vector2(0, 1);
            percent.text = "0%";
            SetProgress(0, "Возведение стен подземелья...");
            Switch(loading, null);
        }
        public void SetProgress(float value, string message)
        {
            targetProgress = Mathf.Max(targetProgress, Mathf.Clamp01(value));
            status.text = message;
        }
        public void ShowGame(int seed, int enemies, int chests)
        {
            hudSeed = seed; hudChests = chests;
            Switch(hud, null);
            RefreshHud();
        }
        public void BindPlayer(DungeonPlayer player, Func<int> enemyCount = null)
        {
            gameplayPlayer = player; aliveEnemies = enemyCount;
            previousHealth = player != null ? player.Health : 0;
            runSeconds = 0;
            displayedScore = 0;
            initialEnemies = aliveEnemies != null ? aliveEnemies() : 0;
            damageBorder?.Clear();
            if (player == null) return;
            resourceParticles.Clear();
            hpArt.SetFraction(player.Health / (float)player.MaxHealth);
            mpArt.SetFraction(player.MaxMana > 0 ? player.Mana / (float)player.MaxMana : 0);
            RefreshStats(player.Stats);
            selectedHero = player.Hero;
            controlLegend.text = "WASD";
            RefreshHud();
        }
        public void ShowDeath()
        {
            deathStats.text = $"Время: {TimeText(runSeconds)}\n\nОчки: {(gameplayPlayer != null ? gameplayPlayer.Score : 0):D9}";
            Switch(death, deathRetryButton);
        }
        public void ShowError(string message) { ShowMenu(false); menuNote.text = message; }
        public void RequestNewJourney()
        {
            if (gameplayPlayer != null && !gameplayPlayer.IsDead)
                Confirm("НАЧАТЬ ЗАНОВО?", "Текущее подземелье будет потеряно при новом спуске.", startAction, "Выбрать героя");
            else startAction();
        }
        public bool HandleBack()
        {
            if (confirmation.activeSelf) { CloseConfirmation(); return true; }
            if (settings.activeSelf) { CloseSettings(); return true; }
            return false;
        }

        private void Switch(GameObject target, Selectable focus)
        {
            CloseSettings(false); CloseConfirmation(false);
            foreach (GameObject page in new[] { menu, heroSelection, loading, hud, pause, death }) page.SetActive(page == target);
            bool wasVisible = current != null;
            current = target;
            Select(focus);
            if (transition != null) StopCoroutine(transition);
            transition = null;
            curtain.alpha = 0; curtain.blocksRaycasts = false;
            if (wasVisible && !DungeonSettings.ReducedMotion) transition = StartCoroutine(Reveal());
        }
        private IEnumerator Reveal()
        {
            curtain.alpha = 1; curtain.blocksRaycasts = true;
            float elapsed = 0;
            const float duration = .42f;
            while (elapsed < duration)
            {
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                RectTransform left = curtainLeft.rectTransform, right = curtainRight.rectTransform;
                left.anchorMin = Vector2.zero; left.anchorMax = new Vector2(.5f * (1 - t), 1);
                right.anchorMin = new Vector2(1 - .5f * (1 - t), 0); right.anchorMax = Vector2.one;
                left.offsetMin = left.offsetMax = right.offsetMin = right.offsetMax = Vector2.zero;
                curtain.alpha = 1 - t * .4f;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            curtain.alpha = 0; curtain.blocksRaycasts = false; transition = null;
        }
        private void ChooseHero(DungeonHero hero)
        {
            selectedHero = hero;
            for (int i = 0; i < heroButtons.Count; i++)
            {
                bool chosen = DungeonHero.All[i] == hero;
                heroFrames[i].color = i == 0 ? new Color(.6f, .21f, .25f, chosen ? .8f : .15f) : new Color(.22f, .38f, .55f, chosen ? .8f : .15f);
                heroMarkers[i].text = chosen ? "ВЫБРАН" : "";
                heroMarkers[i].color = chosen ? Pale : Muted;
            }
            if (createDungeonButton != null) createDungeonButton.GetComponentInChildren<Text>().text = "НАЧАТЬ ИГРУ";
        }
        private int Defeated => Mathf.Max(0, initialEnemies - (aliveEnemies != null ? aliveEnemies() : initialEnemies));
        private static string TimeText(float value) => $"{(int)value / 60:00}:{(int)value % 60:00}";
        private void LateUpdate()
        {
            if (gameplayPlayer != null && gameplayPlayer.CanAct) runSeconds += Time.deltaTime;
            if (hud.activeSelf) RefreshHud();
            if (loading.activeSelf)
            {
                loadingProgress = Mathf.MoveTowards(loadingProgress, targetProgress, Time.unscaledDeltaTime * 1.2f);
                progressFill.anchorMax = new Vector2(loadingProgress, 1);
                percent.text = Mathf.FloorToInt(loadingProgress * 100) + "%";
                if (!DungeonSettings.ReducedMotion) loadingActor.sprite = DungeonHeroAnimator.GetLoadingFrame(selectedHero, Mathf.FloorToInt(Time.unscaledTime * 10));
                loadingActor.rectTransform.anchoredPosition = new Vector2(Mathf.Round(Mathf.Lerp(-160, 160, loadingProgress)), 45);
            }
        }
        private void RefreshHud()
        {
            if (gameplayPlayer == null) return;
            interactionPrompt.gameObject.SetActive(gameplayPlayer.InteractionTarget != null);
            RefreshAuthoredHud();
            displayedScore = Mathf.MoveTowards(displayedScore, gameplayPlayer.Score,
                Mathf.Max(30, (gameplayPlayer.Score - displayedScore) * 5) * Time.unscaledDeltaTime);
            hudLabel.text = Mathf.FloorToInt(displayedScore).ToString("D9");
            if (gameplayPlayer.Health < previousHealth && DungeonSettings.DamageFlash) damageBorder.Pulse();
            if (gameplayPlayer.Health > previousHealth && DungeonSettings.DamageFlash) damageBorder.Heal();
            previousHealth = gameplayPlayer.Health;

        }
        private void ApplyPreferences()
        {
            if (controlLegend != null) controlLegend.gameObject.SetActive(DungeonSettings.ShowHints);
            if (!DungeonSettings.DamageFlash) damageBorder?.Clear();
        }
        private void SetupSound()
        {
            uiAudio = gameObject.AddComponent<AudioSource>(); uiAudio.playOnAwake = false; uiAudio.ignoreListenerPause = true;
            const int count = 2205;
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / 22050f;
                samples[i] = (Mathf.Sin(t * 1130 * Mathf.PI * 2) + .25f * Mathf.Sin(t * 1700 * Mathf.PI * 2)) * Mathf.Exp(-t * 65) * .09f;
            }
            uiClick = AudioClip.Create("Bronze UI tick", count, 1, 22050, false); uiClick.SetData(samples, 0);
        }
        private void Click() { if (uiAudio != null) uiAudio.PlayOneShot(uiClick); }
        private static void Select(Selectable control)
        {
            EventSystem.current?.SetSelectedGameObject(null);
            if (control != null && control.gameObject.activeInHierarchy) control.Select();
        }
        private void OnDestroy()
        {
            Font.textureRebuilt -= KeepFontPixels;
            DungeonSettings.Changed -= ApplyPreferences;
            DungeonSettings.Save();
            if (uiClick != null) Destroy(uiClick);
            if (displayFont != null && displayFont != font) Destroy(displayFont);
        }

        private void KeepFontPixels(Font rebuilt)
        {
            if (rebuilt == font && rebuilt.material != null && rebuilt.material.mainTexture != null)
                rebuilt.material.mainTexture.filterMode = FilterMode.Point;
        }
    }
}
