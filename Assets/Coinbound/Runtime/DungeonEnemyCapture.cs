#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using ProceduralGeneration;
namespace Coinbound
{
    /// <summary>Opt-in built-player encounter validation; never changes a normal run.</summary>
    internal sealed class DungeonEnemyCapture:MonoBehaviour
    {
        private string output;
        private DungeonPlayer player;
        private DungeonGame game;
        private readonly List<DungeonEnemy> staged=new List<DungeonEnemy>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-coinbound-enemy-capture");
            if(i<0||i+1>=args.Length)return;
            new GameObject("Enemy encounter capture").AddComponent<DungeonEnemyCapture>().output=Path.GetFullPath(args[i+1]);
        }
        private DungeonEnemy Spawn(DungeonEnemyKind kind,Vector2 position)
        {
            var obj=new GameObject("Staged "+kind);obj.transform.SetParent(player.transform.parent,false);obj.transform.position=position;
            var enemy=obj.AddComponent<DungeonEnemy>();enemy.Configure(kind,1);enemy.Initialize(player,game.Map);staged.Add(enemy);return enemy;
        }
        private IEnumerator Start()
        {
            Application.runInBackground=true;Directory.CreateDirectory(output);Screen.SetResolution(1280,720,FullScreenMode.Windowed);
            game=FindAnyObjectByType<DungeonGame>();JsonUtility.FromJsonOverwrite("{\"randomSeed\":false,\"seed\":42,\"minimumStatusSeconds\":0.02}",game);
            game.StartNewDungeon();float deadline=Time.realtimeSinceStartup+45;
            while(!game.IsReady&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!game.IsReady){Debug.LogError("Enemy capture generation failed");Application.Quit(1);yield break;}
            player=FindAnyObjectByType<DungeonPlayer>();
            foreach(var enemy in FindObjectsByType<DungeonEnemy>())enemy.gameObject.SetActive(false);
            var room=game.Map.Rooms.Where(r=>r.Kind!=RoomKind.Entrance).OrderByDescending(r=>r.Width*r.Height).First();
            Vector2 center= DungeonView.WorldPosition(game.Map.SectionCenter(room.Center));
            player.TeleportTo(center+Vector2.down*2);
            var tank=Spawn(DungeonEnemyKind.Warden,center);
            Spawn(DungeonEnemyKind.Arbalist,center+new Vector2(-1.2f,3));
            Spawn(DungeonEnemyKind.Arbalist,center+new Vector2(1.2f,3));
            Spawn(DungeonEnemyKind.Cultist,center+new Vector2(0,2));
            Spawn(DungeonEnemyKind.Ghoul,center+new Vector2(-3,1));
            Spawn(DungeonEnemyKind.Ghoul,center+new Vector2(3,1));
            Physics2D.SyncTransforms();yield return new WaitForSeconds(.2f);
            yield return DungeonSpellCapture.CaptureFrame(output,"01-group-approach");
            yield return new WaitForSeconds(.5f);yield return DungeonSpellCapture.CaptureFrame(output,"02-windups");
            yield return new WaitForSeconds(.6f);yield return DungeonSpellCapture.CaptureFrame(output,"03-attacks");
            player.Initialize(player.Hero);player.SetInputEnabled(true);player.TeleportTo(center+new Vector2(3,1));
            yield return new WaitForSeconds(.8f);yield return DungeonSpellCapture.CaptureFrame(output,"04-player-flanks-group");
            tank.ReceiveHit(1000,Vector2.right,0);
            yield return new WaitForSeconds(.4f);yield return DungeonSpellCapture.CaptureFrame(output,"05-tank-gone");
            foreach(var enemy in staged)if(enemy!=null)Debug.Log($"ENEMY_CAPTURE {enemy.Kind} hp={enemy.Health:F2} attacks={enemy.AttackSequence} phase={enemy.Phase} position={enemy.transform.position}");
            Application.Quit(0);
        }
    }
}
#endif
