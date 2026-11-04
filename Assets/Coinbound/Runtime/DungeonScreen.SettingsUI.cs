using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Coinbound
{
    public sealed partial class DungeonScreen
    {
        private Slider volumeSlider, brightnessSlider;
        private Text volumeValue, brightnessValue;
        private Button settingsBack;
        private readonly Dictionary<string, Text> preferenceLabels = new Dictionary<string, Text>();
        private readonly Dictionary<string, Func<bool>> preferenceReads = new Dictionary<string, Func<bool>>();

        private void BuildSettings()
        {
            settings = Layer("Settings screen", new Color(.02f, .02f, .035f, .91f));
            RectTransform card = Framed("Settings panel", settings.transform, Vector2.zero, new Vector2(980, 630));
            Label("Heading", card, "НАСТРОЙКИ", 32, new Vector2(0, 259), new Vector2(800, 48), Pale, true);
            Rule(card, 217, 870);
            Label("Preferences heading", card, "ИЗОБРАЖЕНИЕ И ЗВУК", 11, new Vector2(-220, 182), new Vector2(380, 25), Gold);
            Label("Controls heading", card, "УПРАВЛЕНИЕ", 11, new Vector2(250, 182), new Vector2(330, 25), Gold);
            volumeSlider = SettingSlider(card, "Громкость интерфейса", 131, 0, 1, DungeonSettings.SetVolume, out volumeValue);
            brightnessSlider = SettingSlider(card, "Яркость подземелья", 62, .7f, 1.35f, DungeonSettings.SetBrightness, out brightnessValue);
            Preference(card, "Fullscreen", "Полный экран", 0, () => DungeonSettings.Fullscreen);
            Preference(card, "VSync", "Вертикальная синхронизация", -35, () => DungeonSettings.VSync);
            Preference(card, "ReducedMotion", "Меньше анимации", -70, () => DungeonSettings.ReducedMotion);
            Preference(card, "ShowHints", "Подсказки управления", -105, () => DungeonSettings.ShowHints);
            Preference(card, "DamageFlash", "Индикация урона", -140, () => DungeonSettings.DamageFlash);
            Preference(card, "VoiceInput", "Голосовые заклинания", -175, () => DungeonSettings.VoiceInput);
            string[] keys = { "W A S D / ↑ ← ↓ →", "ЛКМ", "ПКМ", "SHIFT", "E", "CTRL + КОЛЁСИКО", "ESC", "ENTER / СТРЕЛКИ" };
            string[] actions = { "Движение", "Удар / выстрел", "Блок мечника", "Перекат", "Открыть сундук", "Масштаб камеры", "Пауза / назад", "Навигация в меню" };
            for (int i = 0; i < keys.Length; i++)
            {
                float y = 131 - i * 37;
                Label("Key " + i, card, keys[i], 11, new Vector2(145, y), new Vector2(190, 28), Gold).alignment = TextAnchor.MiddleLeft;
                Label("Action " + i, card, actions[i], 14, new Vector2(340, y), new Vector2(200, 28), Muted).alignment = TextAnchor.MiddleLeft;
            }
            Label("Autosave", card, "Изменения сохраняются автоматически", 12, new Vector2(250, -178), new Vector2(420, 70), Muted, true);
            Rule(card, -220, 870);
            settingsBack = MakeButton("Settings back", card, "Готово  ·  Esc", new Vector2(280, -267), new Vector2(270, 42), () => CloseSettings());
            MakeButton("Reset settings", card, "По умолчанию", new Vector2(-280, -267), new Vector2(270, 42), () => { DungeonSettings.Reset(); RefreshSettings(); });
        }

        private Slider SettingSlider(Transform parent, string title, float y, float min, float max, Action<float> changed, out Text valueLabel)
        {
            Label(title, parent, title, 14, new Vector2(-246, y + 8), new Vector2(330, 26), Muted).alignment = TextAnchor.MiddleLeft;
            valueLabel = Label(title + " value", parent, "", 13, new Vector2(-27, y + 8), new Vector2(67, 26), Pale);
            RectTransform sliderRect = Rect(title + " slider", parent, new Vector2(-213, y - 23), new Vector2(390, 26));
            var slider = sliderRect.gameObject.AddComponent<Slider>(); slider.minValue = min; slider.maxValue = max;
            Image track = Solid("Track", sliderRect, Vector2.zero, new Vector2(380, 4), new Color32(54, 47, 43, 255));
            Image fill = Solid("Fill", track.transform, Vector2.zero, Vector2.zero, Gold); Stretch(fill.rectTransform);
            RectTransform handleArea = Rect("Handle area", sliderRect, Vector2.zero, new Vector2(380, 26));
            Image handle = Solid("Handle", handleArea, Vector2.zero, new Vector2(10, -8), Pale); handle.raycastTarget = true;
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            // The complete track, including empty space, responds to pointer clicks.
            var hit = sliderRect.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            slider.onValueChanged.AddListener(v => { changed(v); RefreshSettingValues(); });
            return slider;
        }
        private void Preference(Transform parent, string key, string caption, float y, Func<bool> read)
        {
            Label("Preference " + key, parent, caption, 14, new Vector2(-245, y), new Vector2(330, 28), Muted).alignment = TextAnchor.MiddleLeft;
            Button button = MakeButton("Toggle " + key, parent, "", new Vector2(-30, y), new Vector2(77, 29), () =>
            {
                DungeonSettings.SetToggle(key, !read()); RefreshSettingValues();
            });
            preferenceLabels[key] = Label("Value", button.transform, "", 11, Vector2.zero, new Vector2(72, 26), Pale);
            preferenceReads[key] = read;
        }
        public void ShowSettings()
        {
            if (settings.activeSelf) return;
            settingsReturnFocus = EventSystem.current?.currentSelectedGameObject?.GetComponent<Selectable>();
            settings.SetActive(true);
            CanvasGroup group = current.GetComponent<CanvasGroup>();
            if (group == null) group = current.AddComponent<CanvasGroup>();
            group.interactable = false; group.blocksRaycasts = false;
            RefreshSettings(); Select(volumeSlider);
        }
        public void CloseSettings(bool restoreFocus = true)
        {
            if (settings == null || !settings.activeSelf) return;
            settings.SetActive(false); DungeonSettings.Save();
            if (current != null && current.TryGetComponent(out CanvasGroup group)) { group.interactable = true; group.blocksRaycasts = true; }
            if (restoreFocus) Select(settingsReturnFocus);
        }
        private void RefreshSettings()
        {
            volumeSlider.SetValueWithoutNotify(DungeonSettings.Volume);
            brightnessSlider.SetValueWithoutNotify(DungeonSettings.Brightness);
            RefreshSettingValues();
        }
        private void RefreshSettingValues()
        {
            volumeValue.text = Mathf.RoundToInt(DungeonSettings.Volume * 100) + "%";
            brightnessValue.text = Mathf.RoundToInt(DungeonSettings.Brightness * 100) + "%";
            foreach (var pair in preferenceLabels)
            {
                bool enabled = preferenceReads[pair.Key](); pair.Value.text = enabled ? "ВКЛ" : "ВЫКЛ";
                pair.Value.color = enabled ? Pale : Muted;
            }
        }
        private void BuildConfirmation()
        {
            confirmation = Layer("Confirmation", new Color(0, 0, 0, .88f));
            RectTransform card = Framed("Confirmation panel", confirmation.transform, Vector2.zero, new Vector2(570, 300));
            confirmTitle = Label("Title", card, "", 28, new Vector2(0, 88), new Vector2(530, 45), Pale, true);
            Rule(card, 49, 480);
            confirmMessage = Label("Message", card, "", 16, new Vector2(0, -2), new Vector2(500, 72), Muted);
            confirmButton = MakeButton("Confirm", card, "Подтвердить", new Vector2(128, -97), new Vector2(235, 42), () =>
            {
                Action action = confirmAction; CloseConfirmation(false); action?.Invoke();
            });
            confirmCancel = MakeButton("Keep playing", card, "Отмена", new Vector2(-128, -97), new Vector2(235, 42), () => CloseConfirmation());
        }
        private void Confirm(string title, string message, Action action, string label, bool confirmOnLeft = false)
        {
            confirmReturnFocus = EventSystem.current?.currentSelectedGameObject?.GetComponent<Selectable>();
            confirmTitle.text = title; confirmMessage.text = message; confirmAction = action;
            confirmButton.GetComponentInChildren<Text>().text = label;
            ((RectTransform)confirmButton.transform).anchoredPosition = new Vector2(confirmOnLeft ? -128 : 128, -97);
            ((RectTransform)confirmCancel.transform).anchoredPosition = new Vector2(confirmOnLeft ? 128 : -128, -97);
            confirmation.SetActive(true);
            CanvasGroup group = current.GetComponent<CanvasGroup>();
            if (group == null) group = current.AddComponent<CanvasGroup>();
            group.interactable = false; group.blocksRaycasts = false;
            Select(confirmCancel);
        }
        private void CloseConfirmation(bool restoreFocus = true)
        {
            if (confirmation == null || !confirmation.activeSelf) return;
            confirmation.SetActive(false); confirmAction = null;
            if (current != null && current.TryGetComponent(out CanvasGroup group)) { group.interactable = true; group.blocksRaycasts = true; }
            if (restoreFocus) Select(confirmReturnFocus);
        }
        private void RequestQuit() => Confirm("ВЫЙТИ ИЗ ИГРЫ?", "Текущий забег не сохраняется после выхода из игры.", () =>
        {
            DungeonSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }, "Выйти", confirmOnLeft: true);
    }
}
