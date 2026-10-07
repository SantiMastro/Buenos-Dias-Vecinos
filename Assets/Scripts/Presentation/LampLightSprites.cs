using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Los tres sprites de la luz de una farola, dibujados por código: el halo
    /// alrededor de la lámpara, el cono que baja hasta la vereda y el charco de luz
    /// sobre el piso.
    ///
    /// Van por código y no como PNG porque son manchas de un solo tono con alpha
    /// escalonado: tres o cuatro niveles de opacidad, sin degradé liso, para que
    /// se lean como píxel art y no como un desenfoque. Se fabrican una sola vez y
    /// los comparten todas las farolas.
    ///
    /// Son blancos-cálidos con alpha; el material aditivo los suma sobre la escena
    /// y la opacidad total la maneja <see cref="BuenosDias.Gameplay.GapPropPlacer"/>
    /// según la hora.
    /// </summary>
    public static class LampLightSprites
    {
        private const float PixelsPerUnit = 32f;
        private static readonly Color32 Warm = new Color32(255, 222, 150, 255);

        private static Sprite halo;
        private static Sprite cone;
        private static Sprite pool;
        private static Sprite beam;
        private static Sprite spot;

        /// <summary>Aro de luz alrededor de la lámpara. Pivot al centro.</summary>
        public static Sprite Halo => halo != null ? halo : (halo = BuildHalo());

        /// <summary>Cono que baja desde la lámpara. Pivot arriba al centro.</summary>
        public static Sprite Cone => cone != null ? cone : (cone = BuildCone());

        /// <summary>Charco de luz en el piso. Pivot al centro.</summary>
        public static Sprite Pool => pool != null ? pool : (pool = BuildPool());

        /// <summary>
        /// Haz de un faro de auto: una cuña horizontal que se abre hacia la derecha,
        /// con escalones de alpha. Pivot a la izquierda al centro: nace en el faro.
        /// </summary>
        public static Sprite Beam => beam != null ? beam : (beam = BuildBeam());

        /// <summary>Punto de luz chico (faro y luz trasera). Pivot al centro.</summary>
        public static Sprite Spot => spot != null ? spot : (spot = BuildSpot());

        private static Sprite BuildBeam()
        {
            const int width = 64;
            const int height = 26;
            var pixels = new Color32[width * height];
            float cy = (height - 1) * 0.5f;

            for (int x = 0; x < width; x++)
            {
                float t = x / (float)(width - 1);                  // 0 en el faro, 1 en la punta
                float half = 2.5f + t * 10f;                       // la cuña se abre
                float fade = 1f - 0.75f * t;                       // y se apaga con la distancia

                for (int y = 0; y < height; y++)
                {
                    float r = Mathf.Abs(y - cy) / half;
                    if (r > 1f) continue;

                    float band = r < 0.4f ? 0.34f : r < 0.75f ? 0.22f : 0.10f;
                    pixels[y * width + x] = WithAlpha(Quantize(band * fade));
                }
            }

            return Make(pixels, width, height, new Vector2(0f, 0.5f));
        }

        private static Sprite BuildSpot()
        {
            const int size = 9;
            var pixels = new Color32[size * size];
            float c = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float alpha = d <= 1.2f ? 0.9f : d <= 2.4f ? 0.5f : d <= 4.2f ? 0.18f : 0f;
                    pixels[y * size + x] = WithAlpha(alpha);
                }
            }

            return Make(pixels, size, size, new Vector2(0.5f, 0.5f));
        }

        private static Sprite BuildHalo()
        {
            const int size = 21;
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    float alpha = d <= 2.2f ? 1f
                                : d <= 4.2f ? 0.65f
                                : d <= 6.5f ? 0.38f
                                : d <= 9.5f ? 0.16f
                                : 0f;
                    pixels[y * size + x] = WithAlpha(alpha);
                }
            }

            return Make(pixels, size, size, new Vector2(0.5f, 0.5f));
        }

        private static Sprite BuildCone()
        {
            const int width = 72;
            const int height = 120;
            var pixels = new Color32[width * height];
            float cx = (width - 1) * 0.5f;

            for (int row = 0; row < height; row++)
            {
                // la textura se llena de abajo hacia arriba: row 0 es el pie del cono
                float t = 1f - row / (float)(height - 1);          // 0 arriba, 1 abajo
                float half = 3f + t * 32f;

                for (int x = 0; x < width; x++)
                {
                    float r = Mathf.Abs(x - cx) / half;
                    if (r > 1f) continue;

                    float depth = 1f - 0.65f * t;                   // más tenue hacia abajo
                    float band = r < 0.45f ? 0.30f : r < 0.8f ? 0.18f : 0.08f;
                    float alpha = Quantize(band * depth);
                    pixels[row * width + x] = WithAlpha(alpha);
                }
            }

            return Make(pixels, width, height, new Vector2(0.5f, 1f));
        }

        private static Sprite BuildPool()
        {
            const int width = 80;
            const int height = 16;
            var pixels = new Color32[width * height];
            float cx = (width - 1) * 0.5f;
            float cy = (height - 1) * 0.5f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (x - cx) / (width * 0.5f);
                    float ny = (y - cy) / (height * 0.5f);
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float alpha = d <= 0.42f ? 0.42f
                                : d <= 0.72f ? 0.26f
                                : d <= 1f ? 0.12f
                                : 0f;
                    pixels[y * width + x] = WithAlpha(alpha);
                }
            }

            return Make(pixels, width, height, new Vector2(0.5f, 0.5f));
        }

        /// <summary>Redondea el alpha a escalones, así no hay degradé liso.</summary>
        private static float Quantize(float alpha)
        {
            if (alpha > 0.24f) return 0.30f;
            if (alpha > 0.15f) return 0.20f;
            if (alpha > 0.08f) return 0.12f;
            return alpha > 0.03f ? 0.06f : 0f;
        }

        private static Color32 WithAlpha(float alpha)
        {
            return new Color32(Warm.r, Warm.g, Warm.b, (byte)Mathf.Clamp(alpha * 255f, 0f, 255f));
        }

        private static Sprite Make(Color32[] pixels, int width, int height, Vector2 pivot)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            Sprite sprite = Sprite.Create(
                texture, new Rect(0f, 0f, width, height), pivot, PixelsPerUnit, 0,
                SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
