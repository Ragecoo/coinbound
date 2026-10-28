using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Coinbound
{
    /// <summary>Disposable UI placeholder; no generation or gameplay rules live here.</summary>
    public sealed class DungeonScreen : MonoBehaviour
    {
        private readonly Color background = new Color32(15, 19, 33, 250);
        private readonly Color muted = new Color32(155, 166, 188, 255);
        private readonly Color accent = new Color32(112, 213, 222, 255);
        private Font font;
        private Canvas uiCanvas;
        private GameObject menu;
        private GameObject heroSelection;
        private GameObject loading;
        private GameObject hud;
        private Button resumeButton;
        private Button startButton;
        private Text menuNote;
        private Text status;
        private Text percent;
        private Text seedLabel;
        private Text hudLabel;
        private RectTransform progressFill;
        private DungeonHero selectedHero;
        private Button[] heroButtons;
        private Outline[] heroSelectionOutlines;
        private Text[] heroSelectionLabels;
        private Button createDungeonButton;
        private Button heroBackButton;
        private readonly List<Sprite> heroPortraits = new List<Sprite>();
        private readonly List<RectTransform> heroPortraitRects = new List<RectTransform>();
        private float portraitCanvasScale = -1;

        public void Initialize(Action start, Action resume, Action openMenu, Action cancel, Action<DungeonHero> startHero)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = gameObject.AddComponent<Canvas>();
            uiCanvas = canvas;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 100;
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

            menu = Overlay("Start screen");
            RectTransform menuCard = Center("Menu", menu.transform, new Vector2(560, 390));
            Label("Title", menuCard, "COINBOUND", 48, Vector2.zero, new Vector2(540, 65), new Vector2(0, 135), Color.white);
            Label("Subtitle", menuCard, "ПОДЗЕМЕЛЬЕ", 16, Vector2.zero, new Vector2(540, 30), new Vector2(0, 85), accent);
            startButton = MakeButton("Start", menuCard, "Начать игру", new Vector2(0, 12), new Vector2(300, 50), start);
            resumeButton = MakeButton("Resume", menuCard, "Продолжить", new Vector2(0, -50), new Vector2(300, 50), resume);
            menuNote = Label("Note", menuCard, "Временный стартовый экран", 16, Vector2.zero,
                new Vector2(540, 50), new Vector2(0, -120), muted);
            Label("Controls", menuCard, "WASD — движение  •  Ctrl + колёсико — масштаб  •  Esc — меню", 16, Vector2.zero,
                new Vector2(560, 36), new Vector2(0, -175), muted);

            heroSelection = Overlay("Hero selection screen");
            RectTransform heroCard = Center("Hero selection", heroSelection.transform, new Vector2(660, 500));
            Label("Title", heroCard, "ВЫБЕРИ ПЕРСОНАЖА", 30, Vector2.zero,
                new Vector2(640, 45), new Vector2(0, 205), Color.white);
            Label("Subtitle", heroCard, "Выбери героя и создай подземелье", 17, Vector2.zero,
                new Vector2(640, 30), new Vector2(0, 163), muted);
            heroButtons = new Button[DungeonHero.All.Count];
            heroSelectionOutlines = new Outline[heroButtons.Length];
            heroSelectionLabels = new Text[heroButtons.Length];
            for (int i = 0; i < heroButtons.Length; i++)
            {
                DungeonHero hero = DungeonHero.All[i];
                float x = (i - (heroButtons.Length - 1) * 0.5f) * 260;
                Button card = MakeButton("Hero " + i, heroCard, "", new Vector2(x, 15), new Vector2(220, 240),
                    () => ChooseHero(hero));
                heroButtons[i] = card;
                Outline selectedOutline = card.gameObject.AddComponent<Outline>();
                selectedOutline.effectColor = accent;
                selectedOutline.effectDistance = new Vector2(2, -2);
                selectedOutline.enabled = false;
                heroSelectionOutlines[i] = selectedOutline;

                Sprite sprite = DungeonPlaceholderArt.Create(hero.DisplayName + " portrait", hero.Color, false);
                heroPortraits.Add(sprite);
                RectTransform portrait = Center("Portrait", card.transform, new Vector2(96, 96));
                portrait.anchoredPosition = new Vector2(0, 32);
                heroPortraitRects.Add(portrait);
                Image portraitImage = portrait.gameObject.AddComponent<Image>();
                portraitImage.sprite = sprite;
                portraitImage.preserveAspect = true;
                portraitImage.raycastTarget = false;
                Label("Name", card.transform, hero.DisplayName, 23, Vector2.zero,
                    new Vector2(200, 36), new Vector2(0, -44), Color.white);
                heroSelectionLabels[i] = Label("Selection", card.transform, "", 16, Vector2.zero,
                    new Vector2(200, 30), new Vector2(0, -88), muted);
            }
            createDungeonButton = MakeButton("Create dungeon", heroCard, "Создать подземелье", new Vector2(0, -155),
                new Vector2(330, 50), () => startHero(selectedHero));
            heroBackButton = MakeButton("Back", heroCard, "Назад", new Vector2(0, -216), new Vector2(180, 42), openMenu);
            ConfigureHeroNavigation();
            ChooseHero(DungeonHero.First);
            heroSelection.SetActive(false);

            loading = Overlay("Loading screen");
            RectTransform loadingCard = Center("Loading", loading.transform, new Vector2(620, 280));
            Label("Title", loadingCard, "СОЗДАНИЕ ПОДЗЕМЕЛЬЯ", 25, Vector2.zero,
                new Vector2(620, 45), new Vector2(0, 105), Color.white);
            seedLabel = Label("Seed", loadingCard, "", 15, Vector2.zero,
                new Vector2(620, 30), new Vector2(0, 64), muted);
            var bar = Center("Progress track", loadingCard, new Vector2(520, 12));
            bar.anchoredPosition = new Vector2(0, 16);
            bar.gameObject.AddComponent<Image>().color = new Color32(43, 51, 72, 255);
            progressFill = Center("Progress fill", bar, Vector2.zero);
            progressFill.anchorMin = Vector2.zero;
            progressFill.anchorMax = Vector2.one;
            progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;
            progressFill.gameObject.AddComponent<Image>().color = accent;
            percent = Label("Percentage", loadingCard, "0%", 16, Vector2.zero,
                new Vector2(80, 30), new Vector2(285, 16), Color.white);
            status = Label("Status", loadingCard, "", 20, Vector2.zero,
                new Vector2(620, 60), new Vector2(0, -35), Color.white);
            MakeButton("Cancel", loadingCard, "Отмена", new Vector2(0, -110), new Vector2(180, 42), cancel);

            hud = new GameObject("Game HUD", typeof(RectTransform));
            Stretch((RectTransform)hud.transform, transform);
            hudLabel = Label("Dungeon info", hud.transform, "", 16, new Vector2(0, 1),
                new Vector2(700, 40), new Vector2(24, -20), Color.white);
            hudLabel.alignment = TextAnchor.UpperLeft;
            var hudBackground = hudLabel.gameObject.AddComponent<Outline>();
            hudBackground.effectColor = new Color32(10, 12, 22, 230);
            hudBackground.effectDistance = new Vector2(1, -1);
            var help = Label("Controls", hud.transform, "WASD / стрелки — движение    •    Ctrl + колёсико — масштаб    •    Esc — меню", 16,
                new Vector2(0.5f, 0), new Vector2(900, 35), new Vector2(0, 14), Color.white);
            help.gameObject.AddComponent<Outline>().effectColor = Color.black;
            Button menuButton = MakeButton("Menu", hud.transform, "Меню", Vector2.zero, new Vector2(110, 38), openMenu);
            var menuRect = (RectTransform)menuButton.transform;
            menuRect.anchorMin = menuRect.anchorMax = menuRect.pivot = Vector2.one;
            menuRect.anchoredPosition = new Vector2(-20, -18);
        }

        public void ShowMenu(bool canResume)
        {
            menu.SetActive(true);
            heroSelection.SetActive(false);
            loading.SetActive(false);
            hud.SetActive(false);
            resumeButton.gameObject.SetActive(canResume);
            startButton.GetComponentInChildren<Text>().text = canResume ? "Новое подземелье" : "Начать игру";
            menuNote.text = "Временный стартовый экран";
            Select(canResume ? resumeButton : startButton);
        }

        public void ShowHeroSelection(DungeonHero selected)
        {
            menu.SetActive(false);
            loading.SetActive(false);
            hud.SetActive(false);
            heroSelection.SetActive(true);
            ChooseHero(selected ?? DungeonHero.First);
            RefreshPortraitScale();
            Select(heroButtons[SelectedHeroIndex()]);
        }

        public void ShowLoading(int seed)
        {
            menu.SetActive(false);
            heroSelection.SetActive(false);
            hud.SetActive(false);
            loading.SetActive(true);
            seedLabel.text = $"Seed: {seed}";
            SetProgress(0, "Возведение стен подземелья...");
            EventSystem.current?.SetSelectedGameObject(null);
        }

        public void SetProgress(float value, string message)
        {
            value = Mathf.Clamp01(value);
            progressFill.anchorMax = new Vector2(value, 1);
            percent.text = $"{Mathf.FloorToInt(value * 100)}%";
            status.text = message;
        }

        public void ShowGame(int seed, int enemies, int chests)
        {
            menu.SetActive(false);
            heroSelection.SetActive(false);
            loading.SetActive(false);
            hud.SetActive(true);
            hudLabel.text = $"COINBOUND    ·    Seed: {seed}    ·    Враги: {enemies}    ·    Сундуки: {chests}";
            EventSystem.current?.SetSelectedGameObject(null);
        }

        public void ShowError(string message)
        {
            ShowMenu(false);
            menuNote.text = message;
        }

        private int SelectedHeroIndex()
        {
            for (int i = 0; i < DungeonHero.All.Count; i++)
                if (DungeonHero.All[i] == selectedHero) return i;
            return 0;
        }

        private void ChooseHero(DungeonHero hero)
        {
            selectedHero = hero;
            int selectedIndex = SelectedHeroIndex();
            selectedHero = DungeonHero.All[selectedIndex];
            for (int i = 0; i < heroButtons.Length; i++)
            {
                bool chosen = i == selectedIndex;
                heroSelectionOutlines[i].enabled = chosen;
                heroSelectionLabels[i].text = chosen ? "Выбран" : "Выбрать";
                heroSelectionLabels[i].color = chosen ? accent : muted;
            }
            Navigation confirmNavigation = createDungeonButton.navigation;
            confirmNavigation.selectOnUp = heroButtons[selectedIndex];
            createDungeonButton.navigation = confirmNavigation;
            Navigation backNavigation = heroBackButton.navigation;
            backNavigation.selectOnDown = heroButtons[selectedIndex];
            heroBackButton.navigation = backNavigation;
        }

        private void ConfigureHeroNavigation()
        {
            for (int i = 0; i < heroButtons.Length; i++)
            {
                heroButtons[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = heroButtons[(i + heroButtons.Length - 1) % heroButtons.Length],
                    selectOnRight = heroButtons[(i + 1) % heroButtons.Length],
                    selectOnUp = heroBackButton,
                    selectOnDown = createDungeonButton
                };
            }
            createDungeonButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = heroButtons[0],
                selectOnDown = heroBackButton
            };
            heroBackButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = createDungeonButton,
                selectOnDown = heroButtons[0]
            };
        }

        private void LateUpdate()
        {
            if (heroSelection != null && heroSelection.activeSelf) RefreshPortraitScale();
        }

        private void RefreshPortraitScale()
        {
            float scale = Mathf.Max(0.01f, uiCanvas.scaleFactor);
            if (Mathf.Approximately(scale, portraitCanvasScale)) return;
            portraitCanvasScale = scale;
            // CanvasScaler can use fractional scaling; keep the 32px portrait at a whole screen-pixel multiple.
            int pixelScale = Mathf.Max(1, Mathf.FloorToInt(3 * scale));
            Vector2 size = Vector2.one * (32 * pixelScale / scale);
            foreach (RectTransform portrait in heroPortraitRects) portrait.sizeDelta = size;
        }

        private void OnDestroy()
        {
            foreach (Sprite portrait in heroPortraits)
            {
                if (portrait == null) continue;
                Destroy(portrait.texture);
                Destroy(portrait);
            }
        }

        private void Select(Button button)
        {
            EventSystem.current?.SetSelectedGameObject(null);
            button.Select();
        }

        private GameObject Overlay(string label)
        {
            var panel = new GameObject(label, typeof(RectTransform), typeof(Image));
            Stretch((RectTransform)panel.transform, transform);
            panel.GetComponent<Image>().color = background;
            return panel;
        }

        private static void Stretch(RectTransform rect, Transform parent)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static RectTransform Center(string label, Transform parent, Vector2 size)
        {
            var rect = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return rect;
        }

        private Text Label(string label, Transform parent, string text, int size, Vector2 anchor,
            Vector2 dimensions, Vector2 position, Color color)
        {
            RectTransform rect = Center(label, parent, dimensions);
            if (anchor != Vector2.zero)
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            }
            rect.anchoredPosition = position;
            var caption = rect.gameObject.AddComponent<Text>();
            caption.font = font;
            caption.text = text;
            caption.fontSize = size;
            caption.color = color;
            caption.alignment = TextAnchor.MiddleCenter;
            caption.raycastTarget = false;
            caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            caption.verticalOverflow = VerticalWrapMode.Truncate;
            return caption;
        }

        private Button MakeButton(string label, Transform parent, string caption, Vector2 position, Vector2 size, Action action)
        {
            RectTransform rect = Center(label, parent, size);
            rect.anchoredPosition = position;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color32(44, 65, 83, 255);
            colors.highlightedColor = new Color32(61, 97, 113, 255);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color32(40, 120, 133, 255);
            button.colors = colors;
            button.onClick.AddListener(() => action());
            Label("Label", rect, caption, 20, Vector2.zero, size, Vector2.zero, Color.white);
            return button;
        }
    }
}
