using System.Collections.Generic;
using UnityEngine;
namespace Coinbound
{
    public sealed partial class DungeonProps
    {
        private readonly List<Rect> shadowBoxes=new List<Rect>(256);
        public int ShadowRevision {get;private set;}
        private void RebuildShadowGeometry()
        {
            shadowBoxes.Clear();
            foreach(var item in Items)foreach(var collider in item.GetComponentsInChildren<BoxCollider2D>())
            {
                if(!collider.enabled||!collider.gameObject.activeInHierarchy)continue;
                Bounds bounds=collider.bounds;shadowBoxes.Add(new Rect(bounds.min.x,bounds.min.y,bounds.size.x,bounds.size.y));
            }
            ShadowRevision++;
        }
        public void CollectShadowBoxes(Vector2 origin,float radius,List<Rect> output)
        {output.Clear();foreach(var box in shadowBoxes)if(Gap(box,origin)<=radius)output.Add(box);}
        public bool ClearPropSight(Vector2 from,Vector2 to)
        {
            Vector2 delta=to-from;float distance=delta.magnitude;if(distance<.001f)return true;
            foreach(var box in shadowBoxes)
                if(!box.Contains(to)&&!box.Contains(from)&&Intersect(box,from,delta/distance,out float near,out _)&&near<distance-.04f)return false;
            return true;
        }
        /// <summary>Light-ray intersections with live solid geometry, independent of art and line-of-sight walls.</summary>
        public static Vector2 TraceShadow(IReadOnlyList<Rect> boxes,Vector2 origin,Vector2 direction,float maximum)
        {
            Vector2 result=new Vector2(maximum,maximum);
            foreach(var box in boxes)
            {
                // Candle/brazier sources are mounted on their own base and must not occlude themselves.
                if(box.Contains(origin)||!Intersect(box,origin,direction,out float near,out float far)||near>=result.x||far<=0)continue;
                result=new Vector2(Mathf.Max(0,near),Mathf.Min(maximum,far));
            }
            return result;
        }
        private static bool Intersect(Rect box,Vector2 origin,Vector2 direction,out float near,out float far)
        {
            near=0;far=float.PositiveInfinity;
            if(!Slab(box.xMin,box.xMax,origin.x,direction.x,ref near,ref far))return false;
            return Slab(box.yMin,box.yMax,origin.y,direction.y,ref near,ref far)&&far>=near;
        }
        private static bool Slab(float min,float max,float origin,float direction,ref float near,ref float far)
        {
            if(Mathf.Abs(direction)<.000001f)return origin>=min&&origin<=max;
            float a=(min-origin)/direction,b=(max-origin)/direction;
            near=Mathf.Max(near,Mathf.Min(a,b));far=Mathf.Min(far,Mathf.Max(a,b));return far>=near;
        }
    }
}
