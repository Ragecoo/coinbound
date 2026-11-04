using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Coinbound
{
    /// <summary>Feet-based movement and combat. Art follows this state without changing the collision body.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class DungeonPlayer : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float rollCooldown = 1.5f;
        [SerializeField, Min(0.1f)] private float blockCooldown = 2;
        private const float RollDuration = 0.30f;
        private Rigidbody2D body;
        private Camera aimCamera;
        private Vector2 movement;
        private Vector2 lastMove = Vector2.down;
        private Vector2 aimDirection = Vector2.down;
        private Vector2 attackDirection;
        private Vector2 rollDirection;
        private Vector2 knockbackVelocity;
        private bool inputEnabled;
        private bool blockRequested;
        private bool attackReleased;
        private float rollRemaining;
        private float attackRemaining;
        private float attackDuration;
        private float hurtInvulnerability;
        private float hitFlashRemaining;
        private float hitStopRemaining;
        private Vector2 lastStepPosition;
        private float stepDistance;
        private float rollDustDistance;
        private int stepSequence;
        private bool hasStepPosition;
        private DungeonContactShadow contactShadow;
        private DungeonCombatFx guardVisual;

        public DungeonHero Hero { get; private set; } = DungeonHero.First;
        public PlayerStats Stats => Hero.Stats;
        public float MoveSpeed => Stats.MoveSpeed;
        public DungeonChest InteractionTarget => DungeonChest.FindTarget(this);
        public Vector2 InteractionFacing => Velocity.sqrMagnitude > .01f ? Velocity.normalized : AimDirection;
        public bool TryInteract()
        {
            DungeonChest target = InteractionTarget;
            return target != null && target.TryOpen(this);
        }
        public Vector2 AimDirection => aimDirection;
        public Vector2 AimPosition { get; private set; }
        public Vector2 MoveDirection => movement;
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
        public Vector2 ActionDirection => IsRolling ? rollDirection : IsAttacking ? attackDirection : aimDirection;
        public bool CanAct => inputEnabled && isActiveAndEnabled && !IsDead && InputFocusAllowed;
        public bool IsRolling => rollRemaining > 0;
        public bool IsAttacking => attackRemaining > 0;
        public bool IsBlocking => CanAct && blockRequested && !Hero.IsArcher && !IsRolling && !IsAttacking && BlockCooldownRemaining <= 0;
        public bool IsDead => Health <= 0;
        public bool IsHitStopped => hitStopRemaining > 0;
        public int BowShotSequence { get; private set; }
        public bool IsInvulnerable => hurtInvulnerability > 0 || (IsRolling && RollProgress < 0.8f);
        public float HitFlash => Mathf.Clamp01(hitFlashRemaining / 0.18f);
        public float AttackProgress => IsAttacking ? 1 - attackRemaining / attackDuration : 0;
        public float RollProgress => IsRolling ? 1 - rollRemaining / RollDuration : 0;
        public int Health { get; private set; } = DungeonHero.First.StartingHealth;
        public int MaxHealth => Stats.MaxHealth;
        public DungeonSpellcaster Spells { get; private set; }
        public int Mana { get; private set; } = 100;
        public int MaxMana => Stats.MaxMana;
        public bool TrySpendMana(int amount)
        {
            if (amount < 0 || Mana < amount) return false;
            Mana -= amount;
            return true;
        }
        public int Score { get; private set; }
        public void AwardScore(int points)
        {
            if (points > 0) Score = (int)Math.Min(999999999L, (long)Score + points);
        }
        public float RollCooldownRemaining { get; private set; }
        public float BlockCooldownRemaining { get; private set; }
        public event Action Died;

        private static bool InputFocusAllowed
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // The explicitly requested capture session drives synthetic input without stealing desktop focus.
                if (DungeonCombatCapture.IsRunning) return true;
#endif
                return Application.isFocused || Application.isBatchMode;
            }
        }

        internal Material EffectMaterial
        {
            get
            {
                foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>())
                    if (renderer.sprite != null && renderer.sharedMaterial != null) return renderer.sharedMaterial;
                return null;
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            Health = MaxHealth;
        }

        public void Initialize(DungeonHero hero)
        {
            Hero = hero ?? DungeonHero.First;
            Mana = MaxMana;
            Score = 0;
            rollCooldown = Hero.RollRecovery;
            blockCooldown = Hero.BlockRecovery;
            Health = MaxHealth;
            AimPosition = (Vector2)transform.position + aimDirection;
            aimCamera = Camera.main;
            Spells = GetComponent<DungeonSpellcaster>();
            if (Spells == null) Spells = gameObject.AddComponent<DungeonSpellcaster>();
            Spells.Initialize(this);
        }

        public void SetInputEnabled(bool value)
        {
            inputEnabled = value;
            movement = Vector2.zero;
            blockRequested = false;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void Update()
        {
            if (!CanAct)
            {
                movement = Vector2.zero;
                blockRequested = false;
                if (guardVisual != null) guardVisual.SetVisible(false);
                return;
            }

            float dt = Time.deltaTime;
            if (IsHitStopped)
            {
                hitStopRemaining = Mathf.Max(0, hitStopRemaining - dt);
                body.linearVelocity = Vector2.zero;
                return;
            }
            RollCooldownRemaining = Mathf.Max(0, RollCooldownRemaining - dt);
            BlockCooldownRemaining = Mathf.Max(0, BlockCooldownRemaining - dt);
            hurtInvulnerability = Mathf.Max(0, hurtInvulnerability - dt);
            hitFlashRemaining = Mathf.Max(0, hitFlashRemaining - dt);
            knockbackVelocity = Vector2.MoveTowards(knockbackVelocity, Vector2.zero, 24 * dt);
            rollRemaining = Mathf.Max(0, rollRemaining - dt);
            AdvanceAttack(dt);

            movement = Vector2.zero;
            Keyboard keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.aKey.isPressed || keys.leftArrowKey.isPressed) movement.x--;
                if (keys.dKey.isPressed || keys.rightArrowKey.isPressed) movement.x++;
                if (keys.wKey.isPressed || keys.upArrowKey.isPressed) movement.y++;
                if (keys.sKey.isPressed || keys.downArrowKey.isPressed) movement.y--;
            }
            if (Gamepad.current != null) movement += Gamepad.current.leftStick.ReadValue();
            movement = Vector2.ClampMagnitude(movement, 1);
            if (movement.sqrMagnitude > 0.01f) lastMove = movement.normalized;
            UpdateAim();
            if (keys?.eKey.wasPressedThisFrame == true) TryInteract();

            bool pointerOnUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            SetBlocking(!pointerOnUI && Mouse.current?.rightButton.isPressed == true);
            if (keys?.leftShiftKey.wasPressedThisFrame == true || Gamepad.current?.buttonSouth.wasPressedThisFrame == true)
                TryRoll(movement.sqrMagnitude > 0.01f ? movement : lastMove);
            if (!pointerOnUI && (Mouse.current?.leftButton.isPressed == true || Gamepad.current?.rightTrigger.isPressed == true))
                TryAttack(aimDirection);

            if (guardVisual == null && IsBlocking) guardVisual = DungeonCombatFx.Guard(this);
            if (guardVisual != null) guardVisual.SetVisible(IsBlocking);
        }

        private void UpdateAim()
        {
            if (aimCamera == null) aimCamera = Camera.main;
            if (Mouse.current != null && aimCamera != null)
            {
                Vector2 pointer = Mouse.current.position.ReadValue();
                Vector3 world = aimCamera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y, -aimCamera.transform.position.z));
                AimPosition = world;
                Vector2 delta = (Vector2)world - (Vector2)transform.position;
                if (delta.sqrMagnitude > 0.01f) aimDirection = delta.normalized;
            }
            if (Gamepad.current != null)
            {
                Vector2 stick = Gamepad.current.rightStick.ReadValue();
                if (stick.sqrMagnitude > 0.16f)
                {
                    aimDirection = stick.normalized;
                    AimPosition = (Vector2)transform.position + aimDirection * 6;
                }
            }
        }

        public bool TryAttack(Vector2 direction)
        {
            if (!CanAct || IsHitStopped || IsRolling || IsAttacking || IsBlocking || direction.sqrMagnitude < 0.001f) return false;
            attackDirection = aimDirection = direction.normalized;
            attackDuration = Stats.AttackSeconds;
            attackRemaining = attackDuration;
            attackReleased = false;
            return true;
        }

        public void SetAimDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.001f) return;
            aimDirection = direction.normalized;
            AimPosition = (Vector2)transform.position + aimDirection * 6;
        }

        private void AdvanceAttack(float dt)
        {
            if (!IsAttacking) return;
            attackRemaining = Mathf.Max(0, attackRemaining - dt);
            float elapsed = attackDuration - attackRemaining;
            if (attackReleased || elapsed < attackDuration * (Hero.IsArcher ? 0.14f / 0.38f : 0.16f / 0.42f)) return;
            attackReleased = true;
            if (Hero.IsArcher)
            {
                DungeonArrow.Launch(this, attackDirection);
                BowShotSequence++;
            }
            else
            {
                DungeonCombatFx.Slash(this, attackDirection);
                Vector2 origin = body.position;
                float damage = Stats.RollDamage(UnityEngine.Random.value);
                foreach (DungeonEnemy enemy in new System.Collections.Generic.List<DungeonEnemy>(DungeonEnemy.Active))
                {
                    if (enemy == null || enemy.IsDead) continue;
                    Vector2 offset = (Vector2)enemy.transform.position - origin;
                    if (offset.sqrMagnitude > 1.65f * 1.65f || offset.sqrMagnitude < 0.001f) continue;
                    if (Vector2.Dot(offset.normalized, attackDirection) < 0.52f) continue;
                    if (!DungeonCombatPhysics.ClearLine(origin, enemy.transform.position)) continue;
                    enemy.ReceiveHit(damage, offset.normalized, 5);
                    ApplyHitStop(0.04f);
                    enemy.ApplyHitStop(0.04f);
                }
            }
        }

        public bool TryRoll(Vector2 direction)
        {
            if (!CanAct || IsHitStopped || IsRolling || RollCooldownRemaining > 0 || direction.sqrMagnitude < 0.001f) return false;
            rollDirection = direction.normalized;
            rollRemaining = RollDuration;
            rollDustDistance = 0;
            RollCooldownRemaining = rollCooldown;
            attackRemaining = 0;
            blockRequested = false;
            knockbackVelocity = Vector2.zero;
            DungeonCombatFx.RollDust(this, body.position, rollDirection);
            return true;
        }

        public void SetBlocking(bool value) => blockRequested = value;

        /// <returns>True only when health actually changed; a roll, block or damage grace period returns false.</returns>
        public bool TakeDamage(int damage, Vector2 sourcePosition, DungeonEnemy attacker = null)
        {
            if (!CanAct || damage <= 0) return false;
            if (Spells != null && Spells.AbsorbHit()) return false;
            if (IsInvulnerable) return false;
            Vector2 fromAttack = sourcePosition - body.position;
            if (IsBlocking && fromAttack.sqrMagnitude > 0.001f && Vector2.Dot(fromAttack.normalized, aimDirection) >= 0.42f)
            {
                BlockCooldownRemaining = blockCooldown;
                foreach (DungeonEnemy enemy in DungeonEnemy.Active)
                {
                    if (enemy == null || enemy.IsDead) continue;
                    Vector2 offset = (Vector2)enemy.transform.position - body.position;
                    if (offset.sqrMagnitude > 1.8f * 1.8f || offset.sqrMagnitude < .0001f ||
                        Vector2.Dot(offset.normalized, aimDirection) < .42f ||
                        !DungeonCombatPhysics.ClearLine(body.position, enemy.transform.position)) continue;
                    enemy.Stun(.6f, offset.normalized * 5);
                    enemy.ApplyHitStop(.04f);
                }
                DungeonCombatFx.Parry(this, body.position, fromAttack.normalized);
                ApplyHitStop(0.04f);
                if (attacker != null) attacker.ApplyHitStop(0.04f);
                return false;
            }
            Health = Mathf.Max(0, Health - damage);
            hurtInvulnerability = 0.65f;
            hitFlashRemaining = 0.18f;
            knockbackVelocity = fromAttack.sqrMagnitude > 0.001f ? -fromAttack.normalized * 4 : Vector2.zero;
            attackRemaining = 0;
            DungeonCombatFx.Hit(this, body.position,
                fromAttack.sqrMagnitude > 0.001f ? -fromAttack.normalized : Vector2.down, true);
            if (IsDead)
            {
                inputEnabled = false;
                movement = Vector2.zero;
                blockRequested = false;
                body.linearVelocity = Vector2.zero;
                Died?.Invoke();
            }
            return true;
        }

        private void FixedUpdate()
        {
            if (!CanAct || IsHitStopped) { body.linearVelocity = Vector2.zero; return; }
            if (IsRolling) body.linearVelocity = rollDirection * Mathf.Lerp(12, 8, RollProgress);
            else body.linearVelocity = movement * MoveSpeed * (IsBlocking ? 0.45f : IsAttacking ? 0.55f : 1) + knockbackVelocity;
        }

        internal void ApplyHitStop(float seconds)
        {
            if (!DungeonFxSettings.Current.hitStop) return;
            hitStopRemaining = Mathf.Max(hitStopRemaining, seconds);
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void LateUpdate()
        {
            // The view installs hero art after Initialize; wait until its material exists.
            if (contactShadow == null && EffectMaterial != null)
                contactShadow = DungeonContactShadow.Create(transform, this, null, EffectMaterial, 0.34f);
            Vector2 current = body.position;
            if (!hasStepPosition) { lastStepPosition = current; hasStepPosition = true; }
            float travelled = Vector2.Distance(current, lastStepPosition);
            lastStepPosition = current;
            if (CanAct && !IsHitStopped && IsRolling && travelled <= .5f)
            {
                rollDustDistance += travelled;
                if (rollDustDistance >= .45f)
                {
                    rollDustDistance %= .45f;
                    DungeonCombatFx.RollDust(this, current, rollDirection, true);
                }
            }
            if (!CanAct || IsHitStopped || IsRolling || movement.sqrMagnitude < 0.04f || travelled > 0.5f) return;
            stepDistance += travelled;
            if (stepDistance < 0.72f) return;
            stepDistance %= 0.72f;
            stepSequence++;
            Vector2 side = new Vector2(-lastMove.y, lastMove.x) * (stepSequence % 2 == 0 ? 0.08f : -0.08f);
            Vector2 foot = current + side;
            bool wet = DungeonAtmosphere.IsWet(foot);
            DungeonCombatFx.Footstep(this, foot, lastMove, wet);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            movement = Vector2.zero;
            blockRequested = false;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void OnDisable() { if (body != null) body.linearVelocity = Vector2.zero; }
    }
}
