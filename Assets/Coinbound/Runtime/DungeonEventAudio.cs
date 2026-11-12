using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Coinbound
{
    // Independent of floor/page activation: one-shots overlap and finish naturally.
    public sealed class DungeonEventAudio : MonoBehaviour
    {
        public enum Cue { Hover,Page,Travel,Barrier,Victory,Saved,Denied,Death }
        private static DungeonEventAudio instance;
        private AudioSource voice;
        private AudioClip[] clips;
        private float nextHover;
        public static void Play(Cue cue)
        {
            if(instance==null)
            {
                var obj=new GameObject("Interface and journey audio");DontDestroyOnLoad(obj);instance=obj.AddComponent<DungeonEventAudio>();
                instance.voice=obj.AddComponent<AudioSource>();instance.voice.playOnAwake=false;instance.voice.ignoreListenerPause=true;
                instance.voice.spatialBlend=0;instance.voice.volume=.6f;instance.clips=new AudioClip[8];
                for(int i=0;i<8;i++){var samples=Samples((Cue)i);var clip=AudioClip.Create("Journey "+(Cue)i,samples.Length,1,22050,false);clip.SetData(samples,0);instance.clips[i]=clip;}
            }
            if(cue==Cue.Hover){if(Time.unscaledTime<instance.nextHover)return;instance.nextHover=Time.unscaledTime+.12f;}
            instance.voice.PlayOneShot(instance.clips[(int)cue]);
        }
        public static float[] Samples(Cue cue)
        {
            const int rate=22050;
            float duration=cue==Cue.Hover?.12f:cue==Cue.Page?.35f:cue==Cue.Denied?.3f:cue==Cue.Travel?1.2f:cue==Cue.Saved?1f:2.2f;
            var data=new float[Mathf.CeilToInt(duration*rate)];var random=new System.Random(705+(int)cue);float low=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=i/(float)rate,value=0;
                low+=((float)random.NextDouble()*2-1-low)*.065f;
                if(cue==Cue.Hover||cue==Cue.Page||cue==Cue.Denied)
                {
                    float f=cue==Cue.Hover?660:cue==Cue.Page?330:146.83f;
                    value=(Mathf.Sin(2*Mathf.PI*f*t)+.2f*Mathf.Sin(2*Mathf.PI*f*2.01f*t))*Mathf.Exp(-t*22)*.055f;
                }
                else if(cue==Cue.Travel)
                    value=(low*.18f+Mathf.Sin(2*Mathf.PI*73.416f*t)*.035f)*Mathf.Sin(Mathf.PI*t/duration);
                else
                {
                    bool dark=cue==Cue.Death;
                    float root=dark?146.83f:cue==Cue.Barrier?220:293.66f;
                    for(int note=0;note<3;note++)
                    {
                        float age=t-note*.15f;if(age<0)continue;
                        float ratio=note==0?1:note==1?(dark?.75f:1.5f):(dark?.5f:2);
                        float f=root*ratio;
                        value+=(Mathf.Sin(2*Mathf.PI*f*age)+.16f*Mathf.Sin(2*Mathf.PI*f*2.003f*age))*
                            Mathf.Clamp01(age/.012f)*Mathf.Exp(-age*(cue==Cue.Saved?7:3.5f))*.065f;
                    }
                    if(cue==Cue.Barrier)value+=low*.10f*Mathf.Exp(-t*5);
                }
                float edge=Mathf.SmoothStep(0,1,t/.008f)*Mathf.SmoothStep(0,1,(data.Length-1-i)/(float)rate/.10f);
                data[i]=value*edge;
            }
            return data;
        }
        private void OnDestroy(){if(clips!=null)foreach(var clip in clips)Destroy(clip);if(instance==this)instance=null;}
    }
    public sealed class DungeonHoverSound : MonoBehaviour,IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData data)
        {if(GetComponent<Selectable>()?.IsInteractable()==true)DungeonEventAudio.Play(DungeonEventAudio.Cue.Hover);}
    }
}
