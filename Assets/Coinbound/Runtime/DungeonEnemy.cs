using System.Collections.Generic;
using ProceduralGeneration;
using UnityEngine;

namespace Coinbound
{
    /// <summary>Shared actor/combat clock; planning, artwork and attack effects are separate collaborators.</summary>
    public sealed partial class DungeonEnemy : MonoBehaviour
    {
        private static readonly List<DungeonEnemy> Enemies=new List<DungeonEnemy>();
        public static IReadOnlyList<DungeonEnemy> Active=>Enemies;
        private DungeonPlayer target;
        private DungeonMap map;
        private DungeonCombatNavigation navigation;
        private Rigidbody2D body;
        private Collider2D feet;
        private SpriteRenderer visual;
        private DungeonEnemyAnimator animator;
        private DungeonCombatFx tellVisual,stunVisual;
        private DungeonEnemyArea area;
        private Vector2 spawn,lastKnown,previousPosition,knockbackVelocity,lockedPoint;
        private float memory,actionElapsed,delay,hitStopRemaining,hitFlash,deathRemaining,dashDistance;
        private float contactRemaining;
        private bool visible,committedToDecoy,dashHit,returningHome,movedDashThisStep;
        private DungeonDecoy sensedDecoy;
        public DungeonEnemyKind Kind {get;private set;}=DungeonEnemyKind.Ghoul;
        public int GroupId {get;private set;}
        public DungeonEnemyDefinition Definition=>DungeonEnemyDefinition.For(Kind);
        public EnemyStats Stats {get;private set;}=EnemyStats.Default;
        // Relic auras are intentionally invisible: stats only, no rings, glow or particles.
        private bool InRelicAura=>target!=null&&Vector2.SqrMagnitude((Vector2)transform.position-(Vector2)target.transform.position)<=DungeonRelicInventory.AuraRadius*DungeonRelicInventory.AuraRadius;
        public float EffectiveDamage=>Mathf.Max(0,Stats.Damage*(1+(InRelicAura?target.Relics.EnemyDamage:0)));
        public float EffectiveSpeed=>Mathf.Max(.2f,Stats.MoveSpeed*(1+(InRelicAura?target.Relics.EnemySpeed:0)));
        public float EffectiveDefense=>Stats.Defense+(InRelicAura?target.Relics.EnemyDefense:0);
        public int SaveId {get;internal set;}
        internal void RestoreCheckpoint(float hp,Vector2 position){Health=Mathf.Clamp(hp,.01f,MaxHealth);body.position=position;transform.position=position;previousPosition=position;}
        public float Health {get;private set;}
        public float MaxHealth=>Stats.MaxHealth;
        public bool IsDead=>Health<=0f;
        public bool IsHitStopped=>hitStopRemaining>0 || target!=null&&target.IsHitStopped;
        public DungeonEnemyPhase Phase {get;private set;}
        public bool IsAttacking=>Phase==DungeonEnemyPhase.Windup||Phase==DungeonEnemyPhase.Active||Phase==DungeonEnemyPhase.Recovery;
        public bool IsWindingUp=>Phase==DungeonEnemyPhase.Windup;
        public float WindupProgress=>IsWindingUp?Mathf.Clamp01(actionElapsed/Definition.Windup):0;
        public float StunRemaining {get;private set;}
        public Vector2 Facing {get;private set;}=Vector2.down;
        public Vector2 StrikeDirection {get;private set;}=Vector2.down;
        public Vector2 LastKnownTarget=>lastKnown;
        public bool TargetVisible=>visible;
        public bool Guarding=>Kind==DungeonEnemyKind.Warden&&Phase==DungeonEnemyPhase.Idle&&StunRemaining<=0&&!IsDead;
        public bool CombatClockRunning=>target!=null&&target.CanAct&&!IsHitStopped&&Time.timeScale>0;
        public float ActionElapsed=>actionElapsed;
        public int AttackSequence {get;private set;}
        public int AttackFrame {get{if(!IsAttacking)return 0;float t=actionElapsed;for(int i=0;i<8;i++){if(t<Definition.Frames[i])return i;t-=Definition.Frames[i];}return 7;}}
        internal DungeonPlayer Player=>target;
        internal bool FloorAt(Vector2 point)=>map==null||map.IsTileFloor(new TilePosition(Mathf.FloorToInt(point.x),Mathf.FloorToInt(1-point.y)));
        internal Material EffectMaterial=>visual!=null?visual.sharedMaterial:target!=null?target.EffectMaterial:null;
        public void Configure(DungeonEnemyKind kind,int groupId=0){Kind=kind;GroupId=groupId;}
        private void OnEnable(){if(!Enemies.Contains(this))Enemies.Add(this);}
        private void OnDisable(){Enemies.Remove(this);CancelTell();if(body!=null)body.linearVelocity=Vector2.zero;}
        public void Initialize(DungeonPlayer player,DungeonMap dungeon,EnemyStats stats=null)
        {
            target=player;map=dungeon;Stats=stats??Definition.Stats;Health=Stats.MaxHealth;
            spawn=previousPosition=transform.position;lastKnown=spawn;Phase=DungeonEnemyPhase.Idle;
            navigation=DungeonCombatNavigation.For(map);random=new System.Random(GetEntityId().GetHashCode());
            body=GetComponent<Rigidbody2D>();if(body==null)body=gameObject.AddComponent<Rigidbody2D>();
            body.bodyType=RigidbodyType2D.Dynamic;body.gravityScale=0;body.constraints=RigidbodyConstraints2D.FreezeRotation;
            body.interpolation=RigidbodyInterpolation2D.Interpolate;body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;body.mass=1.5f;
            feet=GetComponent<Collider2D>();if(feet==null){var circle=gameObject.AddComponent<CircleCollider2D>();circle.radius=.30f;feet=circle;}
            if(player!=null&&player.GetComponent<Collider2D>()!=null)feet.sharedMaterial=player.GetComponent<Collider2D>().sharedMaterial;
            var rootVisual=GetComponent<SpriteRenderer>();Material material=rootVisual!=null?rootVisual.sharedMaterial:player!=null?player.EffectMaterial:null;
            if(rootVisual!=null)rootVisual.enabled=false;
            var child=new GameObject("Enemy visual",typeof(SpriteRenderer));child.transform.SetParent(transform,false);
            visual=child.GetComponent<SpriteRenderer>();visual.sharedMaterial=material!=null?material:Resources.Load<Material>("DungeonSprite");
            child.AddComponent<DungeonDepthSort>().Initialize(visual,transform,0,true);
            animator=child.AddComponent<DungeonEnemyAnimator>();animator.Initialize(Kind,visual);
            DungeonContactShadow.Create(transform,player,this,visual.sharedMaterial,.44f);
        }
        private void FixedUpdate()
        {
            if(body==null)return;float dt=Time.fixedDeltaTime;
            if(target==null||!target.CanAct||Time.timeScale<=0){body.linearVelocity=Vector2.zero;previousPosition=body.position;return;}
            if(IsHitStopped){hitStopRemaining=Mathf.Max(0,hitStopRemaining-dt);body.linearVelocity=Vector2.zero;previousPosition=body.position;return;}
            contactRemaining=Mathf.Max(0,contactRemaining-dt);
            if(IsDead){deathRemaining-=dt;var tint=visual.color;tint.a=Mathf.Clamp01(deathRemaining/.30f);visual.color=tint;if(deathRemaining<=0)Destroy(gameObject);return;}
            float actualSpeed=Vector2.Distance(body.position,previousPosition)/Mathf.Max(.001f,dt);previousPosition=body.position;
            body.linearVelocity=Vector2.zero;hitFlash=Mathf.Max(0,hitFlash-dt);knockbackVelocity=Vector2.MoveTowards(knockbackVelocity,Vector2.zero,22f*dt);
            flankCooldown=Mathf.Max(0,flankCooldown-dt);
            if(StunRemaining>0){StunRemaining=Mathf.Max(0,StunRemaining-dt);if(StunRemaining<=0)Phase=DungeonEnemyPhase.Idle;body.linearVelocity=knockbackVelocity;UpdateTint();return;}
            Sense(dt);
            movedDashThisStep=false;
            if(IsAttacking)AdvanceAttack(dt);
            else{delay=Mathf.Max(0,delay-dt);Decide(dt);}
            if(StunRemaining<=0&&!movedDashThisStep)body.linearVelocity+=knockbackVelocity;
            animator.Show(this,dt,actualSpeed);UpdateTint();
        }
        private void Sense(float dt)
        {
            if(returningHome&&Vector2.Distance(body.position,spawn)<.4f)returningHome=false;
            if(Vector2.Distance(body.position,spawn)>12f)returningHome=true;
            if(returningHome){visible=false;memory=0;return;}
            sensedDecoy=DungeonDecoy.Nearest(body.position);
            Vector2 candidate=sensedDecoy!=null?(Vector2)sensedDecoy.transform.position:(Vector2)target.transform.position;
            visible=Vector2.Distance(candidate,body.position)<=8f&&DungeonCombatPhysics.ClearLine(body.position,candidate);
            if(visible){lastKnown=candidate;memory=3f;}else memory=Mathf.Max(0,memory-dt);
        }
        private void BeginAttack()
        {
            if(flanking)EndFlank();lockedPoint=lastKnown;committedToDecoy=sensedDecoy!=null;
            Vector2 offset=lockedPoint-body.position;StrikeDirection=offset.sqrMagnitude>.0001f?offset.normalized:Facing;
            Facing=StrikeDirection;actionElapsed=0;Phase=DungeonEnemyPhase.Windup;AttackSequence++;dashHit=false;dashDistance=0;
            body.linearVelocity=Vector2.zero;route.Clear();
            if(Kind==DungeonEnemyKind.Cultist)area=DungeonEnemyArea.Create(this,lockedPoint);
        }
        private void AdvanceAttack(float dt)
        {
            float before=actionElapsed;actionElapsed+=dt;
            if(Phase==DungeonEnemyPhase.Windup&&actionElapsed>=Definition.Windup)
            {Phase=DungeonEnemyPhase.Active;CancelTell(false);ExecuteAttack();if(IsDead||Phase==DungeonEnemyPhase.Stunned)return;}
            float activeEnd=Definition.Windup+Definition.Active;
            if(Kind==DungeonEnemyKind.Ghoul&&Phase==DungeonEnemyPhase.Active)
                SweepDash(Mathf.Max(0,Mathf.Min(actionElapsed,activeEnd)-Mathf.Max(before,Definition.Windup)));
            if(Phase==DungeonEnemyPhase.Active&&actionElapsed>=activeEnd)Phase=DungeonEnemyPhase.Recovery;
            if(actionElapsed>=activeEnd+Definition.Recovery){Phase=DungeonEnemyPhase.Idle;delay=Definition.Delay;approachDecided=false;planRemaining=0;}
        }
        private void ExecuteAttack()
        {
            if(Kind==DungeonEnemyKind.Warden)
            {
                if(committedToDecoy)return;
                Vector2 offset=(Vector2)target.transform.position-body.position;
                if(offset.magnitude<=1.35f&&Vector2.Dot(offset.normalized,StrikeDirection)>=Mathf.Cos(50f*Mathf.Deg2Rad)&&DungeonCombatPhysics.ClearLine(body.position,target.transform.position))target.TakeDamage(EffectiveDamage,body.position,this);
            }
            else if(Kind==DungeonEnemyKind.Arbalist)DungeonEnemyBolt.Create(this,StrikeDirection,committedToDecoy);
            else if(Kind==DungeonEnemyKind.Cultist)
            {
                if(area==null)area=DungeonEnemyArea.Create(this,lockedPoint);area.Burst();area=null;
                if(!committedToDecoy&&Vector2.Distance(target.transform.position,lockedPoint)<=1.1f&&DungeonCombatPhysics.ClearLine(lockedPoint,target.transform.position))
                    if(target.TakeDamage(EffectiveDamage,body.position,this))target.ApplySlow(.70f,1f);
            }
        }
        private void SweepDash(float dt)
        {
            if(dt<=0||Phase!=DungeonEnemyPhase.Active)return;
            float distance=Mathf.Min(7f*dt,1.82f-dashDistance);Vector2 from=body.position;
            float allowed=EnemyCollision.DistanceToBlocker(this,from,StrikeDirection,distance,.30f);
            bool blocked=allowed<distance-.0001f;Vector2 to=from+StrikeDirection*allowed;
            if(!committedToDecoy&&!dashHit&&EnemyCollision.HitsPlayer(target,from,to,.35f)){dashHit=true;target.TakeDamage(EffectiveDamage,from,this);}
            if(Phase!=DungeonEnemyPhase.Active)return;
            // A sword hit pushes even a committed dash without cancelling its attack clock.
            if(knockbackVelocity.sqrMagnitude>.0001f)
                to+=knockbackVelocity.normalized*EnemyCollision.DistanceToBlocker(this,to,knockbackVelocity.normalized,knockbackVelocity.magnitude*dt,.30f);
            body.MovePosition(to);movedDashThisStep=true;dashDistance+=allowed;
            if(blocked||dashDistance>=1.82f){Phase=DungeonEnemyPhase.Recovery;actionElapsed=Definition.Windup+Definition.Active;}
        }
        private void CancelTell(bool cancelArea=true)
        {
            if(tellVisual!=null){tellVisual.gameObject.SetActive(false);Destroy(tellVisual.gameObject);tellVisual=null;}
            if(cancelArea&&area!=null){area.gameObject.SetActive(false);Destroy(area.gameObject);area=null;}
        }
        private void OnCollisionEnter2D(Collision2D collision)=>ContactDamage(collision);
        private void OnCollisionStay2D(Collision2D collision)=>ContactDamage(collision);
        private void ContactDamage(Collision2D collision)
        {
            if(Definition.Ranged||!isActiveAndEnabled||IsDead||StunRemaining>0||!CombatClockRunning||contactRemaining>0)return;
            if(collision.collider.GetComponentInParent<DungeonPlayer>()!=target)return;
            contactRemaining=.75f;
            target.TakeDamage(EffectiveDamage,body.position,this);
        }
        public void ReceiveHit(float damage,Vector2 direction,float knockback,bool stagger=true,bool critical=false)
        {
            if(IsDead||damage<=0||float.IsNaN(damage)||float.IsInfinity(damage)||target==null||!target.CanAct)return;
            bool shield=Guarding&&Vector2.Dot(-direction.normalized,Facing)>=Mathf.Cos(50f*Mathf.Deg2Rad);
            float healthBefore=Health;
            Health=Mathf.Max(0f,Health-Mathf.Max(0,damage*(1-(InRelicAura?target.Relics.EnemyDefense:0))-Stats.Defense)*(shield?.30f:1f));hitFlash=.16f;
            DungeonDamageNumber.Create(target,transform.position,healthBefore-Health,false,critical);
            if(shield)DungeonCombatFx.Impact(target,body.position,new Color(.8f,.75f,.5f));
            else
            {
                if(stagger)Stun(.16f,direction.normalized*knockback);
                else knockbackVelocity=direction.normalized*knockback;
                DungeonCombatFx.Hit(target,body.position,direction,false);
            }
            if(!IsDead)return;
            CancelTell();if(flanking)EndFlank();Phase=DungeonEnemyPhase.Dead;deathRemaining=.30f;
            feet.enabled=false;body.linearVelocity=Vector2.zero;body.simulated=false;route.Clear();target.AwardScore(Stats.ScoreDrop);
            if(stunVisual!=null)Destroy(stunVisual.gameObject);
        }
        public void Stun(float seconds,Vector2 recoil=default)
        {
            if(IsDead||seconds<=0)return;CancelTell();if(flanking)EndFlank();
            StunRemaining=Mathf.Max(StunRemaining,seconds);Phase=DungeonEnemyPhase.Stunned;knockbackVelocity=recoil;
            body.linearVelocity=Vector2.zero;route.Clear();planRemaining=0;
            if(stunVisual==null&&target!=null)stunVisual=DungeonCombatFx.Stun(target,this);
        }
        internal void ApplyHitStop(float seconds){if(DungeonFxSettings.Current.hitStop){hitStopRemaining=Mathf.Max(hitStopRemaining,seconds);if(body!=null)body.linearVelocity=Vector2.zero;}}
        private void UpdateTint(){if(visual==null)return;visual.color=hitFlash>0?Color.Lerp(Color.white,new Color(1,.55f,.55f),hitFlash/.16f):Color.white;if(stunVisual!=null)stunVisual.SetVisible(StunRemaining>0);}
    }
}
