using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
namespace Coinbound
{
    public sealed partial class DungeonScreen
    {
        private GameObject debugRelics;
        private readonly RawImage[] debugIcons=new RawImage[8];
        private readonly Text[] debugNames=new Text[8],debugEffects=new Text[8];
        private Text debugPageLabel,debugGrantLabel;
        private int debugPage;
        private float debugPreviousTime;
        private bool debugPreviousInput;
        public bool IsDebugRelicsOpen=>debugRelics!=null&&debugRelics.activeSelf;
        private void UpdateDebugRelics()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(Keyboard.current?.f3Key.wasPressedThisFrame==true)ToggleDebugRelics();
#endif
        }
        public void ToggleDebugRelics()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(IsDebugRelicsOpen){CloseDebugRelics();return;}
            if(gameplayPlayer==null||gameplayPlayer.IsDead||current!=hud||IsLootOpen||IsModalOpen)return;
            if(debugRelics==null)BuildDebugRelics();
            debugPreviousTime=Time.timeScale;debugPreviousInput=gameplayPlayer.CanAct;
            gameplayPlayer.SetInputEnabled(false);Time.timeScale=0;
            debugRelics.SetActive(true);debugGrantLabel.text="";RefreshDebugRelics();Select(null);
#endif
        }
        private void BuildDebugRelics()
        {
            debugRelics=Layer("Debug relic menu",new Color(.035f,.035f,.06f,.97f));
            RelicText("Debug title",debugRelics.transform,"АРТЕФАКТЫ · DEBUG",32,new Vector2(0,300),new Vector2(900,46),Pale);
            TextButton("Close debug relics",debugRelics.transform,"F3 / ESC",new Vector2(510,300),new Vector2(180,40),CloseDebugRelics);
            for(int i=0;i<8;i++)
            {
                int slot=i;
                var card=MakeButton("Grant relic "+i,debugRelics.transform,"",new Vector2(-396+i%4*264,140-i/4*238),new Vector2(252,224),()=>GrantDebugRelic(debugPage*8+slot));
                debugIcons[i]=Raw("Debug icon",card.transform,new Vector2(0,72),new Vector2(48,48));
                debugNames[i]=RelicText("Debug name",card.transform,"",16,new Vector2(0,23),new Vector2(232,44),Pale);
                debugEffects[i]=RelicText("Debug effects",card.transform,"",16,new Vector2(0,-54),new Vector2(232,102),Muted);
            }
            TextButton("Debug previous",debugRelics.transform,"НАЗАД",new Vector2(-220,-282),new Vector2(180,40),()=>{debugPage=(debugPage+3)%4;RefreshDebugRelics();});
            TextButton("Debug next",debugRelics.transform,"ДАЛЕЕ",new Vector2(220,-282),new Vector2(180,40),()=>{debugPage=(debugPage+1)%4;RefreshDebugRelics();});
            debugPageLabel=RelicText("Debug page",debugRelics.transform,"",24,new Vector2(0,-282),new Vector2(220,40),Pale);
            debugGrantLabel=RelicText("Debug granted",debugRelics.transform,"",16,new Vector2(0,-326),new Vector2(950,28),Muted);
        }
        private void RefreshDebugRelics()
        {
            debugPageLabel.text=(debugPage+1)+" / 4";
            for(int i=0;i<8;i++)
            {
                var item=DungeonRelics.All[debugPage*8+i];debugIcons[i].texture=item.HudIcon;
                debugNames[i].text=item.Name;debugNames[i].color=item.Positive?new Color(.55f,.8f,.62f):new Color(.85f,.45f,.52f);
                debugEffects[i].text=item.Effects;
            }
        }
        public void GrantDebugRelic(int index)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(!IsDebugRelicsOpen||index<0||index>=DungeonRelics.All.Count)return;
            var item=DungeonRelics.All[index];gameplayPlayer.ReceiveRelic(item);
            debugGrantLabel.text="Выдан: "+item.Name+" · Всего предметов: "+gameplayPlayer.Relics.Items.Count;
#endif
        }
        private void CloseDebugRelics()
        {
            if(!IsDebugRelicsOpen)return;
            debugRelics.SetActive(false);Time.timeScale=debugPreviousTime;
            if(gameplayPlayer!=null)gameplayPlayer.SetInputEnabled(debugPreviousInput);Select(null);
        }
    }
}
