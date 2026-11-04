using UnityEngine;

namespace Coinbound
{
    /// <summary>One inspectable switchboard for presentation; never changes the generator.</summary>
    [CreateAssetMenu(menuName = "Coinbound/Effects settings")]
    public sealed class DungeonFxSettings : ScriptableObject
    {
        private static DungeonFxSettings current;
        public static DungeonFxSettings Current
        {
            get
            {
                if (current == null) current = Resources.Load<DungeonFxSettings>("DungeonEffects");
                if (current == null) { current = CreateInstance<DungeonFxSettings>(); current.hideFlags = HideFlags.DontSave; }
                return current;
            }
        }

        [Header("Combat")]
        public bool swordTrail = true;
        public bool sparks = true;
        public bool blood;
        public bool rollDust = true;
        public bool hitStop;
        public bool bowRecoil;
        public bool arrowImpacts = true;
        public bool stoneChips;
        public bool enemyReactions;
        public bool contactShadows;
        public bool footDust = true;
        public bool wetSplashes;
        public bool capeMotion;
        public bool blockPulse;
        [Header("Sword trail — white pixels, warm local light")]
        public Color slashColor = Color.white;
        public Color slashLightColor = new Color(1, 0.78f, 0.48f);
        [Range(0.1f, 4)] public float slashLightRadius = 0.9f;
        [Range(0, 2)] public float slashLightIntensity = 0.75f;
        [Range(0.02f, 1)] public float slashLightDuration = 0.18f;
        [Range(0, 2)] public float slashEmission = 0.25f;
        [Header("Sparks — bright, short-range amber light")]
        public Color sparkColor = new Color(1, 0.68f, 0.28f);
        public Color sparkHotColor = new Color(1, 0.9f, 0.63f);
        public Color sparkLightColor = new Color(1, 0.68f, 0.3f);
        [Range(0.1f, 4)] public float sparkLightRadius = 0.75f;
        [Range(0, 2)] public float sparkLightIntensity = 1;
        [Range(0.02f, 1)] public float sparkLightDuration = 0.16f;
        [Range(0, 2)] public float sparkEmission = 1;
        [Header("Arrows lodged in walls")]
        [Tooltip("Visual inset above the floor into the north wall face, in tiles.")]
        [Range(0, 1)] public float arrowNorthWallInset = 0.85f;
        [Range(0, 0.5f)] public float arrowOtherWallInset = 0.25f;
        [Range(0.1f, 10)] public float arrowStuckLifetime = 1.1f;
        [Header("Local light from effects")]
        public bool dynamicLights = true;
        [Range(0, 2)] public float effectLightStrength = 1;
        [Header("Dungeon atmosphere")]
        public bool ambientDust;
        public bool mist;
        public bool drips;
        public bool fountainRipples;
        public bool clothMotion;
        public bool cobwebs;
        public bool wallProps;
        public bool floorWear;
        public bool rats;
        public bool fallingDebris;
        [Range(0, 2)] public float ambientDensity = 1;
        [Header("Sound and interface feedback")]
        public bool footsteps;
        public bool ambientAudio;
        public bool combatAudio;
        [Range(0, 1)] public float soundVolume = 0.5f;
        public bool cursorFeedback;
        public bool cooldownPulse;
        public bool damageVignette;
    }
}
