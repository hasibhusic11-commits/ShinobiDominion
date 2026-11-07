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
        Star
    }

    /// <summary>
    /// Every sound is made here in code, so the game ships no audio files to license. Short clips are
    /// built once, and the same sound can't stack up in one instant, so big fights stay readable.
    /// </summary>
    public static class GameSounds
    {
        private const string MutedKey = "sd_sound_muted";
        private const int Rate = 22050;
        private const float MinimumGap = 0.07f;

        private static readonly Dictionary<GameSound, AudioClip> Clips = new Dictionary<GameSound, AudioClip>();
        private static readonly Dictionary<GameSound, float> LastPlayed = new Dictionary<GameSound, float>();
        private static AudioSource _source;

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
            if (_source == null)
            {
                var go = new GameObject("GameSounds");
                Object.DontDestroyOnLoad(go);
                _source = go.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
            }
            _source.PlayOneShot(ClipFor(sound), volume);
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
