using System;
using ProceduralGeneration;
using UnityEngine;

namespace Coinbound
{
    public enum DungeonSpell { Pulse, Trace, Ward, Decoy, Recall }
    public enum SpellCastResult { Success, Unavailable, Cooldown, NotEnoughMana, NoFreeSpace }

    public sealed class SpellDefinition
    {
        public readonly DungeonSpell Id;
        public readonly string Word;
        public readonly float Mana;
        public readonly float Cooldown, Duration;
        private SpellDefinition(DungeonSpell id, string word, float mana, float cooldown, float duration)
        { Id = id; Word = word; Mana = mana; Cooldown = cooldown; Duration = duration; }
        public static readonly SpellDefinition[] All = {
            new SpellDefinition(DungeonSpell.Pulse, "pulse", 20, 3, 0),
            new SpellDefinition(DungeonSpell.Trace, "trace", 15, 6, 3),
            new SpellDefinition(DungeonSpell.Ward, "ward", 40, 10, 3),
            new SpellDefinition(DungeonSpell.Decoy, "decoy", 25, 12, 5),
            new SpellDefinition(DungeonSpell.Recall, "recall", 25, 10, 4)
        };
    }

    /// <summary>Mana and cooldown transactions are independent of microphone/recognition technology.</summary>
    public sealed class DungeonSpellcaster : MonoBehaviour
    {
        private DungeonPlayer owner;
        private readonly float[] cooldowns = new float[SpellDefinition.All.Length];
        private readonly float[] activeRemaining = new float[SpellDefinition.All.Length];
        private readonly float[] cooldownDurations = new float[SpellDefinition.All.Length];
        private readonly System.Collections.Generic.List<GameObject> effects = new System.Collections.Generic.List<GameObject>();
        private readonly System.Collections.Generic.List<DungeonPulse> pulses = new System.Collections.Generic.List<DungeonPulse>();
        private float pulseFlash;
        public DungeonSpell LastCast { get; private set; } = DungeonSpell.Pulse;
        private DungeonRecall recall;
        private float wardRemaining, wardFlash, wardRepelDelay;
        private DungeonCombatMesh wardMesh;
        private DungeonSpellIcon wardIcon;
        public DungeonMap Map { get; private set; }
        public bool WardActive => wardRemaining > 0;
        public DungeonPlayer Owner => owner;
        public event Action<DungeonSpell> CastSucceeded;
        public event Action<DungeonSpell, SpellCastResult> CastRejected;
        internal void RestoreCooldowns(float[] saved)
        { for(int i=0;i<cooldowns.Length;i++){cooldowns[i]=saved!=null&&i<saved.Length?Mathf.Max(0,saved[i]):0;cooldownDurations[i]=SpellDefinition.All[i].Cooldown;} }
        public float Cooldown(DungeonSpell spell) => cooldowns[(int)spell];
        public float CooldownDuration(DungeonSpell spell) => cooldownDurations[(int)spell];
        public float ActiveRemaining(DungeonSpell spell) => activeRemaining[(int)spell];
        public bool IsActive(DungeonSpell spell) => spell == DungeonSpell.Pulse
            ? pulseFlash > 0 || pulses.Exists(p => p != null && p.isActiveAndEnabled)
            : activeRemaining[(int)spell] > 0;
        public float ManaCost(DungeonSpell spell) => spell == DungeonSpell.Recall && activeRemaining[(int)spell] > 0 && recall != null
            ? 0 : SpellDefinition.All[(int)spell].Mana;
        public void Initialize(DungeonPlayer player) { owner = player; }
        public void SetMap(DungeonMap map)
        {
            if (Map == map) return;
            ClearWorldEffects(); Map = map;
        }
        internal void Track(GameObject effect) { effects.RemoveAll(item => item == null); effects.Add(effect); }
        private void StartCooldown(int index, float elapsed = 0)
        {
            cooldownDurations[index] = SpellDefinition.All[index].Cooldown;
            cooldowns[index] = Mathf.Max(0, cooldownDurations[index] - elapsed);
        }
        private void ClearWorldEffects()
        {
            foreach (GameObject effect in effects) if (effect != null) { effect.SetActive(false); Destroy(effect); }
            effects.Clear(); pulses.Clear(); pulseFlash = 0; recall = null; wardRemaining = wardFlash = wardRepelDelay = 0;
            for (int i = 0; i < activeRemaining.Length; i++)
            { if (activeRemaining[i] > 0) StartCooldown(i); activeRemaining[i] = 0; }
            if (wardMesh != null) wardMesh.gameObject.SetActive(false);
            if (wardIcon != null) wardIcon.gameObject.SetActive(false);
        }
        private void OnDestroy() => ClearWorldEffects();
        private SpellCastResult Reject(DungeonSpell spell, SpellCastResult reason)
        { CastRejected?.Invoke(spell, reason); return reason; }
        private void Succeeded(DungeonSpell spell) { LastCast = spell; CastSucceeded?.Invoke(spell); }

