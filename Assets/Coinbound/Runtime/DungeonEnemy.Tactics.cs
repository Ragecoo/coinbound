using System.Collections.Generic;
using UnityEngine;

namespace Coinbound
{
    public sealed partial class DungeonEnemy
    {
        private readonly List<Vector2> route=new List<Vector2>(),candidateRoute=new List<Vector2>();
        private readonly List<Vector2> bestCoverRoute=new List<Vector2>();
        private readonly List<DungeonEnemy> coverCandidates=new List<DungeonEnemy>();
        private int routeIndex,flankSide;
        private Vector2 reservedPosition;
        private bool hasReservation,retreating,approachDecided,flanking;
        private float planRemaining,flankCooldown,flankRemaining,stuckTime;
        private DungeonEnemy cover;
        private System.Random random;
        public bool IsFlanking=>flanking;
        public float FlankCooldown=>flankCooldown;
        public DungeonEnemy Cover=>cover;
        public Vector2 TacticalPosition=>hasReservation?reservedPosition:body.position;

        private void Decide(float dt)
        {
            if(memory<=0)
            {
                if(flanking)EndFlank();approachDecided=false;cover=null;hasReservation=false;
                if(Vector2.Distance(body.position,spawn)>.5f)WalkDirect(spawn);return;
            }
            Vector2 offset=lastKnown-body.position;float gap=offset.magnitude;
            if(visible&&gap>.001f)Facing=offset.normalized;
            if(!visible)
            {
                if(flanking)EndFlank();cover=null;hasReservation=false;route.Clear();
                if(gap>.25f)WalkDirect(lastKnown);return;
            }
            if(Kind==DungeonEnemyKind.Ghoul&&!approachDecided)
            {
                approachDecided=true;
                // Exactly one probability draw per new approach, only when the independent cooldown allows it.
                if(flankCooldown<=0&&random.NextDouble()<DungeonEnemyDefinition.FlankProbability&&gap>1.1f)TryFlank();
            }
            if(flanking)
            {
                flankRemaining-=dt;
                if(flankRemaining<=0||gap<1.1f||Vector2.Distance(body.position,reservedPosition)<.25f||Vector2.Distance(lastKnown,flankTarget)>2f)
                    EndFlank();
                else if(!FollowRoute(dt)){EndFlank();}else return;
            }
            if(Definition.Ranged)
            {
                if(gap<Definition.RetreatStart)retreating=true;
                else if(gap>=Definition.RetreatEnd)retreating=false;
            }
            planRemaining-=dt;
            if(planRemaining<=0)
            {
                planRemaining=DungeonEnemyDefinition.TacticalInterval;
                if(Definition.Ranged)PlanRanged(gap);
                else if(Kind==DungeonEnemyKind.Warden)PlanFront();
            }
            bool freeShot=Kind!=DungeonEnemyKind.Arbalist||EnemyCollision.ClearShot(this,body.position,lastKnown);
            bool canAttack=gap<=Definition.AttackRange&&freeShot&&delay<=0;
            if(canAttack&&(!Definition.Ranged||route.Count==0)) {BeginAttack();return;}
            if(Definition.Ranged)
            {
                if(route.Count>0)FollowRoute(dt);
                // A blocked retreat falls back to the normal, fully telegraphed attack above.
                return;
            }
            if(Kind==DungeonEnemyKind.Warden&&route.Count>0){FollowRoute(dt);return;}
            if(gap>.8f)WalkDirect(lastKnown);
        }
        private void WalkDirect(Vector2 destination)
        {
            Vector2 heading=navigation!=null?navigation.Direction(body.position,destination):(destination-body.position).normalized;
            Move(heading);
        }
        private void Move(Vector2 heading)
        {
            if(heading.sqrMagnitude<.001f)return;
            float distance=EffectiveSpeed*Time.fixedDeltaTime;
            // Do not push allies or feed velocity into an impassable wall.
            float free=EnemyCollision.DistanceToBlocker(this,body.position,heading.normalized,distance+.04f,.30f);
            if(free<=.015f)return;
            body.linearVelocity=heading.normalized*Mathf.Min(EffectiveSpeed,Mathf.Max(0,free-.02f)/Time.fixedDeltaTime);
        }
        private bool FollowRoute(float dt)
        {
            while(routeIndex<route.Count&&Vector2.Distance(body.position,route[routeIndex])<.2f)routeIndex++;
            if(routeIndex>=route.Count){route.Clear();return false;}
            Vector2 waypoint=route[routeIndex];
            if(!DungeonCombatPhysics.ClearLine(body.position,waypoint,.29f)){route.Clear();planRemaining=0;return false;}
            Move(waypoint-body.position);
            if(body.linearVelocity.sqrMagnitude<.01f)stuckTime+=dt;else stuckTime=0;
            if(stuckTime>.3f){route.Clear();hasReservation=false;stuckTime=0;return false;}
            return true;
        }
        private bool Occupied(Vector2 point)
        {
            foreach(DungeonEnemy other in Enemies)
            {
                if(other==this||other==null||other.IsDead)continue;
                if(Vector2.Distance(point,other.body!=null?other.body.position:(Vector2)other.transform.position)<.68f)return true;
                if(other.hasReservation&&Vector2.Distance(point,other.reservedPosition)<.75f)return true;
            }
            return false;
        }
        private bool Candidate(Vector2 point,bool safe)
        {
            if(Vector2.Distance(point,spawn)>11.8f||Occupied(point)||!DungeonCombatPhysics.FreeFeet(point,.32f))return false;
            if(map!=null&&!map.IsTileFloor(new ProceduralGeneration.TilePosition(Mathf.FloorToInt(point.x),Mathf.FloorToInt(1-point.y))))return false;
            candidateRoute.Clear();
            if(navigation!=null){if(!navigation.LocalPath(body.position,point,candidateRoute))return false;}
            else{if(!DungeonCombatPhysics.ClearLine(body.position,point,.31f))return false;candidateRoute.Add(body.position);candidateRoute.Add(point);}
            for(int i=1;i<candidateRoute.Count;i++)
            {
                Vector2 a=candidateRoute[i-1],b=candidateRoute[i];
                if(!DungeonCombatPhysics.ClearLine(a,b,.30f))return false;
                if(safe&&EnemyCollision.SegmentDistance(lastKnown,a,b)<Mathf.Min(.85f,Vector2.Distance(body.position,lastKnown)-.05f))return false;
                foreach(DungeonEnemy other in Enemies)
                    if(other!=this&&other!=null&&!other.IsDead&&EnemyCollision.SegmentDistance(other.transform.position,a,b)<.60f &&
                        (Vector2.Distance(other.transform.position,a)>.60f||Vector2.Dot((Vector2)other.transform.position-a,b-a)>0))return false;
            }
            return true;
        }
        private void Reserve(Vector2 point)
        {reservedPosition=point;hasReservation=true;route.Clear();route.AddRange(candidateRoute);routeIndex=1;stuckTime=0;}
        private void PlanFront()
        {
            if(Vector2.Distance(body.position,lastKnown)>4f){route.Clear();hasReservation=false;return;}
            Vector2 away=(body.position-lastKnown).normalized;if(away.sqrMagnitude<.01f)away=Vector2.up;
            float[] angles={0f,45f,-45f,85f,-85f,130f,-130f};
            foreach(float angle in angles)
            {
                Vector2 point=lastKnown+Rotate(away,angle)*.95f;
                if(!Candidate(point,false))continue;Reserve(point);return;
            }
            route.Clear();hasReservation=false;
        }
        private bool UsefulCover(DungeonEnemy ally)
        {
            if(ally==null||ally==this||ally.IsDead||ally.Kind!=DungeonEnemyKind.Warden||Vector2.Distance(body.position,ally.transform.position)>6f)return false;
            float tankGap=Vector2.Distance(lastKnown,ally.transform.position);
            return tankGap>.6f&&tankGap<Definition.ComfortableMax-.6f;
        }
        private void PlanRanged(float gap)
        {
            if(!UsefulCover(cover))cover=null;
            if(cover==null)
            {
                float best=float.PositiveInfinity;
                foreach(DungeonEnemy ally in Enemies)
                {
                    if(!UsefulCover(ally))continue;
                    float score=Vector2.Distance(body.position,ally.transform.position)+(ally.GroupId==GroupId?0f:1f);
                    if(score<best){best=score;cover=ally;}
                }
            }
            bool clear=Kind!=DungeonEnemyKind.Arbalist||EnemyCollision.ClearShot(this,body.position,lastKnown);
            bool covered=cover!=null&&BehindCover(body.position,cover);
            if(!retreating&&gap>=Definition.ComfortableMin&&gap<=Definition.ComfortableMax&&clear&&(cover==null||covered))
            {route.Clear();reservedPosition=body.position;hasReservation=true;return;}
            Vector2 away=(body.position-lastKnown).normalized;if(away.sqrMagnitude<.01f)away=Vector2.up;
            float bestScore=float.PositiveInfinity;Vector2 bestPoint=default;var bestRoute=bestCoverRoute;bestRoute.Clear();
            coverCandidates.Clear();
            foreach(DungeonEnemy ally in Enemies)if(UsefulCover(ally))coverCandidates.Add(ally);
            coverCandidates.Sort((a,b)=>
            {
                if(a==b)return 0;if(a==cover)return -1;if(b==cover)return 1;
                float aScore=Vector2.Distance(body.position,a.transform.position)+(a.GroupId==GroupId?0f:1f);
                float bScore=Vector2.Distance(body.position,b.transform.position)+(b.GroupId==GroupId?0f:1f);
                return aScore.CompareTo(bScore);
            });
            // Probe both rear and side exits. A comfortable band is not a random distance draw.
            // Keep useful cover, but try other wardens if no complete safe route exists behind it.
            for(int pass=0;pass<=coverCandidates.Count;pass++)
            {
                DungeonEnemy candidateCover=pass<coverCandidates.Count?coverCandidates[pass]:null;
                Vector2 axis=candidateCover!=null?((Vector2)candidateCover.transform.position-lastKnown).normalized:away;
                for(int sample=0;sample<12;sample++)
                {
                    float angle=sample==0?0:((sample+1)/2)*25f*(sample%2==1?1:-1);
                    float radius=sample%3==0?Definition.ComfortableMin:sample%3==1?(Definition.ComfortableMin+Definition.ComfortableMax)*.5f:Definition.ComfortableMax;
                    Vector2 point=lastKnown+Rotate(axis,angle)*radius;
                    if(candidateCover!=null&&!BehindCover(point,candidateCover))continue;
                    if(!DungeonCombatPhysics.ClearLine(point,lastKnown))continue;
                    if(Kind==DungeonEnemyKind.Arbalist&&!EnemyCollision.ClearShot(this,point,lastKnown))continue;
                    float score=Vector2.Distance(body.position,point)+(candidateCover!=null?-1.5f:0);
                    if(gap<Definition.Danger)score-=radius-gap;
                    if(score>=bestScore||!Candidate(point,true))continue;
                    bestScore=score;bestPoint=point;bestRoute.Clear();bestRoute.AddRange(candidateRoute);
                }
                if(bestRoute.Count>0){cover=candidateCover;break;}
            }
            if(bestRoute.Count>0){candidateRoute.Clear();candidateRoute.AddRange(bestRoute);Reserve(bestPoint);}
            else{cover=null;route.Clear();hasReservation=false;}
        }
        private bool BehindCover(Vector2 point,DungeonEnemy ally)
        {
            Vector2 tank=(Vector2)ally.transform.position-lastKnown,back=point-lastKnown;
            return tank.magnitude+.6f<back.magnitude&&Vector2.Dot(tank.normalized,back.normalized)>.75f;
        }
        private Vector2 flankTarget;
        private void TryFlank()
        {
            Vector2 away=(body.position-lastKnown).normalized;int left=0,right=0;
            foreach(DungeonEnemy other in Enemies)if(other!=this&&other!=null&&other.flanking){if(other.flankSide<0)left++;else right++;}
            int preferred=left<right?-1:right<left?1:random.NextDouble()<.5?-1:1;
            for(int attempt=0;attempt<2;attempt++)
            {
                int side=attempt==0?preferred:-preferred;
                Vector2 point=lastKnown+Rotate(away,side*75f)*1.65f;
                if(!Candidate(point,true))continue;
                Reserve(point);flankSide=side;flankTarget=lastKnown;flankRemaining=2.5f;flanking=true;return;
            }
            EndFlank();
        }
        private void EndFlank()
        {
            flanking=false;flankSide=0;route.Clear();hasReservation=false;
            // One uniform draw when an attempt ends/cancels, retained until the timer finishes.
            flankCooldown=3f+(float)random.NextDouble()*2f;
        }
        private static Vector2 Rotate(Vector2 value,float degrees)
        {float a=degrees*Mathf.Deg2Rad;return new Vector2(value.x*Mathf.Cos(a)-value.y*Mathf.Sin(a),value.x*Mathf.Sin(a)+value.y*Mathf.Cos(a));}
    }
}
