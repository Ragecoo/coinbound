using System;

namespace Coinbound.EnemyPack
{
    // Pure decision logic. The host owns physics, navigation, damage and spawned effects.
    public enum EnemyKind { Warden, Arbalist, Cultist, Ghoul }
    public enum EnemyState { Idle, Move, Windup, Active, Recovery, Stunned, Dead }
    public enum EnemyEvent { BeginTell, CancelTell, MeleeStrike, FireBolt, GroundBurst, BeginDash, EndDash, Died }

    public struct EnemyVector
    {
        public float X, Y;
        public EnemyVector(float x, float y) { X = x; Y = y; }
        public float Length { get { return (float)Math.Sqrt(X * X + Y * Y); } }
        public EnemyVector Unit { get { float n = Length; return n > 0.0001f ? this * (1 / n) : new EnemyVector(); } }
        public static EnemyVector operator +(EnemyVector a, EnemyVector b) { return new EnemyVector(a.X+b.X,a.Y+b.Y); }
        public static EnemyVector operator -(EnemyVector a, EnemyVector b) { return new EnemyVector(a.X-b.X,a.Y-b.Y); }
        public static EnemyVector operator *(EnemyVector a, float f) { return new EnemyVector(a.X*f,a.Y*f); }
        public static float Dot(EnemyVector a, EnemyVector b) { return a.X*b.X+a.Y*b.Y; }
    }

    public sealed class EnemyTuning
    {
        public readonly int Health, Damage;
        public readonly float Speed, AttackRange, RetreatRange, Cooldown, Windup, Active, Recovery;
        public readonly float[] FrameSeconds;
        private EnemyTuning(int hp, int damage, float speed, float range, float retreat, float cooldown, float[] frames)
        { Health=hp; Damage=damage; Speed=speed; AttackRange=range; RetreatRange=retreat; Cooldown=cooldown; FrameSeconds=frames;
          Windup=frames[0]+frames[1]+frames[2]+frames[3]; Active=frames[4]; Recovery=frames[5]+frames[6]+frames[7]; }
        public static EnemyTuning For(EnemyKind kind)
        {
            switch(kind)
            {
                case EnemyKind.Warden: return new EnemyTuning(6,14,1.4f,1.1f,0,0.4f,new[]{.08f,.14f,.20f,.23f,.10f,.12f,.20f,.38f});
                case EnemyKind.Arbalist: return new EnemyTuning(3,9,2.1f,6.5f,2.8f,.65f,new[]{.08f,.12f,.20f,.25f,.08f,.25f,.25f,.25f});
                case EnemyKind.Cultist: return new EnemyTuning(4,11,1.3f,5.5f,3f,.8f,new[]{.10f,.20f,.30f,.35f,.12f,.18f,.24f,.30f});
                default: return new EnemyTuning(3,11,2.8f,2.2f,0,.65f,new[]{.07f,.10f,.15f,.18f,.26f,.20f,.25f,.30f});
            }
        }
    }

    public struct EnemySense
    {
        public EnemyVector Position, TargetPosition, Approach, Retreat, Strafe;
        // Host must route Approach to LastKnownTarget when visibility is false.
        // Retreat and Strafe must be collision-checked by navigation, not guessed by the brain.
        public bool TargetVisible, TargetAlive, Paused, MovementBlocked, TargetIsDecoy;
    }

    public sealed class EnemyBrain
    {
        public readonly EnemyKind Kind;
        public readonly EnemyTuning Tuning;
        public EnemyState State { get; private set; }
        public EnemyVector Velocity { get; private set; }
        public EnemyVector Facing { get; private set; }
        public EnemyVector LockedDirection { get; private set; }
        public EnemyVector LockedTarget { get; private set; }
        public EnemyVector LastKnownTarget { get; private set; }
        public bool LockedOnDecoy { get; private set; }
        public float Health { get; private set; }
        public float ActionElapsed { get; private set; }
        public int ActionSequence { get; private set; }
        public bool HasMemory { get { return memory > 0; } }
        public bool Guarding { get { return Kind == EnemyKind.Warden && (State == EnemyState.Idle || State == EnemyState.Move); } }
        public bool IsAction { get { return State == EnemyState.Windup || State == EnemyState.Active || State == EnemyState.Recovery; } }
        public event Action<EnemyEvent, EnemyBrain> Emitted;
        private float remaining, memory, cooldown;
        private readonly EnemyVector spawn;

        public EnemyBrain(EnemyKind kind, EnemyVector spawnPosition)
        { Kind=kind; Tuning=EnemyTuning.For(kind); Health=Tuning.Health; spawn=spawnPosition; State=EnemyState.Idle; Facing=new EnemyVector(0,-1); }

        public int AnimationFrame
        {
            get
            {
                if(!IsAction) return 0;
                float t=ActionElapsed;
                for(int i=0;i<8;i++) { if(t < Tuning.FrameSeconds[i]) return i; t-=Tuning.FrameSeconds[i]; }
                return 7;
            }
        }

