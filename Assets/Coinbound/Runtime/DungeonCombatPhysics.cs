using UnityEngine;

namespace Coinbound
{
    /// <summary>Combat queries use feet coordinates and ignore actors when checking stone walls and solid props.</summary>
    internal static class DungeonCombatPhysics
    {
        private static readonly RaycastHit2D[] CastHits = new RaycastHit2D[64];
        private static readonly Collider2D[] OverlapHits = new Collider2D[32];
        private static readonly ContactFilter2D SolidFilter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = true,
            layerMask = Physics2D.DefaultRaycastLayers
        };

        internal static bool IsObstacle(Collider2D collider) => collider != null && !collider.isTrigger &&
            collider.GetComponentInParent<DungeonPlayer>() == null && collider.GetComponentInParent<DungeonEnemy>() == null;

        internal static bool ClearLine(Vector2 from, Vector2 to, float radius = 0, Collider2D ignored = null, Transform ignoredOwner = null)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < 0.0001f) return true;
            // A zero-radius shape cast is not a ray. Use the real ray query for sight/melee,
            // and a swept circle only when navigating with a nonzero feet clearance.
            int count = radius > 0
                ? Physics2D.CircleCast(from, radius, delta.normalized, SolidFilter, CastHits, delta.magnitude)
                : Physics2D.Raycast(from, delta.normalized, SolidFilter, CastHits, delta.magnitude);
            for (int i = 0; i < count; i++)
                if (CastHits[i].collider != ignored && (ignoredOwner == null || !CastHits[i].collider.transform.IsChildOf(ignoredOwner)) && IsObstacle(CastHits[i].collider)) return false;
            return true;
        }

        internal static bool FreeFeet(Vector2 point, float radius)
        {
            int count = Physics2D.OverlapCircle(point, radius, SolidFilter, OverlapHits);
            for (int i = 0; i < count; i++) if (IsObstacle(OverlapHits[i])) return false;
            return true;
        }

        internal static bool ArrowHit(Vector2 from, Vector2 to, out RaycastHit2D closest)
        {
            Vector2 delta = to - from;
            int count = Physics2D.CircleCast(from, 0.08f, delta.normalized, SolidFilter, CastHits, delta.magnitude);
            closest = default;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = CastHits[i].collider;
                if (collider == null || collider.isTrigger || collider.GetComponentInParent<DungeonPlayer>() != null) continue;
                DungeonEnemy enemy = collider.GetComponentInParent<DungeonEnemy>();
                if (enemy != null && enemy.IsDead) continue;
                if (CastHits[i].distance >= distance) continue;
                distance = CastHits[i].distance;
                closest = CastHits[i];
            }
            return closest.collider != null;
        }
    }
}
