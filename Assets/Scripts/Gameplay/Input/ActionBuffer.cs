namespace BuenosDias.Gameplay
{
    /// <summary>
    /// Guarda un apretón unos instantes, por si llega un poco ANTES de poder usarse.
    ///
    /// Sin esto, un click dado un pelito antes de que el predicador termine de
    /// llegar a la puerta —o justo antes de que se acabe la espera— se perdía, y
    /// se sentía como un control que no respondió. Con el buffer el apretón queda
    /// pendiente y se gasta apenas el estado lo acepta.
    ///
    /// Se gasta UNA sola vez: <see cref="TryConsume"/> lo borra, así un apretón no
    /// puede tocar el timbre y además insistir.
    ///
    /// Es una clase plana para poder testear la ventana sin dispositivos ni reloj.
    /// </summary>
    public sealed class ActionBuffer
    {
        private readonly float windowSeconds;
        private float recordedAt = float.NegativeInfinity;

        /// <summary>
        /// <paramref name="windowSeconds"/> es cuánto sigue vigente un apretón
        /// guardado. En 0 solo vale en el mismo instante en que se guardó.
        /// </summary>
        public ActionBuffer(float windowSeconds)
        {
            this.windowSeconds = windowSeconds < 0f ? 0f : windowSeconds;
        }

        /// <summary>Si hay un apretón guardado que todavía vale.</summary>
        public bool HasPending(float now) => now - recordedAt <= windowSeconds;

        /// <summary>Guarda un apretón. Si ya había uno, el nuevo lo reemplaza.</summary>
        public void Record(float now) => recordedAt = now;

        /// <summary>Gasta el apretón guardado, si todavía vale. Devuelve si había.</summary>
        public bool TryConsume(float now)
        {
            if (!HasPending(now)) return false;

            recordedAt = float.NegativeInfinity;
            return true;
        }

        /// <summary>Descarta lo guardado.</summary>
        public void Clear() => recordedAt = float.NegativeInfinity;
    }
}
