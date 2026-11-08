using UnityEngine;

namespace BuenosDias.Gameplay
{
    /// <summary>Qué teclas manda la placa del felpudo, y por lo tanto cómo se lee.</summary>
    public enum TeclasFelpudo
    {
        /// <summary>
        /// Un toque de Espacio al subirse y otro al bajarse (firmware v16). Cada
        /// toque alterna: si se pierde o sobra uno, el felpudo queda invertido.
        /// </summary>
        Espacio,

        /// <summary>
        /// S al subirse y B al bajarse (firmware v17). Cada tecla dice el estado,
        /// así que una perdida o repetida se corrige sola con la siguiente.
        /// </summary>
        SyB,

        /// <summary>
        /// S REPETIDA mientras hay alguien arriba, y una B al bajarse (firmware
        /// v19). El felpudo deja de ser un estado que se recuerda y pasa a ser un
        /// alquiler: el juego solo está frenado mientras sigan llegando S, y sin
        /// teclas camina. Una tecla perdida, una de más o una placa colgada se
        /// corrigen solas en el "Latido caduca" del InputConfig.
        /// </summary>
        SLatido
    }

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
    ///
    /// Con <see cref="TeclasFelpudo.SyB"/> el felpudo deja de ser un toggle: la
    /// placa manda una tecla al subirse y OTRA al bajarse, y cada una dice el
    /// estado. Para el resto del juego B "se apretó" solo cuando el felpudo
    /// cambia (ver <see cref="CuentaComoB"/>).
    ///
    /// Con <see cref="TeclasFelpudo.SLatido"/> deja de ser un estado que se
    /// recuerda: la placa repite S mientras hay alguien arriba y el juego solo se
    /// queda frenado mientras sigan llegando. El valor por defecto es ABAJO, o sea
    /// caminando. B corta al instante; si no llega, caduca sola.
    /// </summary>
    public sealed class TwoButtonInputSource : IInputSource
    {
        private readonly PhysicalBinding a;
        private readonly PhysicalBinding b;
        private readonly PhysicalBinding bajarse;
        private readonly TwoButtonArbiter arbiter;
        private readonly bool matIsHeld;
        private readonly float latidoSeconds;

        private TwoButtonFrame frame;
        private bool onMat;
        private float sinLatido = float.PositiveInfinity;
        private bool revoked;

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
        ///
        /// Con <paramref name="bajarse"/> el felpudo se lee con DOS teclas
        /// (<see cref="TeclasFelpudo.SyB"/>): <paramref name="b"/> es subirse y
        /// <paramref name="bajarse"/> es bajarse. Ahí no hay toggle ni sostenido, y
        /// <paramref name="matIsHeld"/> no se usa.
        ///
        /// Con <paramref name="latidoSeconds"/> mayor que 0 (<see cref="TeclasFelpudo.SLatido"/>)
        /// el felpudo es un alquiler: <paramref name="b"/> se REPITE mientras hay
        /// alguien arriba, y si pasan estos segundos sin recibirla el predicador
        /// vuelve a caminar. <paramref name="bajarse"/> lo corta al instante.
        /// </summary>
        public TwoButtonInputSource(
            PhysicalBinding a, PhysicalBinding b, float coincidenceSeconds = 0.1f, bool matIsHeld = false,
            PhysicalBinding bajarse = null, float latidoSeconds = 0f)
        {
            this.a = a;
            this.b = b;
            this.bajarse = bajarse;
            this.matIsHeld = matIsHeld;
            this.latidoSeconds = latidoSeconds;
            arbiter = new TwoButtonArbiter(coincidenceSeconds);
        }

        /// <summary>
        /// Cómo queda el felpudo con dos teclas. <paramref name="sube"/> y
        /// <paramref name="baja"/> son las teclas que llegaron en este cuadro. Si
        /// llegaron las dos —dos toques de la placa en un cuadro muy largo— manda la
        /// que sigue apretada, que es la última; si ya se soltaron las dos, queda
        /// abajo.
        /// </summary>
        public static bool SiguienteFelpudo(bool enFelpudo, bool sube, bool baja, bool subeSigueApretada)
        {
            if (sube && baja) return subeSigueApretada;
            if (sube) return true;
            if (baja) return false;
            return enFelpudo;
        }

