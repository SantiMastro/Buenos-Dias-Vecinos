using UnityEngine;

namespace BuenosDias.Config
{
    /// <summary>
    /// Rango del timbre y cómo la puntería se traduce en dificultad.
    ///
    /// La precisión es la mecánica que premia acercarse a la puerta: tocar
    /// pegado da la zona de skillcheck más ancha, tocar al borde del alcance la
    /// más finita.
    /// </summary>
    [CreateAssetMenu(
        fileName = "DoorbellConfig", menuName = "Buenos Días/Config/Timbre", order = 21)]
    public sealed class DoorbellConfig : ScriptableObject
    {
        [Header("Alcance")]
        [Tooltip("Píxeles ANTES de la puerta desde los que ya se puede tocar.")]
        [SerializeField, Min(0f)] private float reachBeforePixels = 78f;

        [Tooltip("Píxeles DESPUÉS de la puerta en los que todavía se puede tocar. " +
                 "Es menor que el de antes a propósito: pasarse se castiga.")]
        [SerializeField, Min(0f)] private float reachAfterPixels = 46f;

        [Header("Coyote")]
        [Tooltip("Píxeles EXTRA, pasado el alcance de DESPUÉS de la puerta, en los que " +
                 "frenar todavía agarra la puerta: el predicador se da vuelta y vuelve.\n\n" +
                 "Es el perdón para el que se pasó por un pelo. Adentro de esta franja " +
                 "la puntería es CERO: se perdona el frenazo, no se premia. Como el " +
                 "alcance, se mide en TIEMPO, así que el ritmo del día lo escala. El " +
                 "cartelito de 'acá podés tocar' no lo muestra, a propósito.\n\n" +
                 "0 lo apaga.")]
        [SerializeField, Range(0f, 120f)] private float coyotePixels = 40f;

        [Header("Bloqueo")]
        [Tooltip("Segundos tras tocar el timbre durante los que NO se puede abortar. " +
                 "Evita que un doble toque accidental cancele la espera recién empezada.")]
        [SerializeField, Range(0f, 1f)] private float abortLockoutSeconds = 0.32f;

        [Header("Precisión")]
        [Tooltip("Cómo se traduce la distancia a la puerta en precisión 0..1.\n" +
                 "X = distancia normalizada al borde del alcance (0 = pegado a la " +
                 "puerta, 1 = al borde).\n" +
                 "Y = precisión resultante (1 = mejor).\n" +
                 "La recta por defecto es la de la spec; doblala para premiar más " +
                 "o menos el acercarse.")]
        [SerializeField]
        private AnimationCurve precisionByDistance = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Header("Felpudo")]
        [Tooltip("Velocidad de la caminata hasta la puerta, como fracción de la " +
                 "velocidad normal. En 1 camina igual que patrullando.")]
        [SerializeField, Range(0.2f, 3f)] private float approachSpeedScale = 1f;

        [Tooltip("A cuántos píxeles de la puerta se considera que llegó. Por debajo " +
                 "de un píxel el predicador tiembla buscando el punto exacto.")]
        [SerializeField, Range(0.5f, 8f)] private float arrivalTolerancePixels = 1.5f;

        [Tooltip("Si la puntería del frenazo además ENDURECE el skillcheck.\n\n" +
                 "Apagalo para probar si el costo en TIEMPO de caminar hasta la " +
                 "puerta ya alcanza como castigo. Frenar mal castiga dos veces —se " +
                 "pierden segundos caminando Y queda el check más difícil— y puede " +
                 "ser demasiado.")]
        [SerializeField] private bool precisionAffectsSkillcheck = true;

        [Header("Insistir")]
        [Tooltip("Chance de que el que está adentro ABRA en el primer timbrazo. " +
                 "⚠️ Esto NO es la chance de que haya alguien: eso se tira una sola " +
                 "vez por casa y no se vuelve a tocar. Acá se decide si el que está " +
                 "se molesta en atender.")]
        [SerializeField, Range(0f, 1f)] private float openChanceFirst = 0.62f;

        [Tooltip("Cuánto sube esa chance con cada insistencia.")]
        [SerializeField, Range(0f, 1f)] private float openChanceGainPerInsist = 0.18f;

        [Tooltip("Cuánto se ACORTA la espera con cada insistencia, como fracción. " +
                 "Con 0.20, el segundo timbrazo espera el 80% del primero.")]
        [SerializeField, Range(0f, 0.9f)] private float waitShortenPerInsist = 0.20f;

        [Tooltip("Piso de la espera, para que insistir mucho no la deje en cero.")]
        [SerializeField, Range(0.05f, 1f)] private float waitScaleMinimum = 0.35f;

        [Tooltip("Cuánto se ACHICA la zona del skillcheck por cada insistencia. " +
                 "Salen molestos: si te atienden al tercer intento, la zona es más " +
                 "chica que si te atendieron al primero.")]
        [SerializeField, Range(0f, 0.5f)] private float zonePenaltyPerInsist = 0.13f;

        [Tooltip("Piso de ese castigo, para que la zona no desaparezca.")]
        [SerializeField, Range(0.1f, 1f)] private float zoneScaleMinimum = 0.55f;

