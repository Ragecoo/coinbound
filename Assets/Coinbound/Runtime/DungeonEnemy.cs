using System.Collections.Generic;
using ProceduralGeneration;
using UnityEngine;

namespace Coinbound
{
    /// <summary>A readable first melee opponent: sight, pursuit, windup, recovery, stun and death.</summary>
    public sealed class DungeonEnemy : MonoBehaviour
    {
        private static readonly List<DungeonEnemy> Enemies = new List<DungeonEnemy>();
        public static IReadOnlyList<DungeonEnemy> Active => Enemies;
        private const float WindupDuration = 0.42f;
        private DungeonPlayer target;
        private DungeonCombatNavigation navigation;
        private Rigidbody2D body;
        private Collider2D feet;
        private SpriteRenderer visual;
        private Color originalColor = Color.white;
        private Vector2 desiredVelocity;
        private Vector2 knockbackVelocity;
        private Vector2 strikeDirection;
        private float windup;
        private float recovery;
        private float alertRemaining;
        private float hitFlash;
        private float deathRemaining;
        private float hitStopRemaining;
        private float recoilRemaining;
        private Vector2 visualRecoil;
        private Vector3 visualBaseScale = Vector3.one;
        private Vector3 visualBasePosition;
        private DungeonCombatFx tellVisual;
        private DungeonCombatFx stunVisual;
        private DungeonDecoy strikeDecoy;
        private bool strikingDecoy;

        public EnemyStats Stats { get; private set; } = EnemyStats.Default;
        public float Health { get; private set; } = EnemyStats.Default.MaxHealth;
        public int MaxHealth => Stats.MaxHealth;
        public bool IsDead => Health <= 0;
        public bool IsHitStopped => hitStopRemaining > 0;
        public bool IsWindingUp => windup > 0;
        public float WindupProgress => IsWindingUp ? 1 - windup / WindupDuration : 0;
        public float StunRemaining { get; private set; }