        public SpellCastResult TryCast(DungeonSpell spell)
        {
            if ((uint)spell >= SpellDefinition.All.Length || owner == null || !owner.CanAct || Time.timeScale <= 0) return SpellCastResult.Unavailable;
            SpellDefinition definition = SpellDefinition.All[(int)spell];
            if (spell == DungeonSpell.Recall && activeRemaining[(int)spell] > 0 && recall != null)
            {
                if (!recall.TryReturn()) return Reject(spell, SpellCastResult.NoFreeSpace);
                activeRemaining[(int)spell] = 0;
                StartCooldown((int)spell);
                recall = null;
                Succeeded(spell);
                return SpellCastResult.Success;
            }
            if (cooldowns[(int)spell] > 0 || activeRemaining[(int)spell] > 0) return Reject(spell, SpellCastResult.Cooldown);
            if (!owner.TrySpendMana(definition.Mana)) return Reject(spell, SpellCastResult.NotEnoughMana);
            activeRemaining[(int)spell] = definition.Duration;
            if (definition.Duration <= 0) StartCooldown((int)spell);
            switch (spell)
            {
                case DungeonSpell.Pulse:
                    pulseFlash = .18f;
                    DungeonSpellFlash.Create(this, owner.transform.position, new Color(.6f,.9f,1), .18f);
                    DungeonEnemy target = FindVisibleEnemy(owner.transform.position, 8, out Vector2 point);
                    if (target != null) { pulses.RemoveAll(p => p == null); pulses.Add(DungeonPulse.Launch(owner, point)); }
                    break;
                case DungeonSpell.Trace: DungeonTrace.Create(this); break;
                case DungeonSpell.Ward: wardRemaining = definition.Duration; wardFlash = .25f; break;
                case DungeonSpell.Decoy: DungeonDecoy.Create(owner, Map, definition.Duration); break;
                case DungeonSpell.Recall: recall = DungeonRecall.Create(this); break;
            }
            Succeeded(spell);
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
            DungeonSpellFlash.Create(this, owner.transform.position, new Color(1,.72f,.15f), .22f);
            if (wardRepelDelay <= 0)
            {
                wardRepelDelay = .12f;
                bool repelled = false;
                foreach (DungeonEnemy enemy in DungeonEnemy.Active)
                {
                    if (enemy == null || enemy.IsDead) continue;
                    Vector2 offset = enemy.transform.position - owner.transform.position;
                    if (offset.sqrMagnitude > 1.3f * 1.3f || !DungeonCombatPhysics.ClearLine(owner.transform.position, enemy.transform.position)) continue;
                    Vector2 away = offset.sqrMagnitude > .001f ? offset.normalized : owner.AimDirection;
                    enemy.Stun(.22f, away * 8.3f);
                    repelled = true;
                }
                if (repelled) DungeonSpellFlash.Create(this, owner.transform.position, new Color(1,.8f,.3f), .3f, true);
            }
            return true;
        }

        private void Update()
        {
            if (owner == null || owner.IsDead)
            { if (wardMesh != null) wardMesh.gameObject.SetActive(false); if (wardIcon != null) wardIcon.gameObject.SetActive(false); return; }
            if (!owner.CanAct) return;
            float dt = Time.deltaTime;
            pulseFlash = Mathf.Max(0, pulseFlash - dt);
            for (int i = 0; i < cooldowns.Length; i++)
            {
                if (activeRemaining[i] > 0)
                {
                    float leftover = Mathf.Max(0, dt - activeRemaining[i]);
                    activeRemaining[i] = Mathf.Max(0, activeRemaining[i] - dt);
                    if (activeRemaining[i] == 0) StartCooldown(i, leftover);
                }
                else cooldowns[i] = Mathf.Max(0, cooldowns[i] - dt);
            }
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
            wardMesh.Begin(center, feet, false, .65f, false, true);
            DungeonEffectLights.Glow(wardMesh, feet, 1.8f + wardFlash * 2, .55f + wardFlash * 2, new Color(1,.67f,.16f));
            float pulse = wardFlash / .25f;
            Color tint = Color.Lerp(new Color(1, .75f, .23f, .65f), Color.white, pulse);
            if (wardRemaining < .5f && !DungeonSettings.ReducedMotion)
                tint.a *= Mathf.FloorToInt(wardRemaining * 12) % 2 == 0 ? .35f : 1;
            float radius = .65f;
            // A filled translucent shell remains visible throughout protection, not only its outline.
            for (int y = -27; y <= 27; y++)
            for (int x = -21; x <= 21; x++)
            {
                float ellipse = x * x / (21f * 21) + y * y / (27f * 27);
                if (ellipse > 1) continue;
                Color shell = Color.Lerp(new Color(1,.65f,.16f), new Color(1,.95f,.65f), pulse * .7f);
                shell.a = (.10f + Mathf.Pow(ellipse,3) * .12f + pulse * .16f) * (tint.a / .65f);
                wardMesh.Pixel(center + new Vector2(x,y) / 32, shell);
            }
            for (int i = 0; i < 64; i++)
            {
                float angle = i * Mathf.PI * 2 / 64;
                wardMesh.Pixel(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 1.18f) * radius, tint, pulse > .5f ? 2 : 1, 1);
            }
            if (!DungeonSettings.ReducedMotion)
                for (int i = 0; i < 6; i++)
                {
                    float angle = wardRemaining * 1.8f + i * Mathf.PI / 3;
                    wardMesh.Glint(center + new Vector2(Mathf.Cos(angle),Mathf.Sin(angle) * 1.18f) * radius,
                        new Color(1,.9f,.55f,tint.a), i % 2 == 0 ? 1 : 0);
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
