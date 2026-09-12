using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Arma sprites chicos a partir de un dibujo en texto, fila por fila.
    ///
    /// Para piezas de pocos píxeles —una silueta, una chimenea, una bocanada de
    /// humo— un PNG cuesta más de mantener que de dibujar, y así el dibujo queda
    /// a la vista en el código que lo usa. Es el mismo truco del chevrón de
    /// <see cref="DoorPromptView"/>.
    ///
    /// Los sprites salen con <c>HideAndDontSave</c>: son objetos de runtime, y
    /// quien los pide los guarda y los comparte entre todas las casas.
    /// </summary>
    public static class PixelSprite
    {
        /// <summary>
        /// Arma el sprite. Las filas se leen de arriba abajo, como se escriben;
        /// <paramref name="palette"/> traduce cada carácter a un color, y lo que
        /// no tenga color queda transparente.
        /// </summary>
        public static Sprite FromRows(string[] rows, Vector2 pivot, System.Func<char, Color32> palette)
        {
            int height = rows.Length;
            int width = 0;
            for (int i = 0; i < height; i++) width = Mathf.Max(width, rows[i].Length);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[width * height];
            for (int row = 0; row < height; row++)
            {
                // Las texturas de Unity tienen el origen abajo: la fila se da vuelta.
                string line = rows[height - 1 - row];
                for (int column = 0; column < line.Length; column++)
                    pixels[row * width + column] = palette(line[column]);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            Sprite sprite = Sprite.Create(
                texture, new Rect(0f, 0f, width, height), pivot,
                ProjectConstants.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.HideAndDontSave;

            return sprite;
        }
    }
}
