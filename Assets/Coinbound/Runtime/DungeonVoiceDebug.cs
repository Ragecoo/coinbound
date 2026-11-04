using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace Coinbound
{
    /// <summary>A bounded, wrapping transcript box. Old words leave as new words arrive.</summary>
    public sealed class DungeonVoiceDebug : MonoBehaviour
    {
        private DungeonVoiceInput source;
        private GameObject overlay;
        private DungeonPixelText heading, live, transcript;
        private readonly List<string> lines = new List<string>();
        private string english="", russian="", previousStatus="";
        private int previousWidth,previousHeight;
        private bool visible;
        private static readonly Regex Commands = new Regex(@"\b(pulse|trace|ward|decoy|пульс|трейс|трей\s+с|вард|декой|де\s+кой)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);

        public void Initialize(DungeonVoiceInput input)
        {
            source=input;
            overlay=new GameObject("Voice debug overlay",typeof(Canvas),typeof(CanvasScaler));
            overlay.transform.SetParent(transform,false);
            var canvas=overlay.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=190;canvas.pixelPerfect=true;
            var scaler=overlay.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var panel=new GameObject("Voice transcript box",typeof(RectTransform),typeof(Image),typeof(RectMask2D));
            var rect=panel.GetComponent<RectTransform>();rect.SetParent(overlay.transform,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,1);
            rect.anchoredPosition=new Vector2(-20,-76);rect.sizeDelta=new Vector2(600,330);
            var background=panel.GetComponent<Image>();background.color=new Color(.025f,.025f,.04f,.94f);background.raycastTarget=false;
            background.sprite=Resources.Load<Sprite>("InterfaceArt/Gui/panel");
            if(background.sprite!=null){background.type=Image.Type.Sliced;background.color=Color.white;panel.AddComponent<DungeonUiArtScale>();}
            heading=Text(panel.transform,"Heading",new Vector2(20,-16),new Vector2(560,28),16);
            live=Text(panel.transform,"Live words",new Vector2(20,-50),new Vector2(560,74),16);
            transcript=Text(panel.transform,"Recognized words",new Vector2(20,-132),new Vector2(560,178),24);
            overlay.SetActive(false);
        }
        private static DungeonPixelText Text(Transform parent,string name,Vector2 position,Vector2 size,int fontSize)
        {
            var obj=new GameObject(name,typeof(RectTransform));var rect=obj.GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=size;
            var text=obj.AddComponent<DungeonPixelText>();text.font=Resources.Load<Font>("InterfaceFonts/OpirusOpik");
            text.ExtraPixelSteps=0;text.fontSize=fontSize;text.alignment=TextAnchor.UpperLeft;text.raycastTarget=false;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;
            text.supportRichText=true;text.color=new Color(.9f,.88f,.81f);return text;
        }
        public void Show(bool value)
        {
            visible=value;overlay.SetActive(value);
            english=russian="";
            if(value)Refresh();
        }
        public void Record(string language,string words)
        {
            if(string.IsNullOrWhiteSpace(words))return;
            lines.Add(language+": "+words);
            if(visible)Refresh();
        }
        private static string Highlight(string words)
        {
            words=words.Replace("<","").Replace(">","");
            return Commands.Replace(words,m=>"<color=#78FF86>"+m.Value+"</color>");
        }
        private void Update()
        {
            bool dirty=false;
            while(source.TryDiagnostic(out VoiceDiagnostic result))
            {
                dirty=true;
                if(result.Final)
                {
                    Record(result.Language,result.Text);
                    if(result.Language=="EN")english="";else russian="";
                }
                else if(result.Language=="EN")english=result.Text;else russian=result.Text;
            }
            if(!visible)return;
            if(dirty || previousStatus!=DungeonVoiceInput.Status || previousWidth!=Screen.width || previousHeight!=Screen.height)Refresh();
        }
        private void Refresh()
        {
            previousStatus=DungeonVoiceInput.Status;previousWidth=Screen.width;previousHeight=Screen.height;
            heading.text="ГОЛОС · F1 — скрыть";
            if(previousStatus.StartsWith("Голос:"))live.text="EN: "+Highlight(Tail(english))+"\nRU: "+Highlight(Tail(russian));
            else live.text=previousStatus;
            RebuildTranscript();
            while(lines.Count>0 && transcript.MeasureHeight(transcript.rectTransform.rect.width)>transcript.rectTransform.rect.height-2)
            {
                if(lines.Count>1)lines.RemoveAt(0);
                else
                {
                    int space=lines[0].IndexOf(' ');
                    if(space<0){lines.Clear();break;}
                    lines[0]=lines[0].Substring(space+1);
                }
                RebuildTranscript();
            }
            RebuildTranscript();
        }
        private void RebuildTranscript()=>transcript.text=Highlight(string.Join("\n",lines));
        private static string Tail(string text)
        {
            if(text.Length<=56)return text;
            int space=text.IndexOf(' ',text.Length-56);
            return space>=0?text.Substring(space+1):text.Substring(text.Length-56);
        }
    }
}
