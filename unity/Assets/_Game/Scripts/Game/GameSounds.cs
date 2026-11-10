using System.Collections.Generic;
using UnityEngine;

namespace ShinobiDominion.Game
{
    public enum GameSound
    {
        Click,
        Hit,
        Shot,
        Crumble,
        Fall,
        Ability,
        Coin,
        Horn,
        Victory,
        Defeat,
        Star,
        /// <summary>A cannon ball or bomb going off.</summary>
        Blast,
        /// <summary>Magic landing: a Magic Tower bolt, a stun.</summary>
        Zap,
        /// <summary>A heal.</summary>
        Mend,
        /// <summary>An arrow or thrown blade leaving the hand.</summary>
        Arrow,
        /// <summary>The battle clock in its last seconds.</summary>
        Tick,
        /// <summary>One step of a number counting up.</summary>
        Count,
        /// <summary>A spell being cast.</summary>
        Cast,
        /// <summary>A unit landing on the field.</summary>
        Land,
        /// <summary>A war drum: two low thumps.</summary>
        Drum
    }

    /// <summary>
    /// Every sound is made here in code, so the game ships no audio files to license. Short clips are
    /// built once, and the same sound can't stack up in one instant, so big fights stay readable.
    /// Battle sounds play a touch higher or lower each time, so a long fight never sounds like one clip on repeat;
    /// tunes and interface sounds always play at their true pitch.
    /// </summary>
    public static class GameSounds
    {
        private const string MutedKey = "sd_sound_muted";
        private const int Rate = 22050;
        private const float MinimumGap = 0.07f;
        private const int VoiceCount = 4;

        private static readonly Dictionary<GameSound, AudioClip> Clips = new Dictionary<GameSound, AudioClip>();
        private static readonly Dictionary<GameSound, float> LastPlayed = new Dictionary<GameSound, float>();
        private static readonly System.Random Jitter = new System.Random(20261110);
        private static AudioSource _steady;
        private static AudioSource[] _voices;
        private static int _nextVoice;

        public static bool Muted
        {
            get => PlayerPrefs.GetInt(MutedKey, 0) == 1;
            set => PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
        }

        public static void Play(GameSound sound, float volume = 1f)
        {
            if (Muted) return;
            var now = Time.unscaledTime;
            if (LastPlayed.TryGetValue(sound, out var last) && now - last < MinimumGap) return;
            LastPlayed[sound] = now;
            if (_steady == null) CreateSources();
            if (KeepsPitch(sound))
            {
                _steady.PlayOneShot(ClipFor(sound), volume);
                return;
            }
            var voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % VoiceCount;
            voice.pitch = 0.94f + (float)Jitter.NextDouble() * 0.12f;
            voice.PlayOneShot(ClipFor(sound), volume);
        }

        private static void CreateSources()
        {
            var go = new GameObject("GameSounds");
            Object.DontDestroyOnLoad(go);
            _steady = NewSource(go);
            _voices = new AudioSource[VoiceCount];
            for (var i = 0; i < VoiceCount; i++) _voices[i] = NewSource(go);
        }

