namespace BuenosDias.Gameplay
{
    /// <summary>
    /// De dónde salen las tres acciones. Hay dos implementaciones y son dos
    /// esquemas de control distintos, no una envoltura de la otra.
    ///
    /// Quien consume esto no sabe cuál está activa, y ese es el punto: la FSM
    /// pregunta por acciones y nunca por teclas.
    /// </summary>
    public interface IInputSource
    {
        /// <summary>Si la acción se disparó en ESTE cuadro. Es un pulso.</summary>
        bool Pressed(GameAction action);

        /// <summary>Si la acción está sostenida ahora mismo. Es un estado.</summary>
        bool Held(GameAction action);

        /// <summary>
        /// Qué espera el juego. La fuente de objeto lo ignora; la de un botón lo
        /// usa para saber qué significa el apretón.
        /// </summary>
        InputContext Context { get; set; }

        /// <summary>Cuánto lleva del gesto mantenido, 0..1. Solo lo usa la selección.</summary>
        float HoldProgress { get; }

        /// <summary>Hay que refrescar el estado interno una vez por cuadro.</summary>
        void Tick();
    }
}
