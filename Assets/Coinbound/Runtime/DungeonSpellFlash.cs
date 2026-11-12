using UnityEngine;

namespace Coinbound
{
    /// <summary>Short event-driven pixel flash, or a wave emitted only by an actual repel.</summary>
    internal sealed class DungeonSpellFlash : MonoBehaviour
    {
        private DungeonPlayer owner;
        private DungeonCombatMesh pixels;
        private Vector2 origin;
        private Color color;
        private float age, duration;
        private bool wave;
        private readonly Vector2[] velocities = new Vector2[16];
        internal static void Create(DungeonSpellcaster caster, Vector2 origin, Color color, float duration, bool wave = false)
        {
            var obj = new GameObject(wave ? "Ward repel wave" : "Spell cast flash");
            obj.transform.SetParent(caster.transform.parent, false);
            var fx = obj.AddComponent<DungeonSpellFlash>();
            fx.owner = caster.Owner; fx.origin = origin; fx.color = color; fx.duration = duration; fx.wave = wave;
            fx.pixels = DungeonCombatMesh.Create(obj.transform, "Flash pixels", caster.Owner.EffectMaterial);
            for (int i = 0; i < fx.velocities.Length; i++)
            {
                float angle = i * 2.399963f;
                float speed = .9f + (i * 7 % 11) * .2f;
                fx.velocities[i] = new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)) * speed;
            }
            caster.Track(obj);
        }
        private void Update()
        {
            if (owner == null || owner.IsDead) { Destroy(gameObject); return; }
            if (!owner.CanAct) return;
            age += Time.deltaTime;
            if (age >= duration) { Destroy(gameObject); return; }
            Vector2 center = origin + Vector2.up * .5f;
            pixels.Begin(center, origin, false, .8f, false, true);
            Color tint = color; tint.a *= 1 - age / duration;
            DungeonEffectLights.Glow(this, origin, wave ? 2.4f : 1.7f, (wave ? .9f : 1.2f) * tint.a, color);
            if (wave && !DungeonSettings.ReducedMotion)
                for (int i = 0; i < 40; i++)
                {
                    float angle = i * Mathf.PI / 20;
                    pixels.Pixel(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * .65f) * (.3f + age / duration * 1.2f), tint);
                }
            {
                float t = age / duration;
                if (!wave && t < .45f) pixels.Glint(center, Color.Lerp(color, Color.white, .75f), DungeonSettings.ReducedMotion ? 2 : Mathf.Max(1, Mathf.RoundToInt(5 * (1 - t))));
                if (!DungeonSettings.ReducedMotion)
                    for (int i = 0; i < velocities.Length; i++)
                    {
                        float lifetime = duration * (.55f + (i % 5) * .11f);
                        if (age >= lifetime) continue;
                        Vector2 offset = velocities[i] * age + Vector2.down * (1.8f * age * age);
                        if (!DungeonCombatPhysics.ClearLine(origin, origin + offset)) continue;
                        Vector2 point = center + offset;
                        Color spark = Color.Lerp(color, Color.white, .35f + .5f * (1 - t));
                        spark.a = Mathf.Ceil((1 - age / lifetime) * 4) / 4;
                        Color tail = color; tail.a = spark.a * .6f;
                        pixels.Line(point - velocities[i].normalized * (2f / 32), point, tail);
                        pixels.Pixel(point, spark, i % 4 == 0 ? 2 : 1, 1);
                    }
            }
            pixels.End();
        }
    }
}
