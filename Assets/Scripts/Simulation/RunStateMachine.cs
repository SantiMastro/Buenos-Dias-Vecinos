namespace BuenosDias.Simulation
{
    /// <summary>Las tres etapas de una partida. Nunca se solapan.</summary>
    public enum RunPhase
    {
        /// <summary>Eligiendo religión. El mundo está quieto.</summary>
        Seleccion,

        /// <summary>El día corre. Es la única etapa en la que se juega.</summary>
        Jugando,

        /// <summary>Se hizo de noche y se muestra el final.</summary>
        Final,

        /// <summary>Pantalla de título. Antes de elegir religión; solo la primera vez.</summary>
        Menu
    }

    /// <summary>
    /// El estado de la partida, como máquina explícita.
    ///
    /// Cada transición declara DESDE dónde sale, y una que salga de otro lado no
    /// pasa: devuelve <c>false</c> y no toca nada. Eso hace que los avisos
    /// repetidos —el final llegando dos veces, el botón apretado dos veces en el
    /// mismo cuadro— sean inofensivos sin que cada quien tenga que acordarse de
    /// llevar su propio flag de "ya lo hice".
    ///
    /// Es una clase plana a propósito: la partida entera se puede recorrer en un
    /// test sin entrar a Play Mode.
    /// </summary>
    public sealed class RunStateMachine
    {
        /// <summary>Etapa actual. Arranca eligiendo religión.</summary>
        public RunPhase Phase { get; private set; }

        /// <summary>Arranca en la etapa pedida (por defecto, la selección).</summary>
        public RunStateMachine(RunPhase initial = RunPhase.Seleccion)
        {
            Phase = initial;
        }

        /// <summary>Sale del menú a la selección de religión.</summary>
        public bool StartSelection() => MoveTo(RunPhase.Menu, RunPhase.Seleccion);

        /// <summary>Se saltea el menú sin avisar a nadie. Solo antes de suscribirse.</summary>
        public void SkipMenu()
        {
            if (Phase == RunPhase.Menu) Phase = RunPhase.Seleccion;
        }

        /// <summary>Avisa el cambio, ya con la etapa nueva puesta.</summary>
        public event System.Action<RunPhase> Changed;

        /// <summary>
        /// Se eligió religión y arranca el día. Solo vale desde la selección.
        /// </summary>
        public bool Confirm() => MoveTo(RunPhase.Seleccion, RunPhase.Jugando);

        /// <summary>
        /// Se hizo de noche. Solo vale jugando: si el reloj avisa dos veces, la
        /// segunda no dispara el final de nuevo.
        /// </summary>
        public bool Finish() => MoveTo(RunPhase.Jugando, RunPhase.Final);

        /// <summary>
        /// Vuelve a la selección para la partida siguiente. Solo vale desde el
        /// final: apretar el botón mientras se juega no reinicia nada.
        /// </summary>
        public bool Restart() => MoveTo(RunPhase.Final, RunPhase.Seleccion);

        private bool MoveTo(RunPhase from, RunPhase to)
        {
            if (Phase != from) return false;

            Phase = to;
            Changed?.Invoke(to);
            return true;
        }
    }
}
