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
                 "(precisión 0). Es el más finito: tocar de lejos se castiga.\n" +
                 "2.094 = un tercio del círculo.")]
        [SerializeField, Range(0.05f, 6.28f)] private float widthAtWorstPrecision = 2.0944f;

        [Tooltip("Ancho de zona, en radianes, con el timbrazo pegado a la puerta " +
                 "(precisión 1). Es el más ancho: premiar el acercarse es toda la " +
                 "razón de ser del rango del timbre.\n2.618 = 150°.")]
        [SerializeField, Range(0.05f, 6.28f)] private float widthAtBestPrecision = 2.618f;

        [Tooltip("Piso del ancho de zona AL ARRANCAR el día (progreso 0), en " +
                 "radianes. Ninguna religión, puntería ni insistencia la deja más " +
                 "chica que esto al principio: el QTE tiene que arrancar grande y " +
                 "fácil. 2.094 = un tercio del círculo.\n\n" +
                 "El piso baja hasta 'Minimum Width Radians' a medida que sube el " +
                 "progreso.")]
        [SerializeField, Range(0.01f, 6.28f)] private float openingMinimumRadians = 2.0944f;

        [Tooltip("Piso absoluto del ancho de zona con la dificultad AL MÁXIMO, en " +
                 "radianes. Ninguna combinación de multiplicadores puede dejar la " +
                 "zona más fina que esto. 0.785 = un octavo del círculo.")]
        [SerializeField, Range(0.01f, 3.14f)] private float minimumWidthRadians = 0.785f;

        [Header("Progresión")]
        [Tooltip("Multiplicador de ancho según el progreso (conversiones / techo).\n" +
                 "X = progreso 0..1. Y = multiplicador.\n" +
                 "La recta por defecto va de 1 a 0.6: arranca con el ancho de la " +
                 "puntería tal cual y termina en el 60 %, donde el piso de 1/8 ya " +
                 "suele mandar.")]
        [SerializeField]
        private AnimationCurve widthByProgress = AnimationCurve.Linear(0f, 1f, 1f, 0.6f);

        [Tooltip("Multiplicador de ancho según el TIEMPO consumido del día.\n" +
                 "X = fracción del día consumida (0 = recién arranca, 1 = se hace de " +
                 "noche). Y = multiplicador.\n\n" +
                 "Se mide con el tiempo que QUEDA, no con la hora del cielo: si una " +
                 "conversión devuelve segundos, la zona se agranda un poco. La " +
                 "presión la pone el reloj, y la zona la acompaña.\n\n" +
                 "Se apila con el de conversiones y el de comitiva; el piso de 1/3 al " +
                 "arrancar baja con el que esté más avanzado de los dos progresos.")]
        [SerializeField]
        private AnimationCurve widthByTimeUsed = AnimationCurve.Linear(0f, 1f, 1f, 0.65f);

        [Tooltip("Velocidad de la aguja en rad/s según el progreso.\n" +
                 "La recta por defecto es el lerp de la spec: 1.95 → 3.05.")]
        [SerializeField]
        private AnimationCurve speedByProgress = AnimationCurve.Linear(0f, 1.95f, 1f, 3.05f);

        [Header("Sentido y ritmo")]
        [Tooltip("Si cada eslabón sortea el sentido de la aguja: horario o " +
                 "antihorario. Apagado, gira siempre en horario.")]
        [SerializeField] private bool randomDirection = true;

        [Tooltip("Cuánto más rápido va cada eslabón CONSECUTIVO de una misma " +
                 "puerta. Con 0.15: el segundo va a ×1.15, el tercero a ×1.30.")]
        [SerializeField, Range(0f, 1f)] private float speedGainPerLink = 0.15f;

        [Header("Apretón")]
        [Tooltip("Tope, en segundos, del tramo de aguja contra el que se juzga un " +
                 "apretón.\n\n" +
                 "El apretón pasó en algún momento entre el último cuadro dibujado y " +
                 "este, así que se juzga contra TODO ese tramo. Eso lo hace justo a " +
                 "cualquier FPS: a 30 fps la aguja podía saltarse la zona entera " +
                 "entre dos cuadros. El tope evita que un tirón de FPS regale un " +
                 "acierto.")]
        [SerializeField, Range(0f, 0.1f)] private float pressSweepMaxSeconds = 0.05f;

        [Header("Cadena")]
        [Tooltip("Segundos de pausa entre eslabones de una objeción encadenada, " +
                 "mientras se muestra la réplica del vecino.")]
        [SerializeField, Range(0f, 2f)] private float chainPauseSeconds = 0.30f;

        /// <summary>
        /// Mantiene los pares mínimo/máximo en orden mientras se tunea. Sin esto,
        /// arrastrar un mínimo por encima de su máximo en el Inspector no da ningún
        /// error: el juego sigue andando con un rango al revés y nadie se entera.
        /// </summary>
        private void OnValidate()
        {
            if (zoneAheadMax < zoneAheadMin) zoneAheadMax = zoneAheadMin;
            if (perfectEnd < perfectStart) perfectEnd = perfectStart;
            if (widthAtBestPrecision < widthAtWorstPrecision) widthAtBestPrecision = widthAtWorstPrecision;
            if (openingMinimumRadians < minimumWidthRadians) openingMinimumRadians = minimumWidthRadians;
        }

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

        /// <summary>Piso absoluto del ancho de zona, con la dificultad al máximo.</summary>
        public float MinimumWidthRadians => minimumWidthRadians;

        /// <summary>Si cada eslabón sortea el sentido de la aguja.</summary>
        public bool RandomDirection => randomDirection;

        /// <summary>Tope del tramo de aguja contra el que se juzga un apretón.</summary>
        public float PressSweepMaxSeconds => pressSweepMaxSeconds;

        /// <summary>Pausa entre eslabones encadenados.</summary>
        public float ChainPauseSeconds => chainPauseSeconds;

        /// <summary>Ancho base de zona según la precisión del timbrazo.</summary>
        public float BaseWidthFor(float precision)
        {
            return Mathf.Lerp(widthAtWorstPrecision, widthAtBestPrecision, Mathf.Clamp01(precision));
        }

        /// <summary>
        /// Piso del ancho de zona para ese progreso: arranca en el de apertura y
        /// baja hasta el absoluto con la dificultad al máximo.
        /// </summary>
        public float MinimumWidthAt(float progress)
        {
            return Mathf.Lerp(openingMinimumRadians, minimumWidthRadians, Mathf.Clamp01(progress));
        }

        /// <summary>Multiplicador de ancho por progreso de la partida.</summary>
        public float WidthMultiplierAt(float progress)
        {
            return widthByProgress.Evaluate(Mathf.Clamp01(progress));
        }

        /// <summary>Multiplicador de ancho por tiempo consumido del día, 0..1.</summary>
        public float WidthMultiplierAtTime(float timeUsed)
        {
            return widthByTimeUsed.Evaluate(Mathf.Clamp01(timeUsed));
        }

        /// <summary>Velocidad de aguja en rad/s por progreso de la partida.</summary>
        public float NeedleSpeedAt(float progress)
        {
            return speedByProgress.Evaluate(Mathf.Clamp01(progress));
        }

        /// <summary>
        /// Multiplicador de velocidad del eslabón <paramref name="linkIndex"/>,
        /// contando desde cero. El primero va a ×1.
        /// </summary>
        public float SpeedMultiplierForLink(int linkIndex)
        {
            return 1f + speedGainPerLink * Mathf.Max(0, linkIndex);
        }
    }
}
