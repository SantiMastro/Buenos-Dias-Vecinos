using UnityEngine;

namespace BuenosDias.Config
{
    /// <summary>
    /// El skillcheck: aguja girando sobre un aro, con zona buena y zona perfecta.
    /// Todos los ángulos están en radianes, como en la spec.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SkillcheckConfig", menuName = "Buenos Días/Config/Skillcheck", order = 24)]
    public sealed class SkillcheckConfig : ScriptableObject
    {
        [Header("Aparición de la zona")]
        [Tooltip("Radianes MÍNIMOS por delante de la aguja a los que puede aparecer " +
                 "la zona. Menos que esto sería injusto: no da tiempo a reaccionar.")]
        [SerializeField, Range(0f, 6.28f)] private float zoneAheadMin = 1.25f;

        [Tooltip("Radianes MÁXIMOS por delante de la aguja.")]
        [SerializeField, Range(0f, 6.28f)] private float zoneAheadMax = 3.1f;

        [Header("Zona perfecta")]
        [Tooltip("Dónde empieza la zona perfecta, como fracción del ancho de la buena.")]
        [SerializeField, Range(0f, 1f)] private float perfectStart = 0.32f;

        [Tooltip("Dónde termina la zona perfecta, como fracción del ancho de la buena.")]
        [SerializeField, Range(0f, 1f)] private float perfectEnd = 0.68f;

        [Header("Falla")]
        [Tooltip("Radianes que la aguja puede pasarse del fin de la zona antes de " +
                 "que se declare falla automática.")]
        [SerializeField, Range(0f, 3.14f)] private float failMarginRadians = 0.42f;

        [Header("Ancho por precisión del timbrazo")]
        // Ojo con el historial: en el prototipo esta variable era la DISTANCIA
        // normalizada, donde 0 era pegado a la puerta, y los dos valores estaban
        // al revés que acá. Al renombrarla a "precisión" (1 = mejor timbrazo) el
        // nombre pasó a decir lo contrario de lo que hacía el número: acercarse a
        // la puerta achicaba la zona. Los valores están dados vuelta a propósito.
        [Tooltip("Ancho de zona, en radianes, con el timbrazo al borde del alcance " +
                 "(precisión 0). Es el más finito: tocar de lejos se castiga.")]
        [SerializeField, Range(0.05f, 2f)] private float widthAtWorstPrecision = 0.24f;

        [Tooltip("Ancho de zona, en radianes, con el timbrazo pegado a la puerta " +
                 "(precisión 1). Es el más ancho: premiar el acercarse es toda la " +
                 "razón de ser del rango del timbre.")]
        [SerializeField, Range(0.05f, 2f)] private float widthAtBestPrecision = 0.52f;

        [Tooltip("Piso absoluto del ancho de zona en radianes. Ninguna combinación " +
                 "de multiplicadores puede dejar la zona más fina que esto.")]
        [SerializeField, Range(0.01f, 1f)] private float minimumWidthRadians = 0.12f;

        [Header("Progresión")]
        [Tooltip("Multiplicador de ancho según el progreso (conversiones / techo).\n" +
                 "X = progreso 0..1. Y = multiplicador.\n" +
                 "La recta por defecto es el lerp de la spec: 1.45 → 0.82. " +
                 "Si la doblás, dejás de tener ese lerp.")]
        [SerializeField]
        private AnimationCurve widthByProgress = AnimationCurve.Linear(0f, 1.45f, 1f, 0.82f);

        [Tooltip("Velocidad de la aguja en rad/s según el progreso.\n" +
                 "La recta por defecto es el lerp de la spec: 1.95 → 3.05.")]
        [SerializeField]
        private AnimationCurve speedByProgress = AnimationCurve.Linear(0f, 1.95f, 1f, 3.05f);

        [Header("Cadena")]
        [Tooltip("Segundos de pausa entre eslabones de una objeción encadenada, " +
                 "mientras se muestra la réplica del vecino.")]
        [SerializeField, Range(0f, 2f)] private float chainPauseSeconds = 0.30f;

        /// <summary>Radianes mínimos por delante a los que aparece la zona.</summary>
        public float ZoneAheadMin => zoneAheadMin;

        /// <summary>Radianes máximos por delante a los que aparece la zona.</summary>
        public float ZoneAheadMax => zoneAheadMax;

        /// <summary>Inicio de la zona perfecta como fracción de la buena.</summary>
        public float PerfectStart => perfectStart;

        /// <summary>Fin de la zona perfecta como fracción de la buena.</summary>
        public float PerfectEnd => perfectEnd;

        /// <summary>Margen de falla automática, en radianes.</summary>
        public float FailMarginRadians => failMarginRadians;

        /// <summary>Piso absoluto del ancho de zona.</summary>
        public float MinimumWidthRadians => minimumWidthRadians;

        /// <summary>Pausa entre eslabones encadenados.</summary>
        public float ChainPauseSeconds => chainPauseSeconds;

        /// <summary>Ancho base de zona según la precisión del timbrazo.</summary>
        public float BaseWidthFor(float precision)
        {
            return Mathf.Lerp(widthAtWorstPrecision, widthAtBestPrecision, Mathf.Clamp01(precision));
        }

        /// <summary>Multiplicador de ancho por progreso de la partida.</summary>
        public float WidthMultiplierAt(float progress)
        {
            return widthByProgress.Evaluate(Mathf.Clamp01(progress));
        }

        /// <summary>Velocidad de aguja en rad/s por progreso de la partida.</summary>
        public float NeedleSpeedAt(float progress)
        {
            return speedByProgress.Evaluate(Mathf.Clamp01(progress));
        }
    }
}
