using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Background music, composed in code as it plays, so the game ships no music files to license. A calm
    /// koto melody over a low drone and soft taiko in the village. In battle the drums and a shaker join and
    /// the melody gets busier. The tune is the same in every copy of the game: it comes from fixed seeds,
    /// not from anything random at run time.
    /// </summary>
    public sealed class GameMusic : MonoBehaviour
    {
        private const string MutedKey = "sd_music_muted";
        private const int Rate = 22050;
        private const float Volume = 0.22f;
        private const double Bpm = 92.0;
        // D minor pentatonic over two octaves: D F G A C.
        private static readonly float[] Scale = { 293.66f, 349.23f, 392.00f, 440.00f, 523.25f, 587.33f, 698.46f, 783.99f };

        private static GameMusic _instance;
        private AudioSource _source;
        private long _sample;
        private volatile float _targetIntensity;
        private float _intensity;
        // The note of the current step and the one before, worked out once per step rather than per sample.
        private long _noteStep = long.MinValue;
        private float _note, _previousNote;
        private bool _plays, _previousPlays;

        /// <summary>0 in the village, 1 in battle. The music eases between them over a few seconds.</summary>
        public static float Intensity
        {
            set { if (_instance != null) _instance._targetIntensity = Mathf.Clamp01(value); }
        }

        public static bool Muted
        {
            get => PlayerPrefs.GetInt(MutedKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
                if (_instance != null) _instance.Apply();
            }
        }

        public static bool Playing => _instance != null && _instance._source != null && _instance._source.isPlaying;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("GameMusic");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GameMusic>();
        }

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
            _source.volume = Volume;
            // A long streamed clip: samples are made as the speaker asks for them, so nothing big sits in memory.
            _source.clip = AudioClip.Create("Music", Rate * 120, 1, Rate, true, Fill);
            Apply();
        }

        private void Apply()
        {
            if (_source == null) return;
            if (Muted) _source.Stop();
            else if (!_source.isPlaying) _source.Play();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ---------- The synthesizer, run on the audio thread: plain maths only, no Unity calls ----------

        private void Fill(float[] data)
        {
            var stepLength = 60.0 / Bpm / 2.0;
            for (var i = 0; i < data.Length; i++)
            {
                // Ease toward the wanted intensity, about two seconds end to end.
                _intensity += (_targetIntensity - _intensity) * 0.00002f;
                var t = _sample / (double)Rate;
                var step = (long)(t / stepLength);
                var local = (float)(t - step * stepLength);
                var value = Drone(t) * 0.22f;
                if (step != _noteStep)
                {
                    if (step == _noteStep + 1)
                    {
                        _previousNote = _note;
                        _previousPlays = _plays;
                    }
                    else _previousNote = NoteFor(step - 1, out _previousPlays);
                    _note = NoteFor(step, out _plays);
                    _noteStep = step;
                }
                // The current note and the tail of the one before it.
                value += Pluck(_note, _plays, local) + Pluck(_previousNote, _previousPlays, local + (float)stepLength) * 0.6f;
                value += Taiko(step, local) * (0.35f + 0.45f * _intensity);
                value += Shaker(step, local) * _intensity * 0.16f;
                data[i] = Mathf.Clamp(value * 0.8f, -1f, 1f);
                _sample++;
            }
        }

        private static float Drone(double t)
        {
            var swell = 0.75f + 0.25f * Mathf.Sin((float)(t * 0.21));
            return (Mathf.Sin((float)(2.0 * Mathf.PI * 73.42 * t)) * 0.6f + Mathf.Sin((float)(2.0 * Mathf.PI * 110.0 * t)) * 0.35f) * swell;
        }

        /// <summary>Whether a step plays a melody note, and which: phrases of 16 steps, each from its own seed.</summary>
        private float NoteFor(long step, out bool plays)
        {
            plays = false;
            if (step < 0) return 0f;
            var phrase = step / 16;
            var position = (int)(step % 16);
            var random = new System.Random((int)(phrase % 64) * 7919 + 17);
            var degree = 2;
            var note = 0f;
            for (var i = 0; i <= position; i++)
            {
                degree = Mathf.Clamp(degree + random.Next(-2, 3), 0, Scale.Length - 1);
                var density = 0.45f + 0.35f * _intensity;
                var on = (float)random.NextDouble() < density || i % 4 == 0;
                if (i == position)
                {
                    plays = on && position != 15;
                    note = Scale[degree];
                }
            }
            return note;
        }

        private static float Pluck(float frequency, bool plays, float age)
        {
            if (!plays || age < 0f) return 0f;
            var phase = 2f * Mathf.PI * frequency * age;
            // A koto-like pluck: a bright attack that dies away fast, with two overtones.
            var tone = Mathf.Sin(phase) * 0.6f + Mathf.Sin(phase * 2f) * 0.25f * Mathf.Exp(-age * 9f) + Mathf.Sin(phase * 3f) * 0.12f * Mathf.Exp(-age * 14f);
            return tone * Mathf.Exp(-age * 4.2f) * Mathf.Clamp01(age / 0.004f) * 0.32f;
        }

        private float Taiko(long step, float age)
        {
            var bar = step % 8;
            var hit = bar == 0 || (bar == 4 && _intensity > 0.3f) || (bar == 6 && _intensity > 0.7f);
            if (!hit) return 0f;
            // A low drum: the pitch falls as it rings.
            var pitch = 55f + 70f * Mathf.Exp(-age * 30f);
            return Mathf.Sin(2f * Mathf.PI * pitch * age) * Mathf.Exp(-age * 9f) * 0.7f;
        }

        private static float Shaker(long step, float age)
        {
            if (step % 2 == 0) return 0f;
            // Cheap, steady noise from the sample time: the same every play.
            var n = Mathf.Sin(age * 91234.567f + step * 12.9898f) * 43758.5453f;
            n -= Mathf.Floor(n);
            return (n * 2f - 1f) * Mathf.Exp(-age * 45f);
        }
    }
}
