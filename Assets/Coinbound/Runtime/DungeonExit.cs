using UnityEngine;
namespace Coinbound
{
    public sealed class DungeonExit : MonoBehaviour
    {
        private DungeonGame game;private DungeonView view;private DungeonPlayer player;
        private SpriteRenderer visual;private Sprite open,closed;private BoxCollider2D barrier;
        private DungeonAmbientCanvas glow;private int total,floor;
        public bool Unlocked {get;private set;}
        public int RemainingKills=>Mathf.Max(0,DungeonGame.RequiredKills(total)-(total-view.AliveEnemyCount));
        public void Initialize(DungeonGame game,DungeonView view,DungeonPlayer player,int total,int floor)
        {
            this.game=game;this.view=view;this.player=player;this.total=total;this.floor=floor;
            visual=GetComponent<SpriteRenderer>();open=visual.sprite;
            closed=Resources.Load<Sprite>((floor==1?"DungeonArt":"DungeonArt_Floor"+floor)+"/object.exit.closed");
            barrier=gameObject.AddComponent<BoxCollider2D>();barrier.size=new Vector2(1.25f,.65f);barrier.offset=new Vector2(0,-.2f);
            glow=DungeonAmbientCanvas.Create(view.transform,"Stair barrier",visual.sharedMaterial);RefreshBarrier();
        }
        public bool IsNear(DungeonPlayer actor)=>actor!=null&&Vector2.Distance(actor.transform.position,transform.position)<1.8f;
        public bool TryUse(DungeonPlayer actor)=>actor==player&&game.TryAscend(this);
        public string Prompt=>Unlocked?(floor==3?"E — Завершить забег":"E — Подняться"):"Барьер: ещё "+RemainingKills+" врагов";
        public void RefreshBarrier()
        {bool wasUnlocked=Unlocked;Unlocked=RemainingKills==0;
            if(!wasUnlocked&&Unlocked&&game.IsReady)DungeonEventAudio.Play(DungeonEventAudio.Cue.Barrier);barrier.enabled=!Unlocked;visual.sprite=Unlocked?open:(closed!=null?closed:open);}
        private void Update()
        {
            RefreshBarrier();if(Unlocked){glow.Hide();return;}
            Vector2 center=transform.position;glow.Begin(visual.sortingOrder+1,center,.45f);
            for(int i=0;i<7;i++)
            {
                float rise=Mathf.Repeat(Time.time*.35f+i*.17f,1);
                glow.Pixel(center+new Vector2((i-3)*.16f,-.5f+rise),new Color(.56f,.49f,.85f,.22f+.22f*Mathf.Sin(rise*Mathf.PI)),1,3);
            }
            glow.End();
        }
        public static DungeonExit Find(DungeonPlayer player)
        {
            if(player==null||!player.CanAct)return null;
            var exit=player.transform.parent.GetComponentInChildren<DungeonExit>();
            return exit!=null&&exit.IsNear(player)?exit:null;
        }
    }
}
