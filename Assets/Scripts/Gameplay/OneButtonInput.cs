using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// El único input del juego: cualquier botón de cualquier dispositivo dispara
    /// la única acción. No hay mapeo porque no hay más de una acción.
    ///
    /// ⚠️ NO se ata a bindings estáticos como <c>&lt;Gamepad&gt;/buttonSouth</c>. Los
    /// encoders USB de arcade no siempre se presentan como <c>Gamepad</c>: muchos
    /// aparecen como <c>Joystick</c> genérico o como HID crudo. Con bindings
    /// estáticos el gabinete no dispara nada aunque Windows lo detecte perfecto, y
    /// es un bug carísimo de diagnosticar porque el control "anda" en el panel de
    /// control. Por eso se escucha <c>onAnyButtonPress</c>, que es agnóstico del
    /// dispositivo.
    ///
    /// Todo se calcula AL CONSULTARLO y nada se cachea en <c>Update</c>: cachear
    /// haría que el resultado dependiera del orden de ejecución entre este
    /// componente y quien lo lee, que es un bug que aparece una vez cada tantos
    /// arranques y cuesta un día encontrar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OneButtonInput : MonoBehaviour
    {
        [Header("Gesto mantenido")]
        [Tooltip("Segundos que hay que sostener el botón para que cuente como " +
                 "confirmación. Lo usa la selección de religión.")]
        [SerializeField, Min(0.05f)] private float holdSeconds = 0.6f;

        [Header("Rescate de gabinete")]
        [Tooltip("PRENDE la aceptación de controles ruidosos y no la apaga nunca. " +
                 "Es el interruptor para una BUILD: si el encoder del gabinete marca " +
                 "sus botones como ruidosos, sin esto la build queda muda y no hay " +
                 "ventana de diagnóstico para rescatarla. En el Editor alcanza con el " +
                 "checkbox de la ventana.")]
        [SerializeField] private bool acceptNoisyControls;

        private System.IDisposable subscription;
        private InputControl active;
        private int pressedFrame = -1;
        private float pressStartTime;

        /// <summary>Si se apretó el botón en este cuadro.</summary>
        public bool PressedThisFrame => pressedFrame == Time.frameCount;

        /// <summary>Si el botón sigue apretado ahora mismo.</summary>
        public bool IsHeld => active != null && active.IsPressed();

        /// <summary>
        /// Segundos que lleva sostenido. Va en tiempo NO escalado para que una
        /// pausa o un cambio de <c>timeScale</c> no le cambien el largo al gesto.
        /// </summary>
        public float HoldSeconds => IsHeld ? Time.unscaledTime - pressStartTime : 0f;

        /// <summary>Cuánto le falta al gesto mantenido, de 0 a 1. Lo dibuja la UI.</summary>
        public float HoldProgress => Mathf.Clamp01(HoldSeconds / holdSeconds);

        /// <summary>Si el gesto mantenido ya se completó.</summary>
        public bool HoldCompleted => IsHeld && HoldSeconds >= holdSeconds;

        /// <summary>Último control que disparó. Lo lee la ventana de diagnóstico.</summary>
        public InputControl LastControl => active;

        /// <summary>Avisa cada apretón aceptado, con el control que lo mandó.</summary>
        public event System.Action<InputControl> ButtonPressed;

        /// <summary>
        /// Palanca de rescate: si está prendida, <see cref="Accepts"/> deja pasar los
        /// controles marcados como RUIDOSOS.
        ///
        /// ⚠️ Es estática a propósito, y es la única del proyecto. El filtro tiene que
        /// poder aflojarse **con el gabinete enchufado y sin recompilar**, desde una
        /// ventana de Editor que también corre en Edit Mode, cuando todavía no existe
        /// ninguna instancia de este componente. Un campo de instancia no llegaría a
        /// tiempo y un asset se colaría en un commit.
        ///
        /// Que sea de diagnóstico y no de juego es lo que la salva de ser un singleton
        /// mutable: nadie del juego la escribe, y con ella apagada —el default— el
        /// comportamiento es exactamente el de antes.
        /// </summary>
        public static bool AcceptNoisyControls { get; set; }

        /// <summary>
        /// Si este control cuenta como botón de juego.
        ///
        /// Es <c>public static</c> a propósito: la ventana de diagnóstico usa ESTA
        /// misma función para decir si un apretón fue aceptado o descartado. Con
        /// dos copias del filtro, el diagnóstico podría decir que todo está bien
        /// mientras el juego descarta el botón.
        /// </summary>
        public static bool Accepts(InputControl control)
        {
            if (control == null || control.device == null) return false;

            // Tiene que ser un BOTÓN de verdad. Sin esto el filtro también daba por
            // bueno /Mouse/position, que es un Vector2: hoy no pasa nada porque
            // onAnyButtonPress solo entrega botones, pero el diagnóstico lo
            // etiquetaría como aceptable y mentiría justo cuando hay que creerle.
            if (!(control is ButtonControl)) return false;

            // Los sintéticos son controles que el layout arma combinando otros:
            // Keyboard.anyKey dispara con CUALQUIER tecla, y las direcciones de un
            // stick son botones que en realidad son el stick. Aceptarlos haría que
            // mover la palanca contara como tocar el timbre.
            //
            if (control.synthetic) return false;

            // ⚠️ Los ruidosos se descartan porque cambian solos —un acelerómetro
            // cruzando el umbral tocaría timbres fantasma—, pero si un encoder de
            // arcade aparece con sus botones marcados como ruidosos, el gabinete
            // queda mudo. Para eso está la palanca: se afloja en el momento, con el
            // botón en la mano, sin recompilar. Los sintéticos NO se aflojan nunca:
            // ahí el problema no es el hardware sino que el control no es un botón.
            if (control.noisy && !AcceptNoisyControls) return false;

            return control.device.enabled;
        }

        /// <summary>
        /// El campo del Inspector solo PRENDE la palanca, nunca la apaga: en el
        /// Editor la ventana de diagnóstico ya la puede haber prendido en caliente, y
        /// apagarla acá le borraría el rescate justo al darle Play.
        /// </summary>
        private void Awake()
        {
            if (acceptNoisyControls) AcceptNoisyControls = true;
        }

        private void OnEnable()
        {
            subscription = InputSystem.onAnyButtonPress.Call(OnPressed);
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;
        }

        /// <summary>
        /// Llega desde el procesamiento de input, que corre ANTES que cualquier
        /// <c>Update</c>. Por eso alcanza con sellar el número de cuadro: quien
        /// pregunte, cuando pregunte, ve lo mismo.
        /// </summary>
        private void OnPressed(InputControl control)
        {
            if (!Accepts(control)) return;

            active = control;
            pressedFrame = Time.frameCount;
            pressStartTime = Time.unscaledTime;

            ButtonPressed?.Invoke(control);
        }
    }
}
