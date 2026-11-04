using System;
using NUnit.Framework;
using UnityEngine;

namespace Coinbound.Tests
{
    public sealed class DungeonStatsTests
    {
        [Test]
        public void HeroesUseRequestedStartingStats()
        {
            Assert.That(DungeonHero.First.Stats.MaxHealth, Is.EqualTo(100));
            Assert.That(DungeonHero.Second.Stats.MaxHealth, Is.EqualTo(100));
            Assert.That(DungeonHero.First.Stats.MoveSpeed, Is.EqualTo(3));
            Assert.That(DungeonHero.Second.Stats.MoveSpeed, Is.EqualTo(3.5f));
            Assert.That(DungeonHero.First.RollRecovery, Is.EqualTo(1.5f));
            Assert.That(DungeonHero.Second.RollRecovery, Is.EqualTo(1.5f));
        }

        [Test]
        public void CriticalDamagePreservesHalfPointsAndExactThreshold()
        {
            var stats = new PlayerStats(6, 100, 1, 5, 2, .25f, 5);
            Assert.That(stats.RollDamage(.249f), Is.EqualTo(1.5f));
            Assert.That(stats.RollDamage(.25f), Is.EqualTo(1));
            Assert.That(stats.AttackSeconds, Is.EqualTo(.5f));
        }

        [Test]
        public void DefenseIsAppliedAfterCriticalAndNeverHeals()
        {
            var stats = new EnemyStats(3, .5f, 1, 2, 75);
            Assert.That(stats.ReduceDamage(1.5f), Is.EqualTo(1));
            Assert.That(stats.ReduceDamage(.25f), Is.Zero);
        }

        [Test]
        public void LuckRaisesFavorableChanceAndCapsAtOne()
        {
            var stats = new PlayerStats(6, 100, 1, 5, 2, 0, 20);
            Assert.That(stats.GoodDropChance(), Is.EqualTo(.7f).Within(.0001f));
            Assert.That(stats.RollGoodDrop(.69f), Is.True);
            Assert.That(stats.RollGoodDrop(.71f), Is.False);
            Assert.That(stats.RollGoodDrop(1, .9f), Is.True);
        }

        [Test]
        public void NegativeLuckReducesChanceAndNeverGoesBelowZero()
        {
            var stats = new PlayerStats(100, 100, 2, 3, 2, .05f, -5);
            Assert.That(stats.GoodDropChance(), Is.EqualTo(.45f).Within(.0001f));
            Assert.That(stats.RollGoodDrop(.44f), Is.True);
            Assert.That(stats.RollGoodDrop(.46f), Is.False);
            Assert.That(stats.GoodDropChance(.02f), Is.Zero);
            Assert.That(stats.RollGoodDrop(0, .02f), Is.False);
            Assert.That(DungeonHero.First.Stats.Luck / 100, Is.EqualTo(-.05f));
            Assert.That(DungeonHero.Second.Stats.Luck / 100, Is.EqualTo(-.05f));
        }

        [Test]
        public void InvalidDefinitionsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerStats(6, 100, 1, 5, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerStats(6, 100, 1, 5, 2, 1.1f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyStats(3, float.NaN, 1, 2, 10));
        }

        [Test]
        public void EnemyUsesDefinitionAndAwardsScoreOnlyOnce()
        {
            var playerObject = new GameObject("Stats player", typeof(Rigidbody2D));
            var enemyObject = new GameObject("Stats enemy");
            try
            {
                var player = playerObject.AddComponent<DungeonPlayer>();
                player.Initialize(DungeonHero.Second);
                player.SetInputEnabled(true);
                Assert.That(player.Health, Is.EqualTo(player.Stats.MaxHealth));
                Assert.That(player.Mana, Is.EqualTo(player.Stats.MaxMana));
                var enemy = enemyObject.AddComponent<DungeonEnemy>();
                enemy.Initialize(player, null, new EnemyStats(3, .5f, 2, 3, 75));
                enemy.ReceiveHit(1.5f, Vector2.right, 0);
                Assert.That(enemy.Health, Is.EqualTo(2));
                enemy.ReceiveHit(3, Vector2.right, 0);
                enemy.ReceiveHit(3, Vector2.right, 0);
                Assert.That(enemy.Health, Is.Zero);
                Assert.That(player.Score, Is.EqualTo(75));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyObject);
                UnityEngine.Object.DestroyImmediate(playerObject);
            }
        }
    }
}
