using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace Coinbound
{
    public sealed partial class DungeonScreen
    {
        private GameObject intro;
        private Text introText;
        private CanvasGroup introHints;
        private Image introHoldFill;
        private AudioSource introAudio;
        private AudioClip introTick;
        private string[] introLines;
        private int introLine, introLetters, introStartFrame, introExitFrame = -1;
        private float introClock, introNextLetter, introHold, introHintRemaining;
        private bool introInputArmed, introFinishRequested;
        private const float IntroHoldSeconds = .85f;

        private void BuildIntro()
        {
            intro = Layer("Story intro", Color.black);
            introText = Label("Story text", intro.transform, "", 24, Vector2.zero, new Vector2(1000, 260), Pale);
            ((DungeonPixelText)introText).ExtraPixelSteps = 0;
            introText.supportRichText = true;
            var hints = Rect("Story hints", intro.transform, Vector2.zero, new Vector2(220, 42));
            Anchor(hints, new Vector2(1, 0), new Vector2(-32, 28));
            introHints = hints.gameObject.AddComponent<CanvasGroup>();
            introHints.alpha = 0; introHints.blocksRaycasts = false; introHints.interactable = false;
            IntroHint(hints, "Пропустить", new Vector2(-56, 3), new Vector2(108, 32));
            IntroKey(hints, "space", new Vector2(62, 3), new Vector2(96, 32));
            var track = Solid("Hold track", hints, new Vector2(62, -16), new Vector2(96, 2), new Color(1, 1, 1, .16f));
            introHoldFill = Solid("Hold progress", track.transform, Vector2.zero, Vector2.zero, Color.white);
            Stretch(introHoldFill.rectTransform);
            introAudio = intro.AddComponent<AudioSource>();
            introAudio.playOnAwake = false; introAudio.ignoreListenerPause = true;
            introAudio.spatialBlend = 0; introAudio.volume = .22f;
            var samples = new float[660];
            var noise = new System.Random(813);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / 22050f;
                samples[i] = ((float)noise.NextDouble() * 2 - 1) * Mathf.Exp(-t * 210) * .13f
                    + Mathf.Sin(t * 1800 * Mathf.PI * 2) * Mathf.Exp(-t * 280) * .035f;
            }
            introTick = AudioClip.Create("Quiet typing", samples.Length, 1, 22050, false);
            introTick.SetData(samples, 0);
        }

        private void IntroKey(Transform parent, string key, Vector2 pos, Vector2 size)
        {
            var icon = Raw("Key " + key, parent, pos, size);
            icon.texture = Resources.Load<Texture2D>("InterfaceArt/Intro/" + key);
        }

        private void IntroHint(Transform parent, string caption, Vector2 pos, Vector2 size)
        {
            var label = (DungeonPixelText)Label(caption, parent, caption, 14, pos, size, Color.white);
            label.fontSize = 16; label.ExtraPixelSteps = 0;
        }

        public void ShowIntro(DungeonHero hero)
        {
            selectedHero = hero ?? DungeonHero.First;
            var story = Resources.Load<TextAsset>("Story/intro");
            introLines = story != null ? story.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();
            if (introLines.Length == 0) { ShowHeroSelection(selectedHero); return; }
            introLine = 0; introHold = 0;
            introInputArmed = introFinishRequested = false;
            introHints.alpha = 0;
            introHintRemaining = 0;
            introStartFrame = Time.frameCount;
            Switch(intro, null);
            BeginIntroLine();
        }

        private void BeginIntroLine()
        {
            introClock = 0; introNextLetter = .35f; introLetters = 0;
            introText.text = "<color=#00000000>" + introLines[introLine] + "</color>";
        }

        private void NextIntroLine()
        {
            introAudio.Stop();
            if (++introLine >= introLines.Length) introFinishRequested = true;
            else BeginIntroLine();
        }

        private void CompleteIntroLine()
        {
            string line = introLines[introLine];
            if (introLetters == line.Length) { NextIntroLine(); return; }
            introLetters = line.Length;
            introText.text = line;
            introText.color = Pale;
            introAudio.Stop();
            // Preserve a full reading pause after revealing the remaining text.
            introClock = Mathf.Max(introClock, .45f);
            introNextLetter = introClock;
        }

        private void UpdateIntro()
        {
            // Ignore the click/key that opened this page, including a held submit key.
            if (Time.frameCount == introStartFrame || IsTransitioning) return;
            var keyboard = Keyboard.current;
            bool space = keyboard?.spaceKey.isPressed == true;
            if (!introInputArmed)
            {
                introInputArmed = !space && keyboard?.enterKey.isPressed != true;
            }
            else
            {
                foreach (var device in InputSystem.devices)
                    foreach (var control in device.allControls)
                        if (control is ButtonControl button && button.wasPressedThisFrame) introHintRemaining = 2.5f;
                if (keyboard?.escapeKey.wasPressedThisFrame == true) introFinishRequested = true;
                if (space)
                {
                    introHintRemaining = 2.5f;
                    introHold += Time.unscaledDeltaTime;
                    if (introHold >= IntroHoldSeconds) introFinishRequested = true;
                }
                else
                {
                    // A tap reveals this phrase; only a long hold skips the story.
                    if (introHold > 0 && !introFinishRequested) CompleteIntroLine();
                    introHold = 0;
                }
            }
            introHintRemaining = Mathf.Max(0, introHintRemaining - Time.unscaledDeltaTime);
            introHints.alpha = Mathf.MoveTowards(introHints.alpha, introHintRemaining > 0 ? .32f : 0, Time.unscaledDeltaTime * .4f);
            introHoldFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(introHold / IntroHoldSeconds), 1);
            if (introFinishRequested)
            {
                introText.color = Color.Lerp(introText.color, Color.clear, Time.unscaledDeltaTime * 14);
                introAudio.Stop();
                // Consume held input before selecting a button on the next screen.
                if (!space && keyboard?.escapeKey.isPressed != true)
                {
                    introExitFrame = Time.frameCount;
                    ShowHeroSelection(selectedHero);
                }
                return;
            }
            introClock += Time.unscaledDeltaTime;
            string line = introLines[introLine];
            if (introLetters < line.Length && introClock >= introNextLetter)
            {
                char letter = line[introLetters++];
                introNextLetter = introClock + (letter == '…' ? .36f : ".!?".IndexOf(letter) >= 0 ? .24f : letter == ',' ? .12f : .043f);
                // Invisible suffix keeps line wrapping and centering fixed while typing.
                introText.text = line.Substring(0, introLetters) + "<color=#00000000>" + line.Substring(introLetters) + "</color>";
                if (!char.IsWhiteSpace(letter))
                {
                    introAudio.pitch = .94f + introLetters % 5 * .03f;
                    introAudio.PlayOneShot(introTick);
                }
            }
            float readingPause = Mathf.Clamp(line.Length * .028f, 1.6f, 3.8f);
            float fade = introLetters == line.Length ? Mathf.Clamp01((introClock - introNextLetter - readingPause) / .45f) : 0;
            introText.color = new Color(Pale.r, Pale.g, Pale.b, Mathf.Clamp01(introClock / .45f) * (1 - fade));
            if (fade >= 1 && !space) NextIntroLine();
        }
    }
}
