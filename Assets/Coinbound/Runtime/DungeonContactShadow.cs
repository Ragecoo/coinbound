using UnityEngine;

namespace Coinbound
{
    /// <summary>A small pixel-stepped contact patch; it never changes the actor's collision or light sample.</summary>
    internal sealed class DungeonContactShadow : MonoBehaviour
    {
        private DungeonPlayer clock;
        private DungeonEnemy enemy;
        private Transform feet;
        private SpriteRenderer[] rows;
        private float width;

        internal static DungeonContactShadow Create(Transform feet, DungeonPlayer clock, DungeonEnemy enemy, Material material, float width)
        {
            var obj = new GameObject("Feet contact shadow");
            obj.transform.SetParent(feet, false);
            var shadow = obj.AddComponent<DungeonContactShadow>();
            shadow.feet = feet;
            shadow.clock = clock;
            shadow.enemy = enemy;
            shadow.width = Mathf.Round(width * 32) / 32;
            shadow.rows = new SpriteRenderer[3];
            for (int i = 0; i < shadow.rows.Length; i++)
                shadow.rows[i] = DungeonCombatPixels.Create(obj.transform, material, Color.black);
            shadow.Draw();
            return shadow;
        }

        private void LateUpdate()
        {
            if (feet == null || clock == null) { Destroy(gameObject); return; }
            if (!clock.CanAct) return;
            Draw();
        }

        private void Draw()
        {
            bool visible = DungeonFxSettings.Current.contactShadows && (enemy == null ? !clock.IsDead : !enemy.IsDead);
            Vector2 center = feet.position;
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i].enabled = visible;
                if (!visible) continue;
                float rowWidth = width - (i == 1 ? 0 : 4f / 32);
                Color color = new Color(0.03f, 0.025f, 0.055f, i == 1 ? 0.31f : 0.18f);
                DungeonCombatPixels.Place(rows[i], center + Vector2.up * ((i - 1) / 32f), center,
                    new Vector2(Mathf.Max(1f / 32, rowWidth), 1f / 32), color, 0, true);
            }
        }
    }
}
