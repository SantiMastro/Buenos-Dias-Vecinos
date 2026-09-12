using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// La única puerta de entrada del input para todo el juego. Expone las tres
    /// acciones; quien la consume pregunta por acciones y nunca por teclas.
    ///
    /// Se juega con un solo esquema, el del control alternativo: el timbre (botón
    /// A, click izquierdo) toca, insiste y le pega al QTE, y el felpudo (botón B,
    /// Espacio desde la Raspberry Pi) frena y vuelve a caminar. Las teclas y los
    /// tiempos viven en un <see cref="InputConfig"/>.
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
        [Header("Config")]
        [Tooltip("Teclas y tiempos del input. Sin asset se juega con los valores por " +
                 "defecto y la consola lo avisa.")]
        [SerializeField] private InputConfig config;

        private IInputSource source;
        private InputConfig fallback;

        /// <summary>
        /// La config en uso. Sin asset asignado se arma una con los defaults, así
        /// quien pregunte antes del Awake —el buffer del predicador, por ejemplo—
        /// no recibe un null.
        /// </summary>
        private InputConfig Config
        {
            get
            {
                if (config != null) return config;
                if (fallback == null) fallback = ScriptableObject.CreateInstance<InputConfig>();
                return fallback;
            }
        }

        /// <summary>Segundos que se guarda un timbrazo adelantado.</summary>
        public float RingBufferSeconds => Config.RingBufferSeconds;

        /// <summary>
        /// Qué espera el juego. Lo escribe la FSM, y es lo que le da significado al
        /// botón A: timbre en la puerta, libro con la puerta abierta.
        /// </summary>
        public InputContext Context
        {
            get => source != null ? source.Context : InputContext.Seleccion;
            set { if (source != null) source.Context = value; }
        }

        /// <summary>Cuánto lleva del gesto mantenido, 0..1. Con este esquema no hay: siempre 0.</summary>
        public float HoldProgress => source != null ? source.HoldProgress : 0f;

        /// <summary>Si esa acción se disparó en este cuadro.</summary>
        public bool Pressed(GameAction action) => source != null && source.Pressed(action);

        /// <summary>Si esa acción está sostenida ahora mismo.</summary>
        public bool Held(GameAction action) => source != null && source.Held(action);

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogWarning(
                    $"[GameInput] '{name}' no tiene InputConfig asignado: se juega con " +
                    "los valores por defecto.", this);
            }

            InputConfig settings = Config;
            WarnIfShared(settings);

            source = new TwoButtonInputSource(
                settings.BotonA, settings.BotonB,
                settings.CoincidenceSeconds, settings.FelpudoSostenido);
        }

        private void OnDestroy()
        {
            if (fallback != null) Destroy(fallback);
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

        /// <summary>
        /// Grita si el timbre y el felpudo leen la MISMA entrada. Solo avisa y no
        /// apaga nada: el juego sigue jugable, pero un apretón va a frenar y tocar
        /// a la vez, y ese bug se ve como un problema del control cuando es de
        /// configuración.
        /// </summary>
        private void WarnIfShared(InputConfig settings)
        {
            if (settings.BotonA == null || !settings.BotonA.SameInputAs(settings.BotonB)) return;

            Debug.LogError(
                $"[GameInput] '{name}': el timbre y el felpudo están atados a la MISMA " +
                $"entrada ({settings.BotonA.Describe()}). Un solo apretón va a frenar y " +
                "tocar a la vez. Asigná entradas distintas en el InputConfig.", this);
        }
    }
}
