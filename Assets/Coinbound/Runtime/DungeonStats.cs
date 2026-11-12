using System;

namespace Coinbound
{
    /// <summary>Immutable base attributes, shared by actor definitions; current resources belong to actors.</summary>
    public abstract class CombatStats
    {
        public float MaxHealth { get; }
        public float Damage { get; }
        public float MoveSpeed { get; }

        protected CombatStats(float maxHealth, float damage, float moveSpeed)
        {
            RequireRange(maxHealth, float.Epsilon, float.MaxValue, nameof(maxHealth));
            RequireRange(damage, 0, float.MaxValue, nameof(damage));
            RequireRange(moveSpeed, 0, float.MaxValue, nameof(moveSpeed));
            MaxHealth = maxHealth;
            Damage = damage;
            MoveSpeed = moveSpeed;
        }

        protected static void RequireRange(float value, float min, float max, string name)
        {
            if (float.IsNaN(value) || value < min || value > max)
                throw new ArgumentOutOfRangeException(name);
        }
    }

    public sealed class PlayerStats : CombatStats
    {
        public const float CriticalMultiplier = 1.5f;
        public float MaxMana { get; }
        /// <summary>Attacks per second.</summary>
        public float AttackRate { get; }
        /// <summary>Probability in [0, 1].</summary>
        public float CriticalRate { get; }
        /// <summary>Signed percentage points of favorable coin probability, in [-100, 100].</summary>
        public float Luck { get; }
        public float Defense { get; }
        public float AttackSeconds => 1 / AttackRate;

        public PlayerStats(float maxHealth, float maxMana, float damage, float moveSpeed,
            float attackRate, float criticalRate, float luck, float defense = 0) : base(maxHealth, damage, moveSpeed)
        {
            RequireRange(maxMana, 0, float.MaxValue, nameof(maxMana));
            RequireRange(attackRate, 0.01f, float.MaxValue, nameof(attackRate));
            RequireRange(criticalRate, 0, 1, nameof(criticalRate));
            RequireRange(luck, -100, 100, nameof(luck));
            RequireRange(defense, -100, 100, nameof(defense));
            MaxMana = maxMana;
            AttackRate = attackRate;
            CriticalRate = criticalRate;
            Luck = luck;
            Defense = defense;
        }

        // Inject the roll so combat and future coin drops can be tested without global randomness.
        public float RollDamage(float roll)
        {
            RequireRange(roll, 0, 1, nameof(roll));
            return Damage * (CriticalRate > 0 && (CriticalRate == 1 || roll < CriticalRate) ? CriticalMultiplier : 1);
        }

        public float GoodDropChance(float baseChance = 0.5f)
        {
            RequireRange(baseChance, 0, 1, nameof(baseChance));
            return Math.Max(0, Math.Min(1, baseChance + Luck / 100));
        }

        public bool RollGoodDrop(float roll, float baseChance = 0.5f)
        {
            RequireRange(roll, 0, 1, nameof(roll));
            float chance = GoodDropChance(baseChance);
            return chance == 1 || roll < chance;
        }
    }

    public sealed class EnemyStats : CombatStats
    {
        public static EnemyStats Default { get; } = new EnemyStats(3, 0, 10, 2.25f, 100);
        /// <summary>Flat damage reduction per hit; cannot turn damage into healing.</summary>
        public float Defense { get; }
        public float ScoreDrop { get; }

        public EnemyStats(float maxHealth, float defense, float damage, float moveSpeed, float scoreDrop)
            : base(maxHealth, damage, moveSpeed)
        {
            RequireRange(defense, 0, float.MaxValue, nameof(defense));
            RequireRange(scoreDrop, 0, float.MaxValue, nameof(scoreDrop));
            Defense = defense;
            ScoreDrop = scoreDrop;
        }

        public float ReduceDamage(float damage)
        {
            RequireRange(damage, 0, float.MaxValue, nameof(damage));
            return Math.Max(0, damage - Defense);
        }
    }
}
