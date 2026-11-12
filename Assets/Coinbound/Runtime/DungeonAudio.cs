using System;
using System.Collections.Generic;
using ProceduralGeneration;
using UnityEngine;

namespace Coinbound
{
    /// <summary>Quiet, locally synthesized dungeon Foley; owns its clips and twelve reusable voices.</summary>
    public sealed class DungeonAudio : MonoBehaviour
    {
        private const int SampleRate = 22050;
        private const int VoiceCount = 12;
        private enum Family { Footsteps, Ambient, Combat }
        private enum Tone { Step, WetStep, Sword, Bow, Block, Hurt, Drop, Creak, Draft, Water }

        private sealed class Sound
        {
            public AudioClip dry;
            public AudioClip room;
        }

        private sealed class Voice
        {
            public AudioSource source;
            public Family family;
            public Vector2 position;
            public float gain;
            public float started;
            public int priority;
            public bool active;
        }

        private readonly List<AudioClip> ownedClips = new List<AudioClip>();
        private readonly Voice[] voices = new Voice[VoiceCount];
        private readonly Sound[] steps = new Sound[4];
        private readonly Sound[] wetSteps = new Sound[3];
        private Sound sword, bow, block, hurt, drop, creak, draft;
        private DungeonMap map;
        private DungeonPlayer player;
        private Transform fountain;
        private System.Random random;
        private Vector2 previousPosition;
        private float previousHealth;
        private int previousBowShot;
        private float previousAttackProgress;
        private float previousBlockCooldown;
        private bool previousAttack;
        private bool swordSoundPlayed;
        private bool paused = true;
        private float stepDistance;
        private float ambientCountdown;
        private float clock;
        private float activityGain;
        private float fountainFade;
        private int lastStep = -1;

        public static DungeonAudio Create(DungeonMap map, DungeonPlayer player, int seed)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (player == null) throw new ArgumentNullException(nameof(player));
            var root = new GameObject("Dungeon audio");
            root.transform.SetParent(player.transform.parent!=null?player.transform.parent.parent:null, false);
            var audio = root.AddComponent<DungeonAudio>();
            audio.Initialize(map, player, seed);
            return audio;
        }

        private void Initialize(DungeonMap dungeon, DungeonPlayer owner, int seed)
        {
            map = dungeon;
            player = owner;
            random = new System.Random(unchecked(seed ^ 0x31415927));
            var synthesis = new System.Random(unchecked(seed ^ 0x5EED1207));
            for (int i = 0; i < VoiceCount; i++)
            {
                var child = new GameObject(i == 0 ? "Fountain water" : "Foley voice " + i);
                child.transform.SetParent(transform, false);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0; // Camera height must not attenuate top-down sounds.
                source.dopplerLevel = 0;
                source.volume = 0;
                source.priority = i == 0 ? 200 : 120;
                voices[i] = new Voice { source = source };
            }

            for (int i = 0; i < steps.Length; i++) steps[i] = MakeSound(Tone.Step, 0.14f, synthesis, i);
            for (int i = 0; i < wetSteps.Length; i++) wetSteps[i] = MakeSound(Tone.WetStep, 0.19f, synthesis, i);
            sword = MakeSound(Tone.Sword, 0.22f, synthesis);
            bow = MakeSound(Tone.Bow, 0.25f, synthesis);
            block = MakeSound(Tone.Block, 0.30f, synthesis);
            hurt = MakeSound(Tone.Hurt, 0.20f, synthesis);
            drop = MakeSound(Tone.Drop, 0.34f, synthesis);
            creak = MakeSound(Tone.Creak, 0.85f, synthesis);
            draft = MakeSound(Tone.Draft, 1.65f, synthesis);
            voices[0].source.clip = MakeClip("Fountain water", SeamlessWater(Synthesize(Tone.Water, 5.3f, synthesis, 0)), false);
            voices[0].source.loop = true;
            voices[0].family = Family.Ambient;
            voices[0].gain = 0.065f;
            Transform dungeonRoot = owner.transform.parent;
            if (dungeonRoot != null)
            {
                foreach (SpriteRenderer renderer in dungeonRoot.GetComponentsInChildren<SpriteRenderer>())
                    if (renderer.name == "object.fountain") { fountain = renderer.transform; break; }
            }
            ambientCountdown = Range(5, 10);
            RememberPlayerState();
        }

