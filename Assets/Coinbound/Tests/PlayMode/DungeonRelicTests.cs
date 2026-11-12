using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Coinbound.Tests
{
    public sealed class DungeonRelicTests
    {
        [Test] public void CatalogHasAllIconsAndBothSidesOfEveryTier()
        {
            Assert.That(DungeonRelics.All.Count,Is.EqualTo(32));
            Assert.That(DungeonRelics.All.Select(x=>x.Id).Distinct().Count(),Is.EqualTo(32));
            foreach(var item in DungeonRelics.All){Assert.That(item.Icon,Is.Not.Null,item.Id);Assert.That(item.Effects,Is.Not.Empty);}
            for(int tier=1;tier<=3;tier++)
            {
                Assert.That(DungeonRelics.All.Any(x=>x.Tier==tier&&x.Positive),Is.True);
                Assert.That(DungeonRelics.All.Any(x=>x.Tier==tier&&!x.Positive),Is.True);
            }
        }
        [Test] public void OffersKeepTheirTierAndOutcomeAndLuckRaisesRarity()
        {
            var random=new System.Random(77);
            for(int i=0;i<100;i++)
            {
                var offer=new DungeonChestOffer(DungeonHero.First.Stats,random);
                Assert.That(offer.Good.Tier,Is.EqualTo(offer.Bad.Tier));
                var result=offer.Resolve();Assert.That(offer.Resolve(),Is.SameAs(result));
                double roll=i/100d;
                Assert.That(DungeonRelics.RollTier(10,roll),Is.GreaterThanOrEqualTo(DungeonRelics.RollTier(-5,roll)));
            }
        }
        [Test] public void RelicsChangeLiveStatsWithoutMutatingHeroAndAurasRespectRange()
        {
            var root=new GameObject("Relic stats");
            try
            {
                var obj=new GameObject("Player",typeof(Rigidbody2D));obj.transform.SetParent(root.transform);
                var player=obj.AddComponent<DungeonPlayer>();player.Initialize(DungeonHero.First);player.SetInputEnabled(true);
                player.ReceiveRelic(DungeonRelics.All[0]);
                Assert.That(player.MaxHealth,Is.EqualTo(115));Assert.That(player.Health,Is.EqualTo(115));
                player.ReceiveRelic(DungeonRelics.All[1]);Assert.That(player.Stats.Defense,Is.EqualTo(1));
                player.TakeDamage(10,Vector2.right);Assert.That(player.Health,Is.EqualTo(106));
                Assert.That(DungeonHero.First.Stats.MaxHealth,Is.EqualTo(100));
                player.ReceiveRelic(DungeonRelics.All[17]);
                var actor=new GameObject("Enemy");actor.transform.SetParent(root.transform);actor.transform.position=Vector2.up*2;
                var enemy=actor.AddComponent<DungeonEnemy>();enemy.Initialize(player,null);
                Assert.That(enemy.EffectiveDefense,Is.EqualTo(-.25f));
                actor.transform.position=Vector2.up*8;Assert.That(enemy.EffectiveDefense,Is.Zero);
                for(int i=0;i<20;i++)player.ReceiveRelic(DungeonRelics.All[30]);
                Assert.That(player.MaxHealth,Is.EqualTo(40));Assert.That(player.Health,Is.EqualTo(40));
                Assert.That(player.MoveSpeed,Is.GreaterThanOrEqualTo(.5f));
            }
            finally{Object.DestroyImmediate(root);}
        }

        [Test] public void RelativeBonusesTreatBothHeroesEquallyAndStacksStayPlayable()
        {
            foreach(var hero in DungeonHero.All)
            {
                var inventory=new DungeonRelicInventory();inventory.Add(DungeonRelics.All[2],hero.Stats);
                Assert.That(inventory.CurrentStats.Damage/hero.Stats.Damage,Is.EqualTo(1.1f).Within(.001f));
                for(int i=0;i<100;i++)foreach(var item in DungeonRelics.All)inventory.Add(item,hero.Stats);
                Assert.That(inventory.CurrentStats.Damage,Is.InRange(hero.Stats.Damage*.45f,hero.Stats.Damage*2.5f));
                Assert.That(inventory.CurrentStats.AttackRate,Is.InRange(hero.Stats.AttackRate*.6f,hero.Stats.AttackRate*1.8f));
                Assert.That(inventory.CurrentStats.MaxHealth,Is.InRange(40,250));
            }
        }
        [UnityTest] public IEnumerator DebugMenuGrantsDuplicateRelicsAndRestoresGameplay()
        {
            Time.timeScale=1;var root=new GameObject("Debug relic test");
            var playerObject=new GameObject("Player",typeof(Rigidbody2D));playerObject.transform.SetParent(root.transform);
            var player=playerObject.AddComponent<DungeonPlayer>();player.Initialize(DungeonHero.First);player.SetInputEnabled(true);
            var ui=new GameObject("UI");ui.transform.SetParent(root.transform);var screen=ui.AddComponent<DungeonScreen>();
            screen.Initialize(()=>{},()=>{},()=>{},()=>{},_=>{});screen.BindPlayer(player);screen.ShowGame(1,0,0);
            try
            {
                screen.ToggleDebugRelics();Assert.That(screen.IsDebugRelicsOpen,Is.True);Assert.That(Time.timeScale,Is.Zero);
                screen.GrantDebugRelic(0);screen.GrantDebugRelic(0);Assert.That(player.Relics.Items.Count,Is.EqualTo(2));
                yield return new WaitForSecondsRealtime(.05f);
                var icons=ui.GetComponentsInChildren<RectTransform>(true).First(x=>x.name=="Held relics").GetComponentsInChildren<RawImage>();Assert.That(icons.Length,Is.EqualTo(2));
                Assert.That(icons[0].rectTransform.sizeDelta,Is.EqualTo(new Vector2(48,48)));
                Assert.That(icons[0].color.a,Is.EqualTo(.6f).Within(.001f));
                for(int i=0;i<80;i++)screen.GrantDebugRelic(0);
                screen.HandleBack();Assert.That(screen.IsDebugRelicsOpen,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1));Assert.That(player.CanAct,Is.True);
                yield return new WaitForSecondsRealtime(.05f);
                var scroll=ui.GetComponentsInChildren<ScrollRect>().Single();
                Assert.That(scroll.enabled,Is.True);Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
                Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
                var last=scroll.content.GetChild(scroll.content.childCount-1);
                Vector2 pointer=RectTransformUtility.WorldToScreenPoint(null,last.position);
                var method=typeof(DungeonScreen).GetMethod("UpdateRelicTooltip",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var tooltip=ui.GetComponentsInChildren<RectTransform>(true).First(x=>x.name=="Relic tooltip");
                method.Invoke(screen,new object[]{false,pointer});Assert.That(tooltip.gameObject.activeSelf,Is.False);
                method.Invoke(screen,new object[]{true,pointer});Assert.That(tooltip.gameObject.activeSelf,Is.True);
                Assert.That(tooltip.GetComponentsInChildren<Text>().Any(t=>t.text==DungeonRelics.All[0].Name),Is.True);
                method.Invoke(screen,new object[]{false,pointer});Assert.That(tooltip.gameObject.activeSelf,Is.False);
            }
            finally{Object.Destroy(root);Time.timeScale=1;}
            yield return null;
        }

        [UnityTest] public IEnumerator ChestCanBeReopenedWithoutRerollAndRewardIsGrantedOnce()
        {
            Time.timeScale=1;
            var root=new GameObject("Loot fixture");
            var screenRoot=new GameObject("Loot UI");
            try
            {
                var actor=new GameObject("Player",typeof(Rigidbody2D),typeof(BoxCollider2D));actor.transform.SetParent(root.transform);actor.transform.position=new Vector2(6000,6000);
                actor.GetComponent<Rigidbody2D>().gravityScale=0;actor.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
                actor.GetComponent<BoxCollider2D>().size=Vector2.one*.3f;
                var player=actor.AddComponent<DungeonPlayer>();player.Initialize(DungeonHero.First);player.SetInputEnabled(true);player.SetAimDirection(Vector2.right);
                var chestObj=new GameObject("Relic chest",typeof(SpriteRenderer));chestObj.transform.SetParent(root.transform);chestObj.transform.position=actor.transform.position+Vector3.right;
                var chest=chestObj.AddComponent<DungeonChest>();chest.Initialize("s");
                var screen=screenRoot.AddComponent<DungeonScreen>();screen.Initialize(()=>{},()=>{},()=>{},()=>{},_=>{});
                screen.BindPlayer(player);screen.ShowGame(1,0,1);Physics2D.SyncTransforms();
                Assert.That(chest.TryOpen(player),Is.True);
                yield return new WaitForSecondsRealtime(.75f);
                Assert.That(screen.IsLootOpen,Is.True);Assert.That(Time.timeScale,Is.Zero);
                var offer=chest.Offer;
                Assert.That(screen.HandleBack(),Is.True);Assert.That(Time.timeScale,Is.EqualTo(1));
                player.SetAimDirection(Vector2.right);Assert.That(chest.TryOpen(player),Is.True);
                Assert.That(chest.Offer,Is.SameAs(offer));
                screen.BeginCoinToss();screen.BeginCoinToss();
                yield return new WaitForSecondsRealtime(2.5f);
                Assert.That(chest.RewardClaimed,Is.True);Assert.That(player.Relics.Items.Count,Is.EqualTo(1));
                Assert.That(chest.ClaimReward(player),Is.False);
                screen.HandleBack();Assert.That(screen.IsLootOpen,Is.False);Assert.That(player.CanAct,Is.True);
                Assert.That(chest.CanInteract(player),Is.False);
            }
            finally{Object.Destroy(screenRoot);Object.Destroy(root);Time.timeScale=1;}
            yield return null;
        }
    }
}
