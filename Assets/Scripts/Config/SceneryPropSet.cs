using System.Collections.Generic;
using UnityEngine;

namespace BuenosDias.Config
{
    /// <summary>
    /// Un tipo de decorado de la cuadra: qué sprite se dibuja, a qué altura
    /// apoya, con qué tinte y cada cuánto aparece.
    ///
    /// Vive como asset y no como campos sueltos en la escena porque la escena se
    /// reconstruye entera desde el menú de Tools: cualquier número puesto a mano
    /// en un componente se perdería en la próxima reconstrucción.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SceneryPropSet", menuName = "Buenos Días/Config/Decorado", order = 40)]
    public sealed class SceneryPropSet : ScriptableObject
    {
        [Header("Sprite")]
        [Tooltip("Variantes del prop. Se sortea una por cada uno que se planta, así " +
                 "que sumar una mata nueva es arrastrar el sprite acá.\n" +
                 "Con una sola entrada, todos salen iguales.")]
        [SerializeField] private List<Sprite> sprites = new List<Sprite>();

        [Tooltip("Filas de píxeles que se descartan desde ABAJO del sprite.\n" +
                 "Es para props de los que solo se ve la parte de arriba. El árbol " +
                 "de fondo descarta tronco y raíces: mide 120 px con la copa " +
                 "arrancando en la fila 56, y los techos llegan a 112, así que para " +
                 "que la copa asome sobre los techos el tronco quedaría flotando.")]
        [SerializeField, Min(0)] private int bottomCropPixels;

        [Header("Ubicación")]
        [Tooltip("Altura del borde inferior del prop YA RECORTADO, en píxeles, " +
                 "medida desde la línea de caminata (Y = 0, el tope de la vereda).")]
        [SerializeField] private float groundOffsetPixels;

        [Tooltip("Sorting layer donde se dibuja. El orden de dibujo en 2D lo " +
                 "gobiernan los sorting layers, no la Z.")]
        [SerializeField] private string sortingLayer = "Far";

        [Tooltip("Orden dentro del sorting layer.")]
        [SerializeField] private int sortingOrder;

        [Header("Tinte de profundidad")]
        [Tooltip("Color hacia el que se multiplica el sprite. Empuja el prop hacia " +
                 "el fondo sin tocar el arte. En la fase 7 lo va a manejar el ciclo " +
                 "de luz, así que queda expuesto acá a propósito.")]
        [SerializeField] private Color tintColor = Color.white;

        [Tooltip("Cuánto se aplica el tinte. 0 deja el sprite tal cual.")]
        [SerializeField, Range(0f, 1f)] private float tintStrength;

        [Header("Distribución")]
        [Tooltip("Separación mínima hasta el próximo prop, en píxeles, medida de " +
                 "borde a borde. Solo la usan los props sembrados a lo largo de la " +
                 "calle; el poste se ubica por hueco y la ignora.")]
        [SerializeField, Min(0f)] private float spacingMinPixels = 120f;

        [Tooltip("Separación máxima hasta el próximo prop, en píxeles.")]
        [SerializeField, Min(0f)] private float spacingMaxPixels = 240f;

        [Tooltip("Ancho mínimo de hueco, en píxeles, para que este decorado se pueda " +
                 "plantar. Es el mismo patrón que 'minimumLotWidthPixels' del auto.\n" +
                 "Sirve para pedir MÁS aire del que exige el sprite: el árbol mide 72 " +
                 "px y entraría en un hueco de 85, pero ahí queda encajado tocando las " +
                 "dos rejas. Con 100 le quedan 14 px por lado.\n" +
                 "0 = alcanza con que entre el sprite.")]
        [SerializeField, Min(0f)] private float minimumGapPixels;

        [Tooltip("Probabilidad de llevarse un hueco entre terrenos. Solo la usan " +
                 "los decorados que se ubican por hueco (poste y árbol).\n" +
                 "Las de todos los candidatos se ACUMULAN en una sola tirada, así " +
                 "que la suma no debería pasar de 1: lo que sobre hasta 1 es la " +
                 "proporción de huecos que quedan vacíos. Con poste 0.35 y árbol " +
                 "0.30, el 35% de los huecos queda pelado.")]
        [SerializeField, Range(0f, 1f)] private float chancePerGap = 0.35f;

        /// <summary>Sorting layer donde se dibuja.</summary>
        public string SortingLayer => sortingLayer;

        /// <summary>Orden dentro del sorting layer.</summary>
        public int SortingOrder => sortingOrder;

        /// <summary>Color final del renderer, ya mezclado con la intensidad.</summary>
        public Color Tint => Color.Lerp(Color.white, tintColor, tintStrength);

        /// <summary>Altura del borde inferior del prop recortado, en unidades.</summary>
        public float GroundOffset => ProjectConstants.ToUnits(groundOffsetPixels);

        /// <summary>Probabilidad de aparecer en un hueco entre terrenos.</summary>
        public float ChancePerGap => chancePerGap;

        /// <summary>Ancho mínimo de hueco pedido a mano, en unidades.</summary>
        public float MinimumGap => ProjectConstants.ToUnits(minimumGapPixels);

        /// <summary>Cuántas variantes tiene.</summary>
        public int VariantCount => sprites.Count;

        /// <summary>Si tiene lo necesario para dibujarse.</summary>
        public bool IsUsable
        {
            get
            {
                if (sprites.Count == 0) return false;
                foreach (Sprite variant in sprites)
                    if (variant == null) return false;
                return true;
            }
        }

        /// <summary>Sortea la separación hasta el próximo prop, en unidades.</summary>
        public float RollSpacing(System.Random random)
        {
            float pixels = Mathf.Lerp(
                spacingMinPixels, spacingMaxPixels, (float)random.NextDouble());
            return ProjectConstants.ToUnits(pixels);
        }

        /// <summary>
        /// Devuelve las variantes a dibujar, recortadas por abajo si corresponde.
        ///
        /// Aloca Sprites nuevos cuando hay recorte, así que se llama UNA vez al
        /// armar el pool y nunca por cuadro. Usa FullRect a propósito: el mesh
        /// Tight necesitaría leer los píxeles de la textura, y las texturas del
        /// proyecto no importan como legibles.
        /// </summary>
        public Sprite[] CreateSprites()
        {
            var result = new Sprite[sprites.Count];
            for (int i = 0; i < sprites.Count; i++) result[i] = Crop(sprites[i]);
            return result;
        }

        private Sprite Crop(Sprite source)
        {
            if (source == null || bottomCropPixels <= 0) return source;

            Rect rect = source.rect;
            float height = rect.height - bottomCropPixels;
            if (height <= 0f) return source;

            // El pivot se recalcula en normalizado sobre el rect ya recortado: si se
            // dejara el original, una mata con pivot BottomCenter se correría media
            // mata al recortarle la base.
            var pivot = new Vector2(
                source.pivot.x / rect.width,
                Mathf.Max(0f, source.pivot.y - bottomCropPixels) / height);

            return Sprite.Create(
                source.texture,
                new Rect(rect.x, rect.y + bottomCropPixels, rect.width, height),
                pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
    }
}
