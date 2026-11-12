using UnityEngine;
using UnityEngine.UI;
namespace Coinbound
{
    public sealed partial class DungeonScreen
    {
        private GameObject victory;private Text victoryScore;private Button victoryButton;
        public void ShowVictory(float score, DungeonRunResult result=null)
        {
            if(victory==null)
            {
                victory=Layer("Victory",new Color32(15,15,26,250));
                Decor("Victory coin",victory.transform,new Vector2(0,190),new Vector2(60,64),DungeonUiDecoration.Kind.Coin,Gold);
                Label("Victory title",victory.transform,"ЗАБЕГ ЗАВЕРШЁН",36,new Vector2(0,95),new Vector2(1000,64),Pale,true);
                Rule(victory.transform,40,650);
                victoryScore=Label("Final score",victory.transform,"",24,new Vector2(0,-35),new Vector2(900,100),Gold,true);
                victoryButton=MakeButton("Victory title menu",victory.transform,"Главный экран",new Vector2(0,-280),new Vector2(350,48),titleAction);
            }
            DungeonEventAudio.Play(DungeonEventAudio.Cue.Victory);
            PrepareResultEntry(result);
            victoryScore.text="Очки: "+Mathf.FloorToInt(score).ToString("D9");
            Switch(victory,victoryButton);
        }
    }
}
