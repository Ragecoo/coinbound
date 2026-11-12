using UnityEngine;

namespace Coinbound.EnemyPack
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class EnemyAtlasPlayer : MonoBehaviour
    {
        [Tooltip("512x512 atlas, rows: down move/attack, left move/attack, right move/attack, up move/attack.")]
        public Texture2D atlas;
        public float pixelsPerUnit=32;
        public EnemyBrain Brain { get; set; }
        private readonly Sprite[,] frames=new Sprite[8,8];
        private SpriteRenderer target;
        private float walkPhase;
        private int facing;
        private bool wasMoving;

        void Awake()
        {
            target=GetComponent<SpriteRenderer>();
            if(atlas==null || atlas.width!=512 || atlas.height!=512)
            { Debug.LogError("Enemy atlas must be 512x512.",this); enabled=false; return; }
            atlas.filterMode=FilterMode.Point;
            for(int r=0;r<8;r++)for(int c=0;c<8;c++)
                frames[r,c]=Sprite.Create(atlas,new Rect(c*64,512-(r+1)*64,64,64),new Vector2(.5f,.125f),pixelsPerUnit,0,SpriteMeshType.FullRect);
            target.flipX=false; target.flipY=false;
        }

        // The host calls this AFTER Brain.Tick, passing the same simulation delta (0 during pause/hit-stop).
        // There is deliberately no independent Update clock that can desynchronize attack and damage.
        public void Show(float simulationDelta)
        {
            if(Brain==null || !enabled || target==null)return;
            if(Brain.State==EnemyState.Dead || Brain.State==EnemyState.Stunned)return;
            EnemyVector look=Brain.IsAction?Brain.LockedDirection:Brain.Facing;
            // Hysteresis prevents diagonal direction chatter. Shading is authored separately per direction.
            float ax=Mathf.Abs(look.X),ay=Mathf.Abs(look.Y);
            if(ax>ay*1.15f)facing=look.X<0?1:2;
            else if(ay>ax*1.15f)facing=look.Y<0?0:3;
            bool moving=Brain.State==EnemyState.Move;
            if(moving && !wasMoving)walkPhase=0;
            if(moving)walkPhase=Mathf.Repeat(walkPhase+Mathf.Max(0,simulationDelta)*10*Brain.Velocity.Length/Mathf.Max(.01f,Brain.Tuning.Speed),8);
            int row=facing*2+(moving?0:1);
            int frame=Brain.IsAction?Brain.AnimationFrame:moving?Mathf.FloorToInt(walkPhase):0;
            target.sprite=frames[row,frame]; wasMoving=moving;
        }
        void OnDestroy() { foreach(Sprite s in frames)if(s!=null)Destroy(s); }
    }
}
