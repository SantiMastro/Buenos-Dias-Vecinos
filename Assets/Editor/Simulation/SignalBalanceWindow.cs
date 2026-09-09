using System.Text;
using BuenosDias.Config;
using BuenosDias.Simulation;
using UnityEditor;
using UnityEngine;

namespace BuenosDias.EditorTools.Simulation
{
    /// <summary>
    /// Corre la generación miles de veces y mide si el balance quedó donde se
    /// diseñó. Es la contraparte de haber sacado la lógica de los MonoBehaviour:
    /// esto corre en milisegundos y sin entrar en Play Mode.
    ///
    /// Mide dos cosas distintas:
    /// 1. Que excluir el auto en terrenos angostos no sesgue las probabilidades.
    /// 2. La peor racha de vacías contando SOLO las casas que pintaban bien, que
    ///    es lo que el jugador percibe como injusto.
    /// </summary>
    public sealed class SignalBalanceWindow : EditorWindow
    {
        private GameConfig gameConfig;
        private int houseCount = 40000;
        private int ringCount = 8000;
        private SignalPickMode pickMode = SignalPickMode.Uniforme;
        private Vector2 scroll;
        private string results = "Todavía no corriste nada.";

        [MenuItem("Tools/Buenos Días/Verificar balance de señales", priority = 400)]
        private static void Open()
        {
            GetWindow<SignalBalanceWindow>("Balance de señales").minSize = new Vector2(520, 400);
        }

        private void OnEnable()
        {
            if (gameConfig != null) return;
            gameConfig = AssetDatabase.LoadAssetAtPath<GameConfig>(
                "Assets/ScriptableObjects/Config/GameConfig.asset");
        }

