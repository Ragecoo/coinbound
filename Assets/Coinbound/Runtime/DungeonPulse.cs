using UnityEngine;

namespace Coinbound
{
    public sealed class DungeonPulse : MonoBehaviour
    {
        private DungeonPlayer owner;
        private DungeonCombatMesh pixels;
        private Vector2 position, direction;
        private float damage, age, explosion;
        private bool impacted;
        public static DungeonPulse Launch(DungeonPlayer owner, Vector2 target)
        {
            var obj = new GameObject("Pulse projectile"); obj.transform.SetParent(owner.transform.parent, false);
            var pulse = obj.AddComponent<DungeonPulse>(); pulse.owner = owner;
            pulse.position = owner.transform.position;
            pulse.direction = (target - pulse.position).normalized;
            if (pulse.direction.sqrMagnitude < .01f) pulse.direction = owner.AimDirection;
            pulse.damage = owner.Stats.Damage * 2;
            pulse.pixels = DungeonCombatMesh.Create(obj.transform, "Pulse pixels", owner.EffectMaterial);
            return pulse;
        }
        private void Update()
        {
            if (owner == null || owner.IsDead) { Destroy(gameObject); return; }
            if (!owner.CanAct) return;
            float dt = Time.deltaTime; age += dt;
            if (!impacted)
            {
                Vector2 next = position + direction * (12 * dt);
                if (DungeonCombatPhysics.ArrowHit(position, next, out RaycastHit2D hit))
                {
                    position = hit.point + hit.normal * .09f;
                    impacted = true;
                    // Death can disable/remove an enemy synchronously during ReceiveHit.
                    foreach (DungeonEnemy enemy in new System.Collections.Generic.List<DungeonEnemy>(DungeonEnemy.Active))
                    {
                        if (enemy == null || enemy.IsDead) continue;
                        Vector2 offset = (Vector2)enemy.transform.position - position;
                        if (offset.sqrMagnitude <= 4 && DungeonSpellcaster.VisiblePoint(position, enemy, out _))
                            enemy.ReceiveHit(damage, offset.sqrMagnitude > .001f ? offset.normalized : direction, 4);
                    }
                }
                else position = next;
            }
            else { explosion += dt; if (explosion >= .3f) { Destroy(gameObject); return; } }
            Vector2 center = position + Vector2.up * .4f;
            pixels.Begin(center, position, false, 0, false, true);
            if (!impacted)
            {
                pixels.Glint(center, new Color(.5f, .9f, 1), 3);
                for (int i = 1; i < 10; i++) pixels.Pixel(center - direction * (i / 16f), new Color(.25f, .7f, 1, 1-i/10f), 2, 2);
            }
            else for (int i = 0; i < 40; i++)
            {
                float angle = i * Mathf.PI * 2 / 40;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Min(1.85f, explosion * 7);
                if (DungeonCombatPhysics.ClearLine(position, position + offset)) pixels.Pixel(center + offset, new Color(.4f, .85f, 1, 1-explosion/.3f), 2, 2);
            }
            pixels.End();
        }
    }
}
