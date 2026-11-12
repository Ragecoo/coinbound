using System;
using UnityEngine;
namespace Coinbound
{
    [Serializable] public sealed class PropCheckpoint
    {public int id,asset,room;public Vector2 position;public string wall;public float hp;public bool destroyed;}
    /// <summary>Persistent collision/state. Static props deliberately have no Update or LateUpdate.</summary>
    public sealed class DungeonProp : MonoBehaviour
    {
        public PropDefinition Definition {get;private set;}
        public float Health {get;private set;}
        public bool Destroyed {get;private set;}
        public int Id {get;private set;}
        public int Room {get;private set;}
        public string Wall {get;private set;}
        internal DungeonProps manager;
        internal SpriteRenderer visual;
        internal readonly System.Collections.Generic.List<SpriteRenderer> renderers=new System.Collections.Generic.List<SpriteRenderer>();
        internal Vector2 lightAnchor;
        private int lastAttack=int.MinValue;
        private Rect roomBounds;
        public Rect VisualBounds=>Definition.Bounds(transform.position);
        public void Initialize(DungeonProps manager,PropDefinition definition,int id,int room,string wall,Vector2 position,Rect bounds)
        {
            this.manager=manager;Definition=definition;Id=id;Room=room;Wall=wall;Health=definition.hp;roomBounds=bounds;
            transform.position=position;
            lightAnchor=new Vector2(Mathf.Clamp(position.x,bounds.xMin+.04f,bounds.xMax-.04f),Mathf.Clamp(position.y,bounds.yMin+.04f,bounds.yMax-.04f));
            visual=manager.Render(transform,definition.Sprite,Vector2.zero,definition.ground,lightAnchor);
            if(definition.solid)AddCollision(definition,transform,Vector2.zero);
        }
        private void AddCollision(PropDefinition definition,Transform parent,Vector2 offset)
        {
            foreach(var box in definition.boxes)
            {
                Rect r=box.Local(definition.pivot);r.position+=(Vector2)transform.position+offset;
                float left=Mathf.Max(r.xMin,roomBounds.xMin),right=Mathf.Min(r.xMax,roomBounds.xMax);
                float bottom=Mathf.Max(r.yMin,roomBounds.yMin),top=Mathf.Min(r.yMax,roomBounds.yMax);
                if(right<=left||top<=bottom)continue;
                var collider=parent.gameObject.AddComponent<BoxCollider2D>();collider.size=new Vector2(right-left,top-bottom);
                collider.offset=new Vector2((left+right)/2,(bottom+top)/2)-(Vector2)parent.position;
            }
        }
        internal bool HitPoint(Vector2 from,out Vector2 point)
        {
            point=transform.position;float best=float.PositiveInfinity;
            if(Destroyed||Definition.hp<=0)return false;
            foreach(var box in Definition.hitboxes)
            {
                Rect r=box.Local(Definition.pivot);r.position+=(Vector2)transform.position;
                Vector2 candidate=new Vector2(Mathf.Clamp(from.x,r.xMin,r.xMax),Mathf.Clamp(from.y,r.yMin,r.yMax));
                float d=(candidate-from).sqrMagnitude;
                if(d<best){best=d;point=candidate;}
            }
            return best<float.PositiveInfinity;
        }
        public bool ReceiveHit(float damage,int attackId)
        {
            if(Destroyed||Definition.hp<=0||damage<=0||float.IsNaN(damage)||float.IsInfinity(damage)||
               lastAttack==attackId||manager==null||!manager.CanAct)return false;
            lastAttack=attackId;Health=0;
            if(Health<=0)Break(true);else manager.HitFeedback(this);
            return true;
        }
        private void Break(bool effects)
        {
            if(Destroyed)return;Destroyed=true;Health=0;visual.enabled=false;
            foreach(var collider in GetComponents<Collider2D>())collider.enabled=false;
            foreach(var debris in Definition.debris)
            {
                var remnant=manager.Render(transform,Resources.Load<Sprite>(debris.file.StartsWith("FloorDecor/")?debris.file:"EnvironmentProps52V5/debris/"+debris.file),new Vector2(debris.offset[0],debris.offset[1])/32,true,lightAnchor);
                manager.RegisterDebris(remnant);
            }
            foreach(var survivor in Definition.survivors)
            {
                var data=manager.Definition(survivor.id);Vector2 offset=new Vector2(survivor.offset[0],survivor.offset[1])/32;
                var sprite=manager.Render(transform,data.Sprite,offset,data.ground,lightAnchor+offset);
                if(survivor.solid&&data.solid)AddCollision(data,sprite.transform,offset);
            }
            manager.ObstaclesChanged();if(effects)manager.BreakFeedback(this);
        }
        public PropCheckpoint Snapshot()=>new PropCheckpoint{id=Id,asset=Definition.id,room=Room,wall=Wall,position=transform.position,hp=Health,destroyed=Destroyed};
        internal void Restore(PropCheckpoint state){Health=Mathf.Clamp(state.hp,0,Definition.hp);if(state.destroyed)Break(false);}
        internal void SetVisible(bool visible)
        {
            foreach(var renderer in renderers)if(renderer!=null)renderer.enabled=visible&&(renderer!=visual||!Destroyed);
        }
    }
}