        public void Tick(float deltaTime, EnemySense sense)
        {
            if(float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            Velocity=new EnemyVector();
            if(State==EnemyState.Dead || deltaTime==0 || sense.Paused) return;
            if(!sense.TargetAlive) { CancelAction(); State=EnemyState.Idle; memory=0; return; }
            cooldown=Math.Max(0,cooldown-deltaTime);
            if(State==EnemyState.Stunned) { remaining=Math.Max(0,remaining-deltaTime); if(remaining==0)State=EnemyState.Idle; return; }
            EnemyVector offset=sense.TargetPosition-sense.Position;
            bool sees=sense.TargetVisible && offset.Length<=8;
            if(sees) { memory=3; LastKnownTarget=sense.TargetPosition; } else memory=Math.Max(0,memory-deltaTime);
            if((sense.Position-spawn).Length>12) { CancelAction(); memory=0; State=EnemyState.Idle; return; }
            if(IsAction)
            {
                // Consume all crossed stages: impact cannot be skipped by a long render frame.
                float left=deltaTime;
                while(left>0 && IsAction)
                {
                    if(State==EnemyState.Active && Kind==EnemyKind.Ghoul && sense.MovementBlocked)
                    { Emit(EnemyEvent.EndDash); State=EnemyState.Recovery; ActionElapsed=Tuning.Windup+Tuning.Active; remaining=Tuning.Recovery; }
                    float step=Math.Min(left,remaining); ActionElapsed+=step; remaining-=step; left-=step;
                    if(remaining>0)break;
                    if(State==EnemyState.Windup)
                    {
                        Emit(EnemyEvent.CancelTell); State=EnemyState.Active; remaining=Tuning.Active;
                        Emit(Kind==EnemyKind.Warden?EnemyEvent.MeleeStrike:Kind==EnemyKind.Arbalist?EnemyEvent.FireBolt:Kind==EnemyKind.Cultist?EnemyEvent.GroundBurst:EnemyEvent.BeginDash);
                    }
                    else if(State==EnemyState.Active)
                    { if(Kind==EnemyKind.Ghoul)Emit(EnemyEvent.EndDash); State=EnemyState.Recovery; remaining=Tuning.Recovery; }
                    else { State=EnemyState.Idle; cooldown=Tuning.Cooldown; }
                }
                if(State==EnemyState.Active && Kind==EnemyKind.Ghoul)Velocity=LockedDirection*7;
                return;
            }
            if(memory<=0) { State=EnemyState.Idle; return; }
            if(sees && offset.Length>0.001f)Facing=offset.Unit;
            if(!sees && (LastKnownTarget-sense.Position).Length<.25f) { State=EnemyState.Idle; return; }
            if(sees && offset.Length<Tuning.RetreatRange)
            { EnemyVector escape=sense.Retreat.Length>.01f?sense.Retreat:sense.Strafe;
              if(escape.Length>.01f) { Move(escape); return; } }
            if(sees && offset.Length<=Tuning.AttackRange && cooldown<=0)
            {
                LockedDirection=offset.Length>.001f?offset.Unit:Facing; Facing=LockedDirection;
                LockedTarget=sense.TargetPosition; LockedOnDecoy=sense.TargetIsDecoy; ActionElapsed=0; ActionSequence++;
                State=EnemyState.Windup; remaining=Tuning.Windup; Emit(EnemyEvent.BeginTell); return;
            }
            bool ranged=Kind==EnemyKind.Arbalist || Kind==EnemyKind.Cultist;
            if(ranged && sees && offset.Length<Tuning.AttackRange*.85f) { Move(sense.Strafe); return; }
            if(!ranged && sees && offset.Length<.8f) { State=EnemyState.Idle; return; }
            Move(sense.Approach);
        }

        private void Move(EnemyVector direction)
        { Velocity=direction.Unit*Tuning.Speed; State=Velocity.Length>.01f?EnemyState.Move:EnemyState.Idle; if(Velocity.Length>.01f)Facing=Velocity.Unit; }

        public void ReceiveHit(float damage, EnemyVector attackerPosition, EnemyVector ownPosition, float stunSeconds=.16f)
        {
            if(State==EnemyState.Dead || damage<=0 || float.IsNaN(damage) || float.IsInfinity(damage))return;
            bool shield=Guarding && EnemyVector.Dot((attackerPosition-ownPosition).Unit,Facing)>=.643f;
            Health=Math.Max(0,Health-damage*(shield?.3f:1f)); memory=3;
            LastKnownTarget=attackerPosition;
            if(Health==0) { CancelAction(); State=EnemyState.Dead; Velocity=new EnemyVector(); Emit(EnemyEvent.Died); return; }
            if(!shield) Stun(stunSeconds);
        }

        public void Stun(float seconds)
        { if(State==EnemyState.Dead || seconds<=0 || float.IsNaN(seconds) || float.IsInfinity(seconds))return;
          float prior=State==EnemyState.Stunned?remaining:0; CancelAction(); State=EnemyState.Stunned; remaining=Math.Max(prior,seconds); cooldown=Math.Max(cooldown,seconds+.2f); Velocity=new EnemyVector(); }
        private void CancelAction()
        { if(State==EnemyState.Windup)Emit(EnemyEvent.CancelTell); if(State==EnemyState.Active && Kind==EnemyKind.Ghoul)Emit(EnemyEvent.EndDash); Velocity=new EnemyVector(); }
        private void Emit(EnemyEvent e) { var handler=Emitted; if(handler!=null)handler(e,this); }
    }
}
