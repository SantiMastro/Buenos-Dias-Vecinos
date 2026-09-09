using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Pinta sectores de anillo en una textura, píxel por píxel, y la expone como
    /// sprite.
    ///
    /// Se pinta en CPU en vez de generar un mesh o resolverlo en un shader porque
    /// el juego es pixel art con cámara pixel-perfect: un arco vectorial entra con
    /// el borde antialiaseado y fuera de la grilla, que es justo lo que rompe la
    /// lectura. Acá el borde de la zona cae siempre en un píxel entero.
    ///
    /// El costo es una repintada por eslabón, no por cuadro: la zona no se mueve,
    /// se mueve la aguja.
    /// </summary>
    public sealed class ArcPainter
    {
        private const float TwoPi = Mathf.PI * 2f;

        private readonly int size;
        private readonly float center;
        private readonly Color32[] pixels;
        private readonly Texture2D texture;

        /// <summary>El sprite que hay que colgar del <c>SpriteRenderer</c>.</summary>
        public Sprite Painted { get; }

        /// <summary>
        /// Arma una textura cuadrada vacía. El pivot queda en el centro para que
        /// el arco comparta origen con la aguja sin tener que corregir nada.
        /// </summary>
        public ArcPainter(int size, float pixelsPerUnit)
        {
            this.size = size;
            center = size * 0.5f;
            pixels = new Color32[size * size];

            // HideAndDontSave: son objetos de runtime. Sin esto quedan colgados en
            // la escena y Unity avisa que hay assets sin dueño al salir de Play.
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Painted = Sprite.Create(
                texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                pixelsPerUnit, 0, SpriteMeshType.FullRect);
            Painted.hideFlags = HideFlags.HideAndDontSave;
        }

        /// <summary>Deja la textura transparente. No la sube: eso lo hace <see cref="Apply"/>.</summary>
        public void Clear()
        {
            System.Array.Clear(pixels, 0, pixels.Length);
        }

        /// <summary>
        /// Pinta un sector de anillo. El ángulo cero apunta hacia ARRIBA y crece en
        /// sentido horario, que es el mismo cero y el mismo sentido con el que gira
        /// la aguja: así el número que dibuja el arco es el mismo que compara la
        /// lógica, sin conversiones en el medio donde meter un signo al revés.
        /// </summary>
        public void Paint(
            float startAngle, float sweep, float innerRadius, float outerRadius, Color32 color)
        {
            if (sweep <= 0f || outerRadius <= innerRadius) return;

            float inner2 = innerRadius * innerRadius;
            float outer2 = outerRadius * outerRadius;

            for (int y = 0; y < size; y++)
            {
                float dy = y + 0.5f - center;

                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - center;

                    float distance = dx * dx + dy * dy;
                    if (distance < inner2 || distance > outer2) continue;

                    // Atan2(dx, dy) mide desde el eje Y hacia el X: cero arriba y
                    // creciendo en horario. Atan2(dy, dx) daría el convenio
                    // matemático, que es el otro sentido.
                    float angle = Mathf.Atan2(dx, dy);
                    if (Mathf.Repeat(angle - startAngle, TwoPi) >= sweep) continue;

                    pixels[y * size + x] = color;
                }
            }
        }

        /// <summary>
        /// Pinta un anillo entero. Es <see cref="Paint"/> con barrido completo:
        /// con sweep = 2π la comparación de ángulo nunca descarta, porque
        /// <c>Mathf.Repeat</c> devuelve siempre menos que el largo.
        /// </summary>
        public void PaintFullRing(float innerRadius, float outerRadius, Color32 color)
        {
            Paint(0f, TwoPi, innerRadius, outerRadius, color);
        }

        /// <summary>
        /// Pinta un rayo recto de ancho CONSTANTE que sale del centro, desde
        /// <paramref name="innerRadius"/> hasta <paramref name="outerRadius"/>.
        ///
        /// Es la aguja. No se usa <see cref="Paint"/> con un barrido angular chico
        /// porque un sector se ensancha con el radio: la aguja saldría en punta
        /// hacia adentro y gorda en la punta de afuera, que es exactamente lo que
        /// no queremos de algo que tiene que leerse como una línea.
        ///
        /// El ángulo cero apunta hacia ARRIBA y crece en horario, igual que en
        /// <see cref="Paint"/>: la aguja se pinta una sola vez apuntando a cero y
        /// después la gira el transform, así que esto corre en el arranque y no
        /// por cuadro.
        /// </summary>
        public void PaintRay(
            float angle, float innerRadius, float outerRadius, float thickness, Color32 color)
        {
            if (outerRadius <= innerRadius || thickness <= 0f) return;

            // Dirección del rayo con el convenio de cero arriba y giro horario.
            float dirX = Mathf.Sin(angle);
            float dirY = Mathf.Cos(angle);
            float halfWidth = thickness * 0.5f;

            for (int y = 0; y < size; y++)
            {
                float dy = y + 0.5f - center;

                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - center;

                    // Proyección sobre el rayo: cuánto avanza y cuánto se desvía.
                    float along = dx * dirX + dy * dirY;
                    if (along < innerRadius || along > outerRadius) continue;

                    float across = dx * dirY - dy * dirX;
                    if (Mathf.Abs(across) > halfWidth) continue;

                    pixels[y * size + x] = color;
                }
            }
        }

        /// <summary>
        /// Pinta un velo radial: parejo hasta <paramref name="fullRadius"/> y
        /// desvaneciéndose hasta <paramref name="fadeRadius"/>, donde llega a cero.
        ///
        /// El desvanecido se CUANTIZA en <paramref name="steps"/> escalones en vez
        /// de ser continuo. Una rampa de alpha suave es correcta en 3D y ajena al
        /// pixel art: con pocos escalones el velo se lee como parte del dibujo y no
        /// como un degradado pegado encima. Con steps alto vuelve a ser continuo,
        /// así que la decisión queda en el Inspector y no acá.
        /// </summary>
        public void PaintRadialVeil(
            float fullRadius, float fadeRadius, Color32 color, float maxAlpha, int steps)
        {
            if (fadeRadius <= fullRadius || maxAlpha <= 0f) return;

            int levels = Mathf.Max(1, steps);
            float band = fadeRadius - fullRadius;

            for (int y = 0; y < size; y++)
            {
                float dy = y + 0.5f - center;

                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - center;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    if (radius >= fadeRadius) continue;

                    float falloff = radius <= fullRadius
                        ? 1f
                        : 1f - SmoothStep(Mathf.Clamp01((radius - fullRadius) / band));

                    float quantized = Mathf.Round(falloff * levels) / levels;
                    if (quantized <= 0f) continue;

                    pixels[y * size + x] = new Color32(
                        color.r, color.g, color.b, (byte)(quantized * maxAlpha * 255f));
                }
            }
        }

        /// <summary>Curva 3t²−2t³: arranca y termina plana, sin el codo del lerp.</summary>
        private static float SmoothStep(float t) => t * t * (3f - 2f * t);

        /// <summary>Sube lo pintado a la GPU.</summary>
        public void Apply()
        {
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
        }

        /// <summary>Destruye textura y sprite. Lo llama quien lo creó, al morir.</summary>
        public void Dispose()
        {
            if (Painted != null) Object.Destroy(Painted);
            if (texture != null) Object.Destroy(texture);
        }
    }
}