        [Tooltip("Tope de timbrazos por casa, contando el primero. En 0 es infinito " +
                 "y lo único que frena es el reloj, que ya es un costo real.")]
        [SerializeField, Min(0)] private int maximumAttempts = 0;

        /// <summary>Alcance antes de la puerta, en unidades.</summary>
        public float ReachBefore => ProjectConstants.ToUnits(reachBeforePixels);

        /// <summary>Alcance después de la puerta, en unidades.</summary>
        public float ReachAfter => ProjectConstants.ToUnits(reachAfterPixels);

        /// <summary>Segundos de bloqueo del aborto tras tocar.</summary>
        public float AbortLockoutSeconds => abortLockoutSeconds;

        /// <summary>
        /// Indica si a esa distancia con signo de la puerta (en unidades, negativo
        /// antes de la puerta) el timbre está al alcance.
        /// </summary>
        public bool IsInReach(float signedDistanceToDoor)
        {
            return signedDistanceToDoor >= -ReachBefore && signedDistanceToDoor <= ReachAfter;
        }

        /// <summary>Franja de perdón pasada la puerta, en unidades.</summary>
        public float Coyote => ProjectConstants.ToUnits(coyotePixels);

        /// <summary>
        /// Si esa distancia cae en la franja de coyote: pasado el alcance de
        /// después de la puerta, pero no tanto.
        /// </summary>
        public bool IsInCoyote(float signedDistanceToDoor)
        {
            return signedDistanceToDoor > ReachAfter && signedDistanceToDoor <= ReachAfter + Coyote;
        }

        /// <summary>
        /// Si frenar a esa distancia agarra la puerta: dentro del alcance, o en la
        /// franja de coyote. La puntería la sigue diciendo <see cref="PrecisionFor"/>,
        /// que en el coyote da cero.
        /// </summary>
        public bool CanGrab(float signedDistanceToDoor)
        {
            return IsInReach(signedDistanceToDoor) || IsInCoyote(signedDistanceToDoor);
        }

        /// <summary>
        /// Precisión 0..1 del timbrazo. 1 es pegado a la puerta.
        /// Devuelve 0 si está fuera de alcance.
        /// </summary>
        public float PrecisionFor(float signedDistanceToDoor)
        {
            if (!IsInReach(signedDistanceToDoor)) return 0f;

            float reach = signedDistanceToDoor < 0f ? ReachBefore : ReachAfter;
            if (reach <= 0f) return 1f;

            float normalized = Mathf.Clamp01(Mathf.Abs(signedDistanceToDoor) / reach);
            return Mathf.Clamp01(precisionByDistance.Evaluate(normalized));
        }

        /// <summary>
        /// Chance de que el ocupante abra en ese timbrazo. <paramref name="insists"/>
        /// es cuántas veces se insistió ANTES de este, así que el primero va con 0.
        ///
        /// ⚠️ Solo tiene sentido preguntarla si hay alguien. En una casa vacía no
        /// hay nada que abrir y ningún número de insistencias lo cambia.
        /// </summary>
        public float OpenChanceAt(int insists)
        {
            return Mathf.Clamp01(openChanceFirst + openChanceGainPerInsist * Mathf.Max(0, insists));
        }

        /// <summary>Cuánto dura la espera de ese timbrazo, como fracción de la primera.</summary>
        public float WaitScaleAt(int insists)
        {
            return Mathf.Max(waitScaleMinimum, 1f - waitShortenPerInsist * Mathf.Max(0, insists));
        }

        /// <summary>
        /// Cuánto se achica la zona del skillcheck por haber insistido. Se
        /// multiplica sobre el ancho que ya salió de la puntería y la comitiva.
        /// </summary>
        public float ZoneScaleAt(int insists)
        {
            return Mathf.Max(zoneScaleMinimum, 1f - zonePenaltyPerInsist * Mathf.Max(0, insists));
        }

        /// <summary>Velocidad de acercamiento a la puerta, en unidades por segundo.</summary>
        public float ApproachSpeed(float walkSpeed) => walkSpeed * approachSpeedScale;

        /// <summary>Distancia a la puerta, en unidades, por debajo de la cual ya llegó.</summary>
        public float ArrivalTolerance => ProjectConstants.ToUnits(arrivalTolerancePixels);

        /// <summary>
        /// Qué puntería ve el skillcheck.
        ///
        /// Con el interruptor apagado devuelve siempre 1: frenar mal sigue costando
        /// los segundos de caminata hasta la puerta, pero deja de endurecer además
        /// la cadena de objeciones. Es la perilla para probar si un castigo solo
        /// alcanza.
        /// </summary>
        public float SkillcheckPrecisionFor(float precision)
        {
            return precisionAffectsSkillcheck ? precision : 1f;
        }

        /// <summary>Si todavía se puede tocar de nuevo. Con el tope en 0 siempre se puede.</summary>
        public bool CanRingAgain(int attemptsSoFar)
        {
            return maximumAttempts <= 0 || attemptsSoFar < maximumAttempts;
        }
    }
}
