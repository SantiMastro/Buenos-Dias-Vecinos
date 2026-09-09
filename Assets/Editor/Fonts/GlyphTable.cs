using System.Collections.Generic;
using System.Text;

namespace BuenosDias.EditorTools.Fonts
{
    /// <summary>Un glifo leído de la tabla: su carácter y su máscara de tinta.</summary>
    public readonly struct Glyph
    {
        /// <summary>Carácter que representa.</summary>
        public char Character { get; }

        /// <summary>Máscara de tinta, indexada <c>[fila][columna]</c>, fila 0 arriba.</summary>
        public bool[][] Ink { get; }

        /// <summary>Arma el glifo.</summary>
        public Glyph(char character, bool[][] ink)
        {
            Character = character;
            Ink = ink;
        }
    }

    /// <summary>
    /// Lee la tabla de glifos de texto.
    ///
    /// El orden del archivo ES el orden del atlas: una sola fuente de verdad. Si
    /// el orden viviera aparte, agregar un glifo en el medio correría todos los
    /// demás y nadie se enteraría hasta ver texto en jeroglíficos.
    /// </summary>
    public static class GlyphTable
    {
        private const char GlyphMarker = '@';
        private const char InkMarker = '#';
        private const string SpaceKeyword = "SPC";

        /// <summary>
        /// Parsea la tabla. Devuelve los glifos en orden de archivo y, si algo está
        /// mal, deja el motivo en <paramref name="error"/> y devuelve <c>null</c>:
        /// una tabla a medias generaría un atlas corrupto sin avisar.
        /// </summary>
        public static List<Glyph> Parse(string text, int width, int height, out string error)
        {
            error = null;
            var glyphs = new List<Glyph>();
            var seen = new HashSet<char>();
            string[] lines = text.Replace("\r\n", "\n").Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length == 0 || line[0] != GlyphMarker) continue;

                if (!TryReadCharacter(line, out char character))
                {
                    error = $"línea {i + 1}: no se entiende el carácter en '{line}'";
                    return null;
                }

                if (!seen.Add(character))
                {
                    error = $"línea {i + 1}: el carácter '{character}' está dos veces";
                    return null;
                }

                bool[][] ink = ReadInk(lines, i + 1, width, height, character, out error);
                if (ink == null) return null;

                glyphs.Add(new Glyph(character, ink));
                i += height;
            }

            if (glyphs.Count == 0) error = "la tabla no tiene ningún glifo";
            return error == null ? glyphs : null;
        }

        /// <summary>Arma la cadena de charset en el orden en que salieron los glifos.</summary>
        public static string CharsetOf(List<Glyph> glyphs)
        {
            var sb = new StringBuilder(glyphs.Count);
            foreach (Glyph glyph in glyphs) sb.Append(glyph.Character);

            return sb.ToString();
        }

        private static bool TryReadCharacter(string line, out char character)
        {
            string key = line.Substring(1).Trim();
            character = ' ';

            if (key == SpaceKeyword) return true;
            if (key.Length != 1) return false;

            character = key[0];
            return true;
        }

        private static bool[][] ReadInk(
            string[] lines, int from, int width, int height, char character, out string error)
        {
            error = null;
            var ink = new bool[height][];

            for (int row = 0; row < height; row++)
            {
                int index = from + row;
                if (index >= lines.Length)
                {
                    error = $"el glifo '{character}' se corta antes de sus {height} filas";
                    return null;
                }

                string raw = lines[index].Trim();
                if (raw.Length != width)
                {
                    error = $"el glifo '{character}', fila {row}: tiene {raw.Length} " +
                            $"columnas y tienen que ser {width} ('{raw}')";
                    return null;
                }

                ink[row] = new bool[width];
                for (int column = 0; column < width; column++)
                    ink[row][column] = raw[column] == InkMarker;
            }

            return ink;
        }
    }
}
