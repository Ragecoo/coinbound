using System;
using UnityEngine;
using UnityEngine.UI;

namespace Coinbound
{
    public sealed partial class DungeonScreen
    {
        private void BuildMenu()
        {
            menu = Layer("Start screen", new Color32(3, 5, 11, 255));
            var scene = Rect("Menu environment", menu.transform, new Vector2(0, 36), new Vector2(1280, 720)).gameObject.AddComponent<DungeonVignetteImage>();
            scene.texture = Resources.Load<Texture2D>("InterfaceArt/menu-crypt"); scene.raycastTarget = false;
            var water = Rect("Menu water reflections", scene.transform, Vector2.zero, new Vector2(1280, 720))
                .gameObject.AddComponent<DungeonMenuWater>();
            water.texture = scene.texture; water.raycastTarget = false;
            var atmosphere = Rect("Menu candlelight and dust", scene.transform, Vector2.zero, new Vector2(1280, 720))
                .gameObject.AddComponent<DungeonMenuAtmosphere>();
            atmosphere.raycastTarget = false;
            gameObject.AddComponent<DungeonMenuAmbience>().Initialize(menu);
            var dust = Rect("Menu drifting dust", menu.transform, Vector2.zero, Vector2.zero).gameObject.AddComponent<DungeonMenuDust>();
            Stretch(dust.rectTransform); dust.raycastTarget = false;
            var logo = Rect("Coinbound logo", menu.transform, new Vector2(0, 100), new Vector2(640, 480)).gameObject.AddComponent<RawImage>();
            logo.raycastTarget = false;
            logo.texture = Resources.Load<Texture2D>("InterfaceArt/coinbound-logo");
            logo.color = new Color(.78f, .78f, .8f, 1);
            resumeButton = TextButton("Resume", menu.transform, "ПРОДОЛЖИТЬ", new Vector2(0, -156), new Vector2(280, 32), resumeAction);
            startButton = TextButton("Start", menu.transform, "НОВАЯ ИГРА", new Vector2(0, -194), new Vector2(280, 32), RequestNewJourney);
            TextButton("Settings", menu.transform, "НАСТРОЙКИ", new Vector2(0, -232), new Vector2(280, 32), ShowSettings);
            TextButton("Results", menu.transform, "РЕЗУЛЬТАТЫ", new Vector2(0, -270), new Vector2(280, 32), ShowLeaderboard);
            TextButton("Quit", menu.transform, "ВЫХОД", new Vector2(0, -308), new Vector2(280, 32), RequestQuit);
            menuNote = Label("Error message", menu.transform, "", 14, new Vector2(0, -344), new Vector2(1000, 30), Red);
        }

