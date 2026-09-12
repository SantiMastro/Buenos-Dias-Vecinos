using UnityEngine;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// Modalidad de TECLADO DOS BOTONES: el intermedio entre un botón y el objeto.
    ///
    /// Existe por un problema real del esquema de un botón: en
    /// <see cref="InputContext.SinRespuesta"/> el apretón insiste, así que no
    /// quedaba forma de bajarse y seguir caminando. La salida obvia habría sido un
    /// mantenido, pero **la decisión de insistir o irse tiene que ser rápida**, y
    /// medio segundo de espera la arruina justo cuando tiene que doler.
    ///
    /// Con dos botones el problema desaparece sin ningún gesto temporizado:
    ///
    /// <list type="bullet">
    /// <item><b>A</b> es todo lo que hace el botón único: timbre, libro y skillcheck,
    /// según el estado.</item>
    /// <item><b>B</b> es el felpudo, y nada más.</item>
    /// </list>
    ///
    /// Acá **no hay mantenidos en ninguna parte**: hasta confirmar la religión, que
    /// con un botón era el gesto sostenido, pasa a ser B. Eso lo hace el más
    /// directo de los tres esquemas.
    ///
    /// El felpudo es un TOGGLE y no un sostenido, a diferencia de la modalidad de
    /// objeto. Una tecla no se siente bajo el pie: soltarla sin querer abandonaría
    /// una puerta en silencio, y no habría nada que delatara por qué. Con el toggle,
    /// estar en el felpudo es un estado que se decide.
    ///
    /// Cuando A y B llegan juntos decide <see cref="TwoButtonArbiter"/>: sin él, un
    /// solo apretón podía frenar y tocar el timbre a la vez.
    /// </summary>
    public sealed class TwoButtonInputSource : IInputSource
    {
        private readonly PhysicalBinding a;
        private readonly PhysicalBinding b;
        private readonly TwoButtonArbiter arbiter;
        private readonly bool matIsHeld;

        private TwoButtonFrame frame;
        private bool onMat;

        /// <summary>Qué espera el juego. Le da significado al botón A.</summary>
        public InputContext Context { get; set; } = InputContext.Seleccion;

        /// <summary>Siempre 0: en este esquema no hay ningún gesto sostenido.</summary>
        public float HoldProgress => 0f;

        /// <summary>
        /// Toma los dos bindings ya configurados y cuánto después de B se descarta
        /// A (ver <see cref="TwoButtonArbiter"/>).
        ///
        /// <paramref name="matIsHeld"/> cambia cómo se lee B jugando: sostenido en
        /// vez de toggle. Es para la plancha física, que manda la tecla MIENTRAS
        /// está pisada; con toggle, bajarse de la plancha no haría nada.
        /// </summary>
        public TwoButtonInputSource(
            PhysicalBinding a, PhysicalBinding b, float coincidenceSeconds = 0.1f, bool matIsHeld = false)
        {
            this.a = a;
            this.b = b;
            this.matIsHeld = matIsHeld;
            arbiter = new TwoButtonArbiter(coincidenceSeconds);
        }

        /// <summary>
        /// Lee A y B UNA vez por cuadro y los arbitra.
        ///
        /// Se puede resolver acá y no al consultar porque <see cref="GameInput"/>
        /// llama a esto con orden de ejecución adelantado: todos los que preguntan
        /// después, en su propio Update, ven lo mismo.
        ///
        /// B tiene DOS trabajos según dónde estemos, y no es un capricho: en la
        /// pantalla de religiones no existe el felpudo, así que la tecla estaría
        /// muerta justo donde hace falta confirmar. Con un botón eso lo resolvía el
        /// mantenido; acá lo resuelve B, y la modalidad se queda sin gestos
        /// temporizados en ninguna parte.
        /// </summary>
        public void Tick()
        {
            bool aPressed = a != null && a.WasPressedThisFrame();
            bool bPressed = b != null && b.WasPressedThisFrame();

            frame = arbiter.Resolve(aPressed, bPressed, Context, Time.unscaledTime);
            if (frame.ToggleMat && !matIsHeld) onMat = !onMat;
        }

        /// <summary>Si esa acción se disparó en este cuadro.</summary>
        public bool Pressed(GameAction action)
        {
            // El felpudo es un estado: no tiene pulso propio en esta modalidad.
            if (action == GameAction.Felpudo) return false;

            // Confirmar la religión es lo único que dispara B como pulso.
            if (action == GameAction.Libro && frame.Confirmed) return true;

            return frame.APulse && Resolve() == action;
        }

        /// <summary>
        /// Si el botón que AHORA significa esa acción está apretado. El felpudo es
        /// el toggle; las otras dos son el botón A con el significado que le da
        /// <see cref="Resolve"/>.
        ///
        /// No cambia ninguna regla —acá no hay gestos sostenidos— pero sí deja que
        /// la vista sepa que el timbre sigue apretado, que es lo que hace que el
        /// brazo se quede estirado en vez de volver solo a los 0,25 s.
        /// </summary>
        public bool Held(GameAction action)
        {
            if (action == GameAction.Felpudo)
                return matIsHeld ? b != null && b.IsPressed() : onMat;

            return a != null && a.IsPressed() && Resolve() == action;
        }

        /// <summary>
        /// Qué significa el botón A en cada momento. Es la misma tabla que la del
        /// botón único MENOS el felpudo, que acá tiene su propia tecla y por eso
        /// nunca compite con nada.
        /// </summary>
        private GameAction Resolve()
        {
            switch (Context)
            {
                // Elegir religión: A pasa a la siguiente y B confirma, sin esperar
                // nada. Con un botón esto era toque contra mantenido.
                case InputContext.Seleccion: return GameAction.Timbre;

                case InputContext.EnElFelpudo: return GameAction.Timbre;

                // El estado que motivó esta modalidad: A insiste, B se baja, y las
                // dos salidas están disponibles en el mismo instante.
                case InputContext.SinRespuesta: return GameAction.Timbre;

                case InputContext.Atendido: return GameAction.Libro;

                // Caminando, esperando y resuelto: lo único que se puede querer es
                // subirse o bajarse, y eso es B. En el final A reinicia.
                default: return GameAction.Timbre;
            }
        }
    }
}
