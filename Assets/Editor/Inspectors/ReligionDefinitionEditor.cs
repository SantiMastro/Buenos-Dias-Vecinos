using BuenosDias.Config;
using UnityEditor;
using UnityEngine;

namespace BuenosDias.EditorTools.Inspectors
{
    /// <summary>
    /// Inspector de una religión. Muestra los colores de la paleta como muestras
    /// y traduce los multiplicadores a lenguaje de diseño, para no tener que
    /// acordarse de si 0.80 en ancho de zona es más fácil o más difícil.
    /// </summary>
    [CustomEditor(typeof(ReligionDefinition))]
    public sealed class ReligionDefinitionEditor : Editor
    {
        /// <summary>Dibuja las muestras de color, la lectura y después los campos.</summary>
        public override void OnInspectorGUI()
        {
            var religion = (ReligionDefinition)target;

            DrawSwatches(religion);
            DrawReading(religion);

            if (!religion.CanAscend)
            {
                EditorGUILayout.HelpBox(
                    "Esta opción NO puede llegar al final de ascensión, por más " +
                    "conversiones que junte.",
                    MessageType.Info);
            }

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        private static void DrawSwatches(ReligionDefinition religion)
        {
            EditorGUILayout.LabelField("Paleta", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                Swatch("Camisa", religion.ShirtColor);
                Swatch("Corbata", religion.TieColor);
            }
        }

        private static void Swatch(string label, Color color)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(120)))
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
                Rect rect = GUILayoutUtility.GetRect(110, 24);
                EditorGUI.DrawRect(rect, color);
                EditorGUILayout.LabelField(
                    "#" + ColorUtility.ToHtmlStringRGB(color), EditorStyles.miniLabel);
            }
        }

        private static void DrawReading(ReligionDefinition religion)
        {
            EditorGUILayout.LabelField("Cómo se juega", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Row("Caminata", Describe(religion.WalkSpeed, "más rápido", "más lento"));
                Row("Espera en la puerta", Describe(religion.WaitDuration, "más larga", "más corta"));
                Row("Zona del skillcheck", Describe(religion.ZoneWidth, "más ancha", "más fina"));
                Row("Aguja", Describe(religion.NeedleSpeed, "más rápida", "más lenta"));
                Row("Bono de tiempo", Describe(religion.TimeBonus, "mayor", "menor"));

                if (religion.ExtraChainLinks > 0)
                    Row("Objeciones extra", $"+{religion.ExtraChainLinks} eslabón(es)");
            }
        }

        /// <summary>
        /// Traduce un multiplicador a texto. El porcentaje solo no alcanza: hay
        /// campos donde "más" es mejor y otros donde es peor.
        /// </summary>
        private static string Describe(float multiplier, string above, string below)
        {
            if (Mathf.Approximately(multiplier, 1f)) return "igual que la base";

            float percent = Mathf.Abs(multiplier - 1f) * 100f;
            string direction = multiplier > 1f ? above : below;
            return $"{percent:0}% {direction}  (×{multiplier:0.00})";
        }

        private static void Row(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(150));
                EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
            }
        }
    }
}
