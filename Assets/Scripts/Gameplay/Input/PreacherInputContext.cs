namespace BuenosDias.Gameplay
{
    /// <summary>
    /// Traduce el estado del predicador al contexto que necesita la entrada.
    ///
    /// Son dos vocabularios distintos y a propósito: la FSM habla de lo que está
    /// haciendo el personaje y la entrada de lo que se espera del jugador. No
    /// siempre hay uno a uno —caminar y acercarse a la puerta son estados
    /// distintos pero el mismo contexto, porque en los dos apretar significa lo
    /// mismo— y esa diferencia es justamente lo que se pierde si se los mezcla.
    ///
    /// Vive aparte del controlador para no hacerle cargar una tabla que no usa
    /// para nada más que pasarla.
    /// </summary>
    public static class PreacherInputContext
    {
        /// <summary>Qué espera el juego cuando el predicador está en ese estado.</summary>
        public static InputContext For(PreacherState state)
        {
            switch (state)
            {
                case PreacherState.EnElFelpudo: return InputContext.EnElFelpudo;
                case PreacherState.Esperando: return InputContext.Esperando;
                case PreacherState.Atendido: return InputContext.Atendido;
                case PreacherState.SinRespuesta: return InputContext.SinRespuesta;
                case PreacherState.Resuelto: return InputContext.Resuelto;

                // Caminando y Acercandose comparten contexto: en los dos, lo único
                // que puede querer el jugador es subirse o bajarse de la plancha.
                default: return InputContext.Caminando;
            }
        }
    }
}
