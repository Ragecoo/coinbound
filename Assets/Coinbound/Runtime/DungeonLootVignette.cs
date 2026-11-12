using UnityEngine;
using UnityEngine.UI;
namespace Coinbound
{
    public sealed class DungeonLootVignette : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=rectTransform.rect;
            for(int y=0;y<36;y++)for(int x=0;x<64;x++)
            {
                float u=(x+.5f)/64,v=(y+.5f)/36;
                float distance=Mathf.Max(Mathf.Abs(u-.5f)*2,Mathf.Abs(v-.5f)*2);
                float alpha=Mathf.Round(Mathf.Pow(distance,3)*.72f*24)/24;
                DungeonUiPixels.Rect(mesh,this,r.xMin+x*r.width/64,r.yMin+y*r.height/36,r.width/64,r.height/36,new Color(.01f,.01f,.025f,alpha));
            }
        }
    }
}
