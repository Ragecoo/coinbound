using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace Coinbound
{
    [Serializable] public sealed class DungeonRunResult
    {
        public string id,name,finishedUtc,hero;
        public float seconds,score,health,mana,maxHealth,maxMana,damage,defense,speed,attackRate,critical,luck;
        public string[] relics;
        public static DungeonRunResult Capture(DungeonPlayer player,float seconds)
        {
            var s=player.Stats;
            return new DungeonRunResult {id=Guid.NewGuid().ToString("N"),finishedUtc=DateTime.UtcNow.ToString("o"),hero=player.Hero.DisplayName,
                seconds=seconds,score=player.Score,health=player.Health,mana=player.Mana,maxHealth=s.MaxHealth,maxMana=s.MaxMana,
                damage=s.Damage,defense=s.Defense,speed=s.MoveSpeed,attackRate=s.AttackRate,critical=s.CriticalRate,luck=s.Luck,
                relics=player.Relics.Items.Select(r=>r.Id).ToArray()};
        }
    }
    public static class DungeonLeaderboard
    {
        [Serializable] private sealed class Data {public List<DungeonRunResult> entries=new List<DungeonRunResult>();}
        public static string FilePath=>Path.Combine(Application.persistentDataPath,"leaderboard.json");
        public static List<DungeonRunResult> Read(string path=null)
        {
            path=path??FilePath;
            foreach(var source in new[]{path,path+".bak"})
                try {if(File.Exists(source)) {var data=JsonUtility.FromJson<Data>(File.ReadAllText(source));
                    if(data?.entries!=null)return data.entries.Where(r=>r!=null&&!string.IsNullOrEmpty(r.id)&&!string.IsNullOrEmpty(r.name)&&r.relics!=null)
                        .OrderByDescending(r=>r.score).ThenBy(r=>r.seconds).ToList();}}
                catch(Exception e){Debug.LogWarning("Leaderboard read: "+e.Message);}
            return new List<DungeonRunResult>();
        }
        public static void Save(DungeonRunResult result,string name,string path=null)
        {
            name=(name??"").Trim();
            if(name.Length==0||name.Length>24||name.Any(char.IsControl))throw new ArgumentException("Введите имя (1–24 символа)");
            path=path??FilePath;var entries=Read(path);
            if(entries.Any(r=>r.id==result.id))return;
            // Freeze the submitted snapshot; later UI edits cannot mutate this record.
            var copy=JsonUtility.FromJson<DungeonRunResult>(JsonUtility.ToJson(result));copy.name=name;entries.Add(copy);
            Write(entries,path);
        }
        public static bool Delete(string id,string path=null)
        {
            path=path??FilePath;var entries=Read(path);
            if(entries.RemoveAll(r=>r.id==id)==0)return false;
            Write(entries,path);
            // A recovered backup must not resurrect intentionally deleted results.
            File.Copy(path,path+".bak",true);
            return true;
        }
        private static void Write(List<DungeonRunResult> entries,string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp=path+".tmp";
            using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
            using(var writer=new StreamWriter(stream)){writer.Write(JsonUtility.ToJson(new Data{entries=entries},true));writer.Flush();stream.Flush(true);}
            if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
        }
        public static string Duration(float seconds)
        {var t=TimeSpan.FromSeconds(Math.Max(0,seconds));return ((int)t.TotalHours).ToString("00")+":"+t.Minutes.ToString("00")+":"+t.Seconds.ToString("00");}
    }
}
