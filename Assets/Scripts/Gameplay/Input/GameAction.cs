namespace BuenosDias.Gameplay
{
    /// <summary>
    /// Las tres entradas del control físico. Son ACCIONES, no botones: quien
    /// pregunta quiere saber si le tocaron el timbre, no si alguien apretó algo.
    ///
    /// Antes había una sola y la FSM preguntaba "¿apretaron?". Con tres entradas
    /// esa pregunta ya no significa nada, porque la respuesta depende de CUÁL.
    /// </summary>
    public enum GameAction
    {
        /// <summary>Pulso. El timbre de pared.</summary>
        Timbre,

        /// <summary>Pulso. Cerrar el libro arranca y sostiene el skillcheck.</summary>
        Libro,

        /// <summary>ESTADO, no pulso. Está parado arriba de la plancha.</summary>
        Felpudo
    }

    /// <summary>
    /// Qué está esperando el juego en este momento.
    ///
    /// Existe SOLO para la modalidad de un botón: con un botón único, el
    /// significado del apretón no puede salir del hardware —es siempre el mismo
    /// botón— así que tiene que salir del estado del juego.
    ///
    /// En modalidad de objeto se ignora por completo: ahí cada acción tiene su
    /// entrada física y no hay nada que desambiguar.
    /// </summary>
    public enum InputContext
    {
        /// <summary>Eligiendo religión, antes de que empiece el día.</summary>
        Seleccion,

        /// <summary>Caminando por la vereda, sin puerta a la vista.</summary>
        Caminando,

        /// <summary>Parado en el felpudo, todavía sin tocar el timbre.</summary>
        EnElFelpudo,

        /// <summary>Timbre tocado, esperando a ver si abren.</summary>
        Esperando,

        /// <summary>Nadie abrió. Se puede insistir o irse.</summary>
        SinRespuesta,

        /// <summary>Puerta abierta, cadena de objeciones en curso.</summary>
        Atendido,

        /// <summary>La puerta se resolvió. Falta bajarse para seguir.</summary>
        Resuelto,

        /// <summary>Pantalla de final.</summary>
        Final
    }
}
