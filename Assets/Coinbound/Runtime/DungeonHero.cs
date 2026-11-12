using System;
using System.Collections.Generic;
using UnityEngine;

namespace Coinbound
{
    /// <summary>The current hero choices; presentation is shared by selection and the spawned player.</summary>
    public sealed class DungeonHero
    {
        public static readonly DungeonHero First = new DungeonHero("hero-1", "Мечник", new Color32(188, 66, 74, 255), "01", false);
        public static readonly DungeonHero Second = new DungeonHero("hero-2", "Лучник", new Color32(115, 161, 92, 255), "02", true);
        public static IReadOnlyList<DungeonHero> All { get; } = Array.AsReadOnly(new[] { First, Second });

        public string Id { get; }
        public string DisplayName { get; }
        public Color32 Color { get; }
        public string ArtFolder { get; }
        public bool IsArcher { get; }
        public PlayerStats Stats { get; }
        public float StartingHealth => Stats.MaxHealth;
        public float AttackDamage => Stats.Damage;
        public float AttackSeconds => Stats.AttackSeconds;
        public float RollRecovery => 1.5f;
        public float BlockRecovery => 2;
        public string Description => IsArcher ? "ЛКМ — выстрел по курсору\nShift — перекат" : "ЛКМ — удар по курсору\nПКМ — блок · Shift — перекат";

        private DungeonHero(string id, string displayName, Color32 color, string artFolder, bool isArcher)
        {
            Id = id;
            DisplayName = displayName;
            Color = color;
            ArtFolder = artFolder;
            IsArcher = isArcher;
            Stats = new PlayerStats(100, 100, isArcher ? 1.5f : 2, isArcher ? 3.5f : 3,
                1 / (isArcher ? 0.38f : 0.42f), 0.05f, 0);
        }
    }
}
