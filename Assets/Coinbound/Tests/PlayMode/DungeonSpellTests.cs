using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Coinbound.Tests
{
    public sealed class DungeonSpellTests
    {
        private GameObject root;
        private DungeonPlayer player;
        private bool hitStop;
        private readonly Vector2 origin = new Vector2(2500,2500);
        [SetUp] public void SetUp()
        {
            Time.timeScale=1;hitStop=DungeonFxSettings.Current.hitStop;DungeonFxSettings.Current.hitStop=false;
            root=new GameObject("Spell fixture");
            var obj=new GameObject("Player",typeof(Rigidbody2D),typeof(BoxCollider2D));obj.transform.SetParent(root.transform);
            obj.transform.position=origin;
            obj.GetComponent<Rigidbody2D>().gravityScale=0;obj.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
            obj.GetComponent<BoxCollider2D>().size=Vector2.one*.36f;
            player=obj.AddComponent<DungeonPlayer>();player.Initialize(DungeonHero.First);player.SetInputEnabled(true);
            Physics2D.SyncTransforms();Assert.That(player.CanAct,Is.True);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale=1;DungeonFxSettings.Current.hitStop=hitStop;Object.Destroy(root);yield return null;
        }
        private DungeonEnemy Enemy(Vector2 offset,bool stationary=true,int health=100)
        {
            var obj=new GameObject("Enemy");obj.transform.SetParent(root.transform);obj.transform.position=origin+offset;
            var enemy=obj.AddComponent<DungeonEnemy>();enemy.Initialize(player,null,new EnemyStats(health,0,10,2.25f,100));
            if(stationary)obj.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
            Physics2D.SyncTransforms();return enemy;
        }
        private void Wall(Vector2 offset,Vector2 size)
        {
            var obj=new GameObject("Wall",typeof(BoxCollider2D));obj.transform.SetParent(root.transform);obj.transform.position=origin+offset;
            obj.GetComponent<BoxCollider2D>().size=size;Physics2D.SyncTransforms();
        }
        [Test] public void EmptyPulseConsumesManaAndCooldownOnlyOnce()
        {
            Assert.That(player.Spells.TryCast(DungeonSpell.Pulse),Is.EqualTo(SpellCastResult.Success));
            Assert.That(player.Mana,Is.EqualTo(80));Assert.That(player.Spells.Cooldown(DungeonSpell.Pulse),Is.EqualTo(3));
            Assert.That(root.GetComponentsInChildren<DungeonPulse>(),Is.Empty);
            Assert.That(player.Spells.TryCast(DungeonSpell.Pulse),Is.EqualTo(SpellCastResult.Cooldown));Assert.That(player.Mana,Is.EqualTo(80));
        }
        [UnityTest] public IEnumerator ManaRegeneratesAtTwoPointFivePerSecondAndStopsWhenPaused()
        {
            player.TrySpendMana(50);
            yield return new WaitForSeconds(1.05f);
            Assert.That(player.Mana, Is.InRange(52.5f, 53f));
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(player.Mana, Is.InRange(52.5f, 53f));
            Time.timeScale = 1;
            player.SetInputEnabled(false);
            yield return new WaitForSeconds(.25f);
            Assert.That(player.Mana, Is.InRange(52.5f, 53f));
            player.Initialize(DungeonHero.First);
            player.SetInputEnabled(true);
            player.TrySpendMana(1);
            yield return new WaitForSeconds(.5f);
            Assert.That(player.Mana, Is.EqualTo(player.MaxMana));
        }
        [Test] public void ManaAndUnavailableFailuresDoNotStartCooldowns()
        {
            player.TrySpendMana(90);
            foreach(SpellDefinition spell in SpellDefinition.All)
            {
                Assert.That(player.Spells.TryCast(spell.Id),Is.EqualTo(SpellCastResult.NotEnoughMana));
                Assert.That(player.Spells.Cooldown(spell.Id),Is.Zero);
            }
            player.SetInputEnabled(false);
            Assert.That(player.Spells.TryCast(DungeonSpell.Trace),Is.EqualTo(SpellCastResult.Unavailable));Assert.That(player.Mana,Is.EqualTo(10));
        }
        [Test] public void RecallReturnIsFreeAndStartsCooldownOnlyAfterReturn()
        {
            Assert.That(player.Spells.TryCast(DungeonSpell.Recall), Is.EqualTo(SpellCastResult.Success));
            Assert.That(player.Mana, Is.EqualTo(75));
            Assert.That(player.Spells.Cooldown(DungeonSpell.Recall), Is.Zero);
            player.TrySpendMana(75);
            player.transform.position = origin + Vector2.right * 5; Physics2D.SyncTransforms();
            Assert.That(player.Spells.TryCast(DungeonSpell.Recall), Is.EqualTo(SpellCastResult.Success));
            Assert.That((Vector2)player.transform.position, Is.EqualTo(origin));
            Assert.That(player.Mana, Is.Zero);
            Assert.That(player.Spells.ActiveRemaining(DungeonSpell.Recall), Is.Zero);
            Assert.That(player.Spells.Cooldown(DungeonSpell.Recall), Is.EqualTo(10));
        }
        [Test] public void RecallBlockedReturnKeepsRuneAndAllowsLaterRetry()
        {
            player.Spells.TryCast(DungeonSpell.Recall);
            player.transform.position = origin + Vector2.right * 5;
            Wall(Vector2.zero, Vector2.one * 4); Physics2D.SyncTransforms();
            Assert.That(player.Spells.TryCast(DungeonSpell.Recall), Is.EqualTo(SpellCastResult.NoFreeSpace));
            Assert.That(player.Mana, Is.EqualTo(75));
            Assert.That(player.Spells.ActiveRemaining(DungeonSpell.Recall), Is.EqualTo(4));
            Assert.That(player.Spells.Cooldown(DungeonSpell.Recall), Is.Zero);
            Object.DestroyImmediate(root.transform.Find("Wall").gameObject);
            Physics2D.SyncTransforms();
            Assert.That(player.Spells.TryCast(DungeonSpell.Recall), Is.EqualTo(SpellCastResult.Success));
        }
        [Test] public void RecallAvoidsEnemyOnRune()
        {
            player.Spells.TryCast(DungeonSpell.Recall);
            player.transform.position = origin + Vector2.right * 5;
            Enemy(Vector2.zero);
            Assert.That(player.Spells.TryCast(DungeonSpell.Recall), Is.EqualTo(SpellCastResult.Success));
            float distance = Vector2.Distance(player.transform.position, origin);
            Assert.That(distance, Is.GreaterThan(.32f).And.LessThan(2));
        }
        [UnityTest] public IEnumerator RecallExpiresThenStartsFullCooldown()
        {
            player.Spells.TryCast(DungeonSpell.Recall);
            yield return new WaitForSeconds(4.1f);
            Assert.That(player.Spells.ActiveRemaining(DungeonSpell.Recall), Is.Zero);
            Assert.That(player.Spells.Cooldown(DungeonSpell.Recall), Is.InRange(9.5f, 10));
            Assert.That(root.GetComponentsInChildren<DungeonRecall>(), Is.Empty);
        }
        [TestCase(DungeonSpell.Trace)] [TestCase(DungeonSpell.Ward)] [TestCase(DungeonSpell.Decoy)]
        public void ActiveSpellsDoNotStartCooldownOrAllowRecasting(DungeonSpell spell)
        {
            player.Spells.TryCast(spell);
            Assert.That(player.Spells.Cooldown(spell), Is.Zero);
            Assert.That(player.Spells.ActiveRemaining(spell), Is.EqualTo(SpellDefinition.All[(int)spell].Duration));
            float mana = player.Mana;
            Assert.That(player.Spells.TryCast(spell), Is.EqualTo(SpellCastResult.Cooldown));
            Assert.That(player.Mana, Is.EqualTo(mana));
        }
        [Test] public void HudCentersSuccessfulCastsOnlyAndRebindsCleanly()
        {
            var hudObject = new GameObject("Spell HUD fixture", typeof(RectTransform));
            hudObject.transform.SetParent(root.transform);
            var hud = hudObject.AddComponent<DungeonSpellHud>(); hud.Bind(player.Spells);
            player.Spells.TryCast(DungeonSpell.Trace);
            Assert.That(hud.CenteredSpell, Is.EqualTo(DungeonSpell.Trace));
            player.TrySpendMana(player.Mana);
            player.Spells.TryCast(DungeonSpell.Ward);
            Assert.That(hud.CenteredSpell, Is.EqualTo(DungeonSpell.Trace));
            hud.Bind(null);
            Assert.That(hud.CenteredSpell, Is.EqualTo(DungeonSpell.Pulse));
        }
        [Test] public void BlockStunsAllVisibleFrontEnemiesButNotBackOrWalls()
        {
            var attacker=Enemy(new Vector2(1,0));var other=Enemy(new Vector2(.8f,.6f));var back=Enemy(new Vector2(-1,0));
            var hidden=Enemy(new Vector2(1,-1));Wall(new Vector2(.5f,-.5f),Vector2.one*.3f);
            player.SetAimDirection(Vector2.right);player.SetBlocking(true);
            Assert.That(player.TakeDamage(10,attacker.transform.position,attacker),Is.False);
            Assert.That(attacker.StunRemaining,Is.GreaterThan(0));Assert.That(other.StunRemaining,Is.GreaterThan(0));
            Assert.That(back.StunRemaining,Is.Zero);Assert.That(hidden.StunRemaining,Is.Zero);
        }
        [UnityTest] public IEnumerator TraceEnemyIndicatorStartsHiddenAndDisappearsWithTarget()
        {
            var target = Enemy(new Vector2(3,0));
            player.Spells.TryCast(DungeonSpell.Trace);
            var trace = root.GetComponentInChildren<DungeonTrace>();
            Transform icon = trace.transform.Find("sword indicator");
            Assert.That(icon.gameObject.activeSelf, Is.False);
            yield return new WaitForSeconds(.05f);
            Assert.That(icon.gameObject.activeSelf, Is.True);
            target.gameObject.SetActive(false);
            yield return new WaitForSeconds(.03f);
            Assert.That(icon.gameObject.activeSelf, Is.False);
            Assert.That(player.Spells.IsActive(DungeonSpell.Trace), Is.True);
        }
        [UnityTest] public IEnumerator HudUsesExactManaAnchorsAndIndependentStates()
        {
            var obj = new GameObject("HUD state fixture", typeof(RectTransform)); obj.transform.SetParent(root.transform);
            var hud = obj.AddComponent<DungeonSpellHud>(); hud.Bind(player.Spells);
            yield return new WaitForSeconds(.05f);
            var pulseArt = obj.transform.Find("pulse/Icon").GetComponent<UnityEngine.UI.RawImage>();
            Assert.That(pulseArt.texture.name, Is.EqualTo("large_inactive"));
            Assert.That(obj.transform.Find("pulse/Mana cost").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(29,-33)));
            Assert.That(obj.transform.Find("recall/Mana cost").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(29,-31)));
            player.Spells.TryCast(DungeonSpell.Recall);
            yield return new WaitForSeconds(.4f);
            Transform cost = obj.transform.Find("recall/Mana cost");
            Assert.That(cost.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(29,-33)));
            Assert.That(cost.Find("Glyph 0").GetComponent<UnityEngine.UI.RawImage>().texture.name, Is.EqualTo("0"));
            Assert.That(cost.GetSiblingIndex(), Is.GreaterThan(obj.transform.Find("recall/Cooldown").GetSiblingIndex()));
            Assert.That(obj.transform.Find("recall/Icon").GetComponent<UnityEngine.UI.RawImage>().texture.name, Is.EqualTo("large_active"));
            string[] sides = { "pulse", "trace", "ward", "decoy" };
            float previousX = float.NegativeInfinity;
            foreach (string name in sides)
            { float x = obj.transform.Find(name).GetComponent<RectTransform>().anchoredPosition.x; Assert.That(x, Is.GreaterThan(previousX)); previousX = x; }
            player.Spells.TryCast(DungeonSpell.Recall);
            yield return new WaitForSeconds(.05f);
            Assert.That(cost.Find("Glyph 0").GetComponent<UnityEngine.UI.RawImage>().texture.name, Is.EqualTo("2"));
            Assert.That(cost.GetComponent<RectTransform>().anchoredPosition.x, Is.EqualTo(29));
            Assert.That(obj.transform.Find("recall/Icon").GetComponent<UnityEngine.UI.RawImage>().texture.name, Is.EqualTo("large_inactive"));
            hud.Bind(player.Spells);
            Assert.That(hud.CenteredSpell, Is.EqualTo(DungeonSpell.Recall));
        }
        [UnityTest] public IEnumerator PulseActivityTracksFlashAndActualProjectileAlongsideCooldown()
        {
            player.Spells.TryCast(DungeonSpell.Pulse);
            Assert.That(player.Spells.IsActive(DungeonSpell.Pulse), Is.True);
            Assert.That(player.Spells.Cooldown(DungeonSpell.Pulse), Is.EqualTo(3));
            yield return new WaitForSeconds(.25f);
            Assert.That(player.Spells.IsActive(DungeonSpell.Pulse), Is.False);
            yield return new WaitForSeconds(2.8f);
            Enemy(new Vector2(7,0));
            player.Spells.TryCast(DungeonSpell.Pulse);
            yield return new WaitForSeconds(.3f);
            Assert.That(player.Spells.IsActive(DungeonSpell.Pulse), Is.True);
            Assert.That(player.Spells.Cooldown(DungeonSpell.Pulse), Is.GreaterThan(0));
            yield return new WaitForSeconds(.7f);
            Assert.That(player.Spells.IsActive(DungeonSpell.Pulse), Is.False);
        }
        [UnityTest] public IEnumerator ChangingFloorClearsWorldEffectsAndSynchronizesTimers()
        {
            player.Spells.TryCast(DungeonSpell.Recall);
            player.Spells.TryCast(DungeonSpell.Trace);
            player.Spells.TryCast(DungeonSpell.Decoy);
            player.Spells.SetMap(new ProceduralGeneration.DungeonMap(4,4));
            Assert.That(player.Spells.IsActive(DungeonSpell.Recall), Is.False);
            Assert.That(player.Spells.ManaCost(DungeonSpell.Recall), Is.EqualTo(25));
            Assert.That(player.Spells.Cooldown(DungeonSpell.Recall), Is.EqualTo(10));
            Assert.That(player.Spells.LastCast, Is.EqualTo(DungeonSpell.Decoy));
            yield return null;
            Assert.That(root.GetComponentsInChildren<DungeonRecall>(), Is.Empty);
            Assert.That(root.GetComponentsInChildren<DungeonDecoy>(), Is.Empty);
            Assert.That(root.GetComponentsInChildren<DungeonTrace>(), Is.Empty);
        }
        [UnityTest] public IEnumerator FailedCastsHighlightOnlyTheirOwnHudElement()
        {
            var obj = new GameObject("HUD failure fixture", typeof(RectTransform)); obj.transform.SetParent(root.transform);
            var hud = obj.AddComponent<DungeonSpellHud>(); hud.Bind(player.Spells);
            player.Spells.TryCast(DungeonSpell.Pulse);
            player.Spells.TryCast(DungeonSpell.Pulse);
            player.TrySpendMana(player.Mana);
            player.Spells.TryCast(DungeonSpell.Ward);
            yield return new WaitForSeconds(.05f);
            Assert.That(hud.CenteredSpell, Is.EqualTo(DungeonSpell.Pulse));
            var glyph = obj.transform.Find("ward/Mana cost/Glyph 0").GetComponent<UnityEngine.UI.RawImage>();
            Assert.That(glyph.color.g, Is.LessThan(.5f));
            yield return new WaitForSeconds(.35f);
            Assert.That(glyph.color, Is.EqualTo(Color.white));
        }
        [UnityTest] public IEnumerator PulseHitsSplashForTwoBasicsAndStopsAtStone()
        {
            var target=Enemy(new Vector2(3,0));var splash=Enemy(new Vector2(3,1));var hidden=Enemy(new Vector2(3,-1));
            Wall(new Vector2(3,-.5f),new Vector2(2,.15f));
            Assert.That(player.Spells.TryCast(DungeonSpell.Pulse),Is.EqualTo(SpellCastResult.Success));
            yield return new WaitForSeconds(.6f);
            Assert.That(target.Health,Is.EqualTo(96));Assert.That(splash.Health,Is.EqualTo(96));Assert.That(hidden.Health,Is.EqualTo(100));
            Assert.That(root.GetComponentsInChildren<DungeonPulse>(),Is.Empty);
        }
        [Test] public void OccludedOrOutOfRangeEnemiesDoNotCreatePulse()
        {
            Enemy(new Vector2(3,0));Enemy(new Vector2(0,9));Wall(new Vector2(1.5f,0),new Vector2(.2f,2));
            Assert.That(player.Spells.TryCast(DungeonSpell.Pulse),Is.EqualTo(SpellCastResult.Success));
            Assert.That(root.GetComponentsInChildren<DungeonPulse>(),Is.Empty);Assert.That(player.Mana,Is.EqualTo(80));
        }
        [UnityTest] public IEnumerator PulseCanKillSeveralEnemiesWithoutInvalidatingItsSplash()
        {
            Enemy(new Vector2(3,0),health:3);Enemy(new Vector2(3,1),health:3);Enemy(new Vector2(3,-1),health:3);
            Assert.That(player.Spells.TryCast(DungeonSpell.Pulse),Is.EqualTo(SpellCastResult.Success));
            yield return new WaitForSeconds(.6f);
            Assert.That(player.Score,Is.EqualTo(300));
        }
        [UnityTest] public IEnumerator WardAbsorbsRepelsAndExpiresWhileCooldownPauses()
        {
            var front=Enemy(new Vector2(1,0));var back=Enemy(new Vector2(-1,0));var far=Enemy(new Vector2(2,0));
            Assert.That(player.Spells.TryCast(DungeonSpell.Ward),Is.EqualTo(SpellCastResult.Success));
            Assert.That(player.TakeDamage(100,front.transform.position,front),Is.False);Assert.That(player.Health,Is.EqualTo(100));
            Assert.That(front.StunRemaining,Is.GreaterThan(0));Assert.That(back.StunRemaining,Is.GreaterThan(0));Assert.That(far.StunRemaining,Is.Zero);
            player.SetInputEnabled(false);yield return new WaitForSeconds(.15f);
            Assert.That(player.Spells.Cooldown(DungeonSpell.Ward),Is.Zero);
            Assert.That(player.Spells.ActiveRemaining(DungeonSpell.Ward),Is.EqualTo(3));
            player.SetInputEnabled(true);yield return new WaitForSeconds(3.1f);
            Assert.That(player.Spells.WardActive,Is.False);
            Assert.That(player.Spells.Cooldown(DungeonSpell.Ward),Is.InRange(9.5f,10f));
        }
        [UnityTest] public IEnumerator DecoyRedirectsPursuitAndExpiresAfterFiveSeconds()
        {
            var enemy=Enemy(new Vector2(3,0),false);
            Assert.That(player.Spells.TryCast(DungeonSpell.Decoy),Is.EqualTo(SpellCastResult.Success));
            player.transform.position=origin+new Vector2(3,3);Physics2D.SyncTransforms();
            yield return new WaitForSeconds(.6f);
            Assert.That(enemy.transform.position.x,Is.LessThan(origin.x+2.5f));
            // A ghoul may flank its target; verify the remembered target, not a straight walking line.
            Assert.That(Vector2.Distance(enemy.LastKnownTarget,origin),Is.LessThan(.05f));
            Assert.That(root.GetComponentsInChildren<DungeonDecoy>().Length,Is.EqualTo(1));
            yield return new WaitForSeconds(4.5f);
            Assert.That(root.GetComponentsInChildren<DungeonDecoy>(),Is.Empty);
        }
        [TestCase("pulse",DungeonSpell.Pulse)] [TestCase("пульс",DungeonSpell.Pulse)]
        [TestCase("trace",DungeonSpell.Trace)] [TestCase("трей с",DungeonSpell.Trace)]
        [TestCase("ward",DungeonSpell.Ward)] [TestCase("вард",DungeonSpell.Ward)]
        [TestCase("decoy",DungeonSpell.Decoy)] [TestCase("де кой",DungeonSpell.Decoy)]
        [TestCase("recall",DungeonSpell.Recall)] [TestCase("ре кол",DungeonSpell.Recall)]
        public void BothVoiceLanguagesResolve(string word,DungeonSpell expected)
        { Assert.That(DungeonVoiceInput.TryWord(word,out DungeonSpell spell),Is.True);Assert.That(spell,Is.EqualTo(expected)); }
        [TestCase("hello")] [TestCase("pulse trace")] [TestCase("[unk]")] [TestCase("impulse")]
        public void UnrelatedSpeechIsNotACommand(string word)=>Assert.That(DungeonVoiceInput.TryWord(word,out _),Is.False);
    }
}
