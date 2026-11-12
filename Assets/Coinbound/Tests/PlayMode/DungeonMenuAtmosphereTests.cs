using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Coinbound.Tests
{
    public sealed class DungeonMenuAtmosphereTests
    {
        [UnityTest]
        public IEnumerator MenuAmbienceFadesOutForStoryAndReturnsAtPausedTime()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            var screen = Object.FindAnyObjectByType<DungeonScreen>();
            var atmosphere = Object.FindAnyObjectByType<DungeonMenuAtmosphere>();
            Assert.That(atmosphere, Is.Not.Null);
            Assert.That(atmosphere.raycastTarget, Is.False);
            var source = screen.GetComponents<AudioSource>().Single(s => s.clip != null && s.clip.name == "menu-ambience");
            Assert.That(source.loop, Is.True);
            Assert.That(source.clip.length, Is.EqualTo(48).Within(.01f));
            Assert.That(source.clip.channels, Is.EqualTo(2));
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(source.volume, Is.GreaterThan(.05f));
            float phase = atmosphere.Phase;
            yield return new WaitForSecondsRealtime(.2f);
            if (!DungeonSettings.ReducedMotion) Assert.That(atmosphere.Phase, Is.Not.EqualTo(phase));
            screen.RequestNewJourney();
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(source.volume, Is.Zero);
            Assert.That(atmosphere.gameObject.activeInHierarchy, Is.False);
            screen.ShowMenu(false);
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(source.volume, Is.GreaterThan(.05f));
            Time.timeScale = 1;
        }
    }
}
