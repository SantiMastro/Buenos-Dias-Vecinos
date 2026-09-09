namespace BuenosDias.Gameplay
{
    /// <summary>
    /// En qué está el predicador. Los estados nunca se solapan.
    ///
    /// Los cuatro del medio son "con el pie en el felpudo": se entra pisando la
    /// plancha y se sale soltándola. Bajarse está permitido en todos menos en
    /// <see cref="Atendido"/>, donde el vecino ya abrió la puerta.
    /// </summary>
    public enum PreacherState
    {
        /// <summary>Avanzando por la vereda, sin pisar nada.</summary>
        Caminando,

        /// <summary>
        /// Frenó sobre el felpudo y camina hasta la puerta.
        ///
        /// La puntería ya se midió al frenar; esto es solo el viaje. Cuanto peor
        /// frenó, más largo, y por eso frenar mal cuesta segundos aunque el
        /// castigo sobre el skillcheck esté apagado. Si se pasó de la puerta,
        /// vuelve para atrás caminando, que se vea que se dio cuenta.
        /// </summary>
        Acercandose,

        /// <summary>
        /// Parado en la puerta, sin tocar todavía.
        ///
        /// También es el estado de haberse subido al felpudo LEJOS de cualquier
        /// puerta: ahí frena igual y no hay timbre que tocar. El costo de bajarse
        /// mal es el tiempo perdido.
        /// </summary>
        EnElFelpudo,

        /// <summary>Tocó el timbre y espera a ver si abren.</summary>
        Esperando,

        /// <summary>Abrieron. Corre la cadena de objeciones.</summary>
        Atendido,

        /// <summary>
        /// Nadie abrió, y ahí está la decisión: insistir o irse.
        ///
        /// La casa puede estar vacía O tener adentro a alguien que decidió no
        /// atender. El estado dice que la puerta no se abrió, no por qué; el asomo
        /// de cortina es lo que le dice al jugador cuál de las dos es.
        /// </summary>
        SinRespuesta,

        /// <summary>
        /// La puerta se resolvió y hay que bajarse a mano para seguir.
        ///
        /// El tiempo muerto es deliberado: convertir cuesta algo más que ganar el
        /// skillcheck. Antes el juego volvía a caminar solo.
        /// </summary>
        Resuelto
    }
}
