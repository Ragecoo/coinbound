using UnityEngine;

namespace Coinbound
{
    internal static class EnemyCollision
    {
        private static readonly RaycastHit2D[] Hits=new RaycastHit2D[128];
        private static readonly ContactFilter2D Filter=new ContactFilter2D{useTriggers=false,useLayerMask=true,layerMask=Physics2D.DefaultRaycastLayers};
        internal static float SegmentDistance(Vector2 point,Vector2 a,Vector2 b)
        {Vector2 d=b-a;float t=d.sqrMagnitude>.000001f?Mathf.Clamp01(Vector2.Dot(point-a,d)/d.sqrMagnitude):0;return Vector2.Distance(point,a+d*t);}
        internal static bool HitsPlayer(DungeonPlayer player,Vector2 from,Vector2 to,float radius)
        {
            int count=Physics2D.CircleCast(from,radius,(to-from).normalized,Filter,Hits,Vector2.Distance(from,to));
            for(int i=0;i<count;i++)if(Hits[i].collider.GetComponentInParent<DungeonPlayer>()==player)return true;
            return SegmentDistance(player.transform.position,from,to)<=radius;
        }
        internal static bool ClearShot(DungeonEnemy shooter,Vector2 from,Vector2 to)
        {
            Vector2 delta=to-from;int count=Physics2D.CircleCast(from,.08f,delta.normalized,Filter,Hits,delta.magnitude);
            if(count==Hits.Length)return false;
            for(int i=0;i<count;i++)
            {
                var collider=Hits[i].collider;
                if(collider.GetComponentInParent<DungeonEnemy>()==shooter||collider.GetComponentInParent<DungeonPlayer>()!=null)continue;
                var ally=collider.GetComponentInParent<DungeonEnemy>();if(ally!=null&&ally.IsDead)continue;
                return false;
            }
            return true;
        }
        internal static float DistanceToBlocker(DungeonEnemy mover,Vector2 from,Vector2 direction,float distance,float radius)
        {
            int count=Physics2D.CircleCast(from,radius,direction,Filter,Hits,distance);
            float result=distance;
            if(count==Hits.Length)return 0;
            for(int i=0;i<count;i++)
            {
                var collider=Hits[i].collider;
                if(collider.GetComponentInParent<DungeonEnemy>()==mover||collider.GetComponentInParent<DungeonPlayer>()!=null)continue;
                var enemy=collider.GetComponentInParent<DungeonEnemy>();if(enemy!=null&&enemy.IsDead)continue;
                // A body touching behind us must not prevent moving away.
                if(Hits[i].distance==0&&Vector2.Dot((Vector2)collider.bounds.center-from,direction)<0)continue;
                result=Mathf.Min(result,Mathf.Max(0,Hits[i].distance-.01f));
            }
            for(float step=.04f;step<=result;step+=.04f)
                if(!mover.FloorAt(from+direction*step)){result=Mathf.Max(0,step-.04f);break;}
            return result;
        }
        internal static bool BoltHit(DungeonEnemy shooter,Vector2 from,Vector2 to,out RaycastHit2D closest)
        {
            Vector2 delta=to-from;int count=Physics2D.CircleCast(from,.08f,delta.normalized,Filter,Hits,delta.magnitude);
            closest=default;float distance=float.PositiveInfinity;
            for(int i=0;i<count;i++)
            {
                Collider2D c=Hits[i].collider;var enemy=c.GetComponentInParent<DungeonEnemy>();
                if(enemy!=null&&(enemy==shooter||enemy.IsDead))continue;
                if(Hits[i].distance>=distance)continue;distance=Hits[i].distance;closest=Hits[i];
            }
            return closest.collider!=null;
        }
    }
}
