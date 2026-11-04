using UnityEngine;
using UnityEngine.UI;

namespace Coinbound
{
    /// <summary>Keep source art pixels whole at fractional CanvasScaler resolutions.</summary>
    public sealed class DungeonUiArtScale : MonoBehaviour
    {
        public float iconSize = 24;
        private Image sliced;
        private RawImage icon;
        private Canvas owner;
        private void Awake()
        {
            sliced = GetComponent<Image>();
            icon = GetComponent<RawImage>();
            owner = GetComponentInParent<Canvas>();
        }

        private void LateUpdate()
        {
            if (owner == null) return;
            float scale = Mathf.Max(.01f, owner.scaleFactor);
            if (sliced != null)
            {
                // Keep even the 44-unit pause button larger than its two 14px borders.
                float fit = Mathf.Min(sliced.rectTransform.rect.width, sliced.rectTransform.rect.height) * scale / 29;
                float pixels = Mathf.Max(1, Mathf.Min(Mathf.Round(scale), Mathf.Floor(fit)));
                sliced.pixelsPerUnitMultiplier = scale / pixels;
            }
            if (icon != null && icon.texture != null)
            {
                float pixels = Mathf.Max(1, Mathf.Floor(iconSize * scale / Mathf.Max(icon.texture.width, icon.texture.height)));
                icon.rectTransform.sizeDelta = new Vector2(icon.texture.width, icon.texture.height) * (pixels / scale);
            }
        }
    }
}
