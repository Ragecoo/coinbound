using UnityEngine;

namespace Coinbound
{
    public sealed class DungeonPulse : MonoBehaviour
    {
        private DungeonPlayer owner;
        private DungeonCombatMesh pixels;
        private Vector2 position, direction;
        private float damage, age, explosion;
        private bool impacted, hitWall;
        public static DungeonPulse Launch(DungeonPlayer owner, Vector2 target)
        {
            var obj = new GameObject("Pulse projectile"); obj.transform.SetParent(owner.transform.parent, false);
            var pulse = obj.AddComponent<DungeonPulse>(); pulse.owner = owner;
            pulse.position = owner.transform.position;
            pulse.direction = (target - pulse.position).normalized;
            if (pulse.direction.sqrMagnitude < .01f) pulse.direction = owner.AimDirection;
            pulse.damage = owner.Stats.Damage * 2;
            pulse.pixels = DungeonCombatMesh.Create(obj.transform, "Pulse pixels", owner.EffectMaterial);
            owner.Spells.Track(obj);
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
                    hitWall = hit.collider.GetComponentInParent<DungeonEnemy>() == null;
                    DungeonSpellFlash.Create(owner.Spells, position, new Color(.2f,.7f,1), hitWall ? .16f : .3f);
                    DungeonProps.Current?.AreaHit(position,2,damage);
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
            else { explosion += dt; if (explosion >= (hitWall ? .16f : .3f)) { gameObject.SetActive(false); Destroy(gameObject); return; } }
            Vector2 center = position + Vector2.up * .4f;
            pixels.Begin(center, position, false, .9f, false, true);
            if (!impacted)
            {
                DungeonEffectLights.Glow(this, position, 1.8f, 1.15f, new Color(.18f,.65f,1));
                pixels.Glint(center, new Color(.5f, .9f, 1), 3);
                Vector2 side = new Vector2(-direction.y, direction.x);
                for (int i = 1; i < 12; i++)
                {
                    Vector2 tail = center - direction * (i / 16f) + side * (Mathf.Round(Mathf.Sin(age * 24 - i) * i * .18f) / 32);
                    if (DungeonCombatPhysics.ClearLine(position, tail - Vector2.up * .4f))
                        pixels.Pixel(tail, new Color(.15f,.65f,1,1-i/12f), i < 4 ? 2 : 1, i < 4 ? 2 : 1);
                }
            }
            pixels.End();
        }
    }
}
