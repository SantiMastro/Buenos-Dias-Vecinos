using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Fabrica sprites de un solo color, para las piezas de UI que son rectángulos
    /// planos y no vale la pena que existan como PNG.
    ///
    /// Se usan con <c>SpriteRenderer.drawMode = Tiled</c> y <c>size</c>, NO con
    /// escala: escalar un sprite en pixel art rompe la grilla, y una barra que se
    /// vacía cambiaría de escala en cada cuadro. Con <c>size</c> lo que cambia es
    /// la malla y el texel sigue midiendo un píxel.
    /// </summary>
    public static class SolidSprite
    {
        /// <summary>
        /// Un cuadrado del color pedido. El lado por defecto es 4 px: con 1 px
        /// Unity se queja al tilear, y más grande no aporta nada porque es plano.
        /// </summary>
        public static Sprite Create(Color32 color, float pixelsPerUnit, int size = 4)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            Sprite sprite = Sprite.Create(
                texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.HideAndDontSave;

            return sprite;
        }

        /// <summary>Destruye el sprite y su textura. Lo llama quien lo creó.</summary>
        public static void Dispose(Sprite sprite)
        {
            if (sprite == null) return;

            Texture texture = sprite.texture;
            Object.Destroy(sprite);
            if (texture != null) Object.Destroy(texture);
        }
    }
}
