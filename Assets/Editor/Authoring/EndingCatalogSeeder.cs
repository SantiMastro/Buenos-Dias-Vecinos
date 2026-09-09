using BuenosDias.Config;
using BuenosDias.EditorTools.Building;
using UnityEditor;
using UnityEngine;

namespace BuenosDias.EditorTools.Authoring
{
    /// <summary>
    /// Crea los tres assets de final con su composición.
    ///
    /// Las posiciones salen de la geometría de los PNG y están todas en píxeles
    /// desde el centro de la cámara. Quedan en el asset para poder correrlas en el
    /// Inspector mirando el resultado, que es la única forma honesta de componer.
    /// </summary>
    public static class EndingCatalogSeeder
    {
        private const string Folder = "Assets/ScriptableObjects/Endings";

        /// <summary>Naranja de atardecer de la paleta, para el final de crucifixión.</summary>
        private static readonly Color Sunset = new Color32(0xEF, 0x72, 0x50, 0xFF);

        /// <summary>Violeta de noche de la paleta.</summary>
        private static readonly Color Night = new Color32(0x1D, 0x16, 0x38, 0xFF);

        /// <summary>
        /// El telón de la ascensión NO es el violeta de noche sino el
        /// <c>#3A3050</c> de la silueta, porque es el relleno de cielo que trae el
        /// PROPIO sprite de nubes — medido en su fila inferior. Con cualquier otro
        /// color, el borde de abajo de las nubes dibuja una raya horizontal que se
        /// lee como un horizonte que no existe.
        /// </summary>
        private static readonly Color CloudSky = new Color32(0x3A, 0x30, 0x50, 0xFF);

        /// <summary>Arma los tres finales y devuelve los assets, en orden de catálogo.</summary>
        public static EndingDefinition[] Build()
        {
            AssetPathUtility.EnsureFolder(Folder);

            return new[]
            {
                BuildCrucifixion(),
                BuildNightfall(),
                BuildAscension()
            };
        }

