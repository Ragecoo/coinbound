using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Coinbound
{
    public sealed partial class DungeonScreen
    {
        private GameObject loot;
        private CanvasGroup lootFade,goodCard,badCard,pickupBanner;
        private Text pickupName,pickupSubtitle,pickupEffects;
        private RawImage cameraPreview,pickupIcon;
        private DungeonFateCoin fateCoin;
        private DungeonLootFx lootFx;
        internal bool IsFountainOpen => IsLootOpen && saveFountain!=null;
        private DungeonFountain saveFountain;
        private Text fountainMessage;
        private bool fountainFinished;
        private Vector2 fountainTarget,fountainOrigin;
        private DungeonCoinCamera coinCamera;
        private DungeonChest lootChest;
        private DungeonPlayer lootPlayer;
        private Coroutine tossRoutine,pickupRoutine,cameraDelay;
        private float lootPreviousTime;
        private bool lootPreviousInput;
        private float rewardAge;
        private Vector2 tossOrigin;
        private readonly Text[] relicNames=new Text[2],relicDescriptions=new Text[2],relicEffects=new Text[2];
        private readonly RawImage[] relicIcons=new RawImage[2];
        private readonly RawImage[,] rarityIcons=new RawImage[2,3];
        private PlayerStats shownStats;
        private RectTransform relicList;
        private readonly System.Collections.Generic.List<GameObject> heldRelicIcons=new System.Collections.Generic.List<GameObject>();
        public bool IsLootOpen=>loot!=null&&loot.activeSelf;

        private Text RelicText(string name,Transform parent,string text,int size,Vector2 pos,Vector2 area,Color tint)
        {
            var label=(DungeonPixelText)Label(name,parent,text,size,pos,area,tint);
            label.fontSize=size;label.ExtraPixelSteps=0;return label;
        }
        private void BuildLoot()
        {
            loot=Layer("Chest fate",new Color(16/255f,16/255f,25/255f,.68f));
            lootFade=loot.AddComponent<CanvasGroup>();
            var vignette=Rect("Loot vignette",loot.transform,Vector2.zero,Vector2.zero).gameObject.AddComponent<DungeonLootVignette>();
            Stretch(vignette.rectTransform);vignette.raycastTarget=false;
            var panel=Rect("Fate composition",loot.transform,Vector2.zero,new Vector2(960,640));
            for(int side=0;side<2;side++)
            {
                var card=Framed(side==0?"Blessing card":"Curse card",panel,new Vector2(side==0?-204:204,0),new Vector2(336,372));
                var group=card.gameObject.AddComponent<CanvasGroup>();if(side==0)goodCard=group;else badCard=group;
                relicIcons[side]=Rect("Relic icon",card,new Vector2(0,92),new Vector2(96,96)).gameObject.AddComponent<DungeonRelicPortrait>();
                relicIcons[side].raycastTarget=false;
                for(int i=0;i<3;i++)
                {
                    var glyph=Raw("Rarity "+i,card,new Vector2((i-1)*24,156),new Vector2(20,20));
                    glyph.texture=Resources.Load<Texture2D>("InterfaceArt/Rarity/rarity_"+(side==0?"star":"skull"));rarityIcons[side,i]=glyph;
                }
                relicNames[side]=RelicText("Relic name",card,"",24,new Vector2(0,20),new Vector2(304,52),Pale);
                Divider("Relic divider",card,new Vector2(0,-12),280);
                relicDescriptions[side]=RelicText("Relic subtitle",card,"",16,new Vector2(0,-40),new Vector2(304,40),Muted);
                relicEffects[side]=RelicText("Relic effects",card,"",16,new Vector2(0,-114),new Vector2(296,112),Pale);
                relicEffects[side].supportRichText=true;
            }
            lootFx=Rect("Loot sparks",panel,Vector2.zero,new Vector2(960,640)).gameObject.AddComponent<DungeonLootFx>();lootFx.raycastTarget=false;
            // The hand overlays both cards; the coin is the final sibling above the hand.
            cameraPreview=Raw("Hand skeleton",panel,new Vector2(0,-76),new Vector2(640,480));cameraPreview.color=Color.clear;
            fateCoin=Rect("Fate coin",panel,new Vector2(0,252),new Vector2(96,96)).gameObject.AddComponent<DungeonFateCoin>();fateCoin.raycastTarget=false;
            coinCamera=loot.AddComponent<DungeonCoinCamera>();coinCamera.Tossed+=BeginCoinToss;
            DungeonChest.RewardRequested+=ShowChestReward;
            var banner=Rect("Relic acquired",hud.transform,new Vector2(0,230),new Vector2(1120,100));
            banner.gameObject.AddComponent<DungeonRelicRibbon>().raycastTarget=false;
            pickupBanner=banner.gameObject.AddComponent<CanvasGroup>();pickupBanner.alpha=0;pickupBanner.blocksRaycasts=false;
            pickupIcon=Raw("Acquired icon",banner,new Vector2(-480,0),new Vector2(64,64));
            pickupName=RelicText("Acquired name",banner,"",64,new Vector2(0,18),new Vector2(900,56),Pale);
            pickupSubtitle=RelicText("Acquired subtitle",banner,"",24,new Vector2(0,-22),new Vector2(900,32),Pale);
            pickupEffects=RelicText("Acquired stats",banner,"",16,new Vector2(0,-68),new Vector2(900,32),Pale);
            foreach(var text in new[]{pickupName,pickupSubtitle,pickupEffects})
            {var shadow=text.gameObject.AddComponent<Shadow>();shadow.effectColor=Color.black;shadow.effectDistance=new Vector2(2,-2);}
            fountainMessage=RelicText("Fountain message",panel,"",24,new Vector2(0,-200),new Vector2(700,50),Pale);
            fountainMessage.gameObject.SetActive(false);
            BuildRelicInventoryHud();

        }

        private void ShowChestReward(DungeonChest chest,DungeonPlayer player)
        {
            if(player!=gameplayPlayer||chest.Offer==null||chest.RewardClaimed||IsLootOpen)return;
            saveFountain=null;loot.GetComponent<Image>().color=new Color(16/255f,16/255f,25/255f,.68f);fountainMessage.gameObject.SetActive(false);goodCard.gameObject.SetActive(true);badCard.gameObject.SetActive(true);
            fateCoin.gameObject.SetActive(true);fateCoin.rectTransform.localScale=Vector3.one;
            lootChest=chest;lootPlayer=player;lootPreviousTime=Time.timeScale;lootPreviousInput=player.CanAct;
            player.SetInputEnabled(false);Time.timeScale=0;
            loot.SetActive(true);lootFade.alpha=0;Select(null);
            goodCard.alpha=badCard.alpha=1;
            rewardAge=0;lootFx.ResetEffect();
            foreach(var icon in relicIcons)icon.rectTransform.sizeDelta=new Vector2(96,96);
            DungeonRelic[] pair={chest.Offer.Good,chest.Offer.Bad};
            for(int side=0;side<2;side++)
            {
                for(int i=0;i<3;i++){rarityIcons[side,i].gameObject.SetActive(i<pair[side].Tier);rarityIcons[side,i].rectTransform.anchoredPosition=new Vector2((i-(pair[side].Tier-1)*.5f)*24,156);}
                relicIcons[side].texture=pair[side].Icon;relicNames[side].text=pair[side].Name;
                relicDescriptions[side].text=pair[side].Subtitle;
                var lines=pair[side].Effects.Split('\n');
                for(int i=0;i<lines.Length;i++)
                {
                    bool bad=lines[i].StartsWith("−");
                    if(lines[i].Contains("врагов"))bad=!bad;
                    lines[i]="<color="+(bad?"#CE697C":"#79BA91")+">"+lines[i]+"</color>";
                }
                relicEffects[side].text=string.Join("\n",lines);
            }
            fateCoin.Skull=false;fateCoin.Spin=0;fateCoin.Refresh();
            fateCoin.rectTransform.anchoredPosition=new Vector2(0,252);
            if(chest.Offer.Resolved)PresentReward();
            else if(!Application.isBatchMode)cameraDelay=StartCoroutine(OpenCameraAfterCards());

        }

        public void ShowFountain(DungeonPlayer player,DungeonFountain fountain)
        {
            if(IsLootOpen||player!=gameplayPlayer)return;
            loot.GetComponent<Image>().color=new Color(16/255f,16/255f,25/255f,.25f);
            saveFountain=fountain;fountainFinished=false;lootChest=null;lootPlayer=player;
            lootPreviousTime=Time.timeScale;lootPreviousInput=player.CanAct;player.SetInputEnabled(false);Time.timeScale=0;
            loot.SetActive(true);lootFade.alpha=0;Select(null);lootFx.ResetEffect();rewardAge=0;
            goodCard.gameObject.SetActive(false);badCard.gameObject.SetActive(false);
            fountainMessage.gameObject.SetActive(true);fountainMessage.text="Брось монетку в фонтан";
            fateCoin.gameObject.SetActive(true);fateCoin.rectTransform.localScale=Vector3.one*.5f;
            fateCoin.Spin=0;fateCoin.Skull=false;fateCoin.Refresh();
            var parent=(RectTransform)fateCoin.transform.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,Camera.main.WorldToScreenPoint(fountain.transform.position),null,out fountainTarget);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,Camera.main.WorldToScreenPoint(player.transform.position+Vector3.up),null,out fountainOrigin);
            fateCoin.rectTransform.anchoredPosition=fountainOrigin;
            if(!Application.isBatchMode)cameraDelay=StartCoroutine(OpenCameraAfterCards());
        }

        private IEnumerator OpenCameraAfterCards()
        {
            // Give layout, texture uploads and the reveal time to render before device startup.
            yield return null;
            while(IsLootOpen && lootFade.alpha<1)yield return null;
            yield return null;
            cameraDelay=null;
            if(IsLootOpen && (saveFountain!=null || lootChest!=null && !lootChest.Offer.Resolved))coinCamera.StartCamera();
        }
        private void CancelPendingCamera()
        {if(cameraDelay!=null){StopCoroutine(cameraDelay);cameraDelay=null;}}

        public void BeginCoinToss()
        {
            if(!IsLootOpen||tossRoutine!=null||fountainFinished||saveFountain==null&&(lootChest==null||lootChest.Offer.Resolved))return;
            CancelPendingCamera();
            tossOrigin=saveFountain!=null?fountainOrigin:new Vector2(0,-100);
            if(coinCamera.HandPoints!=null)
            {
                var thumb=coinCamera.HandPoints[4];
                tossOrigin=cameraPreview.rectTransform.anchoredPosition+new Vector2((.5f-thumb.x)*640,(.5f-thumb.y)*480);
            }
            Select(null);
            // Resolve once at the start; no additional outcome rolls during animation or reopening.
            uiAudio.PlayOneShot(coinLaunchSound,.8f);
            if(lootChest!=null)lootChest.Offer.Resolve();tossRoutine=StartCoroutine(TossCoin());
        }
        private IEnumerator TossCoin()
        {
            float elapsed=0;int lastTick=-1;
            float duration=DungeonSettings.ReducedMotion?.65f:2.2f;
            while(elapsed<duration)
            {
                float t=elapsed/duration;
                fateCoin.Spin=DungeonSettings.ReducedMotion?0:t*Mathf.PI*12;
                fateCoin.Skull=((int)(t*12)&1)!=0;
                Vector2 position=Vector2.Lerp(tossOrigin,saveFountain!=null?fountainTarget:new Vector2(0,252),t);
                if(!DungeonSettings.ReducedMotion)position.y+=Mathf.Sin(t*Mathf.PI)*180;
                fateCoin.rectTransform.anchoredPosition=new Vector2(Mathf.Round(position.x/4)*4,Mathf.Round(position.y/4)*4);
                if(saveFountain!=null)fateCoin.rectTransform.localScale=Vector3.one*Mathf.Lerp(.5f,.08f,t);
                fateCoin.Refresh();lootFx.Follow(fateCoin.rectTransform.anchoredPosition);
                int tick=(int)(t*10);if(tick!=lastTick){lastTick=tick;uiAudio.PlayOneShot(coinSpinSound,.45f);}
                elapsed+=Time.unscaledDeltaTime;yield return null;
            }
            fateCoin.rectTransform.anchoredPosition=new Vector2(0,252);
            tossRoutine=null;
            if(saveFountain!=null)
            {
                uiAudio.PlayOneShot(fountainSplashSound,1);
                bool saved=saveFountain.Save();fountainFinished=true;rewardAge=0;
                fountainMessage.text=saved?"Забег сохранён":"Не удалось сохранить забег";
                fateCoin.gameObject.SetActive(false);coinCamera.StopCamera();cameraPreview.color=Color.clear;
                lootFx.Reveal(fountainTarget,fountainTarget,saved,false);
                uiAudio.PlayOneShot(saved?relicBlessingSound:relicCurseSound,.8f);
            }
            else PresentReward();
        }
        private void PresentReward()
        {
            DungeonRelic reward=lootChest.Offer.Result;
            bool freshlyClaimed=lootChest.ClaimReward(lootPlayer);
            fateCoin.Spin=0;fateCoin.Skull=!reward.Positive;fateCoin.Refresh();
            if(freshlyClaimed)
            {
                uiAudio.PlayOneShot(reward.Positive?relicBlessingSound:relicCurseSound,.85f);
                lootFx.Reveal(new Vector2(reward.Positive?-204:204,92),new Vector2(reward.Positive?-204:204,0),reward.Positive);
            }
            rewardAge=0;coinCamera.StopCamera();cameraPreview.texture=null;cameraPreview.color=Color.clear;
            RefreshStats(lootPlayer.Stats);
        }
        private void CloseLoot(bool restore)
        {
            if(!IsLootOpen||tossRoutine!=null)return;
            DungeonRelic reward=lootChest!=null&&lootChest.RewardClaimed?lootChest.Offer.Result:null;
            CancelPendingCamera();coinCamera.StopCamera();cameraPreview.texture=null;loot.SetActive(false);
            if(restore){Time.timeScale=lootPreviousTime;if(lootPlayer!=null)lootPlayer.SetInputEnabled(lootPreviousInput);}
            lootChest=null;lootPlayer=null;saveFountain=null;fountainFinished=false;Select(null);
            if(reward!=null)
            {
                if(pickupRoutine!=null)StopCoroutine(pickupRoutine);
                pickupRoutine=StartCoroutine(ShowRelicBanner(reward));
            }
        }
        private IEnumerator ShowRelicBanner(DungeonRelic item)
        {
            uiAudio.PlayOneShot(relicAcceptSound,.45f);
            pickupIcon.texture=item.HudIcon;pickupName.text=item.Name.ToUpperInvariant();pickupSubtitle.text=item.Subtitle;
            pickupEffects.text=item.Effects.Replace("\n","  ·  ");
            pickupName.color=item.Positive?Pale:new Color(.87f,.51f,.55f);
            float age=0;
            while(age<4.5f)
            {
                pickupBanner.alpha=Mathf.Min(Mathf.Clamp01(age/.3f),Mathf.Clamp01((4.5f-age)/.6f));
                ((RectTransform)pickupBanner.transform).anchoredPosition=new Vector2(0,230+(DungeonSettings.ReducedMotion?0:Mathf.Round((1-pickupBanner.alpha)*16/4)*4));
                age+=Time.unscaledDeltaTime;yield return null;
            }
            pickupBanner.alpha=0;pickupRoutine=null;
        }
        private void RefreshRelicList()
        {
            HideRelicTooltip();
            foreach(var icon in heldRelicIcons){icon.SetActive(false);Destroy(icon);}
            heldRelicIcons.Clear();
            int index=0;
            foreach(var item in gameplayPlayer.Relics.Items)
            {
                var icon=Raw("Held "+index+" "+item.Id,relicList,new Vector2(-24-(index%4)*56,-24-(index/4)*56),new Vector2(48,48));
                icon.rectTransform.anchorMin=icon.rectTransform.anchorMax=new Vector2(1,1);
                icon.texture=item.HudIcon;icon.color=new Color(1,1,1,.6f);heldRelicIcons.Add(icon.gameObject);index++;
            }
            ResizeRelicInventory();
        }

        private void UpdateLoot()
        {
            if(gameplayPlayer!=null&&shownStats!=gameplayPlayer.Stats)
            {
                shownStats=gameplayPlayer.Stats;RefreshStats(shownStats);
                RefreshRelicList();
            }
            UpdateRelicInventoryHud();
            if(!IsLootOpen)return;
            lootFade.alpha=Mathf.MoveTowards(lootFade.alpha,1,Time.unscaledDeltaTime*4);
            if(coinCamera.Preview!=null)
            {
                cameraPreview.texture=coinCamera.Preview;cameraPreview.color=coinCamera.Preview!=null?Color.white:Color.clear;
            }
            if(saveFountain!=null&&tossRoutine==null)
            {
                if(fountainFinished){rewardAge+=Time.unscaledDeltaTime;if(rewardAge>1.6f)CloseLoot(true);}
                else if(UnityEngine.InputSystem.Keyboard.current?.spaceKey.wasPressedThisFrame==true)BeginCoinToss();
            }
            if(tossRoutine==null && lootChest!=null)
            {
                bool pressed=UnityEngine.InputSystem.Keyboard.current?.spaceKey.wasPressedThisFrame==true;
                if(lootChest.Offer.Resolved)
                {
                    rewardAge+=Time.unscaledDeltaTime;
                    bool good=lootChest.Offer.Result.Positive;
                    goodCard.alpha=Mathf.MoveTowards(goodCard.alpha,good?1:.18f,Time.unscaledDeltaTime*3);
                    badCard.alpha=Mathf.MoveTowards(badCard.alpha,good?.18f:1,Time.unscaledDeltaTime*3);
                    float bump=DungeonSettings.ReducedMotion?0:Mathf.Sin(Mathf.Clamp01(rewardAge/.45f)*Mathf.PI)*12;
                    relicIcons[good?0:1].rectTransform.sizeDelta=Vector2.one*(96+Mathf.Round(bump/4)*4);
                    if(rewardAge>1.4f||pressed)CloseLoot(true);
                }
                else if(pressed)BeginCoinToss();
            }
        }
    }
}
