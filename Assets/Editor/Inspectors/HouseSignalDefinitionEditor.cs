using BuenosDias.Config;
using UnityEditor;
using UnityEngine;

namespace BuenosDias.EditorTools.Inspectors
{
    /// <summary>
    /// Inspector de una señal. Dibuja el peso como una barra centrada en cero
    /// para que se lea de un vistazo si empuja hacia "hay alguien" o hacia
    /// "está vacía", y cuánto.
    /// </summary>
    [CustomEditor(typeof(HouseSignalDefinition))]
    public sealed class HouseSignalDefinitionEditor : Editor
    {
        private static readonly Color PositiveColor = new Color32(0x6F, 0x8A, 0x5E, 0xFF);
        private static readonly Color NegativeColor = new Color32(0x8A, 0x4B, 0x3D, 0xFF);
        private static readonly Color TrackColor = new Color32(0x3A, 0x30, 0x50, 0x60);

        /// <summary>Dibuja la barra de peso, los avisos y después los campos.</summary>
        public override void OnInspectorGUI()
        {
            var signal = (HouseSignalDefinition)target;

            DrawWeightBar(signal);
            DrawWarnings(signal);

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        private static void DrawWeightBar(HouseSignalDefinition signal)
        {
            EditorGUILayout.LabelField(
                signal.IsPositive ? "Pinta HABITADA" : "Pinta VACÍA", EditorStyles.boldLabel);

            Rect rect = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, TrackColor);

            float center = rect.x + rect.width * 0.5f;
            float half = rect.width * 0.5f;
            float fill = Mathf.Clamp01(Mathf.Abs(signal.Weight)) * half;

            var bar = signal.IsPositive
                ? new Rect(center, rect.y, fill, rect.height)
                : new Rect(center - fill, rect.y, fill, rect.height);

            EditorGUI.DrawRect(bar, signal.IsPositive ? PositiveColor : NegativeColor);
            EditorGUI.DrawRect(new Rect(center - 1f, rect.y, 2f, rect.height), Color.white);

            EditorGUI.LabelField(rect, $"   {signal.Weight:+0.00;-0.00}", EditorStyles.whiteBoldLabel);
        }

        private static void DrawWarnings(HouseSignalDefinition signal)
        {
            if (signal.Sprite == null)
            {
                EditorGUILayout.HelpBox(
                    "Falta el sprite de esta señal: no se va a dibujar nada.",
                    MessageType.Error);
            }

            if (signal.AdditiveLayerAnimation != null && !signal.HasAdditiveLayer)
            {
                EditorGUILayout.HelpBox(
                    "Hay animación de capa pero no hay capa aditiva que animar.",
                    MessageType.Error);
            }

            if (signal.HasAdditiveLayer)
            {
                EditorGUILayout.HelpBox(
                    $"Lleva capa de luz '{signal.AdditiveLayer.name}' en aditivo" +
                    (signal.AdditiveLayerAnimation != null
                        ? $", animada con '{signal.AdditiveLayerAnimation.name}'."
                        : ", quieta."),
                    MessageType.Info);
            }

            if (signal.MinimumLotWidthPixels > 0f)
            {
                EditorGUILayout.HelpBox(
                    $"Solo aparece en terrenos de {signal.MinimumLotWidthPixels:0} px o más. " +
                    "Eso reduce su frecuencia real respecto de las demás señales.",
                    MessageType.Info);
            }
        }
    }
}