        private void OnEnable() { if (!Enemies.Contains(this)) Enemies.Add(this); }
        private void OnDisable()
        {
            Enemies.Remove(this);
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        public void Initialize(DungeonPlayer player, DungeonMap map, EnemyStats stats = null)
        {
            Stats = stats ?? EnemyStats.Default;
            Health = MaxHealth;
            target = player;
            navigation = DungeonCombatNavigation.For(map);
            visual = GetComponentInChildren<SpriteRenderer>();
            if (visual != null)
            {
                // Recoil belongs to a visual child. Scaling the root would deform its feet collider.
                if (visual.transform == transform)
                {
                    SpriteRenderer source = visual;
                    var child = new GameObject("Visual", typeof(SpriteRenderer));
                    child.transform.SetParent(transform, false);
                    visual = child.GetComponent<SpriteRenderer>();
                    visual.sprite = source.sprite;
                    visual.sharedMaterial = source.sharedMaterial;
                    visual.color = source.color;
                    visual.flipX = source.flipX;
                    visual.flipY = source.flipY;
                    visual.sortingLayerID = source.sortingLayerID;
                    visual.sortingOrder = source.sortingOrder;
                    var properties = new MaterialPropertyBlock();
                    source.GetPropertyBlock(properties);
                    visual.SetPropertyBlock(properties);
                    source.enabled = false;
                    child.AddComponent<DungeonDepthSort>().Initialize(visual, transform, 0, true);
                }
                originalColor = visual.color;
                visualBaseScale = visual.transform.localScale;
                visualBasePosition = visual.transform.localPosition;
                DungeonContactShadow.Create(transform, player, this, visual.sharedMaterial, 0.44f);
            }
            body = GetComponent<Rigidbody2D>();
            if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.mass = 1.5f;
            feet = GetComponent<Collider2D>();
            if (feet == null)
            {
                var circle = gameObject.AddComponent<CircleCollider2D>();
                circle.radius = 0.30f;
                feet = circle;
            }
            Collider2D playerFeet = target != null ? target.GetComponent<Collider2D>() : null;
            if (playerFeet != null) feet.sharedMaterial = playerFeet.sharedMaterial;
            // Neighbors have slightly different cadence, without consuming dungeon generator randomness.
            recovery = Mathf.Abs(GetEntityId().GetHashCode() % 7) * 0.04f;
        }

        private void Update()
        {
            if (target == null || !target.CanAct)
            {
                desiredVelocity = Vector2.zero;
                if (body != null) body.linearVelocity = Vector2.zero;
                return;
            }
            float dt = Time.deltaTime;
            if (IsHitStopped)
            {
                hitStopRemaining = Mathf.Max(0, hitStopRemaining - dt);
                body.linearVelocity = Vector2.zero;
                UpdateColor();
                return;
            }
            if (IsDead)
            {
                deathRemaining -= dt;
                if (visual != null)
                {
                    Color color = Color.Lerp(originalColor, new Color(0.24f, 0.19f, 0.32f), 0.7f);
                    color.a = Mathf.Clamp01(deathRemaining / 0.3f);
                    visual.color = color;
                    if (DungeonFxSettings.Current.enemyReactions)
                    {
                        float fall = 1 - Mathf.Clamp01(deathRemaining / 0.3f);
                        visual.transform.localScale = Vector3.Scale(visualBaseScale, new Vector3(1 + fall * 0.12f, 1 - fall * 0.55f, 1));
                        visual.transform.localPosition = visualBasePosition + (Vector3)DungeonCombatPixels.Snap(visualRecoil * fall * 0.08f - Vector2.up * fall * 0.10f);
                    }
                }
                if (deathRemaining <= 0) Destroy(gameObject);
                return;
            }

            recovery = Mathf.Max(0, recovery - dt);
            StunRemaining = Mathf.Max(0, StunRemaining - dt);
            hitFlash = Mathf.Max(0, hitFlash - dt);
            recoilRemaining = Mathf.Max(0, recoilRemaining - dt);
            UpdateReaction();
            knockbackVelocity = Vector2.MoveTowards(knockbackVelocity, Vector2.zero, 22 * dt);
            desiredVelocity = Vector2.zero;
            UpdateColor();
            if (stunVisual != null) stunVisual.SetVisible(StunRemaining > 0.15f);
            if (StunRemaining > 0) return;

            if (windup > 0)
            {
                windup = Mathf.Max(0, windup - dt);
                if (windup <= 0) Strike();
                return;
            }

            DungeonDecoy decoy = DungeonDecoy.Nearest(body.position);
            Vector2 targetPosition = decoy != null ? (Vector2)decoy.transform.position : (Vector2)target.transform.position;
            Vector2 offset = targetPosition - body.position;
            float gap = offset.magnitude;
            bool seesPlayer = gap < 10 && DungeonCombatPhysics.ClearLine(body.position, targetPosition);
            alertRemaining = seesPlayer || decoy != null ? 5 : Mathf.Max(0, alertRemaining - dt);
            if (alertRemaining <= 0 || gap > 19) return;
            if (gap < 1.05f && recovery <= 0 && seesPlayer)
            {
                windup = WindupDuration;
                strikeDecoy = decoy; strikingDecoy = decoy != null;
                strikeDirection = offset.sqrMagnitude > 0.001f ? offset.normalized : Vector2.down;
                tellVisual = DungeonCombatFx.Windup(target, this, strikeDirection, WindupDuration);
                return;
            }
            if (gap < 0.85f || recovery > 0.85f) return;
            DungeonCombatNavigation route = decoy != null ? decoy.Navigation : navigation;
            Vector2 heading = route != null ? route.Direction(body.position, targetPosition) : offset.normalized;
            Vector2 separation = Vector2.zero;
            foreach (DungeonEnemy other in Enemies)
            {
                if (other == this || other == null || other.IsDead) continue;
                Vector2 away = body.position - (Vector2)other.transform.position;
                float square = away.sqrMagnitude;
                if (square > 0.001f && square < 0.85f * 0.85f) separation += away.normalized * (1 - Mathf.Sqrt(square) / 0.85f);
            }
            desiredVelocity = Vector2.ClampMagnitude(heading + separation * 1.1f, 1) * Stats.MoveSpeed;
        }

        private void Strike()
        {
            recovery = 1.05f;
            // A swing committed to a ghost must never turn into a hit on the player.
            if (strikingDecoy) { strikeDecoy = null; strikingDecoy = false; return; }
            Vector2 offset = (Vector2)target.transform.position - body.position;
            if (offset.sqrMagnitude > 1.3f * 1.3f || offset.sqrMagnitude < 0.0001f ||
                Vector2.Dot(offset.normalized, strikeDirection) < 0.55f ||
                !DungeonCombatPhysics.ClearLine(body.position, target.transform.position)) return;
            target.TakeDamage(Stats.Damage, body.position, this);
        }

        public void ReceiveHit(float damage, Vector2 direction, float knockback)
        {
            if (IsDead || damage <= 0 || target == null || !target.CanAct) return;
            Health = Mathf.Max(0, Health - Stats.ReduceDamage(damage));
            hitFlash = 0.16f;
            alertRemaining = 6;
            windup = 0;
            if (tellVisual != null) Destroy(tellVisual.gameObject);
            knockbackVelocity = direction.normalized * knockback;
            visualRecoil = direction.normalized;
            recoilRemaining = 0.18f;
            StunRemaining = Mathf.Max(StunRemaining, 0.12f);
            DungeonCombatFx.Hit(target, body.position, direction, false);
            UpdateColor();
            UpdateReaction();
            if (!IsDead) return;
            target.AwardScore(Stats.ScoreDrop);
            deathRemaining = 0.3f;
            feet.enabled = false;
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
            desiredVelocity = Vector2.zero;
            if (stunVisual != null) Destroy(stunVisual.gameObject);
            if (!DungeonFxSettings.Current.enemyReactions) Destroy(gameObject);
        }

        public void Stun(float seconds, Vector2 recoil = default)
        {
            if (IsDead) return;
            StunRemaining = Mathf.Max(StunRemaining, seconds);
            windup = 0;
            recovery = Mathf.Max(recovery, seconds + 0.2f);
            knockbackVelocity = recoil;
            desiredVelocity = Vector2.zero;
            if (tellVisual != null) Destroy(tellVisual.gameObject);
            if (stunVisual == null && target != null) stunVisual = DungeonCombatFx.Stun(target, this);
        }

        private void UpdateColor()
        {
            if (visual == null) return;
            if (hitFlash > 0) visual.color = Color.Lerp(originalColor, Color.white, Mathf.Clamp01(hitFlash / 0.16f));
            else if (windup > 0) visual.color = Color.Lerp(originalColor, new Color(1, 0.65f, 0.65f),
                0.35f + 0.3f * Mathf.Sin(WindupProgress * Mathf.PI * 4));
            else if (StunRemaining > 0) visual.color = Color.Lerp(originalColor, new Color(0.69f, 0.74f, 1), 0.45f);
            else visual.color = originalColor;
        }

        internal void ApplyHitStop(float seconds)
        {
            if (!DungeonFxSettings.Current.hitStop) return;
            hitStopRemaining = Mathf.Max(hitStopRemaining, seconds);
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void UpdateReaction()
        {
            if (visual == null || visual.transform == transform) return;
            float strength = DungeonFxSettings.Current.enemyReactions ? Mathf.Clamp01(recoilRemaining / 0.18f) : 0;
            // Only the image compresses; physical knockback continues to use the unchanged feet body.
            visual.transform.localScale = Vector3.Scale(visualBaseScale, new Vector3(1 + strength * 0.12f, 1 - strength * 0.16f, 1));
            visual.transform.localPosition = visualBasePosition + (Vector3)DungeonCombatPixels.Snap(visualRecoil * (strength * 2f / 32));
        }

        private void FixedUpdate()
        {
            if (body == null || !body.simulated) return;
            body.linearVelocity = target != null && target.CanAct && !IsDead && !IsHitStopped ? desiredVelocity + knockbackVelocity : Vector2.zero;
        }
    }
}
