using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Coinbound
{
    [Serializable] public sealed class DungeonRunData
    {
        public int version=2,seed,width,height,floor,runSeed;
        public bool legacyLayout;
        public double loopChance;
        public string hero;
        public float health,mana,score,roll,block,runSeconds;
        public Vector2 position;
        public string[] relics;
        public float[] cooldowns;
        public PropCheckpoint[] props;
        public EnemyCheckpoint[] enemies;
        public ChestCheckpoint[] chests;
    }
    [Serializable] public sealed class EnemyCheckpoint { public int id; public float health; public Vector2 position; }
    [Serializable] public sealed class ChestCheckpoint
    { public Vector2 position; public string facing,good,bad; public bool star,resolved,claimed; }
    public static class DungeonRunSave
    {
        public static string FilePath => Path.Combine(Application.persistentDataPath,"fountain-save.json");
        public static DungeonRunData Load()
        {
            foreach(string path in new[]{FilePath,FilePath+".bak"})
                try { if(File.Exists(path)){var data=JsonUtility.FromJson<DungeonRunData>(File.ReadAllText(path));if(Valid(data))return data;} }
                catch(Exception error){Debug.LogWarning("Could not read fountain checkpoint: "+error.Message);}
            return null;
        }
        public static bool Valid(DungeonRunData data)
        {
            if(data==null||(data.version!=1&&data.version!=2)||data.floor<0||data.floor>2||data.width<3||data.width>100||data.height<3||data.height>100||
               data.health<=0||float.IsNaN(data.health)||float.IsInfinity(data.health)||
               data.hero!=DungeonHero.First.Id&&data.hero!=DungeonHero.Second.Id||data.enemies==null||data.chests==null||data.relics==null)return false;
            return data.relics.All(id=>DungeonRelics.All.Any(r=>r.Id==id)) && data.chests.All(c=>c!=null &&
                (string.IsNullOrEmpty(c.good) || DungeonRelics.All.Any(r=>r.Id==c.good&&r.Positive)&&DungeonRelics.All.Any(r=>r.Id==c.bad&&!r.Positive)));
        }
        public static void Write(DungeonRunData data)
        {
            if(!Valid(data))throw new InvalidDataException("Invalid fountain checkpoint");
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            string temp=FilePath+".tmp";
            using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
            using(var writer=new StreamWriter(stream)) {writer.Write(JsonUtility.ToJson(data,true));writer.Flush();stream.Flush(true);}
            if(File.Exists(FilePath))File.Replace(temp,FilePath,FilePath+".bak");else File.Move(temp,FilePath);
        }
    }
    public sealed partial class DungeonGame
    {
        private DungeonRunData restoring;
        internal bool SaveAtFountain(DungeonFountain fountain)
        {
            if(player==null||player.IsDead||fountain==null||!fountain.IsNear(player))return false;
            try
            {
                var data=new DungeonRunData {runSeconds=RunSeconds,floor=FloorIndex,runSeed=runSeed,legacyLayout=legacyLayout,seed=CurrentSeed,width=width,height=height,loopChance=loopChance,hero=SelectedHero.Id,
                    health=player.Health,mana=player.Mana,score=player.Score,position=player.transform.position,
                    roll=player.RollCooldownRemaining,block=player.BlockCooldownRemaining,
                    props=DungeonProps.Current?.Snapshot(),
                    relics=player.Relics.Items.Select(r=>r.Id).ToArray(),
                    cooldowns=SpellDefinition.All.Select(s=>player.Spells.Cooldown(s.Id)).ToArray(),
                    enemies=dungeon.GetComponentsInChildren<DungeonEnemy>().Where(e=>!e.IsDead).Select(e=>new EnemyCheckpoint{id=e.SaveId,health=e.Health,position=e.transform.position}).ToArray(),
                    chests=dungeon.GetComponentsInChildren<DungeonChest>().Select(c=>new ChestCheckpoint{position=c.transform.position,facing=c.Facing,
                        good=c.Offer?.Good.Id,bad=c.Offer?.Bad.Id,star=c.Offer?.Star??false,resolved=c.Offer?.Resolved??false,claimed=c.RewardClaimed}).ToArray()};
                DungeonRunSave.Write(data);return true;
            }
            catch(Exception error){Debug.LogError("Fountain save failed: "+error.Message);return false;}
        }
        private void LoadCheckpoint()
        {
            var data=DungeonRunSave.Load();if(data==null||generating)return;
            Time.timeScale=1;ClearDungeon();restoring=data;RunSeconds=Mathf.Max(0,data.runSeconds);
            width=data.width;height=data.height;loopChance=data.loopChance;CurrentSeed=data.seed;
            runSeed=data.version==1?data.seed:data.runSeed;legacyLayout=data.version==1||data.legacyLayout;FloorIndex=data.floor;
            SelectedHero=data.hero==DungeonHero.Second.Id?DungeonHero.Second:DungeonHero.First;
            menuOpen=false;generating=true;screen.ShowLoading(CurrentSeed);loading=StartCoroutine(GuardGeneration());
        }
        private void ApplyCheckpoint(DungeonRunData data)
        {
            DungeonProps.Current?.Restore(data.props);
            foreach(string id in data.relics)player.ReceiveRelic(DungeonRelics.All.First(r=>r.Id==id));
            player.RestoreCheckpoint(data.health,data.mana,data.score,data.position,data.roll,data.block);
            player.Spells.RestoreCooldowns(data.cooldowns);
            foreach(var enemy in dungeon.GetComponentsInChildren<DungeonEnemy>())
            {
                var saved=Array.Find(data.enemies,e=>e.id==enemy.SaveId);
                if(saved==null){enemy.gameObject.SetActive(false);Destroy(enemy.gameObject);}
                else enemy.RestoreCheckpoint(saved.health,saved.position);
            }
            var existing=dungeon.GetComponentsInChildren<DungeonChest>();
            foreach(var old in existing){old.gameObject.SetActive(false);Destroy(old.gameObject);}
            foreach(var saved in data.chests)
            {
                var obj=new GameObject("Saved chest",typeof(SpriteRenderer));obj.transform.SetParent(dungeon.transform);obj.transform.position=saved.position;
                var chest=obj.AddComponent<DungeonChest>();chest.Initialize(saved.facing);
                var visual=obj.GetComponent<SpriteRenderer>();visual.sharedMaterial=player.EffectMaterial;
                obj.AddComponent<DungeonDepthSort>().Initialize(visual,obj.transform,0,true);
                DungeonChestOffer offer=string.IsNullOrEmpty(saved.good)?null:new DungeonChestOffer(DungeonRelics.All.First(r=>r.Id==saved.good),DungeonRelics.All.First(r=>r.Id==saved.bad),saved.star,saved.resolved);
                chest.RestoreCheckpoint(player,offer,saved.claimed);
            }
        }
    }
}
