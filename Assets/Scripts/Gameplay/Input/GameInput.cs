using UnityEngine;
using UnityEngine.InputSystem;

namespace BuenosDias.Gameplay
{
    /// <summary>Con qué se juega.</summary>
    public enum InputMode
    {
        /// <summary>Un botón único. Anda en cualquier máquina. Es el default.</summary>
        Teclado,

        /// <summary>
        /// Dos botones: A hace todo lo del botón único, B es el felpudo.
        ///
        /// Es el intermedio, y existe por un problema real del esquema de un solo
        /// botón: en <see cref="InputContext.SinRespuesta"/> el apretón insiste, así
        /// que no quedaba forma de bajarse. Resolverlo con un mantenido habría
        /// arruinado la decisión, que tiene que poder tomarse rápido.
        /// </summary>
        TecladoDosBotones,

        /// <summary>Timbre, libro y felpudo por separado. Necesita el control físico.</summary>
        Objeto
    }

    /// <summary>
    /// La única puerta de entrada del input para todo el juego. Elige la
    /// modalidad y expone las tres acciones; quien la consume no sabe cuál está
    /// activa.
    ///
    /// No se autodetecta la modalidad a propósito. Las dos usan los mismos
    /// dispositivos —el control físico es un teclado y un mouse para Windows— así
    /// que adivinar saldría mal justo el día que importa.
    /// </summary>
    /// <remarks>
    /// El orden de ejecución va adelantado porque todos los que preguntan lo hacen
    /// en su propio <c>Update</c>. Sin esto, la mitad de los componentes leería el
    /// estado del cuadro anterior según el orden en que Unity los haya puesto, que
    /// es el tipo de bug que aparece una vez cada tantos arranques.
    /// </remarks>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class GameInput : MonoBehaviour
    {
        [Header("Modalidad")]
        [Tooltip("Teclado: un botón único, el esquema de siempre.\n" +
                 "Objeto: timbre, libro y felpudo por separado.\n\n" +
                 "El default es Teclado porque anda en cualquier máquina. Objeto es " +
                 "la bandera que hay que acordarse de PRENDER, y olvidarse solo " +
                 "significa jugar con el teclado.")]
        [SerializeField] private InputMode mode = InputMode.Teclado;

        [Header("Referencias")]
        [Tooltip("Lector crudo del botón único. Lo usa la modalidad de teclado.")]
        [SerializeField] private OneButtonInput button;

        [Header("Modalidad de dos botones")]
        [Tooltip("BOTÓN A: todo lo que hace el botón único —timbre, libro y " +
                 "skillcheck— según el estado. Por defecto el click izquierdo, que " +
                 "es el timbre físico ya soldado.")]
        [SerializeField]
        private PhysicalBinding botonA = new PhysicalBinding(BindingDevice.MouseIzquierdo, Key.Space);

        [Tooltip("BOTÓN B: subirse y bajarse del felpudo, y confirmar la religión. " +
                 "Es un TOGGLE, no un sostenido: una tecla no se siente bajo el pie, " +
                 "y soltarla sin querer abandonaría una puerta en silencio.")]
        [SerializeField]
        private PhysicalBinding botonB = new PhysicalBinding(BindingDevice.Teclado, Key.Space);

        [Header("Modalidad de objeto — entradas físicas")]
        [Tooltip("⚠️ FIJO EN HARDWARE: el pulsador del timbre está soldado al " +
                 "switch del botón izquierdo de una placa de mouse. Cambiarlo acá " +
                 "no cambia el fierro.")]
        [SerializeField]
        private PhysicalBinding timbre = new PhysicalBinding(BindingDevice.MouseIzquierdo, Key.Space);

        [Tooltip("El libro con sensor magnético. Cerrarlo manda un pulso.")]
        [SerializeField]
        private PhysicalBinding libro = new PhysicalBinding(BindingDevice.Teclado, Key.Enter);

        [Tooltip("La plancha que se pisa. Manda la tecla MIENTRAS está pisada.\n\n" +
                 "⚠️ Nunca Shift, Ctrl, Alt, Caps Lock, Tab, Esc ni la de Windows: " +
                 "Shift sostenida abre el diálogo de Sticky Keys ENCIMA del juego y " +
                 "Alt se roba el menú de la ventana.")]
        [SerializeField]
        private PhysicalBinding felpudo = new PhysicalBinding(BindingDevice.Teclado, Key.Space);

        private IInputSource source;

        /// <summary>Con qué modalidad se está jugando.</summary>
        public InputMode Mode => mode;

        /// <summary>
        /// Qué espera el juego. Lo escribe la FSM. En modalidad de objeto no hace
        /// nada, pero se escribe igual: que la FSM tenga que acordarse de decirlo
        /// solo en una de las dos modalidades sería una trampa.
        /// </summary>
        public InputContext Context
        {
            get => source != null ? source.Context : InputContext.Seleccion;
            set { if (source != null) source.Context = value; }
        }

        /// <summary>Cuánto lleva del gesto mantenido, 0..1. Lo dibuja la barrita.</summary>
        public float HoldProgress => source != null ? source.HoldProgress : 0f;

        /// <summary>Si esa acción se disparó en este cuadro.</summary>
        public bool Pressed(GameAction action) => source != null && source.Pressed(action);

        /// <summary>Si esa acción está sostenida ahora mismo.</summary>
        public bool Held(GameAction action) => source != null && source.Held(action);

        private void Awake()
        {
            if (mode == InputMode.Objeto)
            {
                source = new DeviceInputSource(timbre, libro, felpudo);
                return;
            }

            if (mode == InputMode.TecladoDosBotones)
            {
                source = new TwoButtonInputSource(botonA, botonB);
                return;
            }

            if (button == null)
            {
                Debug.LogError(
                    $"[GameInput] '{name}' está en modalidad Teclado y no tiene " +
                    "OneButtonInput asignado.", this);
                enabled = false;
                return;
            }

            source = new SingleButtonInputSource(button);
        }

        /// <summary>
        /// Corre en <c>Update</c> con orden de ejecución adelantado por el
        /// componente: todos los que preguntan lo hacen en su propio Update, así
        /// que el estado del cuadro tiene que estar armado antes.
        /// </summary>
        private void Update()
        {
            source?.Tick();
        }
    }
}