        private void LateUpdate()
        {
            bool audible=player!=null&&player.gameObject.activeInHierarchy&&player.CanAct&&Time.timeScale>0;
            activityGain=Mathf.MoveTowards(activityGain,audible?1:0,Time.unscaledDeltaTime*5);
            for(int i=0;i<voices.Length;i++)
                if(voices[i]?.source!=null&&voices[i].active&&player!=null)PositionVoice(voices[i],DungeonFxSettings.Current.soundVolume*(i==0?fountainFade:1));
            if(!audible)
            {
                if(activityGain==0&&voices[0]?.source!=null){voices[0].source.Pause();voices[0].active=false;}
                if(player==null){foreach(var voice in voices)if(voice?.source!=null)voice.source.volume=Mathf.MoveTowards(voice.source.volume,0,Time.unscaledDeltaTime);if(activityGain==0)Destroy(gameObject);}
                else RememberPlayerState();
                return;
            }
            float delta = Time.deltaTime;
            if (delta <= 0) return;
            clock += delta;
            DungeonFxSettings settings = DungeonFxSettings.Current;
            Vector2 position = player.transform.position;

            AdvanceFootsteps(position, settings);
            AdvanceCombat(position, settings);
            ambientCountdown -= delta;
            if (ambientCountdown <= 0)
            {
                ambientCountdown = Range(8, 18);
                if (settings.ambientAudio)
                {
                    Vector2 origin = AmbientPosition(position);
                    int choice = random.Next(5);
                    Play(choice < 3 ? drop : choice == 3 ? creak : draft, origin,
                        choice < 3 ? 0.075f : 0.045f, Family.Ambient, 1, Range(0.94f, 1.06f));
                }
            }
            AdvanceFountain(delta, settings);
            for (int i = 1; i < voices.Length; i++)
            {
                Voice voice = voices[i];
                if (!voice.active) continue;
                if (!voice.source.isPlaying || !Enabled(voice.family, settings))
                {
                    voice.source.Stop();
                    voice.active = false;
                    continue;
                }
                PositionVoice(voice, settings.soundVolume);
            }
            RememberPlayerState();
        }

        private void AdvanceFootsteps(Vector2 position, DungeonFxSettings settings)
        {
            float distance = Vector2.Distance(position, previousPosition);
            if (!settings.footsteps || player.IsRolling || player.IsHitStopped
                || player.MoveDirection.sqrMagnitude < 0.01f || distance > 1.5f)
            {
                stepDistance = 0;
                return;
            }
            stepDistance += distance;
            if (stepDistance < 1.5f) return;
            stepDistance %= 1.5f;
            Sound[] bank = DungeonAtmosphere.IsWet(position) ? wetSteps : steps;
            int index = random.Next(bank.Length);
            if (index == lastStep) index = (index + 1) % bank.Length;
            lastStep = index;
            Play(bank[index], position, Range(0.11f, 0.14f), Family.Footsteps, 2, Range(0.94f, 1.06f));
        }

        private void AdvanceCombat(Vector2 position, DungeonFxSettings settings)
        {
            bool newAttack = player.IsAttacking && (!previousAttack || player.AttackProgress + 0.05f < previousAttackProgress);
            if (newAttack) swordSoundPlayed = false;
            if (!settings.combatAudio) return;
            if (player.BowShotSequence != previousBowShot)
                Play(bow, position, 0.15f, Family.Combat, 3, Range(0.97f, 1.03f));
            if (!player.Hero.IsArcher && player.IsAttacking && !swordSoundPlayed && player.AttackProgress >= 0.28f)
            {
                swordSoundPlayed = true;
                Play(sword, position, 0.14f, Family.Combat, 3, Range(0.94f, 1.06f));
            }
            if (player.BlockCooldownRemaining > previousBlockCooldown + 0.05f)
                Play(block, position, 0.12f, Family.Combat, 4, Range(0.96f, 1.04f));
            if (player.Health < previousHealth)
                Play(hurt, position, 0.17f, Family.Combat, 4, Range(0.95f, 1.02f));
        }

