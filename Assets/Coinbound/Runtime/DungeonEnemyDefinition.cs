using UnityEngine;

namespace Coinbound
{
    public enum DungeonEnemyKind { Warden, Arbalist, Cultist, Ghoul }
    public enum DungeonEnemyPhase { Idle, Windup, Active, Recovery, Stunned, Dead }

    /// <summary>Fixed tuning, distance bands and random parameters are deliberately separate.</summary>
    public sealed class DungeonEnemyDefinition
    {
        public readonly DungeonEnemyKind Kind;
        public readonly EnemyStats Stats;
        public readonly float AttackRange, Windup, Active, Recovery, Delay;
        public readonly float ComfortableMin, ComfortableMax, RetreatStart, RetreatEnd, Danger;
        public const float Detection = 8f, Memory = 3f, Leash = 12f, HitStun = .16f, TacticalInterval = .30f;
        public const float FlankProbability = .35f, FlankDuration = 2.5f, FlankDelayMin = 3f, FlankDelayMax = 5f;
        public readonly float[] Frames;
        private DungeonEnemyDefinition(DungeonEnemyKind kind,float hp,float damage,float speed,float range,float[] frames,float delay,
            float min=0,float max=0,float retreat=0,float end=0,float danger=0)
        {
            Kind=kind;Stats=new EnemyStats(hp,0f,damage,speed,100f);AttackRange=range;Frames=frames;Delay=delay;
            Windup=frames[0]+frames[1]+frames[2]+frames[3];Active=frames[4];Recovery=frames[5]+frames[6]+frames[7];
            ComfortableMin=min;ComfortableMax=max;RetreatStart=retreat;RetreatEnd=end;Danger=danger;
        }
        private static readonly DungeonEnemyDefinition[] Definitions = {
            new DungeonEnemyDefinition(DungeonEnemyKind.Warden,15f,12f,1.4f,1.1f,new[]{.08f,.14f,.20f,.23f,.10f,.12f,.20f,.38f},.40f),
            new DungeonEnemyDefinition(DungeonEnemyKind.Arbalist,8f,9f,1.8f,6.5f,new[]{.08f,.12f,.20f,.25f,.08f,.25f,.25f,.25f},.65f,4.5f,6f,4f,4.8f,2.8f),
            new DungeonEnemyDefinition(DungeonEnemyKind.Cultist,10f,11f,1.1f,5.5f,new[]{.10f,.20f,.30f,.35f,.12f,.18f,.24f,.30f},.80f,3.8f,5f,3.5f,4.2f,3f),
            new DungeonEnemyDefinition(DungeonEnemyKind.Ghoul,6f,10f,2.8f,2.2f,new[]{.07f,.10f,.15f,.18f,.26f,.20f,.25f,.30f},.65f)
        };
        public static DungeonEnemyDefinition For(DungeonEnemyKind kind) => Definitions[(int)kind];
        public bool Ranged => Kind == DungeonEnemyKind.Arbalist || Kind == DungeonEnemyKind.Cultist;
    }
}
