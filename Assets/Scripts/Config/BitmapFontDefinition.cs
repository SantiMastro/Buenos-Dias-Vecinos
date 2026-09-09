using UnityEngine;

namespace BuenosDias.Config
{
    /// <summary>
    /// Una fuente de bitmap monoespaciada: el atlas y dónde cae cada carácter.
    ///
    /// El atlas se genera desde <c>Assets/Editor/Fonts/fuente_6x8.glifos.txt</c>,
    /// que es la fuente de verdad de los dibujos. Este asset es lo único que el
    /// juego necesita para leerlo, y existe para que medidas y orden de caracteres
    /// se puedan mirar en el Inspector sin abrir un <c>.cs</c>.
    ///
    /// ⚠️ El atlas está pintado de **blanco**, no del color final: es una máscara,
    /// y quien la dibuja le pone el color. Con el atlas ya pintado en hueso,
    /// cualquier otro color saldría de multiplicar dos colores y no caería exacto
    /// en la paleta. Pintado en blanco, el tinte ES el color.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Fuente", menuName = "Buenos Días/Fuente de bitmap", order = 30)]
    public sealed class BitmapFontDefinition : ScriptableObject
    {
        [Header("Atlas")]
        [Tooltip("Textura con todos los glifos en grilla. La genera el constructor " +
                 "de fuentes; no se dibuja a mano.")]
        [SerializeField] private Texture2D atlas;

        [Tooltip("Caracteres en el MISMO orden en que están en el atlas, de " +
                 "izquierda a derecha y de arriba abajo. Lo escribe el constructor.")]
        [SerializeField, TextArea(2, 5)] private string charset = string.Empty;

        [Header("Medidas de la celda, en píxeles")]
        [Tooltip("Ancho de celda. Incluye la separación entre letras: es " +
                 "monoespaciada, así que el hueco es parte del glifo.")]
        [SerializeField, Min(1)] private int cellWidth = 6;

        [Tooltip("Alto de celda: acento, caja de mayúscula y descendente.")]
        [SerializeField, Min(1)] private int cellHeight = 8;

        [Tooltip("Cuántas celdas por fila tiene el atlas.")]
        [SerializeField, Min(1)] private int columns = 16;

        /// <summary>Textura con los glifos.</summary>
        public Texture2D Atlas => atlas;

        /// <summary>Caracteres en el orden del atlas.</summary>
        public string Charset => charset;

        /// <summary>Ancho de una celda, en píxeles.</summary>
        public int CellWidth => cellWidth;

        /// <summary>Alto de una celda, en píxeles.</summary>
        public int CellHeight => cellHeight;

        /// <summary>Cuántos glifos tiene.</summary>
        public int GlyphCount => charset != null ? charset.Length : 0;

        /// <summary>
        /// Posición de un carácter en el atlas, o <c>-1</c> si la fuente no lo
        /// tiene. Las minúsculas se mapean a su mayúscula: la fuente es de caja
        /// alta y el juego es todo carteles cortos.
        /// </summary>
        public int IndexOf(char character)
        {
            if (string.IsNullOrEmpty(charset)) return -1;
            return charset.IndexOf(char.ToUpperInvariant(character));
        }

        /// <summary>
        /// Recorte del glifo dentro de la textura.
        ///
        /// La Y se da vuelta: la tabla de glifos se lee de arriba abajo, como se
        /// escribe, y las texturas de Unity tienen el origen abajo a la izquierda.
        /// </summary>
        public Rect RectFor(int index)
        {
            int column = index % columns;
            int row = index / columns;

            return new Rect(
                column * cellWidth,
                atlas.height - (row + 1) * cellHeight,
                cellWidth,
                cellHeight);
        }
    }
}