        private void AdvanceFountain(float delta, DungeonFxSettings settings)
        {
            Voice voice = voices[0];
            bool audible = fountain != null && settings.ambientAudio && settings.soundVolume > 0
                && Vector2.Distance(player.transform.position, fountain.position) < 11;
            fountainFade = Mathf.MoveTowards(fountainFade, audible ? 1 : 0, delta * 1.6f);
            if (audible && !voice.active)
            {
                voice.active = true;
                voice.source.time = Range(0, voice.source.clip.length * 0.7f);
                voice.source.Play();
            }
            if (!voice.active) return;
            if (fountainFade <= 0)
            {
                voice.source.Stop();
                voice.active = false;
                return;
            }
            if (fountain != null) voice.position = fountain.position;
            PositionVoice(voice, settings.soundVolume * fountainFade);
        }

        private void Play(Sound sound, Vector2 position, float gain, Family family, int priority, float pitch)
        {
            Voice selected = null;
            for (int i = 1; i < voices.Length; i++)
            {
                Voice voice = voices[i];
                if (!voice.active || !voice.source.isPlaying) { selected = voice; break; }

            }
            if (selected == null) return;
            selected.source.Stop();
            selected.position = position;
            selected.family = family;
            selected.gain = gain;
            selected.priority = priority;
            selected.started = clock;
            selected.active = true;
            selected.source.pitch = pitch;
            selected.source.clip = InRoom(position) ? sound.room : sound.dry;
            PositionVoice(selected, DungeonFxSettings.Current.soundVolume);
            selected.source.Play();
        }