        /// <summary>
        /// El vendedor que insistió todo el día terminó en el cartel que cargaba.
        /// El remate es el maletín colgando del travesaño: el chiste apunta al
        /// pesado, no a la fe, y por eso no hay ni corona ni heridas ni un solo
        /// símbolo religioso — el arte tampoco los trae.
        /// </summary>
        private static EndingDefinition BuildCrucifixion()
        {
            EndingDefinition asset = Load("Crucifixion");
            var so = new SerializedObject(asset);

            Head(so, EndingKind.Crucifixion, "NI UN ALMA", "", "", true, Sunset);

            SerializedProperty layers = Reset(so);
            Layer(layers, "Cruz", new[] { "cine_cruz" }, 0, 5, 0, 0f);
            Layer(layers, "Silueta", new[] { "cine_silueta_figura" }, 0, 7, 1, 0f);
            Layer(layers, "Maletín", new[] { "cine_maletin_colgando" }, 45, 20, 2, 0.7f);
            Crow(layers, "Cuervo izquierdo", -128, 74, 0, 1.0f);
            Crow(layers, "Cuervo medio", -84, 90, 1, 1.2f);
            Crow(layers, "Cuervo lejano", -152, 56, 2, 1.4f);
            Layer(layers, "Loma", new[] { "cine_loma" }, 0, -70, 10, 0f);

            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        /// <summary>
        /// El final del medio no tapa nada: la calle YA quedó de noche y con las
        /// ventanas prendidas, que es exactamente la imagen que corresponde. Taparla
        /// con un color plano sería borrar lo que el ciclo de día construyó.
        /// </summary>
        private static EndingDefinition BuildNightfall()
        {
            EndingDefinition asset = Load("SeHizoDeNoche");
            var so = new SerializedObject(asset);

            Head(so, EndingKind.SeHizoDeNoche, "SE HIZO DE NOCHE",
                "{0} ALMAS", "UN ALMA", false, Night);
            Reset(so);

            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static EndingDefinition BuildAscension()
        {
            EndingDefinition asset = Load("Ascension");
            var so = new SerializedObject(asset);

            Head(so, EndingKind.Ascension, "TE LLEVÓ CON ÉL",
                "{0} ALMAS", "UN ALMA", true, CloudSky);

            SerializedProperty layers = Reset(so);

            // Las nubes NO ciclan: se abren y se quedan abiertas.
            SerializedProperty clouds = Layer(layers, "Nubes",
                new[] { "cine_nubes_apertura_00", "cine_nubes_apertura_01" }, 0, 58, 0, 0f);
            clouds.FindPropertyRelative("loop").boolValue = false;
            clouds.FindPropertyRelative("framesPerSecond").floatValue = 1.4f;

            // Los rayos GIRAN, y son la única pieza del juego que rota. Rotar
            // pixel art rompe la grilla, pero en un abanico radial de 256×256 con
            // pivot al centro no hay grilla que romper: no tiene líneas rectas
            // horizontales ni verticales que se puedan escalonar.
            SerializedProperty rays = Layer(
                layers, "Rayos", new[] { "cine_rayos_radiales" }, 0, 30, 1, 0.5f);
            rays.FindPropertyRelative("degreesPerSecond").floatValue = 14f;

            Layer(layers, "Haz", new[] { "cine_haz_luz" }, 0, 0, 2, 0.8f);

            // La figura mide 150 px en una pantalla de 216: por debajo de −33 el
            // borde inferior le corta los pies justo en el filo, que se lee como un
            // error y no como que está subiendo.
            //
            // Y SUBE. Antes se quedaba parada dentro del haz, que es lo que hacía
            // que el final se sintiera una lámina y no una cinemática.
            SerializedProperty figure = Layer(
                layers, "Silueta", new[] { "cine_silueta_figura" }, 0, -28, 3, 1.0f);
            figure.FindPropertyRelative("driftPixelsPerSecond").vector2Value =
                new Vector2(0f, 7f);

            SerializedProperty sparkle = Layer(layers, "Destello",
                new[] { "cine_destello_00", "cine_destello_01",
                        "cine_destello_02", "cine_destello_03" }, 0, 24, 4, 1.3f);
            sparkle.FindPropertyRelative("framesPerSecond").floatValue = 10f;

            Flyer(layers, "Folleto 1", -52, -48, 9f, 30f, 5, 1.9f, 11f, 0);
            Flyer(layers, "Folleto 2", 38, -56, -7f, 34f, 6, 2.3f, 14f, 2);
            Flyer(layers, "Folleto 3", -18, -60, 12f, 26f, 7, 2.8f, 9f, 1);
            Flyer(layers, "Folleto 4", 66, -44, -5f, 32f, 8, 3.2f, 13f, 3);

            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        /// <summary>
        /// Un folleto que se va volando.
        ///
        /// Los cuatro PNG <c>ui_folleto_00/22/45/67</c> son la MISMA hoja en cuatro
        /// inclinaciones, así que el giro es una animación de cuadros y no una
        /// rotación de transform: ya viene dibujado, y dibujado no rompe la grilla.
        ///
        /// Los cuatro salen con rumbo, velocidad, momento y cuadro inicial
        /// distintos. Es a propósito: cuatro folletos idénticos subiendo parejos se
        /// leen como una sola cosa repetida cuatro veces, y lo que se busca es que
        /// el aire quede revuelto.
        /// </summary>
        private static void Flyer(
            SerializedProperty layers, string label, int x, int y,
            float driftX, float driftY, int order, float appearAt, float fps, int phase)
        {
            SerializedProperty flyer = Layer(layers, label,
                new[] { "ui_folleto_00", "ui_folleto_22",
                        "ui_folleto_45", "ui_folleto_67" }, x, y, order, appearAt);

            flyer.FindPropertyRelative("framesPerSecond").floatValue = fps;
            flyer.FindPropertyRelative("startFrame").intValue = phase;
            flyer.FindPropertyRelative("driftPixelsPerSecond").vector2Value =
                new Vector2(driftX, driftY);
        }

        /// <summary>Los tres cuervos son la MISMA animación desfasada, no tres dibujos.</summary>
        private static void Crow(
            SerializedProperty layers, string label, int x, int y, int phase, float appearAt)
        {
            SerializedProperty crow = Layer(layers, label,
                new[] { "cine_cuervo_00", "cine_cuervo_01", "cine_cuervo_02" }, x, y, 3, appearAt);

            crow.FindPropertyRelative("framesPerSecond").floatValue = 6f;
            crow.FindPropertyRelative("startFrame").intValue = phase;
        }

        private static void Head(
            SerializedObject so, EndingKind kind, string title, string subtitle,
            string singular, bool hideWorld, Color backdrop)
        {
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("title").stringValue = title;
            so.FindProperty("subtitleFormat").stringValue = subtitle;
            so.FindProperty("subtitleSingular").stringValue = singular;
            so.FindProperty("hideWorld").boolValue = hideWorld;
            so.FindProperty("backdrop").colorValue = backdrop;
        }

        private static SerializedProperty Reset(SerializedObject so)
        {
            SerializedProperty layers = so.FindProperty("layers");
            layers.ClearArray();
            return layers;
        }

        private static SerializedProperty Layer(
            SerializedProperty layers, string label, string[] sprites,
            int x, int y, int order, float appearAt)
        {
            int index = layers.arraySize;
            layers.InsertArrayElementAtIndex(index);
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);

            layer.FindPropertyRelative("label").stringValue = label;
            layer.FindPropertyRelative("positionPixels").vector2IntValue = new Vector2Int(x, y);
            layer.FindPropertyRelative("sortingOrder").intValue = order;
            layer.FindPropertyRelative("appearAtSeconds").floatValue = appearAt;
            layer.FindPropertyRelative("framesPerSecond").floatValue = 8f;
            layer.FindPropertyRelative("loop").boolValue = true;
            layer.FindPropertyRelative("startFrame").intValue = 0;
            layer.FindPropertyRelative("tint").colorValue = Color.white;
            layer.FindPropertyRelative("driftPixelsPerSecond").vector2Value = Vector2.zero;
            layer.FindPropertyRelative("degreesPerSecond").floatValue = 0f;

            SerializedProperty frames = layer.FindPropertyRelative("frames");
            frames.ClearArray();
            for (int i = 0; i < sprites.Length; i++)
            {
                frames.InsertArrayElementAtIndex(i);
                frames.GetArrayElementAtIndex(i).objectReferenceValue =
                    SpriteLibrary.Load(sprites[i]);
            }

            return layer;
        }

        private static EndingDefinition Load(string name)
        {
            string path = $"{Folder}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<EndingDefinition>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<EndingDefinition>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
