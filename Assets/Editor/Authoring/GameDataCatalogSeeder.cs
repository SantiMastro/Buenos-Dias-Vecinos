using BuenosDias.Config;
using BuenosDias.Core;
using BuenosDias.EditorTools.Building;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BuenosDias.EditorTools.Authoring
{
    /// <summary>
    /// Siembra los catálogos: religiones, señales, vecinos y canales de evento.
    ///
    /// Los valores son los tuneados en el prototipo y validados por playtesting.
    /// Están acá una sola vez, en el momento de crear el asset; a partir de ahí
    /// la fuente de verdad es el asset y esto no lo vuelve a pisar.
    /// </summary>
    public static class GameDataCatalogSeeder
    {
        private const string ReligionFolder = ScriptableObjectSeeder.Root + "/Religions";
        private const string SignalFolder = ScriptableObjectSeeder.Root + "/Signals";
        private const string NeighborFolder = ScriptableObjectSeeder.Root + "/Neighbors";
        private const string EventFolder = ScriptableObjectSeeder.Root + "/Events";

        /// <summary>Las cinco religiones jugables.</summary>
        public static ReligionDefinition[] SeedReligions(ref int created)
        {
            var rows = new[]
            {
                ("Testigos",     "testigos",     1.00f, 1.00f, 1.00f, 1.00f, 0, 1.00f, true,  "F3ECE0", "7A2F3D"),
                ("Mormones",     "mormones",     1.30f, 1.00f, 0.80f, 1.00f, 0, 1.00f, true,  "F3ECE0", "3A3A7D"),
                ("Budistas",     "budistas",     0.92f, 1.00f, 1.00f, 0.75f, 1, 1.00f, true,  "EF7250", "A8455A"),
                ("Evangelistas", "evangelistas", 1.00f, 1.40f, 1.35f, 1.00f, 0, 1.00f, true,  "F3ECE0", "6F8A5E"),
                ("Aspiradoras",  "aspiradoras",  1.10f, 0.82f, 1.00f, 1.10f, 0, 1.60f, false, "C9C2B4", "5C5566"),
            };

            var result = new ReligionDefinition[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                var asset = ScriptableObjectSeeder.GetOrCreate<ReligionDefinition>(
                    $"{ReligionFolder}/{r.Item1}.asset", out bool made);
                result[i] = asset;
                if (!made) continue;

                created++;
                ScriptableObjectSeeder.Set(asset, "displayName", r.Item1.ToUpperInvariant());
                ScriptableObjectSeeder.Set(asset, "id", r.Item2);
                ScriptableObjectSeeder.Set(asset, "walkSpeed", r.Item3);
                ScriptableObjectSeeder.Set(asset, "waitDuration", r.Item4);
                ScriptableObjectSeeder.Set(asset, "zoneWidth", r.Item5);
                ScriptableObjectSeeder.Set(asset, "needleSpeed", r.Item6);
                ScriptableObjectSeeder.Set(asset, "extraChainLinks", r.Item7);
                ScriptableObjectSeeder.Set(asset, "timeBonus", r.Item8);
                ScriptableObjectSeeder.Set(asset, "canAscend", r.Item9);
                ScriptableObjectSeeder.Set(asset, "shirtColor", Hex(r.Item10));
                ScriptableObjectSeeder.Set(asset, "tieColor", Hex(r.Item11));
            }
            return result;
        }

        /// <summary>
        /// Las siete señales de la spec. Cuatro de ellas no son "un sprite en un
        /// ancla": el pasto es una franja tileada, la luz de entrada y el TV
        /// llevan capa aditiva, y persianas y TV pisan el sprite de WindowA.
        /// </summary>
        public static HouseSignalDefinition[] SeedSignals(ref int created)
        {
            // nombre, display, peso, montaje, sprite, capa aditiva, ancho mínimo
            // El farol es un APLIQUE: el sprite tiene la placa de sujeción en la
            // columna izquierda y el brazo saliendo a la derecha. Va en la pared
            // junto al marco de la puerta, no apoyado en el piso.
            var rows = new[]
            {
                ("LuzEntrada",     "Luz de entrada prendida",  0.30f, SignalMountMode.PropEnAnclaDePared,  "prop_farol_entrada",   "fx_farol_luz",   0f),
                ("TvParpadeando",  "TV parpadeando",           0.35f, SignalMountMode.VarianteDeVentana,   "env_ventana_tv",       "fx_ventana_luz", 0f),
                ("Auto",           "Auto estacionado",         0.25f, SignalMountMode.PropEnAnclaDeSuelo,  "prop_auto",            null,             216f),
                ("RopaTendida",    "Ropa tendida",             0.15f, SignalMountMode.PropEnAnclaDePared,  "prop_ropa_tendida",    null,             0f),
                ("PersianasBajas", "Persianas bajas",         -0.35f, SignalMountMode.VarianteDeVentana,   "env_ventana_persiana", null,             0f),
                ("BuzonLleno",     "Buzón desbordado",        -0.40f, SignalMountMode.PropEnAnclaDePared,  "prop_buzon_lleno",     null,             0f),
                ("PastoCrecido",   "Pasto crecido",           -0.22f, SignalMountMode.FranjaTileada,       "prop_pasto_alto",      null,             0f),
            };

            AnimationClip flicker = SignalAssetFactory.BuildTelevisionFlicker();

            var result = new HouseSignalDefinition[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                var s = rows[i];
                var asset = ScriptableObjectSeeder.GetOrCreate<HouseSignalDefinition>(
                    $"{SignalFolder}/{s.Item1}.asset", out bool made);
                result[i] = asset;
                if (!made) continue;

                created++;
                ScriptableObjectSeeder.Set(asset, "displayName", s.Item2);
                ScriptableObjectSeeder.Set(asset, "weight", s.Item3);
                ScriptableObjectSeeder.Set(asset, "mountMode", (int)s.Item4);
                ScriptableObjectSeeder.Set(asset, "sprite", SpriteLibrary.Load(s.Item5));
                ScriptableObjectSeeder.Set(asset, "minimumLotWidthPixels", s.Item7);

                if (s.Item6 != null)
                    ScriptableObjectSeeder.Set(asset, "additiveLayer", SpriteLibrary.Load(s.Item6));

                // Solo el TV parpadea. El halo del farol es una luz quieta.
                if (s.Item1 == "TvParpadeando")
                    ScriptableObjectSeeder.Set(asset, "additiveLayerAnimation", flicker);

                // El farol reclama el ancla pegada al marco; buzón y ropa se corren.
                if (s.Item1 == "LuzEntrada")
                    ScriptableObjectSeeder.Set(asset, "claimsDoorSideAnchor", true);
            }
            return result;
        }

        /// <summary>Los tipos de vecino, cableados a sus overrides de animación.</summary>
        public static NeighborDefinition[] SeedNeighbors(ref int created)
        {
            string[] types = { "Abuela", "Madre", "Musculosa", "Oficinista", "Pibe" };
            var result = new NeighborDefinition[types.Length];

            for (int i = 0; i < types.Length; i++)
            {
                var asset = ScriptableObjectSeeder.GetOrCreate<NeighborDefinition>(
                    $"{NeighborFolder}/{types[i]}.asset", out bool made);
                result[i] = asset;
                if (!made) continue;

                created++;
                ScriptableObjectSeeder.Set(asset, "displayName", types[i]);
                ScriptableObjectSeeder.Set(asset, "animatorOverride",
                    AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                        $"Assets/Animations/Vecinos/Vecino_{types[i]}.overrideController"));
            }
            return result;
        }

        /// <summary>
        /// Canales de evento. Cada tipo vive en su propio .cs porque Unity solo
        /// encuentra el script de un ScriptableObject si el archivo se llama
        /// igual que la clase; agruparlos deja los assets con el script roto.
        /// </summary>
        public static void SeedEventChannels(ref int created)
        {
            Channel<FloatEventChannel>("TiempoRestante", ref created);
            Channel<IntEventChannel>("Conversiones", ref created);
            Channel<IntEventChannel>("Seguidores", ref created);
            Channel<VoidEventChannel>("TimbreTocado", ref created);
            Channel<VoidEventChannel>("DiaTerminado", ref created);
            Channel<BoolEventChannel>("PuertaResuelta", ref created);
            Channel<StringEventChannel>("ReplicaVecino", ref created);
        }

        private static void Channel<T>(string fileName, ref int created) where T : ScriptableObject
        {
            ScriptableObjectSeeder.GetOrCreate<T>($"{EventFolder}/{fileName}.asset", out bool made);
            if (made) created++;
        }

        private static Color Hex(string rgb)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out Color color);
            return color;
        }
    }
}
