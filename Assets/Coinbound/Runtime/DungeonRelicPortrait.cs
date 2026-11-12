using UnityEngine;
using UnityEngine.UI;
namespace Coinbound
{
    public sealed class DungeonRelicPortrait : RawImage
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=rectTransform.rect;
            // Clip the original background to a circle on its native 32-pixel grid.
            for(int y=0;y<32;y++)
            {
                float dy=y+.5f-16;int half=Mathf.FloorToInt(Mathf.Sqrt(256-dy*dy));
                float left=(16-half)/32f,right=(16+half)/32f,bottom=y/32f,top=(y+1)/32f;
                int n=mesh.currentVertCount;
                mesh.AddVert(new Vector3(r.xMin+left*r.width,r.yMin+bottom*r.height),color,new Vector2(left,bottom));
                mesh.AddVert(new Vector3(r.xMin+left*r.width,r.yMin+top*r.height),color,new Vector2(left,top));
                mesh.AddVert(new Vector3(r.xMin+right*r.width,r.yMin+top*r.height),color,new Vector2(right,top));
                mesh.AddVert(new Vector3(r.xMin+right*r.width,r.yMin+bottom*r.height),color,new Vector2(right,bottom));
                mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n+2,n+3,n);
            }
        }
    }
}
