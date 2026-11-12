using UnityEngine;
using UnityEngine.Tilemaps;

namespace Coinbound
{
    /// <summary>A swept projectile: the visual is raised, while hits and wall collision stay on the feet plane.</summary>
    public sealed class DungeonArrow : MonoBehaviour
    {
        private DungeonPlayer owner;
        private float damage;
        private bool critical;
        private Vector2 direction;
        private Vector2 position;
        private float remaining = 1.5f;
        private SpriteRenderer[] pixels;
        private DungeonCombatMesh shadow;
        private float stuckAge;
        private float stuckLifetime;
        private Vector2 stuckTip;
        private const float Speed = 16;

        public Vector2 Direction => direction;
        public bool IsStuck { get; private set; }
        public Vector2 VisibleTip { get; private set; }

        public static DungeonArrow Launch(DungeonPlayer player, Vector2 direction)
        {
            var obj = new GameObject("Arrow");
            obj.transform.SetParent(player.transform.parent, false);
            var arrow = obj.AddComponent<DungeonArrow>();
            arrow.owner = player;
            arrow.damage = player.Stats.RollDamage(Random.value);
            arrow.critical = arrow.damage > player.Stats.Damage;
            arrow.direction = direction.normalized;
            // Start at the feet, so an adjacent obstacle can never be skipped at spawn.
            arrow.position = player.transform.position;
            arrow.pixels = new SpriteRenderer[19];
            for (int i = 0; i < arrow.pixels.Length; i++) arrow.pixels[i] = DungeonCombatPixels.Create(obj.transform, player.EffectMaterial, Color.white);
            arrow.shadow = DungeonCombatMesh.Create(obj.transform, "Arrow ground shadow", player.EffectMaterial);
            arrow.Draw();
            return arrow;
        }

        private void Update()
        {
            if (owner == null) { Destroy(gameObject); return; }
            if (!owner.CanAct || owner.IsHitStopped) return;
            float dt = Time.deltaTime;
            if (IsStuck)
            {
                stuckAge += dt;
                if (stuckAge >= stuckLifetime) { Destroy(gameObject); return; }
                Draw();
                return;
            }
            Vector2 next = position + direction * (Speed * dt);
            if (DungeonCombatPhysics.ArrowHit(position, next, out RaycastHit2D hit))
            {
                DungeonEnemy enemy = hit.collider.GetComponentInParent<DungeonEnemy>();
                if (enemy != null)
                {
                    enemy.ReceiveHit(damage, direction, 2.7f, true, critical);
                    Destroy(gameObject);
                }
                else if(hit.collider.TryGetComponent<DungeonProp>(out var prop))
                {prop.ReceiveHit(damage,DungeonProps.NewAttackId());Destroy(gameObject);}
                else
                {
                    Vector2 contactTip = ProjectContact(hit);
                    DungeonCombatFx.WallHit(owner, hit.point, hit.normal, contactTip);
                    if (!DungeonFxSettings.Current.arrowImpacts) { Destroy(gameObject); return; }
                    // Collision and the light sample stay on the room side of the wall.
                    // The drawn tip is projected onto that particular wall's visible face.
                    position = hit.point + hit.normal * 0.04f;
                    stuckTip = contactTip;
                    stuckLifetime = Mathf.Max(0.1f, DungeonFxSettings.Current.arrowStuckLifetime);
                    IsStuck = true;
                    stuckAge = 0;
                    Draw();
                }
                return;
            }
            position = next;
            remaining -= dt;
            if (remaining <= 0) { Destroy(gameObject); return; }
            Draw();
        }