        private static AudioSource NewSource(GameObject go)
        {
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        /// <summary>Tunes and interface sounds: a bent fanfare or a wobbling coin would sound wrong.</summary>
        private static bool KeepsPitch(GameSound sound)
        {
            switch (sound)
            {
                case GameSound.Click:
                case GameSound.Coin:
                case GameSound.Horn:
                case GameSound.Victory:
                case GameSound.Defeat:
                case GameSound.Star:
                case GameSound.Mend:
                case GameSound.Tick:
                case GameSound.Count:
                case GameSound.Cast:
                    return true;
                default:
                    return false;
            }
        }

        public static AudioClip ClipFor(GameSound sound)
        {
            if (Clips.TryGetValue(sound, out var clip) && clip != null) return clip;
            clip = Build(sound);
            Clips[sound] = clip;
            return clip;
        }

        private static AudioClip Build(GameSound sound)
        {
            switch (sound)
            {
                case GameSound.Click: return Make("click", 0.05f, (t, n) => Tone(t, 720f) * Fade(t, 0.05f, 60f) * 0.5f);
                case GameSound.Hit: return Make("hit", 0.11f, (t, n) => (n * 0.8f + Tone(t, 150f) * 0.6f) * Fade(t, 0.11f, 26f) * 0.7f);
                case GameSound.Shot: return Make("shot", 0.14f, (t, n) => (Tone(t, 900f - t * 4200f) * 0.5f + n * 0.25f) * Fade(t, 0.14f, 18f) * 0.45f);
                case GameSound.Crumble: return Make("crumble", 0.6f, (t, n) => (n * 0.8f + Tone(t, 70f) * 0.7f) * Fade(t, 0.6f, 5f) * 0.8f);
                case GameSound.Fall: return Make("fall", 0.28f, (t, n) => (Tone(t, 260f - t * 500f) * 0.6f + n * 0.15f) * Fade(t, 0.28f, 9f) * 0.6f);
                case GameSound.Ability: return Make("ability", 0.3f, (t, n) => (Tone(t, 420f + t * 1400f) * 0.5f + Tone(t, 840f + t * 2800f) * 0.2f) * Fade(t, 0.3f, 8f) * 0.5f);
                case GameSound.Coin: return Make("coin", 0.22f, (t, n) => Tone(t, t < 0.07f ? 1320f : 1760f) * Fade(t, 0.22f, 14f) * 0.45f);
                case GameSound.Horn: return Make("horn", 0.9f, (t, n) => (Tone(t, 196f) * 0.5f + Tone(t, 294f) * 0.3f + Tone(t, 392f) * 0.15f) * Swell(t, 0.9f) * 0.7f);
                case GameSound.Star: return Make("star", 0.3f, (t, n) => (Tone(t, 1046f) * 0.5f + Tone(t, 1568f) * 0.3f) * Fade(t, 0.3f, 10f) * 0.5f);
                case GameSound.Victory: return Make("victory", 1.1f, (t, n) => Fanfare(t, new[] { 392f, 523f, 659f, 784f }) * 0.5f);
                // A low thump under a burst of noise that dies fast.
                case GameSound.Blast: return Make("blast", 0.45f, (t, n) => (Tone(t, 95f - t * 110f) * 0.9f + n * 0.7f * Mathf.Exp(-t * 14f)) * Fade(t, 0.45f, 7f) * 0.85f);
                // A high tone that wobbles, with a little hiss.
                case GameSound.Zap: return Make("zap", 0.22f, (t, n) => (Tone(t, 1500f + Mathf.Sin(t * 180f) * 500f) * 0.45f + n * 0.3f) * Fade(t, 0.22f, 12f) * 0.5f);
                // Three soft notes going up.
                case GameSound.Mend: return Make("mend", 0.5f, (t, n) => Chime(t, new[] { 659f, 784f, 988f }, 0.11f, 0.5f) * 0.42f);
                // A short rush of air.
                case GameSound.Arrow: return Make("arrow", 0.12f, (t, n) => (n * 0.6f + Tone(t, 1900f - t * 9000f) * 0.2f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.12f)) * 0.5f);
                case GameSound.Tick: return Make("tick", 0.06f, (t, n) => Tone(t, 1250f) * Fade(t, 0.06f, 70f) * 0.5f);
                case GameSound.Count: return Make("count", 0.04f, (t, n) => Tone(t, 1900f) * Fade(t, 0.04f, 90f) * 0.3f);
                // A rising sweep with a second voice a fifth above.
                case GameSound.Cast: return Make("cast", 0.4f, (t, n) => (Tone(t, 300f + t * 1500f) * 0.4f + Tone(t, 450f + t * 2250f) * 0.25f + n * 0.12f) * Swell(t, 0.4f) * 0.6f);
                // A soft thud.
                case GameSound.Land: return Make("land", 0.14f, (t, n) => (Tone(t, 120f - t * 300f) * 0.8f + n * 0.35f * Mathf.Exp(-t * 40f)) * Fade(t, 0.14f, 20f) * 0.7f);
                case GameSound.Drum:
                    return Make("wardrum", 0.5f, (t, n) =>
                    {
                        var local = t < 0.22f ? t : t - 0.22f;
                        return (Tone(t, 80f - local * 60f) * 0.9f + n * 0.3f * Mathf.Exp(-local * 30f)) * Mathf.Exp(-local * 9f) * 0.85f;
                    });
                default: return Make("defeat", 1.1f, (t, n) => Fanfare(t, new[] { 392f, 349f, 311f, 262f }) * 0.45f);
            }
        }

        private static float Tone(float t, float hertz) => Mathf.Sin(2f * Mathf.PI * hertz * t);

        private static float Fade(float t, float length, float sharpness) =>
            Mathf.Exp(-t * sharpness) * Mathf.Clamp01((length - t) / 0.01f);

        private static float Swell(float t, float length) => Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((length - t) / 0.25f);

        private static float Fanfare(float t, float[] notes)
        {
            const float step = 0.22f;
            var index = Mathf.Min((int)(t / step), notes.Length - 1);
            var local = t - index * step;
            return (Tone(t, notes[index]) + Tone(t, notes[index] * 2f) * 0.3f) * Mathf.Exp(-local * (index == notes.Length - 1 ? 2.5f : 6f));
        }

        /// <summary>Soft bell notes one after another, the last one left to ring out.</summary>
        private static float Chime(float t, float[] notes, float step, float length)
        {
            var index = Mathf.Min((int)(t / step), notes.Length - 1);
            var local = t - index * step;
            return (Tone(t, notes[index]) + Tone(t, notes[index] * 2f) * 0.25f) * Mathf.Exp(-local * 7f) * Mathf.Clamp01((length - t) / 0.05f);
        }

        private static AudioClip Make(string name, float seconds, System.Func<float, float, float> wave)
        {
            var count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            // A fixed seed keeps every copy of the game sounding the same.
            var noise = new System.Random(name.Length * 7919);
            for (var i = 0; i < count; i++)
                data[i] = Mathf.Clamp(wave(i / (float)Rate, (float)noise.NextDouble() * 2f - 1f), -1f, 1f);
            var clip = AudioClip.Create(name, count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