        private void PositionVoice(Voice voice, float master)
        {
            Vector2 listener = player.transform.position;
            Vector2 offset = voice.position - listener;
            float distance = offset.magnitude;
            float attenuation = 1 / (1 + distance * distance / 10);
            attenuation *= 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(7, 12, distance));
            if (distance > 2 && AcrossWall(listener, voice.position)) attenuation *= 0.4f;
            voice.source.panStereo = Mathf.Clamp(offset.x / 8, -0.65f, 0.65f);
            voice.source.volume = Mathf.Clamp01(master * voice.gain * attenuation * activityGain);
        }

        private bool AcrossWall(Vector2 from, Vector2 to)
        {
            int samples = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(from, to)), 1, 16);
            for (int i = 1; i < samples; i++)
                if (!map.IsTileFloor(TileAt(Vector2.Lerp(from, to, (float)i / samples)))) return true;
            return false;
        }

        private bool InRoom(Vector2 position)
        {
            TileKind kind = map.GetTile(TileAt(position));
            return kind == TileKind.RoomFloor || kind == TileKind.Entrance || kind == TileKind.Fountain;
        }

        private static TilePosition TileAt(Vector2 position) =>
            new TilePosition(Mathf.FloorToInt(position.x), -Mathf.FloorToInt(position.y));

        private Vector2 AmbientPosition(Vector2 listener)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float angle = Range(0, Mathf.PI * 2);
                Vector2 position = listener + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Range(3, 7);
                if (map.IsTileFloor(TileAt(position))) return position;
            }
            return listener + Vector2.right * 2;
        }

        private static bool Enabled(Family family, DungeonFxSettings settings) =>
            family == Family.Footsteps ? settings.footsteps : family == Family.Combat ? settings.combatAudio : settings.ambientAudio;

        private void RememberPlayerState()
        {
            previousPosition = player.transform.position;
            previousHealth = player.Health;
            previousBowShot = player.BowShotSequence;
            previousAttack = player.IsAttacking;
            previousAttackProgress = player.AttackProgress;
            previousBlockCooldown = player.BlockCooldownRemaining;
        }

        private void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value;
            foreach (Voice voice in voices)
            {
                if (voice == null || voice.source == null || !voice.active) continue;
                if (value) voice.source.Pause();
                else voice.source.UnPause();
            }
            if (value) stepDistance = 0;
        }

        private float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

        // All waveforms and their faint early reflections are built once during Create.
        // Playback never allocates clips, edits samples, or touches UnityEngine.Random.
        private Sound MakeSound(Tone tone, float seconds, System.Random synthesis, int variant = 0)
        {
            float[] samples = Synthesize(tone, seconds, synthesis, variant);
            return new Sound
            {
                dry = MakeClip(tone + " " + variant, samples, false),
                room = MakeClip(tone + " " + variant + " room", samples, true)
            };
        }

        private AudioClip MakeClip(string title, float[] samples, bool room)
        {
            int tail = room ? (int)(0.10f * SampleRate) : 0;
            var data = new float[samples.Length + tail];
            Array.Copy(samples, data, samples.Length);
            if (room)
            {
                AddReflection(data, samples, 0.029f, 0.065f);
                AddReflection(data, samples, 0.053f, 0.040f);
                AddReflection(data, samples, 0.083f, 0.020f);
            }
            AudioClip clip = AudioClip.Create(title, data.Length, 1, SampleRate, false);
            clip.hideFlags = HideFlags.DontSave;
            clip.SetData(data, 0);
            ownedClips.Add(clip);
            return clip;
        }

        private static void AddReflection(float[] output, float[] source, float seconds, float gain)
        {
            int offset = Mathf.RoundToInt(seconds * SampleRate);
            for (int i = 0; i < source.Length && i + offset < output.Length; i++) output[i + offset] += source[i] * gain;
        }

        private static float[] Synthesize(Tone tone, float seconds, System.Random random, int variant)
        {
            var samples = new float[Mathf.CeilToInt(seconds * SampleRate)];
            float low = 0, slow = 0, phase = 0, peak = 0;
            int period = Mathf.RoundToInt(SampleRate / (165f + variant * 9));
            var stringDelay = new float[period];
            for (int i = 0; i < period; i++) stringDelay[i] = ((float)random.NextDouble() * 2 - 1) * 0.5f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SampleRate;
                float u = t / seconds;
                float noise = (float)random.NextDouble() * 2 - 1;
                low += (noise - low) * (tone == Tone.Draft ? 0.035f : 0.16f);
                slow += (low - slow) * 0.008f;
                float value;
                switch (tone)
                {
                    case Tone.Step:
                    case Tone.WetStep:
                        float contact = Mathf.Exp(-t * (34 + variant * 2));
                        float heel = Mathf.Exp(-Mathf.Max(0, t - 0.018f) * 52) * (t >= 0.018f ? 1 : 0);
                        value = (0.65f * low + 0.12f * noise) * contact
                            + 0.22f * Mathf.Sin(2 * Mathf.PI * (92 + variant * 6) * t) * Mathf.Exp(-t * 48)
                            + 0.15f * low * heel;
                        if (tone == Tone.WetStep)
                        {
                            phase += 2 * Mathf.PI * (760 * Mathf.Exp(-t * 12) + 160) / SampleRate;
                            value += 0.24f * (low - slow) * Mathf.Exp(-t * 15)
                                + 0.035f * Mathf.Sin(phase) * Mathf.Exp(-t * 28);
                        }
                        break;
                    case Tone.Sword:
                        value = (low - slow) * Mathf.Pow(Mathf.Sin(Mathf.PI * u), 1.7f);
                        break;
                    case Tone.Bow:
                        int index = i % period;
                        value = stringDelay[index];
                        stringDelay[index] = (stringDelay[index] + stringDelay[(index + 1) % period]) * 0.493f;
                        value = value * Mathf.Exp(-t * 8) + 0.12f * low * Mathf.Exp(-t * 80);
                        break;
                    case Tone.Block:
                        value = (0.18f * Mathf.Sin(2 * Mathf.PI * 1760 * t)
                            + 0.09f * Mathf.Sin(2 * Mathf.PI * 2573 * t)
                            + 0.045f * Mathf.Sin(2 * Mathf.PI * 3431 * t)) * Mathf.Exp(-t * 27)
                            + 0.18f * noise * Mathf.Exp(-t * 160);
                        break;
                    case Tone.Hurt:
                        value = 0.75f * slow * Mathf.Exp(-t * 19)
                            + 0.30f * low * Mathf.Exp(-t * 38)
                            + 0.12f * Mathf.Sin(2 * Mathf.PI * 71 * t) * Mathf.Exp(-t * 35);
                        break;
                    case Tone.Drop:
                        phase += 2 * Mathf.PI * (650 + 1800 * Mathf.Exp(-t * 46)) / SampleRate;
                        value = 0.18f * Mathf.Sin(phase) * Mathf.Exp(-t * 38)
                            + 0.12f * low * Mathf.Exp(-t * 50);
                        if (t > 0.09f) value += 0.045f * Mathf.Sin(2 * Mathf.PI * 1061 * (t - 0.09f)) * Mathf.Exp(-(t - 0.09f) * 35);
                        break;
                    case Tone.Creak:
                        phase += 2 * Mathf.PI * (125 + 22 * Mathf.Sin(u * Mathf.PI * 1.5f)) / SampleRate;
                        value = (0.30f * low + 0.07f * Mathf.Sin(phase) + 0.025f * Mathf.Sin(phase * 2.71f))
                            * Mathf.Pow(Mathf.Sin(Mathf.PI * u), 2) * (0.72f + 0.28f * Mathf.Sin(t * 53));
                        break;
                    case Tone.Draft:
                        value = (low - slow) * Mathf.Pow(Mathf.Sin(Mathf.PI * u), 2);
                        break;
                    default:
                        value = (low - slow) * (0.60f + 0.10f * Mathf.Sin(t * 3.1f) + 0.07f * Mathf.Sin(t * 8.3f));
                        break;
                }
                // Gentle endpoints remove discontinuities; the long water loop has a
                // natural brief lull instead of a repeated seam or constant loud hiss.
                float fade = tone == Tone.Water ? 0 : 0.004f;
                if(fade>0)value *= Mathf.Clamp01(t / fade) * Mathf.Clamp01((seconds - t) / fade);
                samples[i] = value;
                peak = Mathf.Max(peak, Mathf.Abs(value));
            }
            float normalization = peak > 0 ? 0.65f / peak : 1;
            for (int i = 0; i < samples.Length; i++) samples[i] *= normalization;
            return samples;
        }

        public static float[] SeamlessWater(float[] source)
        {
            int overlap=Mathf.Min(11025,source.Length/4),length=source.Length-overlap;
            var result=new float[length];Array.Copy(source,result,length);
            // Equal-power overlap continues into the original head without a silent dip.
            for(int i=0;i<overlap;i++)
            {
                float t=i/(float)(overlap-1);
                result[i]=source[length+i]*Mathf.Cos(t*Mathf.PI*.5f)+source[i]*Mathf.Sin(t*Mathf.PI*.5f);
            }
            return result;
        }
        private void OnDisable() => SetPaused(true);

        private void OnDestroy()
        {
            foreach (Voice voice in voices)
                if (voice?.source != null) { voice.source.Stop(); voice.source.clip = null; }
            foreach (AudioClip clip in ownedClips)
            {
                if (clip == null) continue;
                if (Application.isPlaying) Destroy(clip);
                else DestroyImmediate(clip);
            }
            ownedClips.Clear();
        }
    }
}
