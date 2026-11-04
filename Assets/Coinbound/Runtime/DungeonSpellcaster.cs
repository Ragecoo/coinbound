using System;
using ProceduralGeneration;
using UnityEngine;

namespace Coinbound
{
    public enum DungeonSpell { Pulse, Trace, Ward, Decoy }
    public enum SpellCastResult { Success, Unavailable, Cooldown, NotEnoughMana }

    public sealed class SpellDefinition
    {
        public readonly DungeonSpell Id;
        public readonly string Word;
        public readonly int Mana;
        public readonly float Cooldown, Duration;
        private SpellDefinition(DungeonSpell id, string word, int mana, float cooldown, float duration)
        { Id = id; Word = word; Mana = mana; Cooldown = cooldown; Duration = duration; }
        public static readonly SpellDefinition[] All = {
            new SpellDefinition(DungeonSpell.Pulse, "pulse", 20, 3, 0),
            new SpellDefinition(DungeonSpell.Trace, "trace", 15, 6, 3),
            new SpellDefinition(DungeonSpell.Ward, "ward", 40, 10, 3),
            new SpellDefinition(DungeonSpell.Decoy, "decoy", 25, 12, 5)
        };
    }

    /// <summary>Mana and cooldown transactions are independent of microphone/recognition technology.</summary>
    public sealed class DungeonSpellcaster : MonoBehaviour
    {
        private DungeonPlayer owner;
        private readonly float[] cooldowns = new float[4];
        private float wardRemaining, wardFlash, wardRepelDelay;
        private DungeonCombatMesh wardMesh;
        private DungeonSpellIcon wardIcon;
        public DungeonMap Map { get; private set; }
        public bool WardActive => wardRemaining > 0;
        public DungeonPlayer Owner => owner;
        public float Cooldown(DungeonSpell spell) => cooldowns[(int)spell];
        public void Initialize(DungeonPlayer player) { owner = player; }
        public void SetMap(DungeonMap map) { Map = map; }

        public SpellCastResult TryCast(DungeonSpell spell)
        {
            if ((uint)spell >= 4 || owner == null || !owner.CanAct || Time.timeScale <= 0) return SpellCastResult.Unavailable;
            SpellDefinition definition = SpellDefinition.All[(int)spell];
            if (cooldowns[(int)spell] > 0) return SpellCastResult.Cooldown;
            if (!owner.TrySpendMana(definition.Mana)) return SpellCastResult.NotEnoughMana;
            cooldowns[(int)spell] = definition.Cooldown;
            switch (spell)
            {
                case DungeonSpell.Pulse:
                    DungeonEnemy target = FindVisibleEnemy(owner.transform.position, 8, out Vector2 point);
                    if (target != null) DungeonPulse.Launch(owner, point);
                    break;
                case DungeonSpell.Trace: DungeonTrace.Create(this); break;
                case DungeonSpell.Ward: wardRemaining = definition.Duration; wardFlash = .25f; break;
                case DungeonSpell.Decoy: DungeonDecoy.Create(owner, Map, definition.Duration); break;
            }
            return SpellCastResult.Success;
        }

        public void Recognized(DungeonSpell spell)
        {
            SpellCastResult result = TryCast(spell);
            DungeonSpellSpeech.Create(owner, SpellDefinition.All[(int)spell].Word, result == SpellCastResult.Success);
        }

        public bool AbsorbHit()
        {
            if (!WardActive) return false;
            wardFlash = .25f;
            if (wardRepelDelay <= 0)
            {
                wardRepelDelay = .12f;
                foreach (DungeonEnemy enemy in DungeonEnemy.Active)
                {
                    if (enemy == null || enemy.IsDead) continue;
                    Vector2 offset = enemy.transform.position - owner.transform.position;
                    if (offset.sqrMagnitude > 1.3f * 1.3f || !DungeonCombatPhysics.ClearLine(owner.transform.position, enemy.transform.position)) continue;
                    Vector2 away = offset.sqrMagnitude > .001f ? offset.normalized : owner.AimDirection;
                    enemy.Stun(.22f, away * 8.3f);
                }
            }
            return true;
        }

        private void Update()
        {
            if (owner == null || owner.IsDead)
            { if (wardMesh != null) wardMesh.gameObject.SetActive(false); if (wardIcon != null) wardIcon.gameObject.SetActive(false); return; }
            if (!owner.CanAct) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < cooldowns.Length; i++) cooldowns[i] = Mathf.Max(0, cooldowns[i] - dt);
            wardRemaining = Mathf.Max(0, wardRemaining - dt);
            wardFlash = Mathf.Max(0, wardFlash - dt);
            wardRepelDelay = Mathf.Max(0, wardRepelDelay - dt);
            if (!WardActive)
            { if (wardMesh != null) wardMesh.gameObject.SetActive(false); if (wardIcon != null) wardIcon.gameObject.SetActive(false); return; }
            if (wardMesh == null) wardMesh = DungeonCombatMesh.Create(transform, "Golden ward", owner.EffectMaterial);
            wardMesh.gameObject.SetActive(true);
            Vector2 feet = owner.transform.position;
            Vector2 center = feet + Vector2.up * .5f;
            if (wardIcon == null) wardIcon = DungeonSpellIcon.Create(transform, "ward", owner.EffectMaterial, new Color(1,.83f,.35f));
            if (wardIcon != null) { wardIcon.gameObject.SetActive(true); wardIcon.Place(feet + Vector2.up * 1.65f, feet.y); }
            wardMesh.Begin(center, feet, false, 0, false, true);
            float pulse = wardFlash / .25f;
            Color tint = Color.Lerp(new Color(1, .75f, .23f, .65f), Color.white, pulse);
            float radius = .65f + pulse * .12f;
            for (int i = 0; i < 64; i++)
            {
                float angle = i * Mathf.PI * 2 / 64;
                wardMesh.Pixel(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 1.18f) * radius, tint, pulse > .5f ? 2 : 1, 1);
            }
            wardMesh.End();
        }

        internal static DungeonEnemy FindVisibleEnemy(Vector2 from, float radius, out Vector2 visiblePoint)
        {
            DungeonEnemy nearest = null; visiblePoint = from;
            float best = radius * radius;
            foreach (DungeonEnemy enemy in DungeonEnemy.Active)
            {
                if (enemy == null || enemy.IsDead) continue;
                Vector2 center = enemy.transform.position;
                float distance = (center - from).sqrMagnitude;
                if (distance > best || !VisiblePoint(from, enemy, out Vector2 point)) continue;
                best = distance; nearest = enemy; visiblePoint = point;
            }
            return nearest;
        }

        internal static bool VisiblePoint(Vector2 from, DungeonEnemy enemy, out Vector2 point)
        {
            point = enemy.transform.position;
            if (DungeonCombatPhysics.ClearLine(from, point)) return true;
            Collider2D collider = enemy.GetComponent<Collider2D>();
            if (collider == null) return false;
            Bounds bounds = collider.bounds;
            // Sample the feet silhouette, so a partly exposed enemy can still be selected.
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI / 6;
                Vector2 candidate = (Vector2)bounds.center + new Vector2(Mathf.Cos(angle) * bounds.extents.x, Mathf.Sin(angle) * bounds.extents.y) * .9f;
                if (!DungeonCombatPhysics.ClearLine(from, candidate)) continue;
                point = candidate; return true;
            }
            return false;
        }
    }
}
