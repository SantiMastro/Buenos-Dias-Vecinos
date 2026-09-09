using BuenosDias.Gameplay;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Traduce el estado del predicador a su Animator.
    ///
    /// ⚠️ Sin esto el Animator se queda en su estado por defecto —<c>Idle</c>— y el
    /// predicador **se desliza por la vereda sin animación de caminata**: el
    /// controller tiene los cinco estados y el parámetro, pero hasta acá nadie los
    /// escribía nunca.
    ///
    /// Los números de estado son campos del Inspector y no constantes: son el
    /// contrato con un asset que se edita en la ventana de Animator, así que el día
    /// que ahí se reordenen, esto se arregla sin abrir un <c>.cs</c>.
    ///
    /// La vista no decide: el timbrazo y la conversión son POSES cortas que se
    /// pisan solas y vuelven al estado que manda la FSM. Cuánto duran es cosa de
    /// cómo se ve, no de las reglas del juego, y por eso vive de este lado.
    ///
    /// **El brazo estirado mientras el timbre sigue apretado también es de este
    /// lado.** Mantener el timbre no hace NADA mecánico —el juego lo lee como pulso
    /// para tocar y para insistir— pero el brazo volvía solo a los 0,25 s y eso se
    /// leía como que el juego había soltado el botón por su cuenta. Es la única
    /// razón por la que esta clase mira el input: no para decidir nada, sino para
    /// que el muñeco no desmienta lo que la mano está haciendo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PreacherView : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("La FSM del predicador. Se escucha; nunca se le pide nada.")]
        [SerializeField] private PreacherController preacher;

        [Tooltip("Dueño de la partida. De acá sale si se está jugando o eligiendo.")]
        [SerializeField] private RunDirector runDirector;

        [Tooltip("Quien resuelve las puertas. De acá sale la pose de conversión.")]
        [SerializeField] private SkillcheckRunner skillcheck;

        [Tooltip("Animator del predicador.")]
        [SerializeField] private Animator animator;

        [Tooltip("De acá sale si el timbre sigue apretado. Se lee y nada más: no " +
                 "cambia ninguna regla, solo sostiene la pose del brazo.")]
        [SerializeField] private GameInput input;

        [Header("Parámetro")]
        [Tooltip("Nombre del parámetro entero del controller.")]
        [SerializeField] private string stateParameter = "Estado";

        [Header("Números de estado del controller")]
        [Tooltip("Parado. Es lo que se ve mientras se elige religión y al final.")]
        [SerializeField] private int idleState;

        [Tooltip("Caminando por la vereda.")]
        [SerializeField] private int walkState = 1;

        [Tooltip("Parado en la puerta: esperando y atendiendo.")]
        [SerializeField] private int waitState = 2;

        [Tooltip("El gesto de tocar el timbre.")]
        [SerializeField] private int doorbellState = 3;

        [Tooltip("El festejo de una conversión.")]
        [SerializeField] private int successState = 4;

        [Header("Duración de las poses")]
        [Tooltip("Segundos que dura el gesto del timbre antes de volver a la espera.")]
        [SerializeField, Min(0f)] private float doorbellPoseSeconds = 0.25f;

        [Tooltip("Segundos que dura el festejo antes de volver a lo que corresponda.")]
        [SerializeField, Min(0f)] private float successPoseSeconds = 0.5f;

        private int parameterHash;
        private int baseState;
        private float poseLeft;
        private int written = int.MinValue;

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }
            parameterHash = Animator.StringToHash(stateParameter);
        }

        private void OnEnable()
        {
            preacher.StateChanged += OnStateChanged;
            preacher.DoorbellRung += OnDoorbellRung;
            runDirector.PhaseChanged += OnPhaseChanged;
            skillcheck.Finished += OnDoorFinished;
        }

        private void OnDisable()
        {
            preacher.StateChanged -= OnStateChanged;
            preacher.DoorbellRung -= OnDoorbellRung;
            runDirector.PhaseChanged -= OnPhaseChanged;
            skillcheck.Finished -= OnDoorFinished;
        }

        /// <summary>
        /// La primera escritura va en Start: la FSM no avisa el estado con el que
        /// arranca —no cambió a nada—, así que si esto esperara un aviso, el
        /// predicador se quedaría en el estado por defecto del controller hasta la
        /// primera puerta.
        /// </summary>
        private void Start() => Refresh();

        /// <summary>
        /// Un solo escritor por cuadro, y en este orden: primero la pose corta, si
        /// hay; después el timbre sostenido; al final el estado de fondo.
        ///
        /// El orden importa. La pose del timbrazo dura 0,25 s y arranca en el mismo
        /// cuadro en que la FSM pasa a esperar, así que si el sostenido se mirara
        /// primero no habría diferencia entre tocar y mantener.
        /// </summary>
        private void Update()
        {
            if (poseLeft > 0f)
            {
                poseLeft -= Time.deltaTime;
                if (poseLeft > 0f) return;
            }

            Write(Ringing() ? doorbellState : baseState);
        }

        /// <summary>
        /// Si el timbre está apretado AHORA y estamos en la puerta.
        ///
        /// ⚠️ La segunda mitad no sobra. En la modalidad de dos botones el botón A
        /// significa timbre también mientras se camina, así que sin la condición de
        /// estado el predicador cruzaría la vereda con el brazo estirado tocándole
        /// el timbre al aire.
        /// </summary>
        private bool Ringing()
        {
            if (input == null || runDirector.Phase != RunPhase.Jugando) return false;
            if (!input.Held(GameAction.Timbre)) return false;

            PreacherState state = preacher.State;
            return state == PreacherState.EnElFelpudo
                   || state == PreacherState.Esperando
                   || state == PreacherState.SinRespuesta;
        }

        private void OnStateChanged(PreacherState state) => Refresh();

        private void OnPhaseChanged(RunPhase phase) => Refresh();

        private void OnDoorbellRung(float precision) => Pose(doorbellState, doorbellPoseSeconds);

        private void OnDoorFinished(SkillcheckResult result)
        {
            if (result.State != SkillcheckSessionState.Convertido) return;
            Pose(successState, successPoseSeconds);
        }

        /// <summary>
        /// Recalcula el estado de fondo. Fuera de la partida manda la etapa: el
        /// predicador no puede estar caminando en una pantalla en la que no camina.
        /// </summary>
        private void Refresh()
        {
            baseState = runDirector.Phase == RunPhase.Jugando
                ? StateFor(preacher.State)
                : idleState;

            // El !Ringing() evita que un cambio de estado le arranque el brazo de
            // encima al que todavía tiene el timbre apretado.
            if (poseLeft <= 0f && !Ringing()) Write(baseState);
        }

        private int StateFor(PreacherState state)
        {
            switch (state)
            {
                case PreacherState.Caminando: return walkState;

                // Acercarse a la puerta ES caminar. Sin esta línea el predicador
                // volvería a deslizarse sin animación, que es el bug que ya se
                // pagó una vez en la fase 5 y no se vio hasta la 9.
                case PreacherState.Acercandose: return walkState;

                case PreacherState.Esperando: return waitState;
                case PreacherState.Atendido: return waitState;
                default: return idleState;
            }
        }

        /// <summary>
        /// Una pose se pisa encima del estado de fondo por unos cuadros. Es lo que
        /// deja que el timbrazo se vea: la FSM pasa a esperar en el MISMO cuadro en
        /// que suena el timbre, y sin la pose el gesto no llegaría a dibujarse.
        /// </summary>
        private void Pose(int state, float seconds)
        {
            if (seconds <= 0f) return;

            poseLeft = seconds;
            Write(state);
        }

        /// <summary>
        /// Escribe solo cuando el estado CAMBIA. Ahora que el Update escribe todos
        /// los cuadros, sin esta guarda el Animator recibiría el mismo entero
        /// sesenta veces por segundo.
        /// </summary>
        private void Write(int state)
        {
            if (state == written) return;

            written = state;
            animator.SetInteger(parameterHash, state);
        }

        private bool ValidateSetup()
        {
            if (preacher != null && runDirector != null
                && skillcheck != null && animator != null) return true;

            Debug.LogError(
                $"[PreacherView] '{name}' tiene referencias sin asignar " +
                "(predicador, partida, skillcheck o animator).", this);
            return false;
        }
    }
}
