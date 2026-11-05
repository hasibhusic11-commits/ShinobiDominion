using System;
using System.IO;
using ShinobiDominion.Core;

namespace ShinobiDominion.Tests
{
    /// <summary>Loads the real shipped config, so tests fail if the game's data breaks a rule.</summary>
    internal static class TestCatalog
    {
        private static string _json;

        public static string Json
        {
            get
            {
                if (_json != null) return _json;
                var candidates = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "Config", "buildings.json"),       // .NET harness
                    Path.Combine("Assets", "_Game", "Resources", "Config", "buildings.json")  // Unity
                };
                foreach (var path in candidates)
                    if (File.Exists(path))
                        return _json = File.ReadAllText(path);
                throw new FileNotFoundException("buildings.json not found.");
            }
        }

        public static BuildingCatalog Load() => BuildingCatalog.FromJson(Json);
        public static Village EmptyVillage() => new Village(Load());
    }
}
