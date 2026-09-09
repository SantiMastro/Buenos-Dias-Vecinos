using BuenosDias.Config;
using UnityEditor;
using UnityEngine;

namespace BuenosDias.EditorTools.Inspectors
{
    /// <summary>
    /// Inspector del asset raíz de balance.
    ///
    /// Arriba de los campos pone un resumen legible de lo que la configuración
    /// significa jugando, y avisa de los errores que solo se notarían en Play:
    /// un config sin asignar o el modo debug de tiempo infinito colado en una build.
    /// </summary>
    [CustomEditor(typeof(GameConfig))]
    public sealed class GameConfigEditor : Editor
    {
        /// <summary>Dibuja el panel de resumen y después los campos normales.</summary>
        public override void OnInspectorGUI()
        {
            var config = (GameConfig)target;

            DrawSummary(config);
            DrawWarnings(config);

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        private static void DrawSummary(GameConfig config)
        {
            EditorGUILayout.LabelField("Resumen de partida", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Row("Duración del día", $"{config.DayDurationSeconds:0.#} s");
                Row("Dificultad máxima a las", $"{config.ConvertsForMaxDifficulty} conversiones");
                Row("Religiones cargadas", config.Religions.Count.ToString());
                Row("Tipos de vecino", config.Neighbors.Count.ToString());
            }

            EditorGUILayout.LabelField("Finales", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Row("0 conversiones", DescribeEnding(config, 0));
                Row("5 conversiones", DescribeEnding(config, 5));
                Row("15 conversiones", DescribeEnding(config, 15));
                EditorGUILayout.LabelField(
                    "Con una religión que no puede ascender, 15 cae en Se hizo de noche.",
                    EditorStyles.miniLabel);
            }
        }

        private static string DescribeEnding(GameConfig config, int converts)
        {
            EndingKind ending = config.ResolveEnding(converts, null);
            return ending switch
            {
                EndingKind.Crucifixion => "Crucifixión",
                EndingKind.Ascension => "Ascensión",
                _ => "Se hizo de noche"
            };
        }

        private static void DrawWarnings(GameConfig config)
        {
            if (config.InfiniteTimeDebug)
            {
                EditorGUILayout.HelpBox(
                    "TIEMPO INFINITO ACTIVO. El día no avanza. " +
                    "Es un modo de prueba: apagalo antes de compilar.",
                    MessageType.Warning);
            }

            string missing = CollectMissing(config);
            if (!string.IsNullOrEmpty(missing))
            {
                EditorGUILayout.HelpBox(
                    $"Faltan configs por asignar: {missing}.\n" +
                    "El juego los necesita para arrancar.",
                    MessageType.Error);
            }

            if (config.Religions.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No hay religiones cargadas: no se puede empezar una partida.",
                    MessageType.Error);
            }
        }

        private static string CollectMissing(GameConfig config)
        {
            var missing = new System.Collections.Generic.List<string>();

            if (config.Walk == null) missing.Add("Caminata");
            if (config.Doorbell == null) missing.Add("Timbre");
            if (config.Wait == null) missing.Add("Espera");
            if (config.Skillcheck == null) missing.Add("Skillcheck");
            if (config.Pity == null) missing.Add("Anti-racha");
            if (config.HouseGeneration == null) missing.Add("Generación de casas");
            if (config.DayCycle == null) missing.Add("Ciclo de día");
            if (config.Followers == null) missing.Add("Comitiva");

            return string.Join(", ", missing);
        }

        private static void Row(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(180));
                EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
            }
        }
    }
}
