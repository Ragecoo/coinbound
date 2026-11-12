using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
namespace Coinbound
{
    public sealed partial class DungeonScreen
    {
        private RectTransform relicViewport,relicTooltip;
        private ScrollRect relicScroll;
        private Text relicTooltipName,relicTooltipEffects;
        private int tooltipRelic=-1;
        private void BuildRelicInventoryHud()
        {
            relicViewport=Rect("Relic viewport",hud.transform,Vector2.zero,new Vector2(224,224));
            Anchor(relicViewport,new Vector2(1,1),new Vector2(-24,-72));
            var hitArea=relicViewport.gameObject.AddComponent<Image>();hitArea.color=Color.clear;hitArea.raycastTarget=true;
            relicViewport.gameObject.AddComponent<RectMask2D>();
            relicList=Rect("Held relics",relicViewport,Vector2.zero,new Vector2(224,224));
            Anchor(relicList,new Vector2(1,1),Vector2.zero);
            relicScroll=relicViewport.gameObject.AddComponent<ScrollRect>();
            relicScroll.viewport=relicViewport;relicScroll.content=relicList;
            relicScroll.horizontal=false;relicScroll.vertical=true;relicScroll.inertia=false;
            relicScroll.movementType=ScrollRect.MovementType.Clamped;relicScroll.scrollSensitivity=56;
            relicTooltip=Framed("Relic tooltip",hud.transform,Vector2.zero,new Vector2(300,144));
            var group=relicTooltip.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
            relicTooltipName=RelicText("Tooltip name",relicTooltip,"",24,new Vector2(0,40),new Vector2(276,52),Pale);
            relicTooltipEffects=RelicText("Tooltip effects",relicTooltip,"",16,new Vector2(0,-26),new Vector2(276,80),Muted);
            relicTooltip.gameObject.SetActive(false);ResizeRelicInventory();
        }
        private void ResizeRelicInventory()
        {
            float available=Mathf.Max(56,((RectTransform)hud.transform).rect.height-96);
            float height=Mathf.Ceil(heldRelicIcons.Count/4f)*56;
            relicViewport.sizeDelta=new Vector2(224,Mathf.Min(available,height));
            relicList.sizeDelta=new Vector2(224,height);
            relicScroll.enabled=height>available;
            relicViewport.gameObject.SetActive(heldRelicIcons.Count>0);
            relicList.anchoredPosition=new Vector2(0,Mathf.Clamp(relicList.anchoredPosition.y,0,Mathf.Max(0,height-available)));
        }
        private void HideRelicTooltip()
        {tooltipRelic=-1;if(relicTooltip!=null)relicTooltip.gameObject.SetActive(false);}
        private void UpdateRelicInventoryHud()
        {
            ResizeRelicInventory();
            if(!hud.activeSelf||IsLootOpen||IsModalOpen||Mouse.current==null){HideRelicTooltip();return;}
            var keys=Keyboard.current;
            UpdateRelicTooltip(keys!=null&&(keys.leftCtrlKey.isPressed||keys.rightCtrlKey.isPressed),Mouse.current.position.ReadValue());
        }
        private void UpdateRelicTooltip(bool control,Vector2 pointer)
        {
            if(!control||gameplayPlayer==null||!relicViewport.gameObject.activeInHierarchy||
                !RectTransformUtility.RectangleContainsScreenPoint(relicViewport,pointer,null))
            {HideRelicTooltip();return;}
            int found=-1;
            for(int i=0;i<heldRelicIcons.Count;i++)
                if(RectTransformUtility.RectangleContainsScreenPoint((RectTransform)heldRelicIcons[i].transform,pointer,null)){found=i;break;}
            if(found<0||found>=gameplayPlayer.Relics.Items.Count){HideRelicTooltip();return;}
            if(tooltipRelic!=found)
            {
                var item=gameplayPlayer.Relics.Items[found];tooltipRelic=found;
                relicTooltipName.text=item.Name;relicTooltipEffects.text=item.Effects;
                float titleHeight=Mathf.Max(32,((DungeonPixelText)relicTooltipName).MeasureHeight(276));
                float effectsHeight=Mathf.Max(32,((DungeonPixelText)relicTooltipEffects).MeasureHeight(276));
                float height=titleHeight+effectsHeight+32;
                relicTooltip.sizeDelta=new Vector2(300,height);
                relicTooltipName.rectTransform.sizeDelta=new Vector2(276,titleHeight);
                relicTooltipName.rectTransform.anchoredPosition=new Vector2(0,height/2-12-titleHeight/2);
                relicTooltipEffects.rectTransform.sizeDelta=new Vector2(276,effectsHeight);
                relicTooltipEffects.rectTransform.anchoredPosition=new Vector2(0,-height/2+12+effectsHeight/2);
            }
            var hudRect=(RectTransform)hud.transform;
            Vector3 local=hudRect.InverseTransformPoint(heldRelicIcons[found].transform.position);
            Vector3 viewportLocal=hudRect.InverseTransformPoint(relicViewport.position);
            float x=viewportLocal.x-relicViewport.rect.width-12-relicTooltip.rect.width/2;
            float halfHeight=relicTooltip.rect.height/2;
            relicTooltip.anchoredPosition=new Vector2(Mathf.Clamp(x,hudRect.rect.xMin+158,hudRect.rect.xMax-158),
                Mathf.Clamp(local.y,hudRect.rect.yMin+halfHeight+8,hudRect.rect.yMax-halfHeight-8));
            relicTooltip.SetAsLastSibling();relicTooltip.gameObject.SetActive(true);
        }
    }
}
