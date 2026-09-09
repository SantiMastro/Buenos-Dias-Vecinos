using BuenosDias.Config;
using BuenosDias.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// Dueño de la partida: elegir religión, jugar el día, ver el final, volver a
    /// empezar.
    ///
    /// La lógica de las transiciones no está acá sino en
    /// <see cref="RunStateMachine"/>, que es plana y testeable. Esta clase es el
    /// enchufe: prende y apaga los sistemas que solo pueden correr jugando, y
    /// avisa por eventos en qué etapa está.
    ///
    /// **No dibuja nada.** Las pantallas de selección y de final se suscriben a
    /// <see cref="PhaseChanged"/>. Si esta clase prendiera y apagara vistas, el
    /// juego pasaría a conocer a la UI.
    ///
    /// Los sistemas que apaga están declarados uno por uno en el Inspector, no
    /// buscados: es la misma razón por la que no hay locator en el proyecto, y de
    /// paso deja a la vista qué corre en cada etapa con solo mirar el componente.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunDirector : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Asset raíz de balance. De acá sale qué final corresponde.")]
        [SerializeField] private GameConfig gameConfig;

        [Tooltip("Quien elige religión. Solo corre en la etapa de selección.")]
        [SerializeField] private ReligionSelector selector;

        [Tooltip("La FSM del predicador. Solo corre jugando: apagada, el predicador " +
                 "se queda parado y el botón no toca timbres.")]
        [SerializeField] private PreacherController preacher;

        [Tooltip("El reloj del día. Solo corre jugando.")]
        [SerializeField] private DayDirector day;

        [Tooltip("Input de un botón. Es el mismo de todo el juego: el que reinicia " +
                 "desde el final es el que toca timbres jugando.")]
        [SerializeField] private GameInput input;

        [Header("Final")]
        [Tooltip("Segundos que el final se queda en pantalla antes de aceptar el " +
                 "botón. Sin esto, el apretón que estaba en curso al caer la noche " +
                 "se comería el final sin que nadie lo vea. Tiene que cubrir la " +
                 "cinemática entera: la última pieza entra a 1.4 s y el subtitular " +
                 "a 1.7 s.")]
        [SerializeField, Min(0f)] private float endingLockoutSeconds = 3.5f;

        private readonly RunStateMachine run = new RunStateMachine();
        private float phaseTime;

        /// <summary>Etapa actual de la partida.</summary>
        public RunPhase Phase => run.Phase;

        /// <summary>Religión elegida. <c>null</c> hasta que se confirma.</summary>
        public ReligionDefinition Religion { get; private set; }

        /// <summary>Final que salió. Solo tiene sentido en la etapa de final.</summary>
        public EndingKind Ending { get; private set; }

        /// <summary>Avisa el cambio de etapa. Lo escuchan las pantallas.</summary>
        public event System.Action<RunPhase> PhaseChanged;

        /// <summary>
        /// Avisa qué final salió, con la comitiva que lo disparó. Va aparte
        /// de <see cref="PhaseChanged"/> porque la cinemática necesita saber CUÁL de
        /// los tres, y el resto de las pantallas no.
        /// </summary>
        public event System.Action<EndingKind, int> EndingReached;

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }

            run.Changed += OnPhaseChanged;

            // Se aplica en Awake y no en Start porque Unity garantiza que todos los
            // Awake corren antes que cualquier Update: así el reloj no llega a
            // descontar un cuadro de día mientras el jugador todavía elige.
            Apply(run.Phase);
        }

        private void OnEnable()
        {
            selector.Confirmed += OnReligionConfirmed;
            day.DayEnded += OnDayEnded;
        }

        private void OnDisable()
        {
            selector.Confirmed -= OnReligionConfirmed;
            day.DayEnded -= OnDayEnded;
        }

        private void Update()
        {
            phaseTime += Time.unscaledDeltaTime;

            if (run.Phase != RunPhase.Final) return;
            if (phaseTime < endingLockoutSeconds || !input.Pressed(GameAction.Timbre)) return;

            Restart();
        }

        private void OnReligionConfirmed(ReligionDefinition chosen)
        {
            Religion = chosen;

            // La religión entra ANTES de encender la FSM: si entrara después, el
            // primer cuadro de caminata saldría con la velocidad de la anterior.
            preacher.Religion = chosen;
            run.Confirm();
        }

        /// <summary>
        /// <paramref name="followers"/> es la comitiva que queda al caer la noche,
        /// no las conversiones acumuladas: los umbrales de los finales y el número
        /// que muestra la cinemática tienen que leer el MISMO valor, y el que vale
        /// es la gente que sigue ahí.
        /// </summary>
        private void OnDayEnded(int followers)
        {
            Ending = gameConfig.ResolveEnding(followers, Religion);

            if (!run.Finish()) return;
            EndingReached?.Invoke(Ending, followers);
        }

        /// <summary>
        /// Recargar la escena es el reinicio, en vez de un <c>Reset()</c> por
        /// sistema. Con un reset a mano, el día que alguien agregue un sistema con
        /// memoria y se olvide de vaciarlo, la segunda partida sale distinta de la
        /// primera por un motivo que nadie va a buscar ahí. La escena es chica y
        /// vuelve a cargar al instante.
        /// </summary>
        private void Restart()
        {
            // El cache de paletas es estático y sobrevive a la carga de escena: sin
            // esto, cada partida deja las texturas generadas de la religión anterior.
            PaletteSwapper.ClearCache();
            SceneManager.LoadScene(gameObject.scene.buildIndex);
        }

        private void OnPhaseChanged(RunPhase phase)
        {
            Apply(phase);
            PhaseChanged?.Invoke(phase);
        }

        private void Apply(RunPhase phase)
        {
            phaseTime = 0f;

            bool playing = phase == RunPhase.Jugando;
            selector.enabled = phase == RunPhase.Seleccion;
            preacher.enabled = playing;
            day.enabled = playing;

            // La selección y el predicador declaran su propio contexto al
            // encenderse. El final no tiene dueño —no hay ningún sistema vivo— así
            // que lo declara el director, que es quien sabe que llegó.
            if (phase == RunPhase.Final) input.Context = InputContext.Final;
        }

        private bool ValidateSetup()
        {
            if (gameConfig != null && selector != null && preacher != null
                && day != null && input != null) return true;

            Debug.LogError(
                $"[RunDirector] '{name}' tiene referencias sin asignar " +
                "(GameConfig, selector, predicador, reloj o input).", this);
            return false;
        }
    }
}
