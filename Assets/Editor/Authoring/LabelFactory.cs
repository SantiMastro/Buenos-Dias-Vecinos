using BuenosDias.Config;
using BuenosDias.EditorTools.Building;
using BuenosDias.Presentation;
using UnityEditor;
using UnityEngine;

namespace BuenosDias.EditorTools.Authoring
{
    /// <summary>
    /// Arma carteles de bitmap para los builders.
    ///
    /// Salió de <c>RunStateBuilder</c> cuando el tercer builder pasó a necesitar lo
    /// mismo: la pantalla de religiones, la cinemática de finales y el HUD arman
    /// todos un <see cref="TextLabel"/> con fuente, material sin iluminar, capa y
    /// orden. Tres copias de esa receta serían tres lugares donde arreglar el día
    /// que cambie una.
    /// </summary>
    public static class LabelFactory
    {
        /// <summary>Fuente de bitmap del juego.</summary>
        public const string FontPath = "Assets/ScriptableObjects/Fonts/Fuente6x8.asset";

        /// <summary>Orden por defecto: por encima de todo lo demás de la UI.</summary>
        public const int DefaultOrder = 200;

        /// <summary>Carga la fuente. La necesitan todos los que arman carteles.</summary>
        public static BitmapFontDefinition Font =>
            AssetDatabase.LoadAssetAtPath<BitmapFontDefinition>(FontPath);

        /// <summary>
        /// Un cartel colgado de <paramref name="parent"/>, a
        /// <paramref name="yPixels"/> del CENTRO de la cámara.
        ///
        /// La altura se mide desde el centro y no desde un borde: la Pixel Perfect
        /// Camera cambia el <c>orthographicSize</c> en runtime (§3.8), así que lo
        /// que va pegado a un borde hay que recalcularlo cada cuadro. El centro no
        /// se mueve.
        /// </summary>
        public static TextLabel Create(
            Transform parent, string name, BitmapFontDefinition font,
            float yPixels, int scale, int order = DefaultOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, ProjectConstants.ToUnits(yPixels), 0f);

            var label = go.AddComponent<TextLabel>();
            SerializedFieldUtility.SetReference(label, "font", font);
            SerializedFieldUtility.SetReference(label, "spriteMaterial", UiMaterialFactory.Load());

            var so = new SerializedObject(label);
            so.FindProperty("pixelScale").intValue = scale;
            so.FindProperty("sortingLayer").stringValue = SortingLayerAuthoring.Fx;
            so.FindProperty("sortingOrder").intValue = order;
            so.ApplyModifiedPropertiesWithoutUndo();

            return label;
        }

        /// <summary>
        /// Lo alinea por la IZQUIERDA. Lo quieren los del HUD, que van pegados a un
        /// borde y encadenados uno detrás de otro: centrados, el renglón entero se
        /// movería al pasar de 9 a 10 almas.
        /// </summary>
        public static void LeftAlign(TextLabel label)
        {
            var so = new SerializedObject(label);
            so.FindProperty("alignment").enumValueIndex = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Lo da vuelta: tinta oscura y sombra hueso.
        ///
        /// En hueso sobre el cielo de mediodía da 1,79:1 y no se lee —es el número
        /// que ya estaba medido en <see cref="TextLabel"/>—. Invertido, el violeta
        /// gana 8,18:1 contra ese cielo, y la sombra clara le da el borde que le
        /// falta cuando cae sobre una pared oscura.
        /// </summary>
        public static void InvertInk(TextLabel label)
        {
            var so = new SerializedObject(label);
            so.FindProperty("inkColor").colorValue = new Color32(0x1D, 0x16, 0x38, 0xFF);
            so.FindProperty("shadowColor").colorValue = new Color32(0xF3, 0xEC, 0xE0, 0xFF);
            so.FindProperty("drawShadow").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
