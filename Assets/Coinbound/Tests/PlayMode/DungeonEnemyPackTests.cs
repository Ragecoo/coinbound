using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Coinbound.Tests
{
    public sealed class DungeonEnemyPackTests
    {
        private GameObject root;
        private DungeonPlayer player;
        private readonly Vector2 origin=new Vector2(5000,5000);
        private bool oldHitStop;
        [SetUp] public void Setup()
        {
            Time.timeScale=1;oldHitStop=DungeonFxSettings.Current.hitStop;DungeonFxSettings.Current.hitStop=false;
            root=new GameObject("Enemy pack fixture");
            var obj=new GameObject("Player",typeof(Rigidbody2D),typeof(BoxCollider2D));obj.transform.SetParent(root.transform);obj.transform.position=origin;
            obj.GetComponent<Rigidbody2D>().gravityScale=0;obj.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
            obj.GetComponent<BoxCollider2D>().size=Vector2.one*.36f;
            player=obj.AddComponent<DungeonPlayer>();player.Initialize(DungeonHero.First);player.SetInputEnabled(true);
        }
        [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;DungeonFxSettings.Current.hitStop=oldHitStop;Object.Destroy(root);yield return null;}
        private DungeonEnemy Enemy(DungeonEnemyKind kind,Vector2 offset,bool stationary=false)
        {
            var obj=new GameObject(kind.ToString());obj.transform.SetParent(root.transform);obj.transform.position=origin+offset;
            var enemy=obj.AddComponent<DungeonEnemy>();enemy.Configure(kind,1);enemy.Initialize(player,null);
            if(stationary)obj.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
            Physics2D.SyncTransforms();return enemy;
        }
        private GameObject Wall(Vector2 offset,Vector2 size)
        {var obj=new GameObject("Wall",typeof(BoxCollider2D));obj.transform.SetParent(root.transform);obj.transform.position=origin+offset;obj.GetComponent<BoxCollider2D>().size=size;Physics2D.SyncTransforms();return obj;}
        private static void Field(object obj,string name,object value)=>obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);

        [TestCase(DungeonEnemyKind.Warden,15f,12f,1.4f,.65f,.1f,.7f,.4f)]
        [TestCase(DungeonEnemyKind.Arbalist,8f,9f,1.8f,.65f,.08f,.75f,.65f)]
        [TestCase(DungeonEnemyKind.Cultist,10f,11f,1.1f,.95f,.12f,.72f,.8f)]
        [TestCase(DungeonEnemyKind.Ghoul,6f,10f,2.8f,.5f,.26f,.75f,.65f)]
        public void FixedDefinitions(DungeonEnemyKind kind,float hp,float damage,float speed,float windup,float active,float recovery,float delay)
        {
            var d=DungeonEnemyDefinition.For(kind);Assert.That(d.Stats.MaxHealth,Is.EqualTo(hp));Assert.That(d.Stats.Damage,Is.EqualTo(damage));
            Assert.That(d.Stats.MoveSpeed,Is.EqualTo(speed));Assert.That(d.Stats.Defense,Is.Zero);
            Assert.That(d.Windup,Is.EqualTo(windup).Within(.0001));Assert.That(d.Active,Is.EqualTo(active).Within(.0001));
            Assert.That(d.Recovery,Is.EqualTo(recovery).Within(.0001));Assert.That(d.Delay,Is.EqualTo(delay));
        }
        [UnityTest] public IEnumerator SwordPushesAtHalfStrengthWithoutInterruptingEnemyAttack()
        {
            var enemy=Enemy(DungeonEnemyKind.Warden,new Vector2(0,1));
            yield return new WaitForSeconds(.08f);
            Assert.That(enemy.IsWindingUp,Is.True);
            int sequence=enemy.AttackSequence;
            Assert.That(player.TryAttack(Vector2.up),Is.True);
            typeof(DungeonPlayer).GetMethod("AdvanceAttack",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(player,new object[]{.17f});
            Assert.That(enemy.Health,Is.LessThan(enemy.MaxHealth));
            Assert.That(enemy.StunRemaining,Is.Zero);
            Assert.That(enemy.IsWindingUp,Is.True);
            var recoil=(Vector2)typeof(DungeonEnemy).GetField("knockbackVelocity",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(enemy);
            Assert.That(recoil.magnitude,Is.EqualTo(2.5f).Within(.001f));
            float before=enemy.transform.position.y;
            yield return new WaitForSeconds(.08f);
            Assert.That(enemy.transform.position.y,Is.GreaterThan(before));
            Assert.That(enemy.AttackSequence,Is.EqualTo(sequence));
            Assert.That(enemy.IsWindingUp,Is.True);
        }

        [UnityTest] public IEnumerator RangedEnemiesDoNotDealTouchDamage()
        {
            foreach(var kind in new[]{DungeonEnemyKind.Arbalist,DungeonEnemyKind.Cultist})
            {
                var enemy=Enemy(kind,new Vector2(.4f,0),true);Field(enemy,"delay",100f);
                enemy.GetComponent<Rigidbody2D>().sleepMode=RigidbodySleepMode2D.NeverSleep;
                player.GetComponent<Rigidbody2D>().sleepMode=RigidbodySleepMode2D.NeverSleep;
                yield return new WaitForSeconds(.9f);
                Assert.That(player.Health,Is.EqualTo(100),kind+" must not inflict contact damage");
                enemy.gameObject.SetActive(false);Object.Destroy(enemy.gameObject);yield return null;
            }
        }

        [UnityTest] public IEnumerator ContactDamageRepeatsWithGraceAndStopsWhileStunnedOrDead()
        {
            var enemy=Enemy(DungeonEnemyKind.Warden,new Vector2(.4f,0),true);
            Field(enemy,"delay",100f);
            enemy.GetComponent<Rigidbody2D>().sleepMode=RigidbodySleepMode2D.NeverSleep;
            player.GetComponent<Rigidbody2D>().sleepMode=RigidbodySleepMode2D.NeverSleep;
            yield return new WaitForSeconds(.08f);
            Assert.That(player.Health,Is.EqualTo(88));
            yield return new WaitForSeconds(.2f);
            Assert.That(player.Health,Is.EqualTo(88),"Contact cannot deal damage every physics frame.");
            Time.timeScale=0;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(player.Health,Is.EqualTo(88));
            Time.timeScale=1;
            yield return new WaitForSeconds(.6f);
            Assert.That(player.Health,Is.EqualTo(76));
            enemy.Stun(2);
            yield return new WaitForSeconds(.85f);
            Assert.That(player.Health,Is.EqualTo(76),"A stunned enemy cannot inflict touch damage.");
            enemy.ReceiveHit(999,Vector2.right,0);
            yield return new WaitForSeconds(.8f);
            Assert.That(player.Health,Is.EqualTo(76));
        }

        [Test] public void FractionalDamageAndDirectedGuardArePreserved()
        {
            var tank=Enemy(DungeonEnemyKind.Warden,new Vector2(0,2));
            tank.ReceiveHit(.25f,Vector2.up,0);Assert.That(tank.Health,Is.EqualTo(14.925f).Within(.0001));Assert.That(tank.StunRemaining,Is.Zero);
            tank.ReceiveHit(.25f,Vector2.right,0);Assert.That(tank.Health,Is.EqualTo(14.675f).Within(.0001));Assert.That(tank.StunRemaining,Is.EqualTo(.16f));
            player.TakeDamage(.125f,origin+Vector2.up);Assert.That(player.Health,Is.EqualTo(99.875f));
        }
        [UnityTest] public IEnumerator WardenLocksSwingAndExposesRecovery()
        {
            var tank=Enemy(DungeonEnemyKind.Warden,new Vector2(0,1),true);
            yield return new WaitForSeconds(.08f);Assert.That(tank.IsWindingUp,Is.True);Vector2 direction=tank.StrikeDirection;
            player.transform.position=origin+new Vector2(1,1);Physics2D.SyncTransforms();
            yield return new WaitForSeconds(.62f);
            Assert.That(tank.StrikeDirection,Is.EqualTo(direction));Assert.That(player.Health,Is.EqualTo(100));Assert.That(tank.Guarding,Is.False);
            float health=tank.Health;tank.ReceiveHit(.5f,-direction,0);Assert.That(tank.Health,Is.EqualTo(health-.5f));
        }
        [UnityTest] public IEnumerator WardenImpactBeginsAtFrameFourAndHitsOnlyOnce()
        {
            var tank=Enemy(DungeonEnemyKind.Warden,new Vector2(0,1),true);
            while(!tank.IsWindingUp)yield return null;
            while(tank.IsWindingUp) {Assert.That(player.Health,Is.EqualTo(100));yield return null;}
            Assert.That(tank.Phase,Is.EqualTo(DungeonEnemyPhase.Active));Assert.That(tank.AttackFrame,Is.EqualTo(4));
            Assert.That(player.Health,Is.EqualTo(88));
            yield return new WaitForSeconds(.7f);Assert.That(player.Health,Is.EqualTo(88));
        }
        [UnityTest] public IEnumerator CultistShowsGroundWarningAndStunCancelsStrike()
        {
            var cult=Enemy(DungeonEnemyKind.Cultist,new Vector2(0,4.4f),true);
            yield return new WaitForSeconds(.08f);Assert.That(cult.IsWindingUp,Is.True);
            Assert.That(root.GetComponentInChildren<DungeonEnemyArea>(),Is.Not.Null);
            cult.Stun(2f);yield return new WaitForSeconds(1f);
            Assert.That(root.GetComponentInChildren<DungeonEnemyArea>(),Is.Null);Assert.That(player.Health,Is.EqualTo(100));
        }
        [UnityTest] public IEnumerator CultistAppliesOneNonStackingSlow()
        {
            Enemy(DungeonEnemyKind.Cultist,new Vector2(0,4.4f),true);
            yield return new WaitForSeconds(1.05f);Assert.That(player.Health,Is.EqualTo(89));Assert.That(player.MoveSpeed,Is.EqualTo(2.1f).Within(.001f));
            player.ApplySlow(.7f,1f);Assert.That(player.MoveSpeed,Is.EqualTo(2.1f).Within(.001f));
            yield return new WaitForSeconds(1.1f);Assert.That(player.MoveSpeed,Is.EqualTo(3));
        }
        [UnityTest] public IEnumerator WardensRemainSeparatedInNarrowPassage()
        {
            Wall(new Vector2(-.7f,3),new Vector2(.3f,8));
            Wall(new Vector2(.7f,3),new Vector2(.3f,8));
            var front=Enemy(DungeonEnemyKind.Warden,new Vector2(0,2));
            var rear=Enemy(DungeonEnemyKind.Warden,new Vector2(0,3));
            for(int i=0;i<30;i++)
            {
                yield return new WaitForSeconds(.04f);
                Assert.That(Vector2.Distance(front.transform.position,rear.transform.position),Is.GreaterThan(.55f));
                Assert.That(Mathf.Abs(front.transform.position.x-origin.x),Is.LessThan(.3f));
                Assert.That(rear.transform.position.y,Is.GreaterThan(front.transform.position.y));
            }
        }
        [UnityTest] public IEnumerator LostSightDoesNotRevealNewPlayerPosition()
        {
            var ghoul=Enemy(DungeonEnemyKind.Ghoul,new Vector2(0,5),true);
            yield return new WaitForSeconds(.08f);Vector2 known=ghoul.LastKnownTarget;
            Wall(new Vector2(0,2.5f),new Vector2(20,.3f));player.transform.position=origin+Vector2.right*3;Physics2D.SyncTransforms();
            yield return new WaitForSeconds(.5f);Assert.That(ghoul.TargetVisible,Is.False);Assert.That(ghoul.LastKnownTarget,Is.EqualTo(known));Assert.That(ghoul.IsWindingUp,Is.False);
        }
        [UnityTest] public IEnumerator ArbalistDoesNotShootThroughAllyAndFindsSidePosition()
        {
            var tank=Enemy(DungeonEnemyKind.Warden,new Vector2(0,2),true);
            var shooter=Enemy(DungeonEnemyKind.Arbalist,new Vector2(0,5));
            yield return new WaitForSeconds(.08f);Assert.That(shooter.IsWindingUp,Is.False);Assert.That(root.GetComponentInChildren<DungeonEnemyBolt>(),Is.Null);
            yield return new WaitForSeconds(2.5f);Assert.That(Mathf.Abs(shooter.transform.position.x-origin.x),Is.GreaterThan(.3f));
            Assert.That(shooter.AttackSequence,Is.GreaterThan(0));Assert.That(tank.Health,Is.EqualTo(15));
        }
        [UnityTest] public IEnumerator RangedAlliesReserveDistinctCoverAndReplanAfterTankDeath()
        {
            var tank=Enemy(DungeonEnemyKind.Warden,new Vector2(0,2),true);
            var arbalist=Enemy(DungeonEnemyKind.Arbalist,new Vector2(-.8f,5));var cultist=Enemy(DungeonEnemyKind.Cultist,new Vector2(.8f,4.5f));
            yield return new WaitForSeconds(.4f);Assert.That(Vector2.Distance(arbalist.TacticalPosition,cultist.TacticalPosition),Is.GreaterThan(.65f));
            tank.ReceiveHit(1000,Vector2.right,0);yield return new WaitForSeconds(2f);
            Assert.That(arbalist.Cover==null||arbalist.Cover!=tank,Is.True);Assert.That(cultist.Cover==null||cultist.Cover!=tank,Is.True);
            Assert.That(arbalist.AttackSequence+cultist.AttackSequence,Is.GreaterThan(0));
        }
        [UnityTest] public IEnumerator GhoulDashSweepsWallsAndRequiresFullWindup()
        {
            var ghoul=Enemy(DungeonEnemyKind.Ghoul,new Vector2(0,2));Field(ghoul,"random",new System.Random(2));
            yield return new WaitForSeconds(.08f);Assert.That(ghoul.IsWindingUp,Is.True);
            Vector2 start=ghoul.transform.position;Wall(new Vector2(0,1),new Vector2(3,.15f));
            yield return new WaitForSeconds(.35f);Assert.That((Vector2)ghoul.transform.position,Is.EqualTo(start));
            yield return new WaitForSeconds(.4f);Assert.That(ghoul.transform.position.y,Is.GreaterThan(origin.y+1.3f));Assert.That(player.Health,Is.EqualTo(100));
            Assert.That(ghoul.Phase,Is.EqualTo(DungeonEnemyPhase.Recovery));
        }
        [UnityTest] public IEnumerator GhoulFlankIsOneDecisionWithPersistentSideAndCooldown()
        {
            var ghoul=Enemy(DungeonEnemyKind.Ghoul,new Vector2(0,4));Field(ghoul,"random",new System.Random(1));
            yield return new WaitForSeconds(.08f);Assert.That(ghoul.IsFlanking,Is.True);Vector2 destination=ghoul.TacticalPosition;
            yield return new WaitForSeconds(.4f);Assert.That(ghoul.TacticalPosition,Is.EqualTo(destination));
            Assert.That(Mathf.Abs(ghoul.transform.position.x-origin.x),Is.GreaterThan(.1f));
            ghoul.Stun(.2f);float cooldown=ghoul.FlankCooldown;Assert.That(cooldown,Is.InRange(3f,5f));
            yield return new WaitForSeconds(.1f);Assert.That(ghoul.FlankCooldown,Is.LessThan(cooldown));Assert.That(ghoul.IsFlanking,Is.False);
        }
        [UnityTest] public IEnumerator AnimationHitstopPauseAndDeathShareTheCombatClock()
        {
            var enemy=Enemy(DungeonEnemyKind.Warden,new Vector2(0,1),true);
            yield return new WaitForSeconds(.2f);float elapsed=enemy.ActionElapsed;int frame=enemy.AttackFrame;
            Time.timeScale=0;yield return new WaitForSecondsRealtime(.12f);Assert.That(enemy.ActionElapsed,Is.EqualTo(elapsed));Assert.That(enemy.AttackFrame,Is.EqualTo(frame));
            Time.timeScale=1;Field(enemy,"hitStopRemaining",.2f);yield return new WaitForSeconds(.1f);Assert.That(enemy.ActionElapsed,Is.EqualTo(elapsed));
            Field(enemy,"hitStopRemaining",0f);enemy.ReceiveHit(1000,Vector2.right,0);Assert.That(enemy.GetComponent<Collider2D>().enabled,Is.False);
            var renderer=enemy.transform.Find("Enemy visual").GetComponent<SpriteRenderer>();Sprite deathFrame=renderer.sprite;
            yield return new WaitForSeconds(.15f);Assert.That(renderer.sprite,Is.EqualTo(deathFrame));Assert.That(renderer.color.a,Is.InRange(.1f,.8f));
            yield return new WaitForSeconds(.2f);Assert.That(enemy==null,Is.True);
        }
        [UnityTest] public IEnumerator TightCornerFallsBackToTelegraphedAttackWithoutWallPushing()
        {
            var shooter=Enemy(DungeonEnemyKind.Arbalist,new Vector2(0,2.5f));
            Wall(new Vector2(0,3.1f),new Vector2(4,.3f));Wall(new Vector2(-.65f,2.5f),new Vector2(.3f,1.5f));Wall(new Vector2(.65f,2.5f),new Vector2(.3f,1.5f));
            yield return new WaitForSeconds(2.5f);Assert.That(shooter.AttackSequence,Is.GreaterThan(0));
            Assert.That(shooter.transform.position.y,Is.LessThan(origin.y+2.9f));Assert.That(Mathf.Abs(shooter.transform.position.x-origin.x),Is.LessThan(.4f));
        }
    }
}
