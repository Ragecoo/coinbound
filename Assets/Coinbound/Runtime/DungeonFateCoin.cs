using UnityEngine;
using UnityEngine.UI;
namespace Coinbound
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DungeonFateCoin:MaskableGraphic
    {
        public bool Skull,SymbolOnly;
        public float Spin;
        private static readonly string[] Star={"0001000","0001000","1111111","0111110","0011100","0110110","1100011"};
        private static readonly string[] Death={"0111110","1111111","1101011","1101011","0111110","0010100","0011100"};
        public void Refresh()=>SetVerticesDirty();
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float cell=rectTransform.rect.width/(SymbolOnly?9:32);
            float squash=SymbolOnly?1:Mathf.Max(1f/16,Mathf.Round(Mathf.Abs(Mathf.Cos(Spin))*16)/16);
            void Pixel(float x,float y,float w,float h,Color tint)=>DungeonUiPixels.Rect(mesh,this,
                Mathf.Round((x-w*.5f)*cell*squash/2)*2,Mathf.Round((y-h*.5f)*cell/2)*2,
                Mathf.Max(2,Mathf.Round(w*cell*squash/2)*2),Mathf.Max(2,Mathf.Round(h*cell/2)*2),tint*color);
            if(!SymbolOnly)
            {
                for(int y=-14;y<=14;y++)
                {
                    int half=Mathf.FloorToInt(Mathf.Sqrt(14*14-y*y));
                    Pixel(0,y,half*2+1,1,new Color(.31f,.19f,.08f));
                    if(half>2)Pixel(0,y,half*2-3,1,new Color(y>0?.76f:.6f,y>0?.55f:.39f,.16f));
                    if(half>4)Pixel(-half+1,y,1,1,new Color(1,.83f,.43f));
                }
                for(int y=-10;y<=10;y++)
                {
                    int half=Mathf.FloorToInt(Mathf.Sqrt(100-y*y));
                    Pixel(0,y,half*2+1,1,Skull?new Color(.22f,.14f,.18f):new Color(.46f,.3f,.12f));
                }
            }
            string[] pattern=Skull?Death:Star;
            float size=SymbolOnly?1:2;
            for(int y=0;y<7;y++)for(int x=0;x<7;x++)
                if(pattern[y][x]=='1')Pixel((x-3)*size,(3-y)*size,size,size,Skull?new Color(.94f,.82f,.65f):new Color(1,.91f,.57f));
        }
    }
}
