using System.Collections.Generic;
using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>Dónde cae el texto respecto del origen del cartel.</summary>
    public enum TextAlignment
    {
        /// <summary>El origen es el borde izquierdo.</summary>
        Izquierda,

        /// <summary>El origen es el centro. Es lo que quieren los titulares.</summary>
        Centro
    }

    /// <summary>
    /// Dibuja una cadena con la fuente de bitmap, un <c>SpriteRenderer</c> por letra.
    ///
    /// La escala se logra bajando los <c>pixelsPerUnit</c> del sprite y **no**
    /// tocando <c>localScale</c>: a PPU 16 cada texel mide dos píxeles de mundo,
    /// que es exactamente un ×2, y el transform se queda en 1. Escalando el
    /// transform, cualquier redondeo de la cámara pixel-perfect corre medio píxel y
    /// las letras quedan con los bordes rotos.
    ///
    /// ⚠️ Se dibuja en DOS pasadas, tinta y sombra, y no es decoración: medido, el
    /// hueso `#F3ECE0` da 1.79:1 contra el cielo de mediodía, 1.39:1 contra una
    /// pared beige y 2.20:1 contra una verde, todos por debajo del mínimo de 3:1.
    /// La sombra `#1D1638` gana justo donde el hueso pierde (8.18, 10.51 y 6.66) y
    /// pierde donde el hueso gana. Juntos, el cartel se lee contra cualquier fondo
    /// sin que nadie tenga que elegir el color del texto según dónde va a caer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TextLabel : MonoBehaviour
    {
        [Header("Fuente")]
        [Tooltip("Definición de la fuente de bitmap y su atlas.")]
        [SerializeField] private BitmapFontDefinition font;

        [Tooltip("Qué dice. Se puede cambiar en runtime.")]
        [SerializeField, TextArea(1, 3)] private string text = string.Empty;

        [Header("Dibujo")]
        [Tooltip("Dónde cae el texto respecto del origen de este objeto.")]
        [SerializeField] private TextAlignment alignment = TextAlignment.Centro;

        [Tooltip("Multiplicador de tamaño. ENTERO siempre: 1 para la UI, 2 para los " +
                 "titulares. Un ×1.5 rompe la grilla de la pixel art.")]
        [SerializeField, Range(1, 4)] private int pixelScale = 1;

        [Tooltip("Píxeles extra entre letra y letra. La celda ya trae uno.")]
        [SerializeField, Min(0)] private int trackingPixels;

        [Tooltip("Color de la tinta. El atlas es una máscara blanca, así que esto ES " +
                 "el color final y cae exacto en la paleta.")]
        [SerializeField] private Color inkColor = new Color32(0xF3, 0xEC, 0xE0, 0xFF);

        [Header("Sombra")]
        [Tooltip("Segunda pasada oscura, corrida un píxel. Es lo que hace que el " +
                 "cartel se lea sobre fondos claros; apagarla es apostar a que el " +
                 "texto nunca cae sobre el cielo ni sobre una pared.")]
        [SerializeField] private bool drawShadow = true;

        [Tooltip("Color de la sombra. Violeta oscuro #1D1638 de la paleta.")]
        [SerializeField] private Color shadowColor = new Color32(0x1D, 0x16, 0x38, 0xFF);

        [Tooltip("Corrimiento de la sombra, en píxeles. Uno a la derecha y uno abajo " +
                 "cae en el hueco de separación de la celda y no pisa la letra siguiente.")]
        [SerializeField] private Vector2Int shadowOffsetPixels = new Vector2Int(1, -1);

        [Header("Contorno")]
        [Tooltip("Grosor del contorno, en PÍXELES DE PANTALLA — no de la letra: con " +
                 "2 el borde mide dos píxeles se dibuje el texto en escala 1 o en " +
                 "escala 3. Con 0 no se dibuja. " +
                 "Cuesta caro, porque es una copia entera de la palabra por cada " +
                 "corrimiento: 8 con grosor 1 y 24 con grosor 2. Va solo donde el " +
                 "texto tiene que leerse sobre CUALQUIER fondo, como el contador " +
                 "de almas.")]
        [SerializeField, Range(0, 2)] private int outlinePixels;

        [Tooltip("Color del contorno. Con tinta oscura y contorno hueso el texto " +
                 "gana contra el cielo Y contra una pared, que es lo que la sombra " +
                 "sola no puede: la sombra cubre dos lados y el contorno los cuatro.")]
        [SerializeField] private Color outlineColor = new Color32(0xF3, 0xEC, 0xE0, 0xFF);

        [Header("Orden de dibujo")]
        [Tooltip("Material de las letras. Tiene que ser SIN iluminar: el default de " +
                 "URP 2D es iluminado y la luz global del atardecer apagaría el " +
                 "cartel junto con el barrio.")]
        [SerializeField] private Material spriteMaterial;

        [Tooltip("Sorting layer de las letras.")]
        [SerializeField] private string sortingLayer = "FX";

        [Tooltip("Orden de la tinta. La sombra va justo debajo.")]
        [SerializeField] private int sortingOrder = 200;

        private readonly List<SpriteRenderer> ink = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> shade = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> outline = new List<SpriteRenderer>();
        private Sprite[] glyphs;

        /// <summary>Ancho del texto dibujado, en píxeles de pantalla.</summary>
        public int WidthPixels => (text?.Length ?? 0) * AdvancePixels;

        /// <summary>Alto de una línea, en píxeles de pantalla.</summary>
        public int HeightPixels => font != null ? font.CellHeight * pixelScale : 0;

        private int AdvancePixels =>
            font != null ? (font.CellWidth + trackingPixels) * pixelScale : 0;

        /// <summary>Cambia lo que dice. Si es lo mismo que ya decía, no hace nada.</summary>
        public void SetText(string value)
        {
            value ??= string.Empty;
            if (value == text && glyphs != null) return;

            text = value;
            if (glyphs != null) Rebuild();
        }

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }

            glyphs = BuildGlyphSprites();
            Rebuild();
        }

        private void OnDestroy()
        {
            if (glyphs == null) return;

            foreach (Sprite glyph in glyphs)
                if (glyph != null) Destroy(glyph);
        }

        /// <summary>
        /// Recorta los glifos del atlas una sola vez. <c>FullRect</c> es
        /// obligatorio: el mesh <c>Tight</c> necesita leer los píxeles de la
        /// textura, y recortar con <c>FullRect</c> solo cambia las UV.
        /// </summary>
        private Sprite[] BuildGlyphSprites()
        {
            var built = new Sprite[font.GlyphCount];
            float pixelsPerUnit = ProjectConstants.PixelsPerUnit / pixelScale;

            for (int i = 0; i < built.Length; i++)
            {
                built[i] = Sprite.Create(
                    font.Atlas, font.RectFor(i), Vector2.zero,
                    pixelsPerUnit, 0, SpriteMeshType.FullRect);
                built[i].name = $"glifo_{font.Charset[i]}";
            }

            return built;
        }

        private void Rebuild()
        {
            float startX = StartX();

            HideFrom(outline, LayoutOutline(startX));

            HideFrom(shade, drawShadow
                ? Layout(shade, "sombra", shadowColor, sortingOrder - 1,
                    startX + ProjectConstants.ToUnits(shadowOffsetPixels.x * pixelScale),
                    ProjectConstants.ToUnits(shadowOffsetPixels.y * pixelScale), 0)
                : 0);

            HideFrom(ink, Layout(ink, "letra", inkColor, sortingOrder, startX, 0f, 0));
        }

        /// <summary>
        /// Dibuja el contorno como copias corridas de la palabra entera.
        ///
        /// Se rellena el CUADRADO de lado <c>2r+1</c> y no solo su borde: con grosor
        /// 2, un anillo de radio 2 deja agujeros en las diagonales y el contorno se
        /// ve mordido. Las copias de adentro quedan tapadas por la tinta, así que no
        /// cuestan nada de mirar, solo de dibujar.
        ///
        /// El corrimiento va en píxeles de PANTALLA y no de la letra: así el borde
        /// mide lo mismo en un cartel chico que en el contador de almas, que está en
        /// escala 3. Sigue cayendo en la grilla porque es un entero de píxeles.
        /// </summary>
        private int LayoutOutline(float startX)
        {
            int used = 0;

            for (int dx = -outlinePixels; dx <= outlinePixels; dx++)
                for (int dy = -outlinePixels; dy <= outlinePixels; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    used = Layout(
                        outline, "contorno", outlineColor, sortingOrder - 2,
                        startX + ProjectConstants.ToUnits(dx),
                        ProjectConstants.ToUnits(dy), used);
                }

            return used;
        }

        /// <summary>
        /// Acomoda una copia de la palabra y devuelve cuántos renderers lleva usados
        /// el pool.
        ///
        /// ⚠️ NO apaga los que sobran, y por eso recibe y devuelve el contador: el
        /// contorno la llama hasta 24 veces sobre el MISMO pool, y apagar al final
        /// de cada vuelta borraría todas las copias menos la última.
        /// </summary>
        private int Layout(
            List<SpriteRenderer> pass, string prefix, Color color, int order,
            float startX, float y, int used)
        {
            float x = startX;

            foreach (char character in text)
            {
                int index = font.IndexOf(character);

                // Un carácter que la fuente no tiene avanza igual: así una tilde
                // faltante corre una letra y no desarma el cartel entero.
                if (index >= 0 && glyphs[index] != null)
                {
                    SpriteRenderer letter = Take(pass, prefix, color, order, used++);
                    letter.sprite = glyphs[index];
                    letter.transform.localPosition = new Vector3(x, y, 0f);
                }

                x += ProjectConstants.ToUnits(AdvancePixels);
            }

            return used;
        }

        /// <summary>Apaga la cola del pool que esta vuelta no se usó.</summary>
        private static void HideFrom(List<SpriteRenderer> pass, int used)
        {
            for (int i = used; i < pass.Count; i++) pass[i].gameObject.SetActive(false);
        }

        /// <summary>
        /// Dónde arranca la primera letra. Centrar se redondea a píxel entero: a
        /// medio píxel las letras caen entre la grilla y la cámara pixel-perfect las
        /// corre sola, con lo que el cartel tiembla al cambiar de largo.
        /// </summary>
        private float StartX()
        {
            if (alignment == TextAlignment.Izquierda) return 0f;

            return -ProjectConstants.ToUnits(Mathf.Round(WidthPixels * 0.5f));
        }

        private SpriteRenderer Take(
            List<SpriteRenderer> pass, string prefix, Color color, int order, int index)
        {
            while (pass.Count <= index)
            {
                var go = new GameObject($"{prefix}_{pass.Count:00}");
                go.transform.SetParent(transform, false);

                var created = go.AddComponent<SpriteRenderer>();
                created.sortingLayerName = sortingLayer;
                if (spriteMaterial != null) created.sharedMaterial = spriteMaterial;
                pass.Add(created);
            }

            // Color y orden se escriben SIEMPRE y no solo al crear el renderer: con
            // el pool ya armado, cambiarlos no hacía nada visible hasta reiniciar,
            // que es la misma clase de mentira silenciosa que ya costó dos vueltas
            // de depuración con el HUD.
            SpriteRenderer taken = pass[index];
            taken.color = color;
            taken.sortingOrder = order;
            taken.gameObject.SetActive(true);
            return taken;
        }

        private bool ValidateSetup()
        {
            if (font != null && font.Atlas != null && font.GlyphCount > 0) return true;

            Debug.LogError(
                $"[TextLabel] '{name}' no tiene fuente, o la fuente no tiene atlas ni " +
                "charset. ¿Se corrió el constructor de fuentes?", this);
            return false;
        }
    }
}
