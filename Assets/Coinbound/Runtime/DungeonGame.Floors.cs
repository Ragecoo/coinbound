using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ProceduralGeneration;
namespace Coinbound
{
    public sealed partial class DungeonGame
    {
        private sealed class PreparedFloor
        {public DungeonView view;public DungeonPlayer player;public DungeonMap map;public SpawnPlan spawns;public int seed;public DungeonExit exit;}
        private readonly PreparedFloor[] floors=new PreparedFloor[3];
        private int runSeed;
        public float RunSeconds {get;private set;}
        private bool legacyLayout;
        public int FloorIndex {get;private set;}
        public bool RunCompleted {get;private set;}
        public int PreparedFloorCount=>floors.Count(f=>f!=null);
        public static int RequiredKills(int enemies)=>Mathf.CeilToInt(enemies*.75f);
        public Vector2Int FloorSize(int index)
        {
            float scale=Mathf.Sqrt((legacyLayout?1f:.5f)*Mathf.Pow(1.3f,index));
            return new Vector2Int(Mathf.Max(11,Mathf.RoundToInt((width*scale-1)/2)*2+1),Mathf.Max(11,Mathf.RoundToInt((height*scale-1)/2)*2+1));
        }
        private IEnumerator<object> GenerateDungeon()
        {
            int destination=restoring!=null?restoring.floor:0;
            for(int i=0;i<3;i++)
            {
                CurrentSeed=unchecked(runSeed+i*104729);
                using(var work=GenerateFloor(i))while(work.MoveNext())yield return work.Current;
                var exitVisual=dungeon.GetComponentsInChildren<SpriteRenderer>().First(v=>v.name=="object.exit.open");
                var exit=exitVisual.gameObject.AddComponent<DungeonExit>();
                exit.Initialize(this,dungeon,player,Spawns.Enemies.Count,i+1);
                floors[i]=new PreparedFloor{view=dungeon,player=player,map=Map,spawns=Spawns,seed=CurrentSeed,exit=exit};
                dungeon.gameObject.SetActive(false);
            }
            ActivateFloor(destination);
            if(restoring!=null){ApplyCheckpoint(restoring);restoring=null;}
            floors[destination].exit.RefreshBarrier();
            screen.SetProgress(1,"Подземелье готово");
            yield return null;
        }
        private void ActivateFloor(int index)
        {
            FloorIndex=index;var floor=floors[index];dungeon=floor.view;player=floor.player;Map=floor.map;Spawns=floor.spawns;CurrentSeed=floor.seed;
            dungeon.gameObject.SetActive(true);Physics2D.SyncTransforms();
            voiceInput.Bind(player.Spells);gameplayCursor.Bind(player);screen.BindPlayer(player,()=>dungeon.AliveEnemyCount);
        }
        public bool TryAscend(DungeonExit exit)
        {
            if(!IsReady||menuOpen||exit==null||floors[FloorIndex].exit!=exit||!exit.Unlocked||!exit.IsNear(player))return false;
            if(FloorIndex==2)return CompleteRun();
            return TravelToFloor(FloorIndex+1);
        }
        private bool CompleteRun()
        {
            RunCompleted=true;player.SetInputEnabled(false);menuOpen=true;Time.timeScale=0;
            foreach(string path in new[]{DungeonRunSave.FilePath,DungeonRunSave.FilePath+".bak"})if(System.IO.File.Exists(path))System.IO.File.Delete(path);
            screen.ShowVictory(player.Score,DungeonRunResult.Capture(player,RunSeconds));return true;
        }
        public bool DebugTravelFloor(int index)
        {
            if(!IsReady||menuOpen||!player.CanAct||screen.IsLootOpen||screen.IsModalOpen||index<0||index>3||index==FloorIndex)return false;
            if(index==3)return FloorIndex==2&&CompleteRun();
            return TravelToFloor(index);
        }
        private bool TravelToFloor(int index)
        {
            DungeonEventAudio.Play(DungeonEventAudio.Cue.Travel);
            var previous=player;
            float hp=previous.Health,mp=previous.Mana,score=previous.Score;
            var relics=previous.Relics.Items.ToArray();
            var cooldowns=SpellDefinition.All.Select(s=>previous.Spells.Cooldown(s.Id)).ToArray();
            previous.SetInputEnabled(false);dungeon.gameObject.SetActive(false);
            ActivateFloor(index);
            player.Relics.Clear();
            foreach(var relic in relics)player.ReceiveRelic(relic);
            player.RestoreCheckpoint(hp,mp,score,player.transform.position,previous.RollCooldownRemaining,previous.BlockCooldownRemaining);
            player.Spells.RestoreCooldowns(cooldowns);
            screen.BindPlayer(player,()=>dungeon.AliveEnemyCount);
            screen.ShowGame(CurrentSeed,Spawns.Enemies.Count,Spawns.Chests.Count);
            StartCoroutine(ResumeAfterReveal());return true;
        }
    }
}
