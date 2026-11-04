using UnityEngine;

namespace Coinbound
{
    public sealed class DungeonSpellIcon : MonoBehaviour
    {
        private Sprite ownedSprite;
        public static DungeonSpellIcon Create(Transform parent, string name, Material material, Color color)
        {
            Texture2D texture = Resources.Load<Texture2D>("InterfaceArt/Spells/" + name);
            if (texture == null) return null;
            var obj = new GameObject(name + " indicator", typeof(SpriteRenderer)); obj.transform.SetParent(parent, false);
            var icon = obj.AddComponent<DungeonSpellIcon>();
            icon.ownedSprite = Sprite.Create(texture, new Rect(0,0,texture.width,texture.height), new Vector2(.5f,.5f),32);
            var renderer = obj.GetComponent<SpriteRenderer>(); renderer.sprite = icon.ownedSprite;
            renderer.sharedMaterial = material; renderer.color = color;
            var properties = new MaterialPropertyBlock(); properties.SetFloat("_EffectUnlit",1); renderer.SetPropertyBlock(properties);
            return icon;
        }
        public void Place(Vector2 position, float feetY)
        {
            transform.position = DungeonCombatPixels.Snap(position);
            GetComponent<SpriteRenderer>().sortingOrder = DungeonDepthSort.OrderForY(feetY) + 6;
        }
        private void OnDestroy() { if (ownedSprite != null) Destroy(ownedSprite); }
    }
}
