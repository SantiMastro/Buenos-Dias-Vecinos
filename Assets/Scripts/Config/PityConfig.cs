using UnityEngine;

namespace BuenosDias.Config
{
    /// <summary>
    /// Sistema anti-racha.
    ///
    /// La regla que gobierna todo esto: **el pity nunca puede hacer que abra una
    /// casa que se ve muerta**. Leer señales es la habilidad central del juego y
    /// castigar esa habilidad con una tirada escondida haría que el jugador
    /// dejara de confiar en lo que ve.
    ///
    /// Por eso el mecanismo principal es sesgar la GENERACIÓN hacia casas
    /// amigables, no la tirada: el barrio se pone más amable, pero las señales
    /// siguen diciendo la verdad.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PityConfig", menuName = "Buenos Días/Config/Anti-racha", order = 23)]
    public sealed class PityConfig : ScriptableObject
    {
        [Header("Probabilidad base")]
        [Tooltip("Chance de que haya alguien en una casa sin ninguna señal.")]
        [SerializeField, Range(0f, 1f)] private float baseChance = 0.45f;

        [Tooltip("Piso de la chance de una casa una vez sumadas sus señales.")]
        [SerializeField, Range(0f, 1f)] private float signalClampMin = 0.05f;

        [Tooltip("Techo de la chance de una casa una vez sumadas sus señales.")]
        [SerializeField, Range(0f, 1f)] private float signalClampMax = 0.95f;

        [Header("Umbral de legibilidad")]
        [Tooltip("Chance por debajo de la cual una casa 'se ve muerta'.\n" +
                 "Es el umbral clave del sistema: en casas por debajo de este " +
                 "valor el pity vale CERO y la garantía dura no aplica. Si el " +
                 "jugador insiste en tocar casas que se ven muertas, le siguen " +
                 "saliendo vacías, y eso le enseña a leer.")]
        [SerializeField, Range(0f, 1f)] private float readableChanceThreshold = 0.42f;

        [Header("Pity de tirada")]
        [Tooltip("Cuánto sube el pity con cada casa vacía.")]
        [SerializeField, Range(0f, 1f)] private float increment = 0.22f;

        [Tooltip("Techo del pity acumulado.")]
        [SerializeField, Range(0f, 1f)] private float cap = 0.55f;

        [Tooltip("Piso de la probabilidad final al tocar el timbre.")]
        [SerializeField, Range(0f, 1f)] private float rollClampMin = 0.05f;

        [Tooltip("Techo de la probabilidad final al tocar el timbre.")]
        [SerializeField, Range(0f, 1f)] private float rollClampMax = 0.97f;

        [Header("Garantía dura")]
        [Tooltip("Después de esta cantidad de vacías seguidas, la próxima casa " +
                 "LEGIBLE abre garantizado. Las que se ven muertas siguen exentas.")]
        [SerializeField, Min(1)] private int guaranteedAfterEmptyRun = 3;

        [Header("Pity de generación")]
        [Tooltip("Cuántas casas atrás mira para decidir si el barrio se está " +
                 "viendo muerto.")]
        [SerializeField, Min(1)] private int deadBlockWindow = 3;

        [Tooltip("Con emptyRun >= 1, probabilidad de forzar señales positivas en " +
                 "la casa que se genera. Es el mecanismo PRINCIPAL del anti-racha: " +
                 "sesga lo que el jugador ve, no lo que la tirada esconde.")]
        [SerializeField, Range(0f, 1f)] private float positiveBiasPerEmpty = 0.35f;

        /// <summary>Chance de una casa sin señales.</summary>
        public float BaseChance => baseChance;

        /// <summary>Umbral por debajo del cual una casa se considera ilegible/muerta.</summary>
        public float ReadableChanceThreshold => readableChanceThreshold;

        /// <summary>Incremento del pity por cada vacía.</summary>
        public float Increment => increment;

        /// <summary>Techo del pity acumulado.</summary>
        public float Cap => cap;

        /// <summary>Vacías seguidas tras las cuales la próxima legible abre seguro.</summary>
        public int GuaranteedAfterEmptyRun => guaranteedAfterEmptyRun;

        /// <summary>Ventana de casas para detectar barrio muerto.</summary>
        public int DeadBlockWindow => deadBlockWindow;

        /// <summary>Sesgo hacia señales positivas por cada vacía acumulada.</summary>
        public float PositiveBiasPerEmpty => positiveBiasPerEmpty;

        /// <summary>Clampea la chance propia de una casa según sus señales.</summary>
        public float ClampSignalChance(float chance)
        {
            return Mathf.Clamp(chance, signalClampMin, signalClampMax);
        }

        /// <summary>Clampea la probabilidad final del momento de tocar el timbre.</summary>
        public float ClampRoll(float probability)
        {
            return Mathf.Clamp(probability, rollClampMin, rollClampMax);
        }

        /// <summary>
        /// Si una casa es lo bastante legible como para que el pity y la garantía
        /// dura la alcancen.
        /// </summary>
        public bool IsReadable(float houseChance) => houseChance >= readableChanceThreshold;
    }
}
