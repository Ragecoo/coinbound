using System;
using System.Collections.Generic;
using UnityEngine;

namespace Coinbound
{
    /// <summary>The current hero choices; presentation is shared by selection and the spawned player.</summary>
    public sealed class DungeonHero
    {
        public static readonly DungeonHero First = new DungeonHero("hero-1", "Герой 1", new Color32(96, 223, 229, 255));
        public static readonly DungeonHero Second = new DungeonHero("hero-2", "Герой 2", new Color32(181, 154, 242, 255));
        public static IReadOnlyList<DungeonHero> All { get; } = Array.AsReadOnly(new[] { First, Second });

        public string Id { get; }
        public string DisplayName { get; }
        public Color32 Color { get; }

        private DungeonHero(string id, string displayName, Color32 color)
        {
            Id = id;
            DisplayName = displayName;
            Color = color;
        }
    }
}
