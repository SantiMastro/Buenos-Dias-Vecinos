namespace BuenosDias.Gameplay
{
    /// <summary>
    /// Modalidad de TECLADO: un botón único que significa cosas distintas según
    /// qué esté esperando el juego.
    ///
    /// ⚠️ Esto NO simula tres entradas. Es un esquema de control DISTINTO,
    /// declarado de una sola vez en <see cref="Resolve"/>. Con un botón el
    /// significado no puede venir del hardware —siempre es el mismo botón— así
    /// que viene del estado, y el único lugar honesto para escribirlo es acá.
    ///
    /// Es la modalidad por defecto a propósito: anda en cualquier máquina. La de
    /// objeto es la bandera que hay que acordarse de prender, y si alguien se
    /// olvida, lo peor que pasa es que juegue con el teclado.
    ///
    /// El felpudo es un TOGGLE acá y un estado sostenido allá. Es la única
    /// asimetría real entre las dos modalidades y no se puede evitar: con un
    /// botón no se puede estar pisando la plancha y pegarle al skillcheck a la
    /// vez.
    /// </summary>
    public sealed class SingleButtonInputSource : IInputSource
    {
        private readonly OneButtonInput button;

        private bool wasHeld;
        private bool holdSpent;

        private bool tapped;          // soltó sin completar el mantenido
        private bool holdCompleted;   // completó el mantenido en este cuadro
        private bool onMat;           // el toggle del felpudo

        /// <summary>Qué espera el juego. Acá SÍ se usa: es lo que da el significado.</summary>
        public InputContext Context { get; set; } = InputContext.Seleccion;

        /// <summary>Cuánto lleva del gesto mantenido, para la barrita.</summary>
        public float HoldProgress => button != null ? button.HoldProgress : 0f;

        /// <summary>Toma el lector crudo del botón.</summary>
        public SingleButtonInputSource(OneButtonInput button) => this.button = button;

        /// <summary>
        /// Refresca los dos gestos del cuadro.
        ///
        /// El toque se resuelve al SOLTAR y no al apretar, porque hasta que no
        /// suelta no se sabe si era un toque o el arranque de un mantenido. El
        /// mantenido se consume una sola vez para que sostener el botón no siga
        /// disparando la misma acción cuadro tras cuadro.
        /// </summary>
        public void Tick()
        {
            tapped = false;
            holdCompleted = false;

            if (button == null) return;

            bool held = button.IsHeld;

            if (button.HoldCompleted && !holdSpent)
            {
                holdCompleted = true;
                holdSpent = true;
            }

            if (wasHeld && !held)
            {
                tapped = !holdSpent;
                holdSpent = false;
            }

            wasHeld = held;
            Apply();
        }

        /// <summary>Si esa acción se disparó en este cuadro.</summary>
        public bool Pressed(GameAction action) => Resolve() == action && Fired();

        /// <summary>
        /// Si el botón que AHORA significa esa acción está apretado.
        ///
        /// El felpudo es el toggle. Las otras dos preguntan por el botón físico y
        /// dejan que <see cref="Resolve"/> ponga el significado, que es lo que
        /// permite que el brazo del predicador se quede estirado mientras el timbre
        /// siga apretado.
        ///
        /// ⚠️ En cuanto el mantenido se completa, <see cref="Resolve"/> cambia de
        /// acción y esto deja de decir que sí. Es lo correcto: con un botón,
        /// sostener en la puerta significa BAJARSE y no seguir tocando, y el brazo
        /// no tiene que mentir sobre eso.
        /// </summary>
        public bool Held(GameAction action)
        {
            if (action == GameAction.Felpudo) return onMat;

            return button != null && button.IsHeld && Resolve() == action;
        }

        /// <summary>
        /// El mapeo. Un solo switch, y es la definición completa del esquema de un
        /// botón: para saber qué hace apretar en cualquier momento del juego, se
        /// lee esta tabla y nada más.
        /// </summary>
        private GameAction Resolve()
        {
            switch (Context)
            {
                // Elegir religión: el toque pasa a la siguiente, el mantenido confirma.
                case InputContext.Seleccion:
                    return holdCompleted ? GameAction.Libro : GameAction.Timbre;

                // Subirse al felpudo de la casa que se tenga más cerca.
                case InputContext.Caminando:
                    return GameAction.Felpudo;

                // Parado en la puerta: el toque toca el timbre, el mantenido se
                // baja. Sin el mantenido no habría forma de arrepentirse antes de
                // tocar, porque el toque ya está tomado.
                case InputContext.EnElFelpudo:
                    return holdCompleted ? GameAction.Felpudo : GameAction.Timbre;

                // Durante la espera lo único que queda es arrepentirse.
                case InputContext.Esperando:
                    return GameAction.Felpudo;

                // La decisión del juego: el toque insiste, el mantenido se va.
                case InputContext.SinRespuesta:
                    return holdCompleted ? GameAction.Felpudo : GameAction.Timbre;

                // Puerta abierta: el botón es del skillcheck y de nada más.
                case InputContext.Atendido:
                    return GameAction.Libro;

                // Resuelta la puerta, lo único que queda es bajarse para seguir.
                case InputContext.Resuelto:
                    return GameAction.Felpudo;

                // En el final el apretón reinicia, y lo lee como timbrazo el
                // RunDirector. Cualquier otra acción acá no significaría nada.
                default:
                    return GameAction.Timbre;
            }
        }

        /// <summary>
        /// Si hubo gesto. El skillcheck es el único que responde al APRETAR y no al
        /// soltar: esperar a que suelte le agregaría al golpe una demora que el
        /// jugador siente como input laggeado, y ahí los milisegundos son el juego.
        /// </summary>
        private bool Fired()
        {
            if (Context == InputContext.Atendido) return button != null && button.PressedThisFrame;
            return tapped || holdCompleted;
        }

        /// <summary>Mueve el toggle del felpudo cuando el gesto resuelto es ese.</summary>
        private void Apply()
        {
            if (!Fired() || Resolve() != GameAction.Felpudo) return;

            onMat = Context == InputContext.Caminando;
        }
    }
}