        /// <summary>
        /// Lo más que cuenta un cuadro largo para la caducidad del latido. Si el
        /// juego se traba un segundo (carga, recolector de basura), los latidos que
        /// la placa mandó en el medio no se ven y, sin este tope, el felpudo caducaba
        /// de golpe y soltaba la puerta que se estaba atendiendo.
        /// </summary>
        public const float LatidoTopeCuadro = 0.05f;

        /// <summary>
        /// El felpudo con latido (<see cref="TeclasFelpudo.SLatido"/>). Devuelve si
        /// está pisado. <paramref name="sinLatido"/> son los segundos desde la
        /// última S y arranca en infinito: el valor por defecto es ABAJO.
        ///
        /// Una S (nueva o todavía apretada) renueva el alquiler; una B lo corta al
        /// instante; y sin ninguna de las dos caduca a los
        /// <paramref name="caducaSeconds"/>. Si llegan las dos en el mismo cuadro
        /// manda la S que sigue apretada, que es la última.
        /// </summary>
        public static bool SiguienteFelpudoLatido(
            bool sube, bool baja, bool subeSigueApretada, float deltaTime, float caducaSeconds,
            ref float sinLatido)
        {
            float paso = deltaTime < 0f ? 0f : (deltaTime > LatidoTopeCuadro ? LatidoTopeCuadro : deltaTime);
            sinLatido += paso;

            if (sube && baja) sinLatido = subeSigueApretada ? 0f : float.PositiveInfinity;
            else if (baja) sinLatido = float.PositiveInfinity;
            else if (sube || subeSigueApretada) sinLatido = 0f;

            return sinLatido <= caducaSeconds;
        }

        /// <summary>
        /// El seguro contra el felpudo trabado. Mientras está REVOCADO el felpudo se
        /// lee como bajado, diga lo que diga la placa; se levanta la primera vez
        /// que la placa dice "abajo" (una B, o que las S dejen de llegar).
        /// </summary>
        public static bool ResolverRevocado(bool next, ref bool revocado)
        {
            if (!revocado) return next;
            if (!next) revocado = false;
            return false;
        }

        /// <summary>
        /// Da el felpudo por trabado: lo suelta y lo ignora hasta que la placa diga
        /// "abajo". Devuelve false si esta modalidad no puede (con el toggle de
        /// Espacio no hay forma de saber qué dice la placa).
        /// </summary>
        public bool Revoke()
        {
            if (bajarse == null) return false;
            revoked = true;
            onMat = false;
            return true;
        }

        /// <summary>
        /// Si el cambio del felpudo cuenta como apretón de B para el árbitro (que
        /// confirma la religión y traba el timbre). Una tecla repetida no cambia
        /// nada y no cuenta: la placa reintenta y vuelve a mandar la tecla al salir
        /// de la pausa. En la selección solo confirma SUBIRSE: bajarse de la plancha
        /// después de confirmar no tiene que confirmar de nuevo.
        /// </summary>
        public static bool CuentaComoB(bool antes, bool despues, InputContext context)
        {
            if (antes == despues) return false;
            return despues || context != InputContext.Seleccion;
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

            if (bajarse != null)
            {
                // Dos teclas: el felpudo sigue a la placa en todos los contextos,
                // también en la selección, así no queda invertido al empezar.
                bool baja = bajarse.WasPressedThisFrame();
                bool subeApretada = b != null && b.IsPressed();
                bool next = latidoSeconds > 0f
                    ? SiguienteFelpudoLatido(
                        bPressed, baja, subeApretada, Time.unscaledDeltaTime, latidoSeconds, ref sinLatido)
                    : SiguienteFelpudo(onMat, bPressed, baja, subeApretada);
                next = ResolverRevocado(next, ref revoked);
                bPressed = CuentaComoB(onMat, next, Context);
                onMat = next;
                frame = arbiter.Resolve(aPressed, bPressed, Context, Time.unscaledTime);
                return;
            }

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
                return matIsHeld && bajarse == null ? b != null && b.IsPressed() : onMat;

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
