using NUnit.Framework;
using UnityEngine;

namespace Coinbound.Tests
{
    public sealed class DungeonDamageNumberTests
    {
        [Test]
        public void IndependentSwitchesControlNumbersAndCriticalFormatting()
        {
            bool enemy = DungeonSettings.EnemyDamageNumbers, self = DungeonSettings.PlayerDamageNumbers;
            var root = new GameObject("Numbers fixture");
            try
            {
                var obj = new GameObject("Player", typeof(Rigidbody2D), typeof(BoxCollider2D)); obj.transform.SetParent(root.transform);
                var player = obj.AddComponent<DungeonPlayer>(); player.Initialize(DungeonHero.First);
                DungeonSettings.SetToggle("EnemyDamageNumbers", false); DungeonSettings.SetToggle("PlayerDamageNumbers", true);
                DungeonDamageNumber.Create(player, Vector3.zero, 3, false, true);
                Assert.That(root.GetComponentsInChildren<DungeonDamageNumber>(), Is.Empty);
                DungeonDamageNumber.Create(player, Vector3.zero, 2, true, false);
                var label = root.GetComponentInChildren<DungeonPixelText>();
                Assert.That(label.text, Is.EqualTo("2.0")); Assert.That(label.color.a, Is.EqualTo(.4f)); Assert.That(label.color.r, Is.GreaterThan(label.color.g));
                Object.DestroyImmediate(label.transform.parent.gameObject);
                DungeonSettings.SetToggle("PlayerDamageNumbers", false); DungeonSettings.SetToggle("EnemyDamageNumbers", true);
                DungeonDamageNumber.Create(player, Vector3.zero, 2, true, false);
                Assert.That(root.GetComponentsInChildren<DungeonDamageNumber>(), Is.Empty);
                DungeonDamageNumber.Create(player, Vector3.zero, 3, false, true);
                var labels = root.GetComponentsInChildren<DungeonPixelText>();
                Assert.That(labels.Length, Is.EqualTo(2)); Assert.That(labels[0].text, Is.EqualTo("3.0!"));
            }
            finally
            {
                Object.DestroyImmediate(root); DungeonSettings.SetToggle("EnemyDamageNumbers", enemy); DungeonSettings.SetToggle("PlayerDamageNumbers", self);
            }
        }
        [Test]
        public void FrameLimitAppliesWithoutVsyncAndHintsStayVisible()
        {
            int original = DungeonSettings.FrameLimit;
            try
            {
                DungeonSettings.SetFrameLimit(60); Assert.That(Application.targetFrameRate, Is.EqualTo(60)); Assert.That(QualitySettings.vSyncCount, Is.Zero);
                DungeonSettings.SetFrameLimit(-1); Assert.That(Application.targetFrameRate, Is.EqualTo(-1)); Assert.That(DungeonSettings.ShowHints, Is.True);
            }
            finally { DungeonSettings.SetFrameLimit(original); }
        }
    }
}
