using UnityEngine;

namespace Coinbound
{
    /// <summary>Small vector effects rendered as opaque squares on the same 32-pixel world grid as the art.</summary>
    internal static class DungeonCombatPixels
    {
        private static Sprite square;
        private static readonly int GroundLightPosition = Shader.PropertyToID("_GroundLightPosition");
        private static readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            if (square != null) Object.Destroy(square);
            square = null;
        }

        internal static SpriteRenderer Create(Transform parent, Material material, Color color)
        {
            if (square == null)
            {
                Texture2D white = Texture2D.whiteTexture;
                square = Sprite.Create(white, new Rect(0, 0, white.width, white.height), Vector2.one * 0.5f, white.width);
                square.name = "Combat vector square";
                square.hideFlags = HideFlags.HideAndDontSave;
            }
            var obj = new GameObject("Pixel", typeof(SpriteRenderer));
            obj.transform.SetParent(parent, false);
            var renderer = obj.GetComponent<SpriteRenderer>();
            renderer.sprite = square;
            if (material != null) renderer.sharedMaterial = material;
            renderer.color = color;
            return renderer;
        }

        internal static Vector2 Snap(Vector2 position) => new Vector2(Mathf.Round(position.x * 32) / 32, Mathf.Round(position.y * 32) / 32);

        internal static void Place(SpriteRenderer renderer, Vector2 position, Vector2 feet, Vector2 size, Color color, int offset = 2, bool ground = false)
        {
            renderer.transform.position = Snap(position);
            renderer.transform.localScale = new Vector3(size.x, size.y, 1);
            renderer.sortingOrder = ground ? 2 : DungeonDepthSort.OrderForY(feet.y) + offset;
            renderer.color = color;
            Properties.Clear();
            Properties.SetVector(GroundLightPosition, ground ? Vector4.zero : new Vector4(feet.x, feet.y, 0, 1));
            renderer.SetPropertyBlock(Properties);
        }
    }
}
