using UnityEngine;
namespace Coinbound
{
    public sealed class DungeonEnemyBolt:MonoBehaviour
    {
        private DungeonEnemy shooter;
        private DungeonPlayer player;
        private DungeonCombatMesh pixels;
        private Vector2 position,direction;
        private float traveled,damage;
        private bool ghost;
        public static DungeonEnemyBolt Create(DungeonEnemy owner,Vector2 direction,bool decoy)
        {
            var obj=new GameObject("Arbalist bolt");obj.transform.SetParent(owner.transform.parent,false);
            var bolt=obj.AddComponent<DungeonEnemyBolt>();bolt.shooter=owner;bolt.player=owner.Player;
            bolt.position=owner.transform.position;bolt.direction=direction;bolt.damage=owner.EffectiveDamage;bolt.ghost=decoy;
            bolt.pixels=DungeonCombatMesh.Create(obj.transform,"Bolt pixels",owner.EffectMaterial);return bolt;
        }
        private void FixedUpdate()
        {
            if(player==null||player.IsDead){Destroy(gameObject);return;}
            if(!player.CanAct||player.IsHitStopped||shooter!=null&&shooter.IsHitStopped)return;
            float distance=Mathf.Min(8f*Time.fixedDeltaTime,9f-traveled);Vector2 next=position+direction*distance;
            if(EnemyCollision.BoltHit(shooter,position,next,out RaycastHit2D hit))
            {
                if(!ghost&&hit.collider.GetComponentInParent<DungeonPlayer>()==player)player.TakeDamage(damage,position,shooter);
                DungeonCombatFx.Impact(player,hit.point,new Color(.85f,.72f,.4f));Destroy(gameObject);return;
            }
            position=next;traveled+=distance;if(traveled>=9f){Destroy(gameObject);return;}
            Vector2 center=position+Vector2.up*.5f;pixels.Begin(center,position,false,0,false,true);
            pixels.Line(center-direction*.25f,center,new Color(.75f,.62f,.37f));pixels.Pixel(center,new Color(.95f,.9f,.72f),2,2);pixels.End();
        }
    }
}
