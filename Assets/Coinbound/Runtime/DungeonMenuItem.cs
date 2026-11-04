using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Coinbound
{
    /// <summary>A quiet text menu: the marker and text respond to mouse and keyboard focus.</summary>
    public sealed class DungeonMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public Text caption;
        public Graphic marker;
        private bool hovered, selected;
        public void OnPointerEnter(PointerEventData data)
        {
            hovered = true;
            EventSystem.current?.SetSelectedGameObject(gameObject);
            Refresh();
        }
        public void OnPointerExit(PointerEventData data) { hovered = false; Refresh(); }
        public void OnSelect(BaseEventData data) { selected = true; Refresh(); }
        public void OnDeselect(BaseEventData data) { selected = false; Refresh(); }
        private void OnEnable() { hovered = selected = false; Refresh(); }
        public void Refresh()
        {
            bool active = hovered || selected;
            if (caption != null) caption.color = active ? new Color32(232, 231, 218, 255) : new Color32(137, 150, 159, 255);
            if (marker != null) marker.enabled = active;
        }
    }
}
