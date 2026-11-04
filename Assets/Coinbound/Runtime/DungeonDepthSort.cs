using UnityEngine;

namespace Coinbound
{
    /// <summary>Depth follows the feet, while tall sprites retain one coherent ground-light sample.</summary>
    [DefaultExecutionOrder(80)]
    public sealed class DungeonDepthSort : MonoBehaviour
    {
        private static readonly int GroundLightPositionId = Shader.PropertyToID("_GroundLightPosition");
        private SpriteRenderer visual;
        private Transform feet;
        private float footOffset;
        private bool useGroundLight;
        private MaterialPropertyBlock properties;

        public static int OrderForY(float feetY) => Mathf.Clamp(2000 - Mathf.RoundToInt(feetY * 32), 10, 32000);

        public void Initialize(SpriteRenderer renderer, Transform anchor, float offset = 0, bool groundLight = false)
        {
            visual = renderer;
            feet = anchor;
            footOffset = offset;
            useGroundLight = groundLight;
            if (groundLight) properties = new MaterialPropertyBlock();
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (visual == null || feet == null) return;
            Vector3 position = feet.position + Vector3.up * footOffset;
            visual.sortingOrder = OrderForY(position.y);
            if (!useGroundLight) return;
            visual.GetPropertyBlock(properties);
            properties.SetVector(GroundLightPositionId, new Vector4(position.x, position.y, 0, 1));
            visual.SetPropertyBlock(properties);
        }
    }
}
