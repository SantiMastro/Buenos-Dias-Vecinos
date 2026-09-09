using BuenosDias.Config;
using BuenosDias.Gameplay;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// La barra de lo que queda de día.
    ///
    /// Se dibuja con <c>SpriteRenderer.size</c> y NO con escala: escalar en pixel
    /// art rompe la grilla, y una barra que se vacía cambiaría de escala en cada
    /// cuadro. Con <c>size</c> lo que cambia es la malla y el texel sigue midiendo
    /// un píxel. El ancho se redondea a píxeles enteros por el mismo motivo.
    ///
    /// ⚠️ La altura se recalcula cada cuadro desde <c>orthographicSize</c> en vez
    /// de dejarse fija en el prefab: la Pixel Perfect Camera CAMBIA el tamaño
    /// ortográfico en runtime según la ventana —se midió 3.688 donde el diseño dice
    /// 3.375— y una barra con offset fijo se despega del borde de pantalla en
    /// cuanto la ventana no tiene la relación de referencia.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TimeBarView : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("De dónde sale cuánto queda. Se lee; nunca se le pide nada.")]
        [SerializeField] private DayDirector director;

        [Tooltip("Dueño de la partida. La barra se esconde sola fuera del día: un " +
                 "reloj lleno mientras se elige religión anunciaría un tiempo que " +
                 "todavía no está corriendo.")]
        [SerializeField] private RunDirector runDirector;

        [Tooltip("Asset raíz de balance: duración del día y umbral de alarma.")]
        [SerializeField] private GameConfig gameConfig;

        [Tooltip("Cámara a cuyo borde superior se ancla la barra.")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Fondo de la barra, el riel que se ve al vaciarse.")]
        [SerializeField] private SpriteRenderer track;

        [Tooltip("Relleno, lo que queda de día.")]
        [SerializeField] private SpriteRenderer fill;

        [Tooltip("Marco de la barra, con el interior transparente. Va ENCIMA del " +
                 "relleno: el relleno llena el hueco y el marco le dibuja el borde.")]
        [SerializeField] private SpriteRenderer frame;

        [Tooltip("Sol, en la punta izquierda: el día empieza lleno.")]
        [SerializeField] private SpriteRenderer sunIcon;

        [Tooltip("Luna, en la punta derecha: hacia donde se vacía.")]
        [SerializeField] private SpriteRenderer moonIcon;

        [Header("Medidas, en píxeles")]
        [Tooltip("Ancho del RELLENO, que es el interior del marco y no el marco. " +
                 "El marco mide 128×12 y su hueco 124×8.")]
        [SerializeField, Min(8f)] private float widthPixels = 124f;

        [Tooltip("Alto del relleno. También es el interior del marco, no el marco.")]
        [SerializeField, Min(1f)] private float heightPixels = 8f;

        [Tooltip("Separación entre el borde superior de la pantalla y el relleno. " +
                 "⚠ El marco sobresale 2 px por arriba, porque el que se ancla es " +
                 "el relleno y el marco es más alto que su propio hueco.")]
        [SerializeField, Min(0f)] private float marginTopPixels = 7f;

        [Header("Colores")]
        [Tooltip("Relleno normal. Hueso #F3ECE0 de la paleta.")]
        [SerializeField] private Color fillColor = new Color32(0xF3, 0xEC, 0xE0, 0xFF);

        [Tooltip("Relleno cuando queda poco. Rojo #A8455A de la paleta.")]
        [SerializeField] private Color warningColor = new Color32(0xA8, 0x45, 0x5A, 0xFF);

        [Tooltip("Parpadeos por segundo cuando queda poco tiempo.")]
        [SerializeField, Range(0.5f, 12f)] private float blinkHertz = 4f;

        private Sprite pixel;

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }

            // Un solo sprite blanco para las dos piezas: el color lo pone cada
            // renderer, así no hacen falta dos texturas idénticas.
            pixel = SolidSprite.Create(Color.white, ProjectConstants.PixelsPerUnit);
            Dress(track);
            Dress(fill);
        }

        private void OnDestroy()
        {
            SolidSprite.Dispose(pixel);
        }

        private void Dress(SpriteRenderer renderer)
        {
            renderer.sprite = pixel;
            renderer.drawMode = SpriteDrawMode.Tiled;
        }

        /// <summary>
        /// En LateUpdate para leer el reloj que ya avanzó este cuadro. Al revés, la
        /// barra iría siempre un cuadro atrasada respecto del cielo.
        /// </summary>
        private void LateUpdate()
        {
            bool playing = runDirector.Phase == RunPhase.Jugando;
            track.enabled = playing;
            fill.enabled = playing;
            Show(frame, playing);
            Show(sunIcon, playing);
            Show(moonIcon, playing);
            if (!playing) return;

            AnchorToTop();

            float fraction = Mathf.Clamp01(
                director.Remaining / Mathf.Max(1f, gameConfig.DayDurationSeconds));

            // A píxeles ENTEROS: media barra de píxel no existe en esta grilla.
            float filled = Mathf.Round(widthPixels * fraction);

            track.size = Units(widthPixels, heightPixels);
            fill.size = Units(filled, heightPixels);

            // El relleno se vacía desde la derecha, así que su centro se corre a
            // medida que se achica: con el centro fijo se vaciaría por los dos
            // lados a la vez y no se leería como un reloj.
            fill.transform.localPosition = new Vector3(
                Units(filled - widthPixels, 0f).x * 0.5f, 0f, 0f);

            fill.color = ColorNow();
        }

        /// <summary>
        /// El marco, el sol y la luna son OPCIONALES a propósito.
        ///
        /// Es una decisión de polaridad: si fueran obligatorios,
        /// <see cref="ValidateSetup"/> apagaría la barra entera —que es información
        /// de juego— por culpa de una decoración que falta. Al revés, lo peor que
        /// pasa es que la barra se vea pelada.
        ///
        /// Sus posiciones viven en el transform de cada hijo y no acá: son fijas
        /// respecto de la barra, así que recalcularlas cada cuadro sería inventar
        /// números que ya están en la escena.
        /// </summary>
        private static void Show(SpriteRenderer renderer, bool visible)
        {
            if (renderer != null) renderer.enabled = visible;
        }

        private void AnchorToTop()
        {
            float half = heightPixels * 0.5f;
            float y = targetCamera.orthographicSize
                      - ProjectConstants.ToUnits(marginTopPixels + half);

            Vector3 local = transform.localPosition;
            transform.localPosition = new Vector3(local.x, y, local.z);
        }

        /// <summary>
        /// El parpadeo es una onda cuadrada y no un desvanecido: un fundido suave
        /// entre dos colores inventa tonos que no están en la paleta, y encima se
        /// lee peor de reojo, que es como se mira una barra de tiempo.
        /// </summary>
        private Color ColorNow()
        {
            if (director.Remaining > gameConfig.DayCycle.LowTimeWarningSeconds)
                return fillColor;

            bool on = Mathf.Repeat(Time.unscaledTime * blinkHertz, 1f) < 0.5f;
            return on ? warningColor : fillColor;
        }

        private static Vector2 Units(float widthPixels, float heightPixels)
        {
            return new Vector2(
                ProjectConstants.ToUnits(widthPixels), ProjectConstants.ToUnits(heightPixels));
        }

        private bool ValidateSetup()
        {
            if (director != null && runDirector != null && gameConfig != null
                && gameConfig.DayCycle != null && targetCamera != null
                && track != null && fill != null) return true;

            Debug.LogError(
                $"[TimeBarView] '{name}' tiene referencias sin asignar " +
                "(director, partida, GameConfig, cámara, riel o relleno).", this);
            return false;
        }
    }
}
