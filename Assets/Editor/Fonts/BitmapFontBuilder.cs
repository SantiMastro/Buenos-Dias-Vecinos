using System.Collections.Generic;
using System.Text;
using BuenosDias.Config;
using BuenosDias.EditorTools.Building;
using UnityEditor;
using UnityEngine;

namespace BuenosDias.EditorTools.Fonts
{
    /// <summary>
    /// Construye el atlas de la fuente desde la tabla de glifos de texto.
    ///
    /// La fuente se genera y no se dibuja porque **una fuente de bitmap no es
    /// arte, es datos**: una máscara de bits por glifo. Un generador de imágenes
    /// devuelve algo que parece texto y no lo es, y dibujarla a mano en un PNG deja
    /// los glifos donde nadie los puede editar sin volver a abrir el PNG.
    ///
    /// Es idempotente: correrlo de nuevo rehace el atlas y el asset.
    /// </summary>
    public static class BitmapFontBuilder
    {
        private const string TablePath = "Assets/Editor/Fonts/fuente_6x8.glifos.txt";
        private const string AtlasPath = "Assets/Sprites/UI/ui_fuente_6x8.png";
        private const string DefinitionFolder = "Assets/ScriptableObjects/Fonts";
        private const string DefinitionPath = DefinitionFolder + "/Fuente6x8.asset";

        private const int CellWidth = 6;
        private const int CellHeight = 8;
        private const int Columns = 16;

        /// <summary>
        /// El atlas va en BLANCO, no en hueso. Es una máscara: quien la dibuja le
        /// pone el color, y así cualquier color de la paleta sale exacto en vez de
        /// salir de multiplicar dos colores.
        /// </summary>
        private static readonly Color32 Ink = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

        private static readonly Color32 Empty = new Color32(0xFF, 0xFF, 0xFF, 0x00);

        [MenuItem("Tools/Buenos Días/Fase 9 · Construir fuente de bitmap", priority = 130)]
        public static void BuildFromMenu()
        {
            string report = Build();
            Debug.Log(report);
        }

        /// <summary>Construye atlas y asset. Devuelve un reporte legible.</summary>
        public static string Build()
        {
            var table = AssetDatabase.LoadAssetAtPath<TextAsset>(TablePath);
            if (table == null) return $"[Fuente] No encontré la tabla en {TablePath}.";

            List<Glyph> glyphs = GlyphTable.Parse(table.text, CellWidth, CellHeight, out string error);
            if (glyphs == null) return $"[Fuente] La tabla está mal: {error}";

            int rows = Mathf.CeilToInt(glyphs.Count / (float)Columns);
            Texture2D atlas = Paint(glyphs, rows);

            System.IO.File.WriteAllBytes(AtlasPath, atlas.EncodeToPNG());
            Object.DestroyImmediate(atlas);
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);

            BitmapFontDefinition definition = WriteDefinition(GlyphTable.CharsetOf(glyphs));
            AssetDatabase.SaveAssets();

            return Report(glyphs, rows, definition);
        }

        /// <summary>
        /// Pinta la grilla. La fila 0 de cada glifo va ARRIBA de su celda: la tabla
        /// se lee como se escribe y las texturas de Unity tienen el origen abajo.
        /// </summary>
        private static Texture2D Paint(List<Glyph> glyphs, int rows)
        {
            int width = Columns * CellWidth;
            int height = rows * CellHeight;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Empty;

            for (int index = 0; index < glyphs.Count; index++)
            {
                int cellX = (index % Columns) * CellWidth;
                int cellTop = (index / Columns) * CellHeight;
                bool[][] ink = glyphs[index].Ink;

                for (int row = 0; row < CellHeight; row++)
                {
                    int y = height - 1 - (cellTop + row);

                    for (int column = 0; column < CellWidth; column++)
                    {
                        if (!ink[row][column]) continue;
                        pixels[y * width + cellX + column] = Ink;
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            return texture;
        }

        private static BitmapFontDefinition WriteDefinition(string charset)
        {
            AssetPathUtility.EnsureFolder(DefinitionFolder);

            var definition = AssetDatabase.LoadAssetAtPath<BitmapFontDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BitmapFontDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            var so = new SerializedObject(definition);
            so.FindProperty("atlas").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            so.FindProperty("charset").stringValue = charset;
            so.FindProperty("cellWidth").intValue = CellWidth;
            so.FindProperty("cellHeight").intValue = CellHeight;
            so.FindProperty("columns").intValue = Columns;
            so.ApplyModifiedPropertiesWithoutUndo();

            return definition;
        }

        private static string Report(List<Glyph> glyphs, int rows, BitmapFontDefinition definition)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Fuente] Atlas construido.");
            sb.AppendLine($"  Glifos.............. {glyphs.Count}");
            sb.AppendLine($"  Grilla.............. {Columns} × {rows} celdas de {CellWidth}×{CellHeight}");
            sb.AppendLine($"  Atlas............... {Columns * CellWidth} × {rows * CellHeight} px");
            sb.AppendLine($"  Ruta................ {AtlasPath}");
            sb.Append($"  Charset............. {definition.Charset}");

            return sb.ToString();
        }
    }
}
