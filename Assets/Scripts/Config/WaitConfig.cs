using UnityEngine;

namespace BuenosDias.Config
{
    /// <summary>
    /// La espera en la puerta, y los tells que delatan si hay alguien.
    ///
    /// Leer estos tells es la habilidad central del juego, así que todo lo que
    /// haga a su legibilidad se ajusta desde acá.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WaitConfig", menuName = "Buenos Días/Config/Espera", order = 22)]
    public sealed class WaitConfig : ScriptableObject
    {
        [Header("Duración")]
        [Tooltip("Espera mínima en segundos. Se sortea por casa.")]
        [SerializeField, Min(0.1f)] private float minimumSeconds = 1.8f;

        [Tooltip("Espera máxima en segundos. La religión multiplica el resultado.")]
        [SerializeField, Min(0.1f)] private float maximumSeconds = 2.8f;

        [Header("Tell de cortina")]
        [Tooltip("En qué punto de la espera aparece el primer asomo, como fracción " +
                 "de la duración total. 0.55 = un poco pasada la mitad.")]
        [SerializeField, Range(0f, 1f)] private float tellFraction = 0.55f;

        [Tooltip("Cada cuántos segundos se repite el asomo mientras siga la espera.\n" +
                 "La repetición es a propósito: si el jugador estaba mirando la puerta " +
                 "y se perdió el primero, tiene otro.\n" +
                 "OJO: la DURACIÓN del asomo (0.4 s) no está acá, vive en el clip " +
                 "Cortina_Asomo y se edita desde la ventana de Animation.")]
        [SerializeField, Min(0.1f)] private float tellRepeatSeconds = 1.2f;

        [Header("Pasos")]
        [Tooltip("Segundos entre pasos al empezar a escucharse. Van acelerando.")]
        [SerializeField, Min(0.01f)] private float stepIntervalStart = 0.34f;

        [Tooltip("Segundos entre pasos justo antes de que se abra la puerta.")]
        [SerializeField, Min(0.01f)] private float stepIntervalEnd = 0.17f;

        [Header("Casa vacía")]
        [Tooltip("Segundos EXTRA de silencio, después de terminada la espera, antes " +
                 "de mostrar NO HAY NADIE. El silencio es la información: no se " +
                 "adelanta el cartel.")]
        [SerializeField, Min(0f)] private float nobodyHomeDelaySeconds = 0.85f;

        /// <summary>Fracción de la espera a la que aparece el primer asomo.</summary>
        public float TellFraction => tellFraction;

        /// <summary>Segundos entre asomos.</summary>
        public float TellRepeatSeconds => tellRepeatSeconds;

        /// <summary>Demora extra antes del cartel de casa vacía.</summary>
        public float NobodyHomeDelaySeconds => nobodyHomeDelaySeconds;

        /// <summary>
        /// Sortea una duración de espera para una casa, ya multiplicada por la
        /// religión. Recibe el <see cref="System.Random"/> de la partida para que
        /// la generación siga siendo reproducible.
        /// </summary>
        public float RollDuration(System.Random random, ReligionDefinition religion)
        {
            float t = (float)random.NextDouble();
            float seconds = Mathf.Lerp(minimumSeconds, maximumSeconds, t);
            float multiplier = religion != null ? religion.WaitDuration : 1f;
            return seconds * multiplier;
        }

        /// <summary>
        /// Intervalo entre pasos según cuánto falta para que abran.
        /// <paramref name="progress"/> va de 0 (recién arrancaron) a 1 (abren ya).
        /// </summary>
        public float StepIntervalAt(float progress)
        {
            return Mathf.Lerp(stepIntervalStart, stepIntervalEnd, Mathf.Clamp01(progress));
        }
    }
}