        private static Vector2 ProjectContact(RaycastHit2D hit)
        {
            SpriteRenderer objectArt = hit.collider.GetComponent<SpriteRenderer>();
            if (!(hit.collider is TilemapCollider2D) && objectArt != null && objectArt.sprite != null)
            {
                // A fountain or entrance has its own silhouette, not a one-tile wall face.
                // Keep the raised contact inside its art instead of treating its lower rim as a north wall.
                Vector2 objectTip = hit.point + Vector2.up * DungeonHeroAnimator.HandHeight - hit.normal * 0.08f;
                Bounds bounds = objectArt.bounds;
                const float margin = 2f / 32;
                objectTip.x = Mathf.Clamp(objectTip.x, bounds.min.x + margin, bounds.max.x - margin);
                objectTip.y = Mathf.Clamp(objectTip.y, bounds.min.y + margin, bounds.max.y - margin);
                return objectTip;
            }

            DungeonFxSettings settings = DungeonFxSettings.Current;
            bool northWall = hit.normal.y < -0.7f;
            bool southWall = hit.normal.y > 0.7f;
            float inset = northWall ? settings.arrowNorthWallInset : settings.arrowOtherWallInset;
            Vector2 tip = hit.point - hit.normal * Mathf.Max(0, inset);
            if (northWall || southWall) return tip;

            // Side walls retain the projectile's height. Stop at the current stone tile's top
            // so an impact beside a corner cannot appear over the floor in the next row.
            tip.y += DungeonHeroAnimator.HandHeight;
            Tilemap terrain = hit.collider.GetComponent<Tilemap>();
            float bottom, top;
            if (terrain != null)
            {
                Vector3Int cell = terrain.WorldToCell(hit.point - hit.normal * 0.05f);
                float first = terrain.CellToWorld(cell).y;
                float second = terrain.CellToWorld(cell + Vector3Int.up).y;
                bottom = Mathf.Min(first, second);
                top = Mathf.Max(first, second);
            }
            else
            {
                bottom = hit.collider.bounds.min.y;
                top = hit.collider.bounds.max.y;
            }
            // Leave one more pixel for the brief impact vibration and grid rounding.
            if (top - bottom > 4f / 32) tip.y = Mathf.Clamp(tip.y, bottom + 2f / 32, top - 2f / 32);
            return tip;
        }

        private void Draw()
        {
            transform.position = position;
            Vector2 side = new Vector2(-direction.y, direction.x);
            float vibration = IsStuck ? Mathf.Sin(stuckAge * 85) * Mathf.Clamp01(1 - stuckAge / 0.23f) / 32 : 0;
            Vector2 tip = (IsStuck ? stuckTip : position + Vector2.up * DungeonHeroAnimator.HandHeight) + side * vibration;
            VisibleTip = DungeonCombatPixels.Snap(tip);
            Color shaft = new Color32(156, 154, 181, 255);
            Color metal = new Color32(226, 230, 246, 255);
            float opacity = IsStuck ? Mathf.Clamp01((stuckLifetime - stuckAge) / Mathf.Min(0.3f, stuckLifetime)) : 1;
            shaft.a = metal.a = opacity;
            for (int i = 0; i < 13; i++)
                DungeonCombatPixels.Place(pixels[i], tip - direction * (i / 32f), position, Vector2.one / 32, i < 3 ? metal : shaft);
            for (int i = 0; i < 2; i++)
            {
                float sign = i == 0 ? -1 : 1;
                DungeonCombatPixels.Place(pixels[13 + i], tip - direction * (2 / 32f) + side * (sign / 32), position, Vector2.one / 32, metal);
                DungeonCombatPixels.Place(pixels[15 + i], tip - direction * (10 / 32f) + side * (sign / 32), position, Vector2.one / 32, metal);
                DungeonCombatPixels.Place(pixels[17 + i], tip - direction * (11 / 32f) + side * (sign * 2 / 32), position, Vector2.one / 32, shaft);
            }
            shadow.Begin(position, position, true);
            if (DungeonFxSettings.Current.contactShadows)
                shadow.Line(position - direction * (12f / 32), position,
                    new Color(0.03f, 0.025f, 0.05f, opacity * 0.22f));
            shadow.End();
        }
    }
}
