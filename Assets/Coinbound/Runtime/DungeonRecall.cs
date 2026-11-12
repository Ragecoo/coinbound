using ProceduralGeneration;
using UnityEngine;

namespace Coinbound
{
    /// <summary>Stationary return anchor. Its lifetime is owned by the spellcaster.</summary>
    public sealed class DungeonRecall : MonoBehaviour
    {
        private DungeonSpellcaster caster;
        private DungeonCombatMesh pixels;
        private DungeonCombatMesh departurePixels;
        private Vector2 origin, departure, arrival;
        private bool returned;
        private float flashAge;
        private readonly Collider2D[] overlaps = new Collider2D[64];
        public Vector2 Position => origin;
        public static DungeonRecall Create(DungeonSpellcaster caster)
        {
            var obj = new GameObject("Recall rune");
            obj.transform.SetParent(caster.Owner.transform.parent, false);
            var rune = obj.AddComponent<DungeonRecall>();
            rune.caster = caster; rune.origin = caster.Owner.transform.position;
            obj.transform.position = rune.origin;
            rune.pixels = DungeonCombatMesh.Create(obj.transform, "Rune pixels", caster.Owner.EffectMaterial);
            caster.Track(obj);
            DungeonSpellFlash.Create(caster, rune.origin, new Color(.65f,.25f,1), .2f);
            return rune;
        }
        private bool Free(Vector2 point)
        {
            if (caster.Map != null && !caster.Map.IsTileFloor(new TilePosition(Mathf.FloorToInt(point.x), Mathf.FloorToInt(1 - point.y)))) return false;
            var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = Physics2D.DefaultRaycastLayers };
            int count = Physics2D.OverlapCircle(point, .32f, filter, overlaps);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = overlaps[i];
                if (collider.GetComponentInParent<DungeonPlayer>() == caster.Owner) continue;
                DungeonEnemy enemy = collider.GetComponentInParent<DungeonEnemy>();
                if (enemy != null && enemy.IsDead) continue;
                return false;
            }
            return true;
        }
        public bool TryReturn()
        {
            if (returned) return false;
            Vector2 target = origin;
            if (!Free(target))
            {
                float best = float.PositiveInfinity;
                Vector2 center = new Vector2(Mathf.Floor(origin.x) + .5f, Mathf.Floor(origin.y) + .5f);
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    Vector2 candidate = center + new Vector2(x, y);
                    float distance = (candidate - origin).sqrMagnitude;
                    if (distance >= best || !Free(candidate)) continue;
                    best = distance; target = candidate;
                }
                if (float.IsPositiveInfinity(best)) return false;
            }
            departure = caster.Owner.transform.position; arrival = target;
            DungeonSpellFlash.Create(caster, departure, new Color(.7f,.35f,1), .3f);
            DungeonSpellFlash.Create(caster, arrival, new Color(.7f,.35f,1), .3f);
            departurePixels = DungeonCombatMesh.Create(transform, "Departure sparks", caster.Owner.EffectMaterial);
            caster.Owner.TeleportTo(target); returned = true;
            return true;
        }
        private void LateUpdate()
        {
            if (caster == null || caster.Owner == null || caster.Owner.IsDead) { Destroy(gameObject); return; }
            if (!caster.Owner.CanAct) return;
            if (returned)
            {
                flashAge += Time.deltaTime;
                if (flashAge >= .3f) { Destroy(gameObject); return; }
                DrawTeleport(); return;
            }
            float remaining = caster.ActiveRemaining(DungeonSpell.Recall);
            if (remaining <= 0) { Destroy(gameObject); return; }
            pixels.Begin(origin, origin, true, .7f, false, true);
            DungeonEffectLights.Glow(this, origin, 1.65f, .65f * Mathf.Min(1,remaining/.3f), new Color(.65f,.24f,1));
            Color32 tint = new Color32(179, 119, 240, 220);
            Vector2[] corners = { origin + Vector2.up * .28f, origin + Vector2.right * .46f,
                origin + Vector2.down * .28f, origin + Vector2.left * .46f };
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = corners[i], b = corners[(i + 1) % 4];
                pixels.Line(Vector2.Lerp(a, b, .13f), Vector2.Lerp(a, b, .87f), tint);
                if (remaining > 1 || DungeonSettings.ReducedMotion || Mathf.FloorToInt(remaining * 8) % 2 == 0)
                    pixels.Pixel(a, tint, 2, 2);
            }
            if (!DungeonSettings.ReducedMotion)
                for (int i = 0; i < 4; i++)
                {
                    float rise = (4 - remaining + i * .23f) % .8f;
                    Color spark = new Color(.8f,.5f,1,(1-rise/.8f)*.7f);
                    pixels.Pixel(corners[i] + Vector2.up * (rise * .25f), spark);
                }
            pixels.End();
        }
        private void DrawTeleport()
        {
            float strength = 1 - flashAge / .3f;
            Color32 white = new Color(1, 1, 1, strength);
            DungeonEffectLights.Glow(departurePixels, departure, 2.3f, 1.3f * strength, new Color(.7f,.4f,1));
            DungeonEffectLights.Glow(pixels, arrival, 2.3f, 1.3f * strength, new Color(.7f,.4f,1));
            departurePixels.Begin(departure + Vector2.up * .3f, departure, false, 0, false, true);
            departurePixels.Glint(departure + Vector2.up * .55f, white, DungeonSettings.ReducedMotion ? 2 : Mathf.Max(1, Mathf.RoundToInt(strength * 6)));
            if (!DungeonSettings.ReducedMotion)
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4;
                    departurePixels.Pixel(departure + Vector2.up * .3f + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (.1f + flashAge * 1.7f), white, 2, 2);
                }
            departurePixels.End();
            pixels.Begin(arrival + Vector2.up * .5f, arrival, false, 0, false, true);
            Vector2 center = arrival + Vector2.up * .55f;
            pixels.Line(center - Vector2.up * .55f * strength, center + Vector2.up * .55f * strength, white, 2);
            pixels.Line(center - Vector2.right * .35f * strength, center + Vector2.right * .35f * strength, white, 2);
            pixels.End();
        }
    }
}
