using UnityEngine;

namespace BuenosDias.Config
{
    /// <summary>Cómo camina el predicador.</summary>
    [CreateAssetMenu(
        fileName = "WalkConfig", menuName = "Buenos Días/Config/Caminata", order = 20)]
    public sealed class WalkConfig : ScriptableObject
    {
        [Header("Velocidad")]
        [Tooltip("Velocidad base en PÍXELES por segundo. 152 es el valor tuneado " +
                 "en el prototipo. La religión la multiplica.")]
        [SerializeField, Min(1f)] private float pixelsPerSecond = 152f;

        [Header("Aceleración del día")]
        [Tooltip("Multiplicador de velocidad según cuánto AVANZÓ el día, de 0 a 1. " +
                 "⚠ Tiene que empezar en 1: el día arranca a la velocidad de " +
                 "siempre y se acelera desde ahí. Una curva que empiece en otra " +
                 "cosa no cambia el final del día, cambia el juego entero.\n\n" +
                 "Lo que entra acá es el progreso MONÓTONO del atardecer y no el " +
                 "tiempo que queda: ganar segundos en una puerta no tiene que " +
                 "hacer caminar más lento, porque entonces convertir aflojaría la " +
                 "dificultad por una segunda vía que nadie decidió.")]
        [SerializeField]
        private AnimationCurve speedByDayProgress = AnimationCurve.EaseInOut(0f, 1f, 1f, 1.35f);

        [Header("Frenado")]
        [Tooltip("Segundos que tarda en frenar al tocar el timbre. La spec pide " +
                 "que se detenga EN SECO, así que 0 es el valor fiel; subilo solo " +
                 "si el corte se siente demasiado brusco.")]
        [SerializeField, Range(0f, 0.3f)] private float stopSeconds;

        /// <summary>Velocidad base en píxeles por segundo, como la escribió el diseño.</summary>
        public float PixelsPerSecond => pixelsPerSecond;

        /// <summary>Velocidad base en unidades de mundo por segundo.</summary>
        public float UnitsPerSecond => ProjectConstants.ToUnits(pixelsPerSecond);

        /// <summary>Segundos de frenado. 0 = corte en seco.</summary>
        public float StopSeconds => stopSeconds;

        /// <summary>
        /// Piso del ritmo. Existe por una razón boba y cara: una curva que alguien
        /// deje en 0 dejaría al predicador clavado en la vereda sin ningún error en
        /// la consola, y eso se lee como que el juego se colgó.
        /// </summary>
        private const float MinimumPace = 0.25f;

        /// <summary>Velocidad efectiva en unidades, ya multiplicada por la religión.</summary>
        public float UnitsPerSecondFor(ReligionDefinition religion)
        {
            float multiplier = religion != null ? religion.WalkSpeed : 1f;
            return UnitsPerSecond * multiplier;
        }

        /// <summary>
        /// Cuánto multiplica la velocidad a esa altura del día.
        ///
        /// Devuelve el RITMO y no la velocidad ya multiplicada porque el mismo
        /// número escala dos cosas: cuánto se avanza por segundo y cuánto mide la
        /// hitbox del felpudo. Que las dos salgan de acá es lo que garantiza que la
        /// ventana de frenado dure lo mismo en segundos cuando el día acelera.
        /// </summary>
        public float PaceAt(float dayProgress)
        {
            if (speedByDayProgress == null || speedByDayProgress.length == 0) return 1f;

            return Mathf.Max(
                MinimumPace, speedByDayProgress.Evaluate(Mathf.Clamp01(dayProgress)));
        }

        /// <summary>Velocidad efectiva con religión Y con el ritmo del día.</summary>
        public float UnitsPerSecondFor(ReligionDefinition religion, float pace)
        {
            return UnitsPerSecondFor(religion) * pace;
        }
    }
}
