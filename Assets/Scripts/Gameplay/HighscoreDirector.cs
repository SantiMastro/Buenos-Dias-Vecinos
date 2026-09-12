using BuenosDias.Config;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Gameplay
{
    /// <summary>En qué parte de la tabla de récords estamos.</summary>
    public enum HighscoreStage
    {
        /// <summary>Jugando, o un final de una religión sin tabla.</summary>
        Apagado,

        /// <summary>Cayó la noche; el final se muestra solo un rato.</summary>
        Esperando,

        /// <summary>El puntaje entra en la tabla y se están cargando las iniciales.</summary>
        Iniciales,

        /// <summary>Se muestra la tabla. Después de un rato, el click reinicia.</summary>
        Tabla
    }

    /// <summary>
    /// Dueño de la tabla de récords: la carga al caer la noche, toma las iniciales
    /// con el timbre y la guarda.
    ///
    /// El puntaje es la comitiva que queda al caer la noche —lo mismo que decide el
    /// final—, así que se escucha <see cref="RunDirector.EndingReached"/> y no se
    /// cuenta nada por separado.
    ///
    /// **No dibuja nada.** <c>HighscoreView</c> lee el estado y escucha los
    /// eventos. El reinicio sigue siendo del <see cref="RunDirector"/>, que le
    /// pregunta a esta clase con <see cref="HoldsRestart"/> si ya puede.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HighscoreDirector : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Dueño de la partida. De acá salen el final, la religión y el puntaje.")]
        [SerializeField] private RunDirector runDirector;

        [Tooltip("El input de todo el juego. Las iniciales se cargan con el timbre: " +
                 "toque = letra siguiente, mantener = fijar.")]
        [SerializeField] private GameInput input;

        [Tooltip("Perillas de la tabla.")]
        [SerializeField] private HighscoreConfig config;

        private HighscoreStore store;
        private HighscoreSave save;
        private string key;
        private float stageTime;
        private float idleTime;

        /// <summary>En qué parte estamos.</summary>
        public HighscoreStage Stage { get; private set; }

        /// <summary>La tabla de la religión que se jugó. <c>null</c> hasta que cae la noche.</summary>
        public HighscoreTable Table { get; private set; }

        /// <summary>La carga de iniciales. <c>null</c> si el puntaje no entró.</summary>
        public InitialsEntry Entry { get; private set; }

        /// <summary>Comitiva al caer la noche.</summary>
        public int Score { get; private set; }

        /// <summary>Puesto, desde 0, que se ganó esta partida. −1 hasta guardar, o si no entró.</summary>
        public int NewRank { get; private set; } = -1;

        /// <summary>Puesto, desde 0, que va a ocupar el puntaje mientras se cargan las iniciales.</summary>
        public int PendingRank => Table != null ? Table.RankFor(Score) : -1;

        /// <summary>Perillas en uso.</summary>
        public HighscoreConfig Config => config;

        /// <summary>
        /// Si la tabla todavía no deja reiniciar: mientras espera, mientras se cargan
        /// las iniciales y un rato después de guardar.
        /// </summary>
        public bool HoldsRestart =>
            Stage == HighscoreStage.Esperando
            || Stage == HighscoreStage.Iniciales
            || (Stage == HighscoreStage.Tabla && stageTime < config.RestartLockoutSeconds);

        /// <summary>Si un click ahora reinicia. Lo usa la vista para mostrar el cartel.</summary>
        public bool RestartReady => runDirector != null && runDirector.AcceptsRestart;

        /// <summary>Avisa el cambio de parte, ya con la nueva puesta.</summary>
        public event System.Action<HighscoreStage> StageChanged;

        /// <summary>Avisa cada cambio de la carga de iniciales, para redibujar solo entonces.</summary>
        public event System.Action<InitialsStep> EntryStepped;

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }

            store = new HighscoreStore(Application.persistentDataPath, config.FileName);
        }

        private void OnEnable()
        {
            if (runDirector != null) runDirector.EndingReached += OnEndingReached;
        }

        private void OnDisable()
        {
            if (runDirector != null) runDirector.EndingReached -= OnEndingReached;
        }

        /// <summary>
        /// El archivo se lee acá y no en Awake: así una tabla editada a mano con el
        /// juego abierto se ve en la partida siguiente, y un disco que falla no
        /// molesta mientras se juega.
        /// </summary>
        private void OnEndingReached(EndingKind kind, int followers)
        {
            if (!config.AppliesTo(runDirector.Religion)) return;

            Score = followers;
            key = HighscoreConfig.KeyFor(runDirector.Religion);
            save = store.Load();
            Table = new HighscoreTable(config.Capacity, save.EntriesFor(key), config.MinimumScore);
            NewRank = -1;

            Enter(HighscoreStage.Esperando);
        }

        private void Update()
        {
            if (Stage == HighscoreStage.Apagado) return;

            stageTime += Time.unscaledDeltaTime;

            if (Stage == HighscoreStage.Esperando && stageTime >= config.ShowAfterSeconds) Reveal();
            else if (Stage == HighscoreStage.Iniciales) TypeInitials();
        }

        private void Reveal()
        {
            if (!Table.Qualifies(Score))
            {
                Enter(HighscoreStage.Tabla);
                return;
            }

            Entry = new InitialsEntry(config.InitialsLength, config.Alphabet, config.HoldSeconds);
            idleTime = 0f;
            Enter(HighscoreStage.Iniciales);
        }

        /// <summary>
        /// El timbre en el contexto de final es el botón A sin arbitrar: el felpudo
        /// no compite acá, así que un toque del felpudo no cambia ni fija letras.
        /// </summary>
        private void TypeInitials()
        {
            bool pressed = input.Pressed(GameAction.Timbre);
            bool held = input.Held(GameAction.Timbre);

            InitialsStep step = Entry.Update(pressed, held, Time.unscaledTime);
            if (step != InitialsStep.Nada) EntryStepped?.Invoke(step);

            // Cuenta desde el último apretón o cambio, y NO se frena mientras el
            // botón esté abajo: un contacto trabado del gabinete dejaría la
            // pantalla clavada para siempre. Nadie mantiene 20 s para fijar.
            idleTime = pressed || step != InitialsStep.Nada ? 0f : idleTime + Time.unscaledDeltaTime;

            if (!Entry.IsComplete && config.IdleTimeoutSeconds > 0f
                && idleTime >= config.IdleTimeoutSeconds)
            {
                Entry.Complete();
                EntryStepped?.Invoke(InitialsStep.Completo);
            }

            if (Entry.IsComplete) Commit();
        }

        private void Commit()
        {
            NewRank = Table.Insert(Entry.Initials, Score);
            save.Set(key, Table.Entries);
            store.Save(save);

            Enter(HighscoreStage.Tabla);
        }

        private void Enter(HighscoreStage stage)
        {
            Stage = stage;
            stageTime = 0f;
            StageChanged?.Invoke(stage);
        }

        private bool ValidateSetup()
        {
            if (runDirector != null && input != null && config != null) return true;

            Debug.LogError(
                $"[HighscoreDirector] '{name}' tiene referencias sin asignar " +
                "(partida, input o HighscoreConfig).", this);
            return false;
        }
    }
}
