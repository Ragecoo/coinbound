using System.Collections.Generic;
using UnityEngine;

namespace Coinbound
{
    /// <summary>Fixed authored feet pivot; no root motion, mirroring or independent animation clock.</summary>
    public sealed class DungeonEnemyAnimator : MonoBehaviour
    {
        private static readonly Dictionary<DungeonEnemyKind,Sprite[,,]> Cache = new Dictionary<DungeonEnemyKind,Sprite[,,]>();
        private Sprite[,,] frames;
        private SpriteRenderer visual;
        private int facing;
        private float walkPhase;
        public int FrameIndex { get; private set; }
        public void Initialize(DungeonEnemyKind kind,SpriteRenderer renderer)
        {
            visual=renderer;
            if (!Cache.TryGetValue(kind,out frames))
            {
                frames=new Sprite[4,3,8]; string[] names={"down","left","right","up"};
                for(int d=0;d<4;d++)
                {
                    Texture2D texture=Resources.Load<Texture2D>("Enemies/"+kind.ToString().ToLowerInvariant()+"/"+names[d]);
                    if(texture==null)continue;
                    for(int row=0;row<3;row++)for(int f=0;f<8;f++)
                    {
                        frames[d,row,f]=Sprite.Create(texture,new Rect(f*64,texture.height-(row+1)*64,64,64),new Vector2(.5f,.125f),32,0,SpriteMeshType.FullRect);
                        frames[d,row,f].name=$"Enemy {kind} {names[d]} row {row} frame {f}";
                    }
                }
                Cache.Add(kind,frames);
            }
            visual.transform.localScale=Vector3.one*.8f; visual.flipX=visual.flipY=false;
            visual.sprite=frames[0,0,0];
        }
        public void Show(DungeonEnemy enemy,float dt,float actualSpeed)
        {
            if(visual==null || enemy.IsDead || enemy.IsHitStopped || dt<=0 || enemy.StunRemaining>0)return;
            Vector2 look=enemy.IsAttacking?enemy.StrikeDirection:enemy.Facing;
            if(Mathf.Abs(look.x)>Mathf.Abs(look.y)*1.18f)facing=look.x<0?1:2;
            else if(Mathf.Abs(look.y)>Mathf.Abs(look.x)*1.18f)facing=look.y<0?0:3;
            int row=0;FrameIndex=0;
            if(enemy.IsAttacking)
            {
                row=2;FrameIndex=enemy.AttackFrame;
            }
            else if(actualSpeed>.08f)
            {
                row=1;walkPhase=Mathf.Repeat(walkPhase+dt*10*actualSpeed/Mathf.Max(.01f,enemy.Stats.MoveSpeed),8);
                FrameIndex=Mathf.FloorToInt(walkPhase);
            }
            else walkPhase=0;
            visual.sprite=frames[facing,row,FrameIndex];
        }
    }
}
