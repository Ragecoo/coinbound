using UnityEngine;
namespace Coinbound
{
    public sealed partial class DungeonScreen
    {
        private AudioClip fountainSplashSound;
        private AudioClip coinLaunchSound,coinSpinSound,relicBlessingSound,relicCurseSound,relicAcceptSound;
        private void BuildLootSounds()
        {
            fountainSplashSound=MakeLootSound("Coin water splash",.45f,5);
            coinLaunchSound=MakeLootSound("Coin flick",.22f,0);
            coinSpinSound=MakeLootSound("Coin ring",.09f,1);
            relicBlessingSound=MakeLootSound("Blessing chime",.95f,2);
            relicCurseSound=MakeLootSound("Curse resonance",.95f,3);
            relicAcceptSound=MakeLootSound("Relic accepted",.28f,4);
        }
        private static AudioClip MakeLootSound(string name,float duration,int kind)
        {
            const int rate=22050;var samples=new float[Mathf.CeilToInt(duration*rate)];
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate,value=0;
                if(kind==0)value=Mathf.Sin(2*Mathf.PI*(700*t+1600*t*t))*Mathf.Exp(-t*18);
                else if(kind==1)value=(Mathf.Sin(t*2100*2*Mathf.PI)+.3f*Mathf.Sin(t*3157*2*Mathf.PI))*Mathf.Exp(-t*48);
                else if(kind==4)value=Mathf.Sin(t*1046.5f*2*Mathf.PI)*Mathf.Exp(-t*18);
                else if(kind==5)value=(Mathf.Sin(2*Mathf.PI*(320*t-220*t*t))+.25f*Mathf.Sin(2*Mathf.PI*1711*t))*Mathf.Exp(-t*12);
                else for(int note=0;note<3;note++)
                {
                    float age=t-note*.10f;if(age<0)continue;
                    float frequency=kind==2?(note==0?523.25f:note==1?659.25f:783.99f):(note==0?293.66f:note==1?246.94f:196f);
                    float envelope=Mathf.Min(1,age/.008f)*Mathf.Exp(-age*5);
                    value+=(Mathf.Sin(age*frequency*2*Mathf.PI)+.18f*Mathf.Sin(age*frequency*4.02f*Mathf.PI))*envelope*.45f;
                }
                float edge=Mathf.Clamp01(t/.003f)*Mathf.Clamp01((duration-t)/.025f);
                samples[i]=Mathf.Clamp(value*.14f*edge,-.25f,.25f);
            }
            var clip=AudioClip.Create(name,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        private void DisposeLootSounds()
        {foreach(var clip in new[]{fountainSplashSound,coinLaunchSound,coinSpinSound,relicBlessingSound,relicCurseSound,relicAcceptSound})if(clip!=null)Destroy(clip);}
    }
}
