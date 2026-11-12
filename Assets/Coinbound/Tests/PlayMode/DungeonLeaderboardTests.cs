using System;
using System.IO;
using NUnit.Framework;
namespace Coinbound.Tests
{
    public sealed class DungeonLeaderboardTests
    {
        private string path;
        [SetUp] public void Setup(){path=Path.Combine(Path.GetTempPath(),"coinbound-results-"+Guid.NewGuid()+".json");}
        [TearDown] public void Cleanup(){foreach(var p in new[]{path,path+".bak",path+".tmp"})if(File.Exists(p))File.Delete(p);}
        private DungeonRunResult Record(string id,float score)=>new DungeonRunResult{id=id,score=score,relics=new[]{"one","one"},seconds=125};
        [Test] public void PersistsSnapshotsAndDuplicateRelicsWithoutDuplicateSubmission()
        {
            var record=Record("a",100);DungeonLeaderboard.Save(record,"  Hero  ",path);
            record.score=999;record.relics[0]="changed";
            DungeonLeaderboard.Save(record,"Other",path);
            var rows=DungeonLeaderboard.Read(path);Assert.AreEqual(1,rows.Count);Assert.AreEqual("Hero",rows[0].name);
            Assert.AreEqual(100,rows[0].score);CollectionAssert.AreEqual(new[]{"one","one"},rows[0].relics);
        }
        [Test] public void OrdersByScoreThenDurationAndRecoversBackup()
        {
            DungeonLeaderboard.Save(Record("a",100),"A",path);DungeonLeaderboard.Save(Record("b",200),"B",path);
            Assert.AreEqual("b",DungeonLeaderboard.Read(path)[0].id);
            File.WriteAllText(path,"broken");Assert.AreEqual("a",DungeonLeaderboard.Read(path)[0].id);
        }
        [Test] public void DeletesOnlySelectedIdAndDoesNotRestoreItFromBackup()
        {
            DungeonLeaderboard.Save(Record("a",100),"Same name",path);
            DungeonLeaderboard.Save(Record("b",200),"Same name",path);
            Assert.IsTrue(DungeonLeaderboard.Delete("a",path));
            Assert.AreEqual("b",DungeonLeaderboard.Read(path)[0].id);Assert.AreEqual(1,DungeonLeaderboard.Read(path).Count);
            File.WriteAllText(path,"broken");Assert.AreEqual("b",DungeonLeaderboard.Read(path)[0].id);
            Assert.IsTrue(DungeonLeaderboard.Delete("b",path));Assert.IsEmpty(DungeonLeaderboard.Read(path));
            Assert.IsFalse(DungeonLeaderboard.Delete("b",path));
        }
        [Test] public void RejectsBlankNameAndFormatsLongRuns()
        {
            Assert.Throws<ArgumentException>(()=>DungeonLeaderboard.Save(Record("a",1),"  ",path));
            Assert.IsFalse(File.Exists(path));Assert.AreEqual("25:01:01",DungeonLeaderboard.Duration(90061));
        }
    }
}
