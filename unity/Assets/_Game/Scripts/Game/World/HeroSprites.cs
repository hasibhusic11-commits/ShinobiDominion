using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>One animated hero (sprite or 3D model) listed in Config/hero_sprites.json, by hero and evolution form; form -1 means every form.</summary>
    public sealed class HeroSpriteEntry
    {
        public string heroId;
        public int form;
        public string path;
        public bool enabled;
        public string note;
    }

    /// <summary>
    /// Finds sprite heroes by hero and evolution form. Each form has its own art and animations, so an
    /// evolution swaps its look by listing another entry. A form with no entry returns null and callers
    /// use the old procedural figure, which remains only as a development fallback.
    /// Nothing here reads or changes hero rules, stats or assignments.
    /// </summary>
    public static class HeroSprites
    {
        private sealed class Document
        {
            public int schemaVersion;
            public List<HeroSpriteEntry> sprites = new List<HeroSpriteEntry>();
        }

        private static Dictionary<string, HeroSpriteEntry> _entries;

        public static bool Has(HeroLook look)
        {
            return look != null && look.DesignId != null && Find(look.DesignId, look.Evolution) != null;
        }

        public static HeroSpriteActor Spawn(HeroLook look, Transform parent)
        {
            if (!Has(look)) return null;
            var entry = Find(look.DesignId, look.Evolution);
            var source = Resources.Load<GameObject>(entry.path);
            if (source == null)
            {
                Debug.LogWarning($"Sprite hero is listed but missing: {entry.path}");
                return null;
            }

            var instance = UnityEngine.Object.Instantiate(source, parent, false);
            instance.name = $"{entry.heroId}_form{entry.form}";
            return instance.GetComponent<HeroSpriteActor>();
        }

        private static HeroSpriteEntry Find(string heroId, int form)
        {
            if (_entries == null)
            {
                var config = Resources.Load<TextAsset>("Config/hero_sprites");
                var document = config != null ? JsonConvert.DeserializeObject<Document>(config.text) : new Document();
                _entries = document.sprites.Where(s => s.enabled).ToDictionary(s => $"{s.heroId}|{s.form}");
            }
            // An entry for form -1 covers every form the hero has no entry of its own for (the 3D models).
            if (_entries.TryGetValue($"{heroId}|{form}", out var entry)) return entry;
            return _entries.TryGetValue($"{heroId}|-1", out var any) ? any : null;
        }
    }
}
