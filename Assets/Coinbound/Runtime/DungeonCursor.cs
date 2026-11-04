using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Coinbound
{
    /// <summary>Authored cursor sprites with native hotspots and integer pixel scaling.</summary>
    public sealed class DungeonCursor : MonoBehaviour
    {
        private DungeonPlayer player;
        private GameObject canvasObject;
        private RectTransform reticle;
        private RawImage graphic;
        private Texture2D arrow, hover, crosshair, pressed;
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private PointerEventData pointer;
        private EventSystem pointerSystem;

        private void Awake()
        {
            arrow = Load("arrow"); hover = Load("button_hover");
            crosshair = Load("crosshair"); pressed = Load("crosshair_pressed");
            canvasObject = new GameObject("Authored cursor", typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200; canvas.pixelPerfect = true;
            var cursor = new GameObject("Cursor sprite", typeof(RectTransform), typeof(CanvasRenderer));
            reticle = cursor.GetComponent<RectTransform>();
            reticle.SetParent(canvasObject.transform, false);
            reticle.anchorMin = reticle.anchorMax = Vector2.zero;
            graphic = cursor.AddComponent<RawImage>();
            graphic.raycastTarget = false;
            canvasObject.SetActive(false);
        }
        private static Texture2D Load(string name) => Resources.Load<Texture2D>("InterfaceArt/Cursor/" + name);
        public void Bind(DungeonPlayer target) => player = target;

        private bool OverButton(Vector2 position)
        {
            EventSystem system = EventSystem.current;
            if (system == null) return false;
            if (pointer == null || pointerSystem != system)
            {
                pointerSystem = system;
                pointer = new PointerEventData(system);
            }
            pointer.Reset(); pointer.position = position;
            hits.Clear(); system.RaycastAll(pointer, hits);
            if (hits.Count == 0) return false;
            Selectable control = hits[0].gameObject.GetComponentInParent<Selectable>();
            return control != null && control.IsActive() && control.IsInteractable();
        }
        private void LateUpdate()
        {
            bool focused = Application.isFocused;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            focused |= DungeonCombatCapture.IsRunning;
#endif
            if (!focused || Mouse.current == null) { RestoreCursor(); return; }
            Vector2 position = Mouse.current.position.ReadValue();
            bool overButton = OverButton(position);
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool aiming = player != null && player.CanAct && !overUi && !overButton;
            Texture2D sprite = aiming
                ? (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed ? pressed : crosshair)
                : (overButton ? hover : arrow);
            if (sprite == null) { RestoreCursor(); return; }
            canvasObject.SetActive(true); Cursor.visible = false;
            graphic.texture = sprite; graphic.color = Color.white;
            int zoom = Mathf.Max(1, Mathf.RoundToInt(Screen.height / 720f));
            reticle.sizeDelta = new Vector2(sprite.width, sprite.height) * zoom;
            // Source hotspots are measured from the top-left corner (see cursor/README.txt).
            Vector2 hotspot = aiming ? new Vector2(8, 8) : Vector2.one;
            reticle.pivot = new Vector2(hotspot.x / sprite.width, 1 - hotspot.y / sprite.height);
            reticle.anchoredPosition = new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
        }
        private void OnApplicationFocus(bool focused) { if (!focused) RestoreCursor(); }
        private void OnDisable() => RestoreCursor();
        private void OnDestroy() => RestoreCursor();
        private void RestoreCursor()
        {
            Cursor.visible = true;
            if (canvasObject != null) canvasObject.SetActive(false);
        }
    }
}
