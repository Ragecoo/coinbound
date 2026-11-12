using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Coinbound.Tests
{
    public sealed class DungeonInterfaceTests
    {
        [TestCase(typeof(DungeonVignetteImage))]
        [TestCase(typeof(DungeonUiDecoration))]
        [TestCase(typeof(DungeonHudGlyph))]
        [TestCase(typeof(DungeonDamageBorder))]
        [TestCase(typeof(DungeonMenuAtmosphere))]
        [TestCase(typeof(DungeonMenuWater))]
        [TestCase(typeof(DungeonMenuDust))]
        [TestCase(typeof(DungeonMenuLogo))]
        public void RuntimeGraphicsHaveRendererBeforeFirstCanvasPass(System.Type graphicType)
        {
            var root = new GameObject("Inactive UI fixture", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                root.AddComponent(graphicType);
                // The overlay must discover the renderer when the page is enabled.
                // Accessing Graphic.canvasRenderer here would hide the bug by creating it lazily.
                Assert.That(root.GetComponent<CanvasRenderer>(), Is.Not.Null,
                    "Runtime UI must register its renderer before the initial canvas rebuild.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void DamageFlashPreferenceActuallyControlsVisibleGeometry()
        {
            const string key = "Coinbound.Settings.DamageFlash";
            bool had = PlayerPrefs.HasKey(key);
            int previous = PlayerPrefs.GetInt(key, 1);
            var root = new GameObject("Damage preference fixture", typeof(RectTransform));
            var graphic = root.AddComponent<DungeonDamageBorder>();
            var render = typeof(DungeonDamageBorder).GetMethod("OnPopulateMesh",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new[] { typeof(VertexHelper) }, null);
            using var mesh = new VertexHelper();
            try
            {
                DungeonSettings.SetToggle("DamageFlash", true);
                graphic.Pulse();
                render.Invoke(graphic, new object[] { mesh });
                Assert.That(mesh.currentVertCount, Is.GreaterThan(0), "The user setting must work independently of the authored effects asset.");
                DungeonSettings.SetToggle("DamageFlash", false);
                render.Invoke(graphic, new object[] { mesh });
                Assert.That(mesh.currentVertCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (had) PlayerPrefs.SetInt(key, previous); else PlayerPrefs.DeleteKey(key);
                DungeonSettings.Apply(); DungeonSettings.Save();
            }
        }

        [UnityTest]
        public IEnumerator SelectionSettingsPauseDeathAndRetry()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            var game = Object.FindAnyObjectByType<DungeonGame>();
            var screen = Object.FindAnyObjectByType<DungeonScreen>();
            JsonUtility.FromJsonOverwrite("{\"randomSeed\":false,\"seed\":42,\"minimumStatusSeconds\":0}", game);
            Assert.That(screen.CurrentPage, Is.EqualTo("Start screen"));
            GameObject.Find("Start").GetComponent<Button>().onClick.Invoke();
            Assert.That(screen.CurrentPage, Is.EqualTo("Story intro"));
            Assert.That(GameObject.Find("Story hints").GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(GameObject.Find("Story text").GetComponent<Text>().text, Does.Contain("…"));
            foreach (var icon in GameObject.Find("Story hints").GetComponentsInChildren<RawImage>())
                Assert.That(icon.texture, Is.Not.Null, icon.name);
            Assert.That(screen.HandleBack(), Is.True);
            float introTimeout = Time.realtimeSinceStartup + 3;
            while (screen.CurrentPage == "Story intro" && Time.realtimeSinceStartup < introTimeout) yield return null;
            Assert.That(screen.CurrentPage, Is.EqualTo("Hero selection screen"));
            GameObject.Find("Hero 1").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Create dungeon").GetComponent<Button>().onClick.Invoke();
            float timeout = Time.realtimeSinceStartup + 30;
            while (!game.IsReady && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.That(game.IsReady, Is.True);
            var player = Object.FindAnyObjectByType<DungeonPlayer>();
            Assert.That(player.Hero, Is.SameAs(DungeonHero.Second));
            Assert.That(player.MaxHealth, Is.EqualTo(DungeonHero.Second.StartingHealth));
            Assert.That(player.Mana, Is.EqualTo(100));
            Assert.That(GameObject.Find("Authored HUD"), Is.Not.Null);
            Assert.That(GameObject.Find("Sound placeholder").GetComponent<Button>(), Is.Null);
            Assert.That(GameObject.Find("Sound placeholder").GetComponent<RawImage>().raycastTarget, Is.False);
            Assert.That(GameObject.Find("Block").GetComponent<RawImage>().texture.name, Is.EqualTo("empty_normal"));
            Assert.That(GameObject.Find("Attack").GetComponent<RawImage>().texture.name, Is.EqualTo("bow_normal"));
            Assert.That(GameObject.Find("Mana track").GetComponent<RawImage>().texture, Is.Not.Null);
            Assert.That(GameObject.Find("Stat value health"), Is.Null);
            Assert.That(GameObject.Find("Stat value mana"), Is.Null);
            Assert.That(GameObject.Find("Player stats").GetComponentsInChildren<RawImage>().Length, Is.EqualTo(6));
            Assert.That(GameObject.Find("Stat value critical").GetComponent<Text>().text, Is.EqualTo("0.05"));
            foreach (RawImage icon in GameObject.Find("Player stats").GetComponentsInChildren<RawImage>())
            {
                Assert.That(icon.texture, Is.Not.Null, icon.name);
                Assert.That(icon.raycastTarget, Is.False);
            }
            var statsRect = GameObject.Find("Player stats").GetComponent<RectTransform>();
            var scoreRect = GameObject.Find("Score").GetComponent<RectTransform>();
            Assert.That(statsRect.anchorMax, Is.EqualTo(new Vector2(0, 1)));
            Assert.That(statsRect.anchoredPosition.y, Is.LessThan(scoreRect.anchoredPosition.y - scoreRect.rect.height));
            Assert.That(GameObject.Find("Score").GetComponent<Text>().text, Is.EqualTo("000000000"));
            var enemy = Object.FindAnyObjectByType<DungeonEnemy>();
            enemy.ReceiveHit(100, Vector2.right, 0);
            enemy.ReceiveHit(100, Vector2.right, 0);
            Assert.That(player.Score, Is.EqualTo(100), "A defeated enemy must award points exactly once.");
            yield return new WaitForSecondsRealtime(.1f);
            string score = GameObject.Find("Score").GetComponent<Text>().text;
            Assert.That(score.Length, Is.EqualTo(9));
            Assert.That(int.Parse(score), Is.InRange(1, 100));
            game.ShowMenu();
            Assert.That(screen.CurrentPage, Is.EqualTo("Pause screen"));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(player.CanAct, Is.False);
            var position = player.transform.position;
            screen.ShowSettings();
            Assert.That(screen.CurrentPage, Is.EqualTo("Settings"));
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(player.transform.position, Is.EqualTo(position));
            Assert.That(screen.HandleBack(), Is.True);
            Assert.That(screen.CurrentPage, Is.EqualTo("Pause screen"));
            Assert.That(Time.timeScale, Is.Zero);
            // New-game confirmation from pause keeps the current run until confirmed.
            screen.RequestNewJourney();
            Assert.That(screen.IsModalOpen, Is.True);
            Assert.That(GameObject.Find("Confirm").GetComponentInChildren<Text>().text, Is.EqualTo("Я знаю что делаю"));
            Assert.That(screen.HandleBack(), Is.True);
            Assert.That(game.IsReady, Is.True, "Cancel must preserve the existing run.");
            game.Resume();
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(player.CanAct, Is.True);
            Assert.That(player.TakeDamage(100, player.transform.position + Vector3.up), Is.True);
            Assert.That(screen.CurrentPage, Is.EqualTo("Death screen"));
            Assert.That(Time.timeScale, Is.Zero);
            GameObject.Find("Retry").GetComponent<Button>().onClick.Invoke();
            timeout = Time.realtimeSinceStartup + 30;
            while (!game.IsReady && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.That(game.IsReady, Is.True);
            Assert.That(Object.FindAnyObjectByType<DungeonPlayer>().Hero, Is.SameAs(DungeonHero.Second));
            Assert.That(Object.FindAnyObjectByType<DungeonPlayer>().Score, Is.Zero);
        }

        [Test]
        public void PreferencesApplyAndClampWithoutChangingAuthoredFx()
        {
            const string volumeKey = "Coinbound.Settings.Volume", brightnessKey = "Coinbound.Settings.Brightness";
            bool hadVolume = PlayerPrefs.HasKey(volumeKey), hadBrightness = PlayerPrefs.HasKey(brightnessKey);
            float volume = DungeonSettings.Volume, brightness = DungeonSettings.Brightness;
            string fx = JsonUtility.ToJson(DungeonFxSettings.Current);
            try
            {
                DungeonSettings.SetVolume(-10);
                Assert.That(AudioListener.volume, Is.Zero);
                DungeonSettings.SetBrightness(9);
                Assert.That(DungeonSettings.Brightness, Is.EqualTo(1.35f));
                Assert.That(Shader.GetGlobalFloat("_CoinboundBrightness"), Is.EqualTo(1.35f));
                DungeonSettings.Save();
                Assert.That(PlayerPrefs.GetFloat(brightnessKey), Is.EqualTo(1.35f));
                Assert.That(JsonUtility.ToJson(DungeonFxSettings.Current), Is.EqualTo(fx));
            }
            finally
            {
                if (hadVolume) PlayerPrefs.SetFloat(volumeKey, volume); else PlayerPrefs.DeleteKey(volumeKey);
                if (hadBrightness) PlayerPrefs.SetFloat(brightnessKey, brightness); else PlayerPrefs.DeleteKey(brightnessKey);
                DungeonSettings.Apply(); DungeonSettings.Save();
            }
        }
    }
}
