using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace Coinbound
{
    public sealed partial class DungeonScreen
    {
        private GameObject leaderboard;
        private InputField resultName;
        private Button saveResult;
        private Text resultMessage,detailsText;
        private DungeonRunResult pendingResult;
        private RectTransform resultRows,resultItems;
        private void PrepareResultEntry(DungeonRunResult result)
        {
            if(resultName==null)
            {
                var box=Framed("Run name",victory.transform,new Vector2(0,-135),new Vector2(420,46));
                resultName=box.gameObject.AddComponent<InputField>();
                var text=Label("Name text",box,"",18,Vector2.zero,new Vector2(390,40),Pale);
                text.supportRichText=false;resultName.textComponent=text;resultName.characterLimit=24;
                resultName.lineType=InputField.LineType.SingleLine;
                resultName.placeholder=Label("Name placeholder",box,"Имя героя",18,Vector2.zero,new Vector2(390,40),Muted);
                resultName.targetGraphic=box.GetComponent<Image>();resultName.targetGraphic.raycastTarget=true;
                saveResult=MakeButton("Save result",victory.transform,"Записать результат",new Vector2(0,-195),new Vector2(420,44),SaveResult);
                resultMessage=Label("Result message",victory.transform,"",14,new Vector2(0,-238),new Vector2(900,32),Muted);
            }
            pendingResult=result;resultName.gameObject.SetActive(result!=null);saveResult.gameObject.SetActive(result!=null);
            resultName.interactable=true;saveResult.interactable=true;resultName.text=PlayerPrefs.GetString("LeaderboardName","");
            resultMessage.text=result==null?"":"Время: "+DungeonLeaderboard.Duration(result.seconds);
        }
        private void SaveResult()
        {
            if(pendingResult==null)return;
            try
            {
                DungeonLeaderboard.Save(pendingResult,resultName.text);
                DungeonEventAudio.Play(DungeonEventAudio.Cue.Saved);
                PlayerPrefs.SetString("LeaderboardName",resultName.text.Trim());PlayerPrefs.Save();
                resultMessage.text="Результат сохранён";saveResult.interactable=false;resultName.interactable=false;pendingResult=null;
            }
            catch(ArgumentException e){resultMessage.text=e.Message;DungeonEventAudio.Play(DungeonEventAudio.Cue.Denied);}
            catch(Exception e){resultMessage.text="Не удалось сохранить. Попробуйте ещё раз.";Debug.LogWarning(e);}
        }
        private RectTransform ResultsScroll(string name,Transform parent,Vector2 pos,Vector2 size)
        {
            var viewport=Rect(name,parent,pos,size);
            var hit=viewport.gameObject.AddComponent<Image>();hit.color=Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content=Rect("Content",viewport,Vector2.zero,size);content.anchorMin=content.anchorMax=new Vector2(.5f,1);content.pivot=new Vector2(.5f,1);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;
            scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=48;
            return content;
        }
        private Text ResultText(string name,Transform parent,string value,Vector2 pos,Vector2 size,Color color)
        {var t=Label(name,parent,value,18,pos,size,color);t.fontSize=18;t.supportRichText=false;return t;}
        public void ShowLeaderboard()=>ShowLeaderboard(DungeonLeaderboard.Read());
        public void ShowLeaderboard(System.Collections.Generic.List<DungeonRunResult> entries)
        {
            if(leaderboard==null)
            {
                leaderboard=Layer("Leaderboard",Ink);
                Label("Results title",leaderboard.transform,"РЕЗУЛЬТАТЫ",36,new Vector2(0,300),new Vector2(1000,64),Pale,true);
                Rule(leaderboard.transform,255,1150);
                ResultText("Name column",leaderboard.transform,"ИМЯ",new Vector2(-460,220),new Vector2(210,30),Gold);
                ResultText("Date column",leaderboard.transform,"ДАТА",new Vector2(-275,220),new Vector2(175,30),Gold);
                ResultText("Time column",leaderboard.transform,"ВРЕМЯ",new Vector2(-115,220),new Vector2(130,30),Gold);
                ResultText("Score column",leaderboard.transform,"ОЧКИ",new Vector2(40,220),new Vector2(150,30),Gold);
                resultRows=ResultsScroll("Results list",leaderboard.transform,new Vector2(-220,-20),new Vector2(720,440));
                var panel=Framed("Run details",leaderboard.transform,new Vector2(380,-5),new Vector2(420,500));
                detailsText=ResultText("Final stats",panel,"Наведите на результат",new Vector2(0,110),new Vector2(380,250),Pale);
                detailsText.lineSpacing=.8f;
                resultItems=ResultsScroll("Final relics",panel,new Vector2(0,-140),new Vector2(384,190));
                TextButton("Results back",leaderboard.transform,"НАЗАД",new Vector2(0,-310),new Vector2(250,40),titleAction);
            }
            foreach(Transform child in resultRows)Destroy(child.gameObject);
            foreach(Transform child in resultItems)Destroy(child.gameObject);
            detailsText.text="Наведите на результат";
            resultRows.sizeDelta=new Vector2(720,Mathf.Max(440,entries.Count*58));resultRows.anchoredPosition=Vector2.zero;
            if(entries.Count==0)ResultText("No results",resultRows,"Пока нет завершённых забегов",new Vector2(0,-80),new Vector2(700,50),Muted);
            for(int i=0;i<entries.Count;i++)
            {
                var entry=entries[i];var row=Rect("Result row",resultRows,new Vector2(0,-29-i*58),new Vector2(710,54));row.anchorMin=row.anchorMax=new Vector2(.5f,1);
                var bg=row.gameObject.AddComponent<Image>();bg.color=i%2==0?Panel:new Color(0,0,0,0);
                string date=DateTime.TryParse(entry.finishedUtc,null,System.Globalization.DateTimeStyles.RoundtripKind,out var stamp)?stamp.ToLocalTime().ToString("dd.MM.yy HH:mm"):"—";
                var nameText=ResultText("Player name",row,entry.name,new Vector2(-240,0),new Vector2(210,50),Pale);
                if(entry.name.Length>14)((DungeonPixelText)nameText).ExtraPixelSteps=0;
                ResultText("Date",row,date,new Vector2(-55,0),new Vector2(175,50),Muted);
                ResultText("Duration",row,DungeonLeaderboard.Duration(entry.seconds),new Vector2(105,0),new Vector2(130,50),Pale);
                ResultText("Score",row,Mathf.FloorToInt(entry.score).ToString(),new Vector2(260,0),new Vector2(150,50),Gold);
                var hover=row.gameObject.AddComponent<DungeonResultHover>();hover.enter=()=>ShowResultDetails(entry);hover.remove=()=>RequestDeleteResult(entry);
                var button=row.gameObject.AddComponent<Button>();button.targetGraphic=bg;button.onClick.AddListener(()=>ShowResultDetails(entry));
            }
            DungeonEventAudio.Play(DungeonEventAudio.Cue.Page);
            Switch(leaderboard,null);
        }
        private void RequestDeleteResult(DungeonRunResult entry)
        {
            if(current!=leaderboard||IsModalOpen)return;
            Confirm("Вы уверены?","Удалить этот результат?",()=>
            {
                try
                {
                    DungeonLeaderboard.Delete(entry.id);
                    DungeonEventAudio.Play(DungeonEventAudio.Cue.Saved);
                    ShowLeaderboard();
                }
                catch(Exception error)
                {
                    Debug.LogWarning("Result deletion failed: "+error.Message);
                    detailsText.text="Не удалось удалить результат. Попробуйте ещё раз.";
                    DungeonEventAudio.Play(DungeonEventAudio.Cue.Denied);
                }
            },"Удалить");
        }
        private void ShowResultDetails(DungeonRunResult entry)
        {
            detailsText.text=entry.name+" · "+entry.hero+"\n"+
                $"Здоровье {entry.health:0.#} / {entry.maxHealth:0.#}\nМана {entry.mana:0.#} / {entry.maxMana:0.#}\n"+
                $"Урон {entry.damage:0.##}    Защита {entry.defense:0.##}\nСкорость {entry.speed:0.##}    Атак/с {entry.attackRate:0.##}\n"+
                $"Крит {entry.critical*100:0.#}%    Удача {entry.luck:0.##}\nАртефакты: {entry.relics.Length}";
            foreach(Transform child in resultItems)Destroy(child.gameObject);
            resultItems.sizeDelta=new Vector2(384,Mathf.Max(190,Mathf.Ceil(entry.relics.Length/6f)*64));resultItems.anchoredPosition=Vector2.zero;
            for(int i=0;i<entry.relics.Length;i++)
            {
                var relic=DungeonRelics.All.FirstOrDefault(r=>r.Id==entry.relics[i]);if(relic==null)continue;
                var icon=Raw(relic.Name,resultItems,new Vector2(-160+(i%6)*64,-32-(i/6)*64),new Vector2(56,56));
                icon.rectTransform.anchorMin=icon.rectTransform.anchorMax=new Vector2(.5f,1);icon.texture=relic.Icon;
            }
        }
    }
    public sealed class DungeonResultHover : MonoBehaviour,IPointerEnterHandler,ISelectHandler,IPointerClickHandler
    {
        public Action enter,remove;
        public void OnPointerClick(PointerEventData data)
        {if(data.button==PointerEventData.InputButton.Right)remove?.Invoke();}
        public void OnPointerEnter(PointerEventData data)=>enter?.Invoke();
        public void OnSelect(BaseEventData data)=>enter?.Invoke();
    }
}
