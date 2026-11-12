using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Coinbound.Tests
{
    public sealed class DungeonCheckpointTests
    {
        private byte[] original,backup;
        [SetUp] public void PreserveSave()
        {
            original=File.Exists(DungeonRunSave.FilePath)?File.ReadAllBytes(DungeonRunSave.FilePath):null;
            backup=File.Exists(DungeonRunSave.FilePath+".bak")?File.ReadAllBytes(DungeonRunSave.FilePath+".bak"):null;
        }
        [TearDown] public void RestoreSave()
        {
            foreach(var pair in new[]{(DungeonRunSave.FilePath,original),(DungeonRunSave.FilePath+".bak",backup)})
                if(pair.Item2==null){if(File.Exists(pair.Item1))File.Delete(pair.Item1);}else File.WriteAllBytes(pair.Item1,pair.Item2);
            Time.timeScale=1;
        }
        [UnityTearDown] public IEnumerator CleanupScene()
        {
            var game=Object.FindAnyObjectByType<DungeonGame>();if(game!=null)Object.Destroy(game.gameObject);
            if(Camera.main!=null)Object.Destroy(Camera.main.gameObject);
            yield return null;
        }
        [UnityTest] public IEnumerator FountainThrowPersistsAndRestoresRunAcrossSceneReload()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            var game=Object.FindAnyObjectByType<DungeonGame>();
            JsonUtility.FromJsonOverwrite("{\"randomSeed\":false,\"seed\":42,\"minimumStatusSeconds\":0}",game);
            game.StartNewDungeon();
            float deadline=Time.realtimeSinceStartup+40;
            while(!game.IsReady&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(game.IsReady,Is.True);
            var player=Object.FindAnyObjectByType<DungeonPlayer>();
            var fountain=Object.FindAnyObjectByType<DungeonFountain>();
            var screen=Object.FindAnyObjectByType<DungeonScreen>();
            var bounds=fountain.GetComponent<Collider2D>().bounds;
            player.transform.position=new Vector2(bounds.center.x,bounds.min.y-.4f);player.GetComponent<Rigidbody2D>().position=player.transform.position;Physics2D.SyncTransforms();
            player.ReceiveRelic(DungeonRelics.All[0]);player.ReceiveRelic(DungeonRelics.All[0]);player.AwardScore(123);player.TrySpendMana(37);
            var spentChest=Object.FindAnyObjectByType<DungeonChest>();
            var offer=new DungeonChestOffer(player.Stats,new System.Random(12));offer.Resolve();
            typeof(DungeonChest).GetMethod("RestoreCheckpoint",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(spentChest,new object[]{player,offer,true});
            var brokenProp=DungeonProps.Current.Items.FirstOrDefault(p=>p.Definition.hp>0);
            Assert.That(brokenProp,Is.Not.Null);
            Assert.That(brokenProp.ReceiveHit(999,DungeonProps.NewAttackId()),Is.True);
            int brokenId=brokenProp.Id;
            var killed=DungeonEnemy.Active.First();killed.ReceiveHit(1000,Vector2.up,0);
            yield return new WaitForSeconds(.5f);
            Assert.That(DungeonFountain.TryUse(player),Is.True);
            screen.BeginCoinToss();
            deadline=Time.realtimeSinceStartup+8;
            while(screen.IsLootOpen&&Time.realtimeSinceStartup<deadline)yield return null;
            var saved=DungeonRunSave.Load();Assert.That(saved,Is.Not.Null);Assert.That(saved.relics.Length,Is.EqualTo(2));
            byte[] savedBytes=File.ReadAllBytes(DungeonRunSave.FilePath);
            player.AwardScore(999);player.ReceiveRelic(DungeonRelics.All[1]);
            game.ShowMenu();game.Resume();
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(player.Score,Is.EqualTo(saved.score+999),"Pause must preserve live progress.");
            game.ReturnToTitle();
            Assert.That(game.IsReady,Is.False,"Returning to title must discard the live run.");
            Assert.That(File.ReadAllBytes(DungeonRunSave.FilePath),Is.EqualTo(savedBytes),"Leaving must not write a save.");
            game.Resume();
            deadline=Time.realtimeSinceStartup+40;
            while(!game.IsReady&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(game.IsReady,Is.True);
            player=Object.FindAnyObjectByType<DungeonPlayer>();
            Assert.That(player.Score,Is.EqualTo(saved.score),"Title Continue must load the fountain, not paused state.");
            Assert.That(player.Relics.Items.Count,Is.EqualTo(2));
            player.AwardScore(999); // Unsaved progress must not overwrite the fountain checkpoint.
            yield return SceneManager.LoadSceneAsync("SampleScene");
            game=Object.FindAnyObjectByType<DungeonGame>();game.Resume();
            deadline=Time.realtimeSinceStartup+40;
            while(!game.IsReady&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(game.IsReady,Is.True);
            player=Object.FindAnyObjectByType<DungeonPlayer>();
            Assert.That(player.Score,Is.EqualTo(saved.score));Assert.That(player.Health,Is.EqualTo(saved.health));
            Assert.That(DungeonProps.Current.Items.Single(p=>p.Id==brokenId).Destroyed,Is.True);
            Assert.That(DungeonProps.Current.Items.Count,Is.EqualTo(saved.props.Length));
            Assert.That(player.Relics.Items.Count,Is.EqualTo(2));Assert.That(Vector2.Distance(player.transform.position,saved.position),Is.LessThan(.05f));
            Assert.That(DungeonEnemy.Active.Count(e=>!e.IsDead),Is.EqualTo(saved.enemies.Length));
            var chests=Object.FindObjectsByType<DungeonChest>(FindObjectsSortMode.None);
            Assert.That(chests.Length,Is.EqualTo(saved.chests.Length));
            Assert.That(chests.Count(c=>c.RewardClaimed),Is.EqualTo(1));
            Assert.That(chests.Single(c=>c.RewardClaimed).Offer.Result.Id,Is.EqualTo(offer.Result.Id));
        }
        [UnityTest] public IEnumerator LeavingWithoutFountainHasNothingToContinue()
        {
            if(File.Exists(DungeonRunSave.FilePath))File.Delete(DungeonRunSave.FilePath);
            if(File.Exists(DungeonRunSave.FilePath+".bak"))File.Delete(DungeonRunSave.FilePath+".bak");
            yield return SceneManager.LoadSceneAsync("SampleScene");
            var game=Object.FindAnyObjectByType<DungeonGame>();
            JsonUtility.FromJsonOverwrite("{\"randomSeed\":false,\"minimumStatusSeconds\":0}",game);
            game.StartNewDungeon();
            float deadline=Time.realtimeSinceStartup+40;
            while(!game.IsReady&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(game.IsReady,Is.True);
            game.ReturnToTitle();yield return null;
            Assert.That(game.IsReady,Is.False);Assert.That(DungeonRunSave.Load(),Is.Null);
            Assert.That(GameObject.Find("Resume"),Is.Null);
            game.Resume();yield return null;
            Assert.That(game.IsReady,Is.False);Assert.That(File.Exists(DungeonRunSave.FilePath),Is.False);
        }
        private static void MovePlayer(DungeonPlayer player,Vector2 point)
        {player.transform.position=point;player.GetComponent<Rigidbody2D>().position=point;Physics2D.SyncTransforms();}
        [UnityTest] public IEnumerator ThreeFloorsPreloadUnlockRestoreAndFinish()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            var game=Object.FindAnyObjectByType<DungeonGame>();
            JsonUtility.FromJsonOverwrite("{\"randomSeed\":false,\"seed\":42,\"minimumStatusSeconds\":0}",game);game.StartNewDungeon();
            float deadline=Time.realtimeSinceStartup+90;while(!game.IsReady&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(game.IsReady,Is.True);Assert.That(game.PreparedFloorCount,Is.EqualTo(3));
            var views=game.GetComponentsInChildren<DungeonView>(true);Assert.That(views.Length,Is.EqualTo(3));
            Assert.That(views.Count(v=>v.gameObject.activeInHierarchy),Is.EqualTo(1));
            float area=game.FloorSize(0).x*game.FloorSize(0).y;
            Assert.That(area/(39*19),Is.InRange(.45f,.55f));
            for(int i=1;i<3;i++){float next=game.FloorSize(i).x*game.FloorSize(i).y;Assert.That(next/area,Is.InRange(1.2f,1.4f));area=next;}
            for(int floor=0;floor<3;floor++)
            {
                var player=Object.FindAnyObjectByType<DungeonPlayer>();var exit=Object.FindAnyObjectByType<DungeonExit>();
                var enemies=Object.FindObjectsByType<DungeonEnemy>().Where(e=>!e.IsDead).OrderBy(e=>e.SaveId).ToArray();
                Assert.That(game.FloorIndex,Is.EqualTo(floor));Assert.That(exit.Unlocked,Is.False);
                foreach(var enemy in enemies)Assert.That(enemy.MaxHealth,Is.EqualTo(enemy.Definition.Stats.MaxHealth*Mathf.Pow(1.2f,floor)).Within(.001f));
                if(floor==0){player.ReceiveRelic(DungeonRelics.All[0]);player.AwardScore(123);}
                int required=DungeonGame.RequiredKills(enemies.Length);
                for(int i=0;i<required-1;i++)enemies[i].ReceiveHit(9999,Vector2.right,0,false);
                exit.RefreshBarrier();Assert.That(exit.Unlocked,Is.False);
                MovePlayer(player,(Vector2)exit.transform.position+Vector2.down*1.4f);Assert.That(game.TryAscend(exit),Is.False);
                enemies[required-1].ReceiveHit(9999,Vector2.right,0,false);exit.RefreshBarrier();Assert.That(exit.Unlocked,Is.True);
                Assert.That(player.Relics.Items.Count,Is.EqualTo(1));
                if(floor==1)
                {
                    var fountain=Object.FindAnyObjectByType<DungeonFountain>();var bounds=fountain.GetComponent<Collider2D>().bounds;
                    MovePlayer(player,new Vector2(bounds.center.x,bounds.min.y-.4f));
                    var save=typeof(DungeonGame).GetMethod("SaveAtFountain",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    Assert.That((bool)save.Invoke(game,new object[]{fountain}),Is.True);float savedScore=player.Score;
                    Assert.That(DungeonRunSave.Load().floor,Is.EqualTo(1));
                    game.ReturnToTitle();yield return null;game.Resume();deadline=Time.realtimeSinceStartup+90;while(!game.IsReady&&Time.realtimeSinceStartup<deadline)yield return null;
                    Assert.That(game.IsReady,Is.True);Assert.That(game.FloorIndex,Is.EqualTo(1));
                    player=Object.FindAnyObjectByType<DungeonPlayer>();exit=Object.FindAnyObjectByType<DungeonExit>();exit.RefreshBarrier();
                    Assert.That(exit.Unlocked,Is.True);Assert.That(player.Score,Is.EqualTo(savedScore));Assert.That(player.Relics.Items.Count,Is.EqualTo(1));
                }
                MovePlayer(player,(Vector2)exit.transform.position+Vector2.down*1.4f);float score=player.Score;
                Assert.That(game.TryAscend(exit),Is.True);
                if(floor<2){yield return new WaitForSecondsRealtime(.6f);Assert.That(Object.FindAnyObjectByType<DungeonPlayer>().Score,Is.EqualTo(score));}
            }
            Assert.That(game.RunCompleted,Is.True);Assert.That(DungeonRunSave.Load(),Is.Null);Assert.That(GameObject.Find("Victory"),Is.Not.Null);
        }
        [UnityTest] public IEnumerator DebugFloorTravelReturnsWithoutDuplicatingAndFinishes()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            var game=Object.FindAnyObjectByType<DungeonGame>();
            JsonUtility.FromJsonOverwrite("{\"randomSeed\":false,\"seed\":42,\"minimumStatusSeconds\":0}",game);game.StartNewDungeon();
            float deadline=Time.realtimeSinceStartup+90;while(!game.IsReady&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(game.IsReady);
            var player=Object.FindAnyObjectByType<DungeonPlayer>();player.ReceiveRelic(DungeonRelics.All[0]);player.AwardScore(123);
            foreach(int floor in new[]{1,0,1,2})
            {
                Assert.IsTrue(game.DebugTravelFloor(floor));yield return new WaitForSecondsRealtime(.6f);
                player=Object.FindAnyObjectByType<DungeonPlayer>();
                Assert.AreEqual(floor,game.FloorIndex);Assert.AreEqual(1,player.Relics.Items.Count);Assert.AreEqual(123,player.Score);
            }
            Assert.IsTrue(game.DebugTravelFloor(3));Assert.IsTrue(game.RunCompleted);
            Assert.IsNotNull(GameObject.Find("Run name"));Assert.IsNotNull(GameObject.Find("Save result"));
            Assert.IsNull(GameObject.Find("Floor number"));Assert.IsFalse(game.DebugTravelFloor(3));
        }
        [Test] public void LuckChangesCoinOutcomeWithoutRerolling()
        {
            var low=new DungeonRelicInventory();var high=new DungeonRelicInventory();
            for(int i=0;i<10;i++){low.Add(DungeonRelics.All[27],DungeonHero.First.Stats);high.Add(DungeonRelics.All[6],DungeonHero.First.Stats);}
            int a=0,b=0;
            for(int i=0;i<4000;i++)
            {
                var poor=new DungeonChestOffer(low.CurrentStats,new System.Random(i));var rich=new DungeonChestOffer(high.CurrentStats,new System.Random(i));
                if(poor.Resolve().Positive)a++;if(rich.Resolve().Positive)b++;
                Assert.That(rich.Resolve(),Is.SameAs(rich.Result));
            }
            Assert.That(a/4000f,Is.InRange(.37f,.43f));Assert.That(b/4000f,Is.InRange(.62f,.68f));
        }
    }
}
