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
        [Test] public void BlockStunsAllVisibleFrontEnemiesButNotBackOrWalls()
        {
            var attacker=Enemy(new Vector2(1,0));var other=Enemy(new Vector2(.8f,.6f));var back=Enemy(new Vector2(-1,0));
            var hidden=Enemy(new Vector2(1,-1));Wall(new Vector2(.5f,-.5f),Vector2.one*.3f);
            player.SetAimDirection(Vector2.right);player.SetBlocking(true);
            Assert.That(player.TakeDamage(10,attacker.transform.position,attacker),Is.False);
            Assert.That(attacker.StunRemaining,Is.GreaterThan(0));Assert.That(other.StunRemaining,Is.GreaterThan(0));
            Assert.That(back.StunRemaining,Is.Zero);Assert.That(hidden.StunRemaining,Is.Zero);
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
            Assert.That(player.Spells.Cooldown(DungeonSpell.Ward),Is.EqualTo(10));
            player.SetInputEnabled(true);yield return new WaitForSeconds(3.1f);
            Assert.That(player.Spells.WardActive,Is.False);
            Assert.That(player.Spells.Cooldown(DungeonSpell.Ward),Is.InRange(6.5f,7f));
        }
        [UnityTest] public IEnumerator DecoyRedirectsPursuitAndExpiresAfterFiveSeconds()
        {
            var enemy=Enemy(new Vector2(3,0),false);
            Assert.That(player.Spells.TryCast(DungeonSpell.Decoy),Is.EqualTo(SpellCastResult.Success));
            player.transform.position=origin+new Vector2(3,3);Physics2D.SyncTransforms();
            yield return new WaitForSeconds(.6f);
            Assert.That(enemy.transform.position.x,Is.LessThan(origin.x+2.5f));
            Assert.That(enemy.transform.position.y,Is.EqualTo(origin.y).Within(.25f));
            Assert.That(root.GetComponentsInChildren<DungeonDecoy>().Length,Is.EqualTo(1));
            yield return new WaitForSeconds(4.5f);
            Assert.That(root.GetComponentsInChildren<DungeonDecoy>(),Is.Empty);
        }
        [TestCase("pulse",DungeonSpell.Pulse)] [TestCase("пульс",DungeonSpell.Pulse)]
        [TestCase("trace",DungeonSpell.Trace)] [TestCase("трей с",DungeonSpell.Trace)]
        [TestCase("ward",DungeonSpell.Ward)] [TestCase("вард",DungeonSpell.Ward)]
        [TestCase("decoy",DungeonSpell.Decoy)] [TestCase("де кой",DungeonSpell.Decoy)]
        public void BothVoiceLanguagesResolve(string word,DungeonSpell expected)
        { Assert.That(DungeonVoiceInput.TryWord(word,out DungeonSpell spell),Is.True);Assert.That(spell,Is.EqualTo(expected)); }
        [TestCase("hello")] [TestCase("pulse trace")] [TestCase("[unk]")] [TestCase("impulse")]
        public void UnrelatedSpeechIsNotACommand(string word)=>Assert.That(DungeonVoiceInput.TryWord(word,out _),Is.False);
    }
}
