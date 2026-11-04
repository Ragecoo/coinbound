#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Coinbound
{
    /// <summary>Explicit development-only visual smoke check; never runs in ordinary play.</summary>
    internal sealed class DungeonSpellCapture : MonoBehaviour
    {
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-coinbound-spell-capture");
            if(flag<0 || flag+1>=args.Length)return;
            var capture=new GameObject("Spell visual check").AddComponent<DungeonSpellCapture>();
            capture.output=Path.GetFullPath(args[flag+1]);
        }
        private IEnumerator Start()
        {
            Application.runInBackground=true;Directory.CreateDirectory(output);
            Screen.SetResolution(1280,720,FullScreenMode.Windowed);
            var game=FindAnyObjectByType<DungeonGame>();
            JsonUtility.FromJsonOverwrite("{\"randomSeed\":false,\"seed\":42,\"minimumStatusSeconds\":0.02}",game);
            game.StartNewDungeon();
            float deadline=Time.realtimeSinceStartup+45;
            while(!game.IsReady && Time.realtimeSinceStartup<deadline)yield return null;
            if(!game.IsReady){Debug.LogError("Spell capture generation timed out");Application.Quit(1);yield break;}
            DungeonPlayer player=FindAnyObjectByType<DungeonPlayer>();
            bool found=false;
            foreach(DungeonEnemy enemy in DungeonEnemy.Active)
            {
                for(int i=0;i<8;i++)
                {
                    float angle=i*Mathf.PI/4;
                    Vector2 point=(Vector2)enemy.transform.position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*2.5f;
                    if(!DungeonCombatPhysics.FreeFeet(point,.35f) || !DungeonCombatPhysics.ClearLine(point,enemy.transform.position))continue;
                    player.GetComponent<Rigidbody2D>().position=point;Physics2D.SyncTransforms();found=true;break;
                }
                if(found)break;
            }
            yield return new WaitForSeconds(.3f);
            player.Spells.TryCast(DungeonSpell.Decoy);player.Spells.TryCast(DungeonSpell.Ward);
            player.Spells.Recognized(DungeonSpell.Trace);player.Spells.TryCast(DungeonSpell.Pulse);
            yield return new WaitForSeconds(.12f);yield return Capture("01-spells");
            yield return new WaitForSeconds(.25f);yield return Capture("02-trace-ward-decoy");
            player.Spells.Recognized(DungeonSpell.Ward);
            yield return new WaitForSeconds(.25f);yield return Capture("03-failed-word");
            game.ShowMenu();FindAnyObjectByType<DungeonScreen>().ShowSettings();
            yield return new WaitForSecondsRealtime(.2f);yield return Capture("04-settings");
            FindAnyObjectByType<DungeonVoiceInput>().ToggleDebug();
            var voiceDebug=FindAnyObjectByType<DungeonVoiceDebug>();
            for(int i=0;i<12;i++)voiceDebug.Record("RU","проверка текста пульс трейс вард декой");
            voiceDebug.Record("EN","hello pulse trace ward decoy testing voice commands");
            yield return Capture("05-voice-debug");
            Application.Quit(0);
        }
        private IEnumerator Capture(string name)
        {
            Camera camera=Camera.main;
            RenderTexture previous=camera.targetTexture,active=RenderTexture.active;
            var target=new RenderTexture(Screen.width,Screen.height,24,RenderTextureFormat.ARGB32){filterMode=FilterMode.Point};
            var pixels=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            Canvas[] canvases=FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var modes=new RenderMode[canvases.Length];var cameras=new Camera[canvases.Length];
            var sorting=new int[canvases.Length];var overrides=new bool[canvases.Length];var distances=new float[canvases.Length];
            try
            {
                target.Create();camera.targetTexture=target;
                for(int i=0;i<canvases.Length;i++)
                {
                    Canvas canvas=canvases[i];modes[i]=canvas.renderMode;cameras[i]=canvas.worldCamera;
                    sorting[i]=canvas.sortingOrder;overrides[i]=canvas.overrideSorting;distances[i]=canvas.planeDistance;
                    if(canvas.renderMode!=RenderMode.ScreenSpaceOverlay)continue;
                    canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                    canvas.overrideSorting=true;canvas.sortingOrder=30000+sorting[i];
                }
                yield return null;
                foreach(Canvas canvas in canvases)if(canvas!=null)foreach(Graphic graphic in canvas.GetComponentsInChildren<Graphic>())graphic.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);pixels.Apply();
                File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=previous;RenderTexture.active=active;
                for(int i=0;i<canvases.Length;i++)if(canvases[i]!=null)
                {
                    canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=distances[i];
                    canvases[i].overrideSorting=overrides[i];canvases[i].sortingOrder=sorting[i];
                }
                target.Release();Destroy(target);Destroy(pixels);
            }
        }
    }
}
#endif
