using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Coinbound.Tests
{
    public sealed class DungeonIntroTests
    {
        [UnityTest]
        public IEnumerator IntroHandlesTypingHintsTapHoldAndEscapeWhilePaused()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            var background = InputSystem.settings.backgroundBehavior;
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                var screen = Object.FindAnyObjectByType<DungeonScreen>();
                var game = Object.FindAnyObjectByType<DungeonGame>();
                void SetKeys(params Key[] keys)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
                    InputSystem.Update();
                    screen.SendMessage("UpdateIntro");
                }
                game.ShowHeroSelection();
                Assert.That(Time.timeScale, Is.Zero);
                yield return new WaitForSecondsRealtime(.65f);
                var text = GameObject.Find("Story text").GetComponent<Text>();
                var hints = GameObject.Find("Story hints").GetComponent<CanvasGroup>();
                Assert.That(hints.alpha, Is.Zero);
                Assert.That(hints.GetComponentsInChildren<RawImage>().Length, Is.EqualTo(1));
                Assert.That(hints.GetComponentsInChildren<Text>().Length, Is.EqualTo(1));
                Assert.That(hints.GetComponentInChildren<Text>().text, Is.EqualTo("Пропустить"));
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(text.text, Does.StartWith("…"), "Typing must continue with timeScale zero.");

                SetKeys(Key.A);
                yield return null;
                SetKeys();
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(hints.alpha, Is.GreaterThan(0).And.LessThanOrEqualTo(.32f));

                const string phrase = "Давным-давно, глубоко под землёй, появился Лабиринт.";
                float timeout = Time.realtimeSinceStartup + 5;
                while (!text.text.Contains("Лабиринт") && Time.realtimeSinceStartup < timeout) yield return null;
                Assert.That(text.text, Does.Contain("Лабиринт"));
                Assert.That(text.text, Does.Contain("<color=#00000000>"));

                SetKeys(Key.Space);
                yield return new WaitForSecondsRealtime(.1f);
                SetKeys();
                yield return null;
                yield return null;
                Assert.That(text.text, Is.EqualTo(phrase), "A tap must reveal the current phrase without advancing it.");
                Assert.That(text.color.a, Is.EqualTo(1));
                Assert.That(screen.CurrentPage, Is.EqualTo("Story intro"));

                SetKeys(Key.Space);
                yield return new WaitForSecondsRealtime(.1f);
                SetKeys();
                Assert.That(System.Text.RegularExpressions.Regex.Replace(text.text, "<[^>]+>", ""),
                    Is.EqualTo("Никто не знает, кто его создал."), "Tapping a fully revealed phrase advances to the next one.");

                yield return new WaitForSecondsRealtime(3.5f);
                Assert.That(hints.alpha, Is.Zero, "Hints fade after inactivity.");
                SetKeys(Key.A);
                SetKeys();
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(hints.alpha, Is.GreaterThan(0), "Fresh input reveals hints again.");

                SetKeys(Key.Space);
                yield return new WaitForSecondsRealtime(1f);
                SetKeys();
                yield return null;
                yield return null;
                Assert.That(screen.CurrentPage, Is.EqualTo("Hero selection screen"));

                game.ShowHeroSelection();
                Assert.That(hints.alpha, Is.Zero, "Every new game starts with hidden hints.");
                yield return new WaitForSecondsRealtime(.65f);
                SetKeys(Key.Escape);
                yield return null;
                SetKeys();
                yield return null;
                yield return null;
                Assert.That(screen.CurrentPage, Is.EqualTo("Hero selection screen"), "Escape must not return to the title.");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
                Time.timeScale = 1;
            }
        }
    }
}
