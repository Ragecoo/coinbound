using System.Collections.Generic;
using UnityEngine;

namespace Coinbound
{
    public sealed class DungeonFountain : MonoBehaviour
    {
        private static readonly List<DungeonFountain> active=new List<DungeonFountain>();
        private DungeonGame game;
        private DungeonScreen screen;
        private Collider2D basin;
        private static Sprite[] frames;
        private SpriteRenderer visual;
        private int frameIndex=-1;
        private float animationTime;
        private MaterialPropertyBlock animationProperties;
        private void Update()
        {
            animationTime+=IsOffering?Time.unscaledDeltaTime:Time.deltaTime;
            Animate(animationTime);
        }
        internal void Animate(float elapsed)
        {
            if(visual==null)visual=GetComponent<SpriteRenderer>();
            if(frames==null)
            {
                frames=new Sprite[24];
                for(int i=0;i<frames.Length;i++)frames[i]=Resources.Load<Sprite>("FountainAnimation/fountain_"+i.ToString("00"));
            }
            int index=Mathf.FloorToInt(Mathf.Repeat(elapsed,2)*12);
            if(index==frameIndex||visual==null||frames[index]==null)return;
            visual.sprite=frames[index];frameIndex=index;
            if(animationProperties==null)animationProperties=new MaterialPropertyBlock();
            visual.GetPropertyBlock(animationProperties);
            DungeonSpriteGrid.Apply(visual,animationProperties);
        }
        internal bool IsOffering => screen!=null && screen.IsFountainOpen;
        public float SplashAt {get;private set;}=-100;
        public void Initialize(DungeonGame game,DungeonScreen screen){this.game=game;this.screen=screen;basin=GetComponent<Collider2D>();Animate(0);}
        private void OnEnable(){active.Add(this);}
        private void OnDisable(){active.Remove(this);}
        internal bool IsNear(DungeonPlayer player)=>player!=null&&basin!=null&&Vector2.Distance(basin.ClosestPoint(player.transform.position),player.transform.position)<1.25f;
        public static DungeonFountain Find(DungeonPlayer player)
        {
            if(player==null||!player.CanAct||player.IsAttacking||player.IsRolling||player.IsBlocking)return null;
            foreach(var fountain in active)if(fountain!=null&&fountain.IsNear(player)&&DungeonCombatPhysics.ClearLine(player.transform.position,fountain.transform.position,0,fountain.basin))return fountain;
            return null;
        }
        public static bool TryUse(DungeonPlayer player)
        {var fountain=Find(player);if(fountain==null)return false;fountain.screen.ShowFountain(player,fountain);return true;}
        internal bool Save(){SplashAt=Time.unscaledTime;return game.SaveAtFountain(this);}
    }
}