        private void BuildHeroes()
        {
            heroSelection = Layer("Hero selection screen", new Color32(21, 21, 35, 255));
            Label("Title", heroSelection.transform, "ВЫБОР ПЕРСОНАЖА", 24, new Vector2(0, 309), new Vector2(900, 38), Pale);
            Solid("Center divider", heroSelection.transform, new Vector2(0, 8), new Vector2(1, 510), new Color(.25f, .29f, .34f, .6f));
            for (int i = 0; i < DungeonHero.All.Count; i++)
            {
                DungeonHero hero = DungeonHero.All[i];
                float x = i == 0 ? -310 : 310;
                RectTransform half = Rect("Hero " + i, heroSelection.transform, new Vector2(x, 0), new Vector2(590, 546));
                Image hit = half.gameObject.AddComponent<Image>(); hit.color = Color.clear;
                Button button = half.gameObject.AddComponent<Button>(); button.targetGraphic = hit;
                button.transition = Selectable.Transition.None; button.onClick.AddListener(() => { Click(); ChooseHero(hero); });
                heroButtons.Add(button);
                var glow = Divider("Hero selection line", half, new Vector2(0, 188), 270);
                heroFrames.Add(glow);
                RawImage portrait = Raw("Portrait", half, new Vector2(0, 30), new Vector2(240, 320));
                SetPortrait(portrait, hero, false);
                Label("Name", half, hero.DisplayName, 30, new Vector2(0, 251), new Vector2(480, 42), Pale);
                Label("Combat type", half, hero.IsArcher ? "ДАЛЬНИЙ БОЙ" : "БЛИЖНИЙ БОЙ", 11, new Vector2(0, 216), new Vector2(480, 25), Muted);
                Rule(half, -173, 400);
                Label("Health stat", half, "Здоровье  " + hero.StartingHealth, 15, new Vector2(-111, -207), new Vector2(210, 28), Pale);
                Label("Damage stat", half, "Урон  " + hero.AttackDamage, 15, new Vector2(111, -207), new Vector2(210, 28), Pale);
                Label("Attack stat", half, "Атака  " + hero.AttackSeconds.ToString("0.00") + " с", 13, new Vector2(-111, -239), new Vector2(210, 26), Muted);
                Label("Ability stat", half, hero.IsArcher ? "Перекат  1,5 с" : "Блок  2 с", 13, new Vector2(111, -239), new Vector2(210, 26), Muted);
                heroMarkers.Add(Label("Selection", half, "", 12, new Vector2(0, -272), new Vector2(230, 24), Pale));
            }
            createDungeonButton = TextButton("Create dungeon", heroSelection.transform, "НАЧАТЬ ИГРУ", new Vector2(0, -318), new Vector2(300, 38), () => heroAction(selectedHero));
            heroBackButton = TextButton("Back", heroSelection.transform, "НАЗАД", new Vector2(-526, -318), new Vector2(160, 38), titleAction);
            for (int i = 0; i < heroButtons.Count; i++)
                heroButtons[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnLeft = heroButtons[1 - i], selectOnRight = heroButtons[1 - i], selectOnDown = createDungeonButton, selectOnUp = heroBackButton };
            createDungeonButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = heroButtons[0], selectOnLeft = heroBackButton };
            heroBackButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = createDungeonButton, selectOnUp = heroButtons[0] };
        }

        // Stats are intentionally short: no generated lore or marketing copy.

        private void BuildLoading()
        {
            loading = Layer("Loading screen", new Color32(4, 6, 11, 255));
            Label("Heading", loading.transform, "ЗАГРУЗКА", 24, new Vector2(0, 140), new Vector2(600, 36), Pale);
            for (int row = 0; row < 3; row++)
            for (int i = 0; i < 12; i++)
            {
                var tile = Rect("Corridor " + row + " " + i, loading.transform, new Vector2(-176 + i * 32, (row - 1) * 32), new Vector2(32, 32)).gameObject.AddComponent<Image>();
                tile.sprite = Resources.Load<Sprite>(row == 2 ? "DungeonArt/wall.F0" : "DungeonArt/floor.corridor." + i % 3);
                tile.color = new Color(.6f, .65f, .8f, Mathf.Min(1, (i + 1) / 2f, (12 - i) / 2f));
                tile.raycastTarget = false;
            }
            loadingActor = Rect("Loading hero", loading.transform, new Vector2(-160, 45), new Vector2(64, 128)).gameObject.AddComponent<Image>();
            loadingActor.preserveAspect = true; loadingActor.raycastTarget = false;
            progressFill = Bar("Progress", loading.transform, new Vector2(0, -91), new Vector2(420, 3), new Color32(135, 154, 176, 255));
            percent = Label("Percentage", loading.transform, "0%", 14, new Vector2(245, -91), new Vector2(75, 30), Pale);
            status = Label("Status", loading.transform, "", 12, new Vector2(0, -129), new Vector2(850, 28), Muted);
            TextButton("Cancel", loading.transform, "ОТМЕНА", new Vector2(0, -185), new Vector2(180, 32), cancelAction);
        }

        private void BuildHud()
        {
            hud = Layer("Game HUD", Color.clear, false);
            var ambient = Rect("Ambient edge", hud.transform, Vector2.zero, Vector2.zero);
            Stretch(ambient); ambient.gameObject.AddComponent<DungeonAmbientBorder>();
            var border = Rect("Damage edge", hud.transform, Vector2.zero, Vector2.zero);
            Stretch(border); damageBorder = border.gameObject.AddComponent<DungeonDamageBorder>(); damageBorder.raycastTarget = false;
            BuildAuthoredHud();
            hudLabel = Label("Score", hud.transform, "000000000", 30, Vector2.zero, new Vector2(260, 44), Pale);
            hudLabel.fontSize = 18;
            Anchor(hudLabel.rectTransform, Vector2.one, new Vector2(-27, -18)); hudLabel.alignment = TextAnchor.MiddleRight;
            hudLabel.gameObject.AddComponent<Outline>().effectColor = Color.black;
            BuildStatsHud();
            controlLegend = Label("Controls", hud.transform, "pulse · trace · ward · decoy · recall", 11, Vector2.zero, new Vector2(520, 24), Muted);
            controlLegend.alignment = TextAnchor.MiddleLeft;
            Anchor(controlLegend.rectTransform, new Vector2(0, 0), new Vector2(24, 18));
            interactionPrompt = Label("Chest interaction", hud.transform, "E — Открыть сундук", 12,
                Vector2.zero, new Vector2(320, 28), Pale);
            interactionPrompt.fontSize = 18;
            Anchor(interactionPrompt.rectTransform, new Vector2(.5f, 0), new Vector2(0, 24));
            interactionPrompt.gameObject.AddComponent<Shadow>().effectColor = Color.black;
            interactionPrompt.gameObject.SetActive(false);
            voiceStatus = Label("Voice status", hud.transform, "", 12, Vector2.zero, new Vector2(620, 26), Muted);
            ((DungeonPixelText)voiceStatus).ExtraPixelSteps = 0; voiceStatus.fontSize = 16;
            voiceStatus.alignment = TextAnchor.MiddleLeft;
            Anchor(voiceStatus.rectTransform, new Vector2(0, 0), new Vector2(24, 44));
        }

        private void BuildPause()
        {
            pause = Layer("Pause screen", new Color(.025f, .025f, .04f, .78f));
            RectTransform card = Framed("Pause parchment", pause.transform, Vector2.zero, new Vector2(470, 536));
            Label("Eyebrow", card, "", 11, new Vector2(0, 217), new Vector2(430, 25), Gold);
            Label("Title", card, "Пауза", 44, new Vector2(0, 165), new Vector2(430, 60), Pale, true);
            Rule(card, 122, 380);
            pauseStats = Label("Run summary", card, "", 13, new Vector2(0, 79), new Vector2(420, 60), Muted);
            pauseResumeButton = MakeButton("Pause resume", card, "Продолжить", new Vector2(0, 12), new Vector2(322, 44), resumeAction);
            MakeButton("Pause settings", card, "Настройки", new Vector2(0, -43), new Vector2(322, 44), ShowSettings);
            MakeButton("Pause restart", card, "Новая игра", new Vector2(0, -98), new Vector2(322, 44), RequestNewJourney);
            MakeButton("Pause title", card, "Главный экран", new Vector2(0, -153), new Vector2(322, 44), titleAction);
            Rule(card, -199, 380);
            Label("Pause hint", card, "Esc — продолжить", 12, new Vector2(0, -229), new Vector2(430, 26), Muted);
        }

        private void BuildDeath()
        {
            death = Layer("Death screen", new Color(.03f, .018f, .027f, .93f));
            Decor("Last coin", death.transform, new Vector2(0, 210), new Vector2(45, 48), DungeonUiDecoration.Kind.Coin, new Color(.44f, .28f, .24f));
            Label("Title", death.transform, "ВЫ ПОГИБЛИ", 56, new Vector2(0, 119), new Vector2(1100, 82), new Color32(187, 109, 106, 255), true);
            Label("Epitaph", death.transform, "", 18, new Vector2(0, 51), new Vector2(1000, 45), Muted, true);
            Rule(death.transform, 6, 650);
            deathStats = Label("Death summary", death.transform, "", 15, new Vector2(0, -65), new Vector2(900, 100), Pale);
            deathRetryButton = MakeButton("Retry", death.transform, "Повторить", new Vector2(0, -171), new Vector2(350, 48), () => heroAction(selectedHero));
            MakeButton("Death heroes", death.transform, "Выбрать другого героя", new Vector2(0, -229), new Vector2(350, 42), startAction);
            MakeButton("Death title", death.transform, "Главный экран", new Vector2(0, -282), new Vector2(350, 40), titleAction);
        }

        private GameObject Layer(string name, Color color, bool blocks = true)
        {
            RectTransform rect = Rect(name, transform, Vector2.zero, Vector2.zero); Stretch(rect);
            Image image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = blocks;
            rect.gameObject.SetActive(false); return rect.gameObject;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false); r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = size; r.anchoredPosition = pos; return r;
        }
        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        }
        private static void Anchor(RectTransform r, Vector2 anchor, Vector2 pos)
        {
            r.anchorMin = r.anchorMax = r.pivot = anchor; r.anchoredPosition = pos;
        }
        private Image Solid(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            Image i = Rect(name, parent, pos, size).gameObject.AddComponent<Image>(); i.color = color; i.raycastTarget = false; return i;
        }
        private Text Label(string name, Transform parent, string text, int size, Vector2 pos, Vector2 area, Color color, bool display = false)
        {
            Text label = Rect(name, parent, pos, area).gameObject.AddComponent<DungeonPixelText>();
            label.font = display ? displayFont : font;
            // Menus need a larger reading size; gameplay HUD keeps its separately tuned typography.
            label.fontStyle = FontStyle.Normal;
            bool isHud = hud != null && parent.IsChildOf(hud.transform);
            label.fontSize = isHud
                ? (size <= 20 ? 13 : size <= 32 ? 26 : size <= 48 ? 39 : 52)
                : (size <= 20 ? 18 : size <= 32 ? 32 : size <= 48 ? 46 : 60);
            label.text = text; label.color = color;
            label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        private DungeonUiDecoration Decor(string name, Transform parent, Vector2 pos, Vector2 size, DungeonUiDecoration.Kind kind, Color color)
        {
            var d = Rect(name, parent, pos, size).gameObject.AddComponent<DungeonUiDecoration>();
            d.kind = kind; d.color = color; d.raycastTarget = false; return d;
        }
        private DungeonUiDivider Divider(string name, Transform parent, Vector2 pos, float width)
        {
            var divider = Rect(name, parent, pos, new Vector2(width, 12)).gameObject.AddComponent<DungeonUiDivider>();
            divider.color = Color.white; divider.raycastTarget = false; return divider;
        }
        private void Rule(Transform parent, float y, float width) => Divider("Ornament", parent, new Vector2(0, y), width);
        private static void ApplyGuiArt(Image image, string asset)
        {
            image.sprite = Resources.Load<Sprite>("InterfaceArt/Gui/" + asset);
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            image.gameObject.AddComponent<DungeonUiArtScale>();
        }
        private RectTransform Framed(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            Image panel = Solid(name, parent, pos, size, Color.white);
            ApplyGuiArt(panel, "panel");
            return panel.rectTransform;
        }
        private Button MakeButton(string name, Transform parent, string text, Vector2 pos, Vector2 size, Action action)
        {
            var r = Rect(name, parent, pos, size);
            Image background = r.gameObject.AddComponent<Image>();
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = background;
            ApplyGuiArt(background, "button");
            b.transition = Selectable.Transition.SpriteSwap;
            b.spriteState = new SpriteState {
                highlightedSprite = Resources.Load<Sprite>("InterfaceArt/Gui/button_hover"),
                selectedSprite = Resources.Load<Sprite>("InterfaceArt/Gui/button_hover"),
                pressedSprite = Resources.Load<Sprite>("InterfaceArt/Gui/button_pressed"),
                disabledSprite = Resources.Load<Sprite>("InterfaceArt/Gui/button_disabled")
            };
            if (!string.IsNullOrEmpty(text)) Label("Label", r, text, 18, Vector2.zero, size - new Vector2(24, 4), Pale, true);
            b.onClick.AddListener(() => { Click(); action?.Invoke(); });
            b.gameObject.AddComponent<DungeonHoverSound>();
            return b;
        }
        private RawImage Raw(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            RawImage image = Rect(name, parent, pos, size).gameObject.AddComponent<RawImage>(); image.raycastTarget = false; return image;
        }
        private void Crypt(Transform parent, Vector2 pos, Vector2 size)
        {
            var art = Rect("Crypt vignette", parent, pos, size).gameObject.AddComponent<DungeonVignetteImage>();
            art.raycastTarget = false;
            art.texture = Resources.Load<Texture2D>("InterfaceArt/crypt-pixel");
            if (art.texture != null) art.texture.filterMode = FilterMode.Point;
        }
        private static void SetPortrait(RawImage image, DungeonHero hero, bool face)
        {
            image.texture = Resources.Load<Texture2D>("InterfaceArt/" + (hero.IsArcher ? "archer-portrait" : "swordsman-portrait"));
            image.color = new Color(.80f, .84f, .92f, 1);
            image.uvRect = hero.IsArcher ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1);
        }
        private Button TextButton(string name, Transform parent, string text, Vector2 pos, Vector2 size, Action action)
        {
            RectTransform r = Rect(name, parent, pos, size);
            Image hit = r.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            Button b = r.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = hit;
            Text caption = Label("Label", r, text, 17, Vector2.zero, size, Pale);
            Text marker = Label("Marker", r, ">", 15, new Vector2(-size.x * .5f + 18, 0), new Vector2(28, 34), Pale);
            var item = r.gameObject.AddComponent<DungeonMenuItem>(); item.caption = caption; item.marker = marker; item.Refresh();
            b.onClick.AddListener(() => { Click(); action?.Invoke(); });
            b.gameObject.AddComponent<DungeonHoverSound>();
            return b;
        }
        private DungeonHudGlyph Glyph(string name, Transform parent, Vector2 pos, Vector2 size, DungeonHudGlyph.Symbol symbol, Color color)
        {
            var glyph = Rect(name, parent, pos, size).gameObject.AddComponent<DungeonHudGlyph>();
            glyph.symbol = symbol; glyph.color = color; glyph.raycastTarget = false; return glyph;
        }
        private RectTransform Bar(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            Image track = Solid(name + " track", parent, pos, size, new Color32(45, 40, 39, 255));
            var fill = Solid(name + " fill", track.transform, Vector2.zero, Vector2.zero, color).rectTransform;
            Stretch(fill); return fill;
        }
    }
}
