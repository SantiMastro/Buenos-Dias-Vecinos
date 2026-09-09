using System.Text;
using BuenosDias.Config;
using UnityEditor;
using UnityEngine;

namespace BuenosDias.EditorTools.Authoring
{
    /// <summary>
    /// Crea los ScriptableObjects del juego y los cablea entre sí.
    ///
    /// No pisa lo que ya exista: correrlo de nuevo completa lo que falte y deja
    /// intacto el balance ya ajustado a mano.
    /// </summary>
    public static class BuildGameDataMenu
    {
        private const string ConfigFolder = ScriptableObjectSeeder.Root + "/Config";

        [MenuItem("Tools/Buenos Días/Fase 3 · Sembrar datos del juego", priority = 300)]
        public static void BuildFromMenu() => Debug.Log(Build());

        /// <summary>Crea y cablea todos los assets de datos. Devuelve un reporte.</summary>
        public static string Build()
        {
            SpriteLibrary.Invalidate();
            int created = 0;

            ReligionDefinition[] religions = GameDataCatalogSeeder.SeedReligions(ref created);
            HouseSignalDefinition[] signals = GameDataCatalogSeeder.SeedSignals(ref created);
            NeighborDefinition[] neighbors = GameDataCatalogSeeder.SeedNeighbors(ref created);
            GameDataCatalogSeeder.SeedEventChannels(ref created);
            created += SceneryCatalogSeeder.Seed();

            var walk = Config<WalkConfig>("WalkConfig", ref created);
            var doorbell = Config<DoorbellConfig>("DoorbellConfig", ref created);
            var wait = Config<WaitConfig>("WaitConfig", ref created);
            var skillcheck = Config<SkillcheckConfig>("SkillcheckConfig", ref created);
            var pity = Config<PityConfig>("PityConfig", ref created);
            var houseGen = Config<HouseGenConfig>("HouseGenConfig", ref created);
            var dayCycle = Config<DayCycleConfig>("DayCycleConfig", ref created);
            var followers = Config<FollowerConfig>("FollowerConfig", ref created);

            WireHouseGeneration(houseGen, signals);

            var game = ScriptableObjectSeeder.GetOrCreate<GameConfig>(
                $"{ConfigFolder}/GameConfig.asset", out bool madeGame);
            if (madeGame) created++;

            ScriptableObjectSeeder.SetList(game, "religions", religions);
            ScriptableObjectSeeder.SetList(game, "neighbors", neighbors);
            ScriptableObjectSeeder.Set(game, "walk", walk);
            ScriptableObjectSeeder.Set(game, "doorbell", doorbell);
            ScriptableObjectSeeder.Set(game, "wait", wait);
            ScriptableObjectSeeder.Set(game, "skillcheck", skillcheck);
            ScriptableObjectSeeder.Set(game, "pity", pity);
            ScriptableObjectSeeder.Set(game, "houseGeneration", houseGen);
            ScriptableObjectSeeder.Set(game, "dayCycle", dayCycle);
            ScriptableObjectSeeder.Set(game, "followers", followers);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return BuildReport(religions.Length, signals.Length, neighbors.Length, created);
        }

        private static void WireHouseGeneration(HouseGenConfig houseGen, Object[] signals)
        {
            ScriptableObjectSeeder.SetList(houseGen, "availableSignals", signals);
            ScriptableObjectSeeder.SetList(houseGen, "wallSprites",
                SpriteLibrary.Load("env_pared_a"),
                SpriteLibrary.Load("env_pared_b"),
                SpriteLibrary.Load("env_pared_c"),
                SpriteLibrary.Load("env_pared_d"),
                SpriteLibrary.Load("env_pared_e"));
        }

        private static T Config<T>(string fileName, ref int created) where T : ScriptableObject
        {
            var asset = ScriptableObjectSeeder.GetOrCreate<T>(
                $"{ConfigFolder}/{fileName}.asset", out bool made);
            if (made) created++;
            return asset;
        }

        private static string BuildReport(int religions, int signals, int neighbors, int created)
        {
            var report = new StringBuilder("[Fase 3] Datos sembrados.\n");
            report.AppendLine($"  Religiones ....... {religions}");
            report.AppendLine($"  Señales .......... {signals}");
            report.AppendLine($"  Vecinos .......... {neighbors}");
            report.AppendLine("  Configs .......... 9 (incluye GameConfig raíz)");
            report.AppendLine("  Canales de evento  7");
            report.AppendLine("  Decorado ......... 3 (árboles, postes, arbustos)");
            report.Append($"Assets creados en esta corrida: {created}. " +
                          "Los que ya existían quedaron intactos.");
            return report.ToString();
        }
    }
}
