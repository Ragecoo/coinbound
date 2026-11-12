using UnityEngine;
namespace Coinbound
{
    /// <summary>Fixed ground marker, driven by the owner's attack clock; never tracks its victim.</summary>
    public sealed class DungeonEnemyArea:MonoBehaviour
    {
        private DungeonEnemy owner;
        private DungeonCombatMesh pixels;
        private Vector2 center;
        private bool burst;
        private float age;
        public Vector2 Center=>center;
        public static DungeonEnemyArea Create(DungeonEnemy owner,Vector2 point)
        {
            var obj=new GameObject("Cultist warning");obj.transform.SetParent(owner.transform.parent,false);
            var area=obj.AddComponent<DungeonEnemyArea>();area.owner=owner;area.center=point;
            area.pixels=DungeonCombatMesh.Create(obj.transform,"Cultist rune",owner.EffectMaterial);return area;
        }
        public void Burst(){burst=true;age=0;}
        private void LateUpdate()
        {
            if(owner==null||owner.IsDead&&!burst){Destroy(gameObject);return;}
            if(!owner.CombatClockRunning)return;
            if(!burst&&!owner.IsWindingUp){Destroy(gameObject);return;}
            if(burst){age+=Time.deltaTime;if(age>=.3f){Destroy(gameObject);return;}}
            float progress=burst?1-age/.3f:owner.WindupProgress;
            pixels.Begin(center,center,true,.3f,false,true);
            Color color=burst?new Color(.5f,1,.65f,progress):new Color(.25f,.85f,.55f,.4f+progress*.5f);
            for(int i=0;i<72;i++)
            {
                float angle=i*Mathf.PI/36;Vector2 direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                pixels.Pixel(center+direction*1.1f,color,2,1);
                if(burst)pixels.Line(center+direction*(age*1.5f),center+direction*(.2f+age*2),color);
                else if(i%3==0)pixels.Pixel(center+direction*(1.1f*(1-progress)),color);
            }
            pixels.End();DungeonEffectLights.Glow(this,center,1.6f,burst?.9f:.25f,new Color(.25f,1,.55f));
        }
    }
}