        private void OnGUI()
        {
            gameConfig = (GameConfig)EditorGUILayout.ObjectField(
                "Game Config", gameConfig, typeof(GameConfig), false);

            houseCount = EditorGUILayout.IntSlider("Casas a generar", houseCount, 1000, 200000);
            ringCount = EditorGUILayout.IntSlider("Timbrazos", ringCount, 1000, 100000);
            pickMode = (SignalPickMode)EditorGUILayout.EnumPopup("Sorteo de señal", pickMode);

            using (new EditorGUI.DisabledScope(gameConfig == null))
            {
                if (GUILayout.Button("Correr", GUILayout.Height(28))) results = Run();
            }

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(results, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private string Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Sorteo: {pickMode}\n");
            AppendWidthBias(sb);
            AppendEmptyRuns(sb);
            return sb.ToString();
        }

        /// <summary>
        /// Compara los dos rangos de ancho. El auto solo entra desde 216 px, así
        /// que si la sustitución estuviera mal hecha los terrenos angostos serían
        /// sistemáticamente más negativos.
        /// </summary>
        private void AppendWidthBias(StringBuilder sb)
        {
            HouseGenConfig houses = gameConfig.HouseGeneration;
            var generator = new HouseLayoutGenerator(houses, gameConfig.Pity, 12345, pickMode);

            var narrow = new Bucket();
            var wide = new Bucket();
            float threshold = FindCarThreshold(houses);

            for (int i = 0; i < houseCount; i++)
            {
                HouseLayout layout = generator.Next(0);
                Bucket bucket = layout.LotWidthPixels < threshold ? narrow : wide;
                bucket.Add(layout);
            }

            sb.AppendLine("=== SESGO POR ANCHO DE TERRENO ===");
            sb.AppendLine($"Corte: {threshold:0} px (mínimo que pide el auto)\n");
            sb.AppendLine($"                       angostos      anchos      dif");
            sb.AppendLine($"  casas                {narrow.Count,9}   {wide.Count,9}");
            sb.AppendLine($"  chance media         {narrow.MeanChance,9:0.0000}   {wide.MeanChance,9:0.0000}   " +
                          $"{narrow.MeanChance - wide.MeanChance,+8:+0.0000;-0.0000}");
            sb.AppendLine($"  con >=1 positiva     {narrow.PositiveRatio,9:0.0000}   {wide.PositiveRatio,9:0.0000}   " +
                          $"{narrow.PositiveRatio - wide.PositiveRatio,+8:+0.0000;-0.0000}");
            sb.AppendLine($"  señales por casa     {narrow.MeanSignals,9:0.0000}   {wide.MeanSignals,9:0.0000}   " +
                          $"{narrow.MeanSignals - wide.MeanSignals,+8:+0.0000;-0.0000}");

            float chanceGap = Mathf.Abs(narrow.MeanChance - wide.MeanChance);
            float ratioGap = Mathf.Abs(narrow.PositiveRatio - wide.PositiveRatio);
            sb.AppendLine();
            sb.AppendLine(chanceGap < 0.01f && ratioGap < 0.01f
                ? "  VEREDICTO: sin sesgo material (ambas diferencias < 0.01)."
                : "  VEREDICTO: HAY SESGO. Revisar el sorteo de reemplazo.");
            sb.AppendLine();
        }

        /// <summary>
        /// Peor racha de vacías contando solo timbrazos a casas legibles. Ese es
        /// el número que importa: cuántas veces seguidas te clava una casa que
        /// pintaba bien.
        /// </summary>
        private void AppendEmptyRuns(StringBuilder sb)
        {
            var generator = new HouseLayoutGenerator(
                gameConfig.HouseGeneration, gameConfig.Pity, 999, pickMode);
            var tracker = new PityTracker(gameConfig.Pity);
            var random = new System.Random(4242);

            int worstReadable = 0, currentReadable = 0;
            int worstAll = 0, currentAll = 0;
            int readableRings = 0, occupied = 0;

            for (int i = 0; i < ringCount; i++)
            {
                HouseLayout layout = generator.Next(tracker.EmptyRun);
                bool readable = gameConfig.Pity.IsReadable(layout.Chance);
                bool isOccupied = tracker.Resolve(layout.Chance, random);

                if (isOccupied) occupied++;

                currentAll = isOccupied ? 0 : currentAll + 1;
                worstAll = Mathf.Max(worstAll, currentAll);

                // La racha se corta con CUALQUIER casa que abra, no solo con una
                // legible. Si el jugador toca una que pintaba muerta y le abre, se
                // llevó una conversión: la racha de frustración se cortó ahí.
                // Contando solo los resets legibles el número da 4 en vez de 3, y
                // ese 4 son tres decepciones con un éxito en el medio.
                if (isOccupied) currentReadable = 0;

                if (!readable) continue;
                readableRings++;
                if (!isOccupied) currentReadable++;
                worstReadable = Mathf.Max(worstReadable, currentReadable);
            }

            sb.AppendLine("=== RACHAS DE CASAS VACÍAS ===");
            sb.AppendLine($"  timbrazos                     {ringCount}");
            sb.AppendLine($"  de esos, a casas legibles     {readableRings} ({readableRings / (float)ringCount:0.0%})");
            sb.AppendLine($"  tasa de apertura              {occupied / (float)ringCount:0.0%}");
            sb.AppendLine($"  peor racha (todas)            {worstAll}");
            sb.AppendLine($"  peor racha (solo legibles)    {worstReadable}   <-- el que importa");
            sb.AppendLine();
            sb.AppendLine(worstReadable <= 3
                ? "  VEREDICTO: dentro de lo diseñado (<= 3)."
                : "  VEREDICTO: SE DISPARA. Bajar el incremento del pity.");
        }

        private static float FindCarThreshold(HouseGenConfig houses)
        {
            float threshold = 0f;
            foreach (HouseSignalDefinition signal in houses.AvailableSignals)
                if (signal != null) threshold = Mathf.Max(threshold, signal.MinimumLotWidthPixels);
            return threshold > 0f ? threshold : houses.LotWidthMinPixels;
        }

        private sealed class Bucket
        {
            private float chanceSum;
            private int withPositive;
            private int signalSum;

            public int Count { get; private set; }
            public float MeanChance => Count == 0 ? 0f : chanceSum / Count;
            public float PositiveRatio => Count == 0 ? 0f : withPositive / (float)Count;
            public float MeanSignals => Count == 0 ? 0f : signalSum / (float)Count;

            public void Add(HouseLayout layout)
            {
                Count++;
                chanceSum += layout.Chance;
                signalSum += layout.Signals.Count;
                if (layout.PositiveSignalCount > 0) withPositive++;
            }
        }
    }
}
