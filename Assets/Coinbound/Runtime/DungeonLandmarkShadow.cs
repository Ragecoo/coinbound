using UnityEngine;

namespace Coinbound
{
    /// <summary>A low object's finite projected shadow. It never enters the wall visibility map.</summary>
    [DefaultExecutionOrder(60)]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class DungeonLandmarkShadow : MonoBehaviour
    {
        private static readonly int ShadowId = Shader.PropertyToID("_GroundShadow");
        private SpriteRenderer caster;
        private SpriteRenderer shadowRenderer;
        private Transform lightSource;
        private MaterialPropertyBlock properties;
        private float heightRatio;
        private float maximumLength;

        public void Initialize(SpriteRenderer landmark, Transform source, float projectionRatio,
            float length, float opacity, float softness)
        {
            caster = landmark;
            lightSource = source;
            heightRatio = projectionRatio;
            maximumLength = Mathf.Max(0, length);
            shadowRenderer = GetComponent<SpriteRenderer>();
            shadowRenderer.sprite = caster.sprite;
            shadowRenderer.sharedMaterial = caster.sharedMaterial;
            shadowRenderer.sortingLayerID = caster.sortingLayerID;
            shadowRenderer.sortingOrder = caster.sortingOrder - 1;
            properties = new MaterialPropertyBlock();
            properties.SetVector(ShadowId, new Vector4(Mathf.Clamp01(opacity), Mathf.Max(0.03125f, softness), 0, 1));
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (shadowRenderer == null) return;
            bool visible = caster != null && lightSource != null && caster.enabled && caster.gameObject.activeInHierarchy;
            shadowRenderer.enabled = visible && maximumLength > 0;
            if (!visible || maximumLength <= 0) return;

            Vector2 awayFromLight = (Vector2)caster.transform.position - (Vector2)lightSource.position;
            // A low object's top projects away from the torch and widens slightly with distance.
            // Include the silhouette radius in the limit, so the outer edge stays close to its base.
            float radius = caster.bounds.extents.magnitude;
            float projection = Mathf.Min(heightRatio, maximumLength / Mathf.Max(awayFromLight.magnitude + radius, 0.001f));
            transform.position = caster.transform.position + (Vector3)(awayFromLight * projection);
            Vector3 scale = caster.transform.localScale;
            transform.localScale = new Vector3(scale.x * (1 + projection), scale.y * (1 + projection), 1);
            DungeonSpriteGrid.Apply(shadowRenderer, properties);
        }
    }
}
