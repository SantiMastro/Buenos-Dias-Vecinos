using System.Collections.Generic;
using BuenosDias.Config;
using BuenosDias.Gameplay;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Todos los efectos de sonido del juego. Se suscribe a los eventos que el
    /// juego ya levanta —timbre, cortina, skillcheck, comitiva, power up, final,
    /// highscore— y reproduce el clip que corresponde. No decide nada: si este
    /// componente no existiera, el juego se resolvería igual, solo que mudo.
    ///
    /// Los clips viven en <c>Assets/Resources/Audio</c> y se cargan por nombre, así
    /// que reemplazar un sonido es soltar un archivo con el mismo nombre ahí, sin
    /// tocar la escena. Sacar un archivo apaga ese sonido y deja un aviso en
    /// consola, no un error.
    ///
    /// Usa un grupo chico de <c>AudioSource</c> rotativo: dos sonidos al mismo
    /// tiempo (acierto + conversión) no se cortan entre sí, y cada uno puede
    /// llevar su propio tono sin tocar al resto.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameAudio : MonoBehaviour
    {
        private const string Folder = "Audio/";
        private const int VoiceCount = 8;

        [Header("Referencias")]
        [SerializeField] private PreacherController preacher;
        [SerializeField] private SkillcheckRunner skillcheck;
        [SerializeField] private RunDirector runDirector;
        [SerializeField] private HighscoreDirector highscore;

        [Header("Mezcla")]
        [Tooltip("Volumen general de los efectos, 0 a 1.")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.5f;

        [Tooltip("Cuánto varía el tono de cada sonido repetido (±). Evita que el " +
                 "mismo paso suene idéntico veinte veces seguidas. 0 = sin variación.")]
        [SerializeField, Range(0f, 0.2f)] private float pitchVariation = 0.04f;

        [Header("Música de fondo")]
        [Tooltip("Volumen de la música, 0 a 1. Va aparte de los efectos.")]
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.3f;

        [Tooltip("Segundos que tarda en subir al arrancar o al cambiar de pista.")]
        [SerializeField, Min(0f)] private float musicFadeInSeconds = 1.5f;

        [Tooltip("Segundos que tarda en bajar la pista anterior antes de cambiar.")]
        [SerializeField, Min(0f)] private float musicFadeOutSeconds = 0.6f;

        [Tooltip("Si el final 'se hizo de noche' (pocas conversiones) suena como " +
                 "ganaste. Apagado, solo la ascensión suena como ganaste.")]
        [SerializeField] private bool middleEndingCountsAsWin = true;

        [Header("Pasos")]
        [SerializeField] private bool footsteps = true;

        [Tooltip("Segundos entre pasos con el ritmo base. Con el día acelerado se acorta.")]
        [SerializeField, Min(0.05f)] private float stepSeconds = 0.26f;

        [SerializeField, Range(0f, 1f)] private float stepVolume = 0.3f;

        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly HashSet<string> warned = new HashSet<string>();
        private AudioSource[] voices;
        private int nextVoice;
        private float stepTimer;

        /// <summary>
        /// Nivel común de TODOS los efectos. Los wav ya están nivelados a la misma
        /// sonoridad, así que ninguno necesita su propio multiplicador: el volumen
        /// general se baja con <c>masterVolume</c>.
        /// </summary>
        private const float Level = 0.85f;

        // Música por pantalla. Todas son opcionales: la que no exista no suena y no
        // es un error. Las de final suenan UNA vez; las otras en bucle.
        private const string MusicMenu = "musica_menu";
        private const string MusicGame = "musica_fondo";
        private const string MusicWin = "musica_ganaste";
        private const string MusicLose = "musica_perdiste";
        private const int MaxBellVariants = 8;
        private readonly List<string> bellNames = new List<string>();
        private string currentBell;
        private AudioSource music;
        private float musicFade;
        private string musicWanted;      // pista que corresponde a la pantalla de ahora
        private string musicPlaying;     // pista cargada en el AudioSource

        private void Awake()
        {
            voices = new AudioSource[VoiceCount];

            for (int i = 0; i < voices.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f; // 2D: el juego es de un solo plano
                voices[i] = source;
            }

            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.spatialBlend = 0f;
            music.loop = true;
            music.volume = 0f;

            Preload();
            StartMusic();
        }

        /// <summary>
        /// Elige la pista de la pantalla donde arranca el juego (menú o selección).
        /// </summary>
        private void StartMusic()
        {
            musicWanted = runDirector != null && runDirector.Phase == RunPhase.Jugando
                ? MusicGame
                : MusicMenu;
        }

        /// <summary>Pide la pista de la etapa. El cambio real lo hace Update con fundido.</summary>
        private void OnPhaseChanged(RunPhase phase)
        {
            switch (phase)
            {
                case RunPhase.Jugando: musicWanted = MusicGame; break;
                case RunPhase.Final: break;                       // lo decide OnEnding
                default: musicWanted = MusicMenu; break;
            }
        }

        /// <summary>
        /// Baja la pista que suena y, cuando llega a cero, carga la que corresponde
        /// y la sube. Sin archivo para esa pantalla queda en silencio.
        /// </summary>
        private void UpdateMusic()
        {
            if (music == null) return;

            float dt = Time.unscaledDeltaTime;
            bool switching = musicWanted != musicPlaying;

            if (switching && music.clip != null && music.isPlaying)
            {
                musicFade = musicFadeOutSeconds <= 0f ? 0f : Mathf.Max(0f, musicFade - dt / musicFadeOutSeconds);
                if (musicFade > 0f) { music.volume = musicVolume * musicFade; return; }
            }

            if (switching)
            {
                music.Stop();
                music.clip = musicWanted != null ? Resources.Load<AudioClip>(Folder + musicWanted) : null;

                // sin música de menú, suena la del juego en vez de quedar mudo
                if (music.clip == null && musicWanted == MusicMenu)
                    music.clip = Resources.Load<AudioClip>(Folder + MusicGame);
                music.loop = musicWanted == MusicMenu || musicWanted == MusicGame;
                musicPlaying = musicWanted;
                musicFade = 0f;
                if (music.clip != null) music.Play();
            }

            if (music.clip == null) return;

            musicFade = musicFadeInSeconds <= 0f ? 1f : Mathf.Min(1f, musicFade + dt / musicFadeInSeconds);
            music.volume = musicVolume * musicFade;
        }

        /// <summary>
        /// Carga y descomprime TODOS los clips al arrancar. Sin esto, la primera vez
        /// que suena cada sonido Unity lo carga en ese mismo cuadro y se nota como un
        /// micro-tirón justo en el momento del timbre, del acierto o de la conversión.
        /// </summary>
        private void Preload()
        {
            string[] names =
            {
                "timbre", "cortina", "paso", "puerta_abre", "sin_respuesta", "perfecto",
                "bueno", "fallo", "convierte", "rechazo", "racha", "powerup_listo",
                "powerup_usa", "tecla", "fijar", "tabla", "fin"
            };

            foreach (string clipName in names)
            {
                AudioClip clip = Clip(clipName);
                if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            }

            // Timbres alternativos: timbre_2 ... timbre_8. Los que no existan se
            // ignoran sin avisos; el 'timbre' base siempre cuenta.
            bellNames.Clear();
            if (Clip("timbre") != null) bellNames.Add("timbre");

            for (int i = 2; i <= MaxBellVariants; i++)
            {
                string variant = "timbre_" + i;
                var clip = Resources.Load<AudioClip>(Folder + variant);
                if (clip == null) continue;

                clips[variant] = clip;
                if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                bellNames.Add(variant);
            }
        }

        private void OnEnable()
        {
            if (preacher != null)
            {
                preacher.DoorbellRung += OnDoorbellRung;
                preacher.TellFired += OnTell;
                preacher.StepFired += OnHouseStep;
                preacher.StateChanged += OnPreacherState;
            }

            if (skillcheck != null)
            {
                skillcheck.AttemptResolved += OnAttemptResolved;
                skillcheck.Finished += OnDoorFinished;
                skillcheck.PowerUpChanged += OnPowerUpChanged;
                skillcheck.PowerUpConsumed += OnPowerUpConsumed;
            }

            if (runDirector != null)
            {
                runDirector.EndingReached += OnEnding;
                runDirector.PhaseChanged += OnPhaseChanged;
            }

            if (highscore != null)
            {
                highscore.StageChanged += OnHighscoreStage;
                highscore.EntryStepped += OnInitialsStep;
            }
        }

        private void OnDisable()
        {
            if (preacher != null)
            {
                preacher.DoorbellRung -= OnDoorbellRung;
                preacher.TellFired -= OnTell;
                preacher.StepFired -= OnHouseStep;
                preacher.StateChanged -= OnPreacherState;
            }

            if (skillcheck != null)
            {
                skillcheck.AttemptResolved -= OnAttemptResolved;
                skillcheck.Finished -= OnDoorFinished;
                skillcheck.PowerUpChanged -= OnPowerUpChanged;
                skillcheck.PowerUpConsumed -= OnPowerUpConsumed;
            }

            if (runDirector != null)
            {
                runDirector.EndingReached -= OnEnding;
                runDirector.PhaseChanged -= OnPhaseChanged;
            }

            if (highscore != null)
            {
                highscore.StageChanged -= OnHighscoreStage;
                highscore.EntryStepped -= OnInitialsStep;
            }
        }

        /// <summary>Pasos del predicador mientras camina, al ritmo del día.</summary>
        private void Update()
        {
            UpdateMusic();

            if (!footsteps || preacher == null || runDirector == null) return;

            bool walking = runDirector.Phase == RunPhase.Jugando
                           && preacher.State == PreacherState.Caminando;

            if (!walking) { stepTimer = 0f; return; }

            stepTimer -= Time.deltaTime;
            if (stepTimer > 0f) return;

            stepTimer = stepSeconds / Mathf.Max(0.5f, preacher.Pace);
            Play("paso", stepVolume, 0.08f);
        }

        /// <summary>
        /// Cada casa tiene SU timbre: se sortea al llegar y se mantiene mientras
        /// el predicador insiste en la misma puerta, así que el timbre de una casa
        /// suena siempre igual y el de la siguiente es otro.
        /// </summary>
        private void OnDoorbellRung(float precision)
        {
            if (bellNames.Count == 0) return;
            if (currentBell == null) currentBell = bellNames[Random.Range(0, bellNames.Count)];
            Play(currentBell, Level);
        }

        private void OnTell() => Play("cortina", Level);

        private void OnHouseStep() => Play("paso", stepVolume, 0.1f);

        private void OnPreacherState(PreacherState state)
        {
            // al volver a caminar se cambia de casa, y con ella el timbre
            if (state == PreacherState.Caminando) currentBell = null;

            if (state == PreacherState.Atendido) Play("puerta_abre", Level);
            else if (state == PreacherState.SinRespuesta) Play("sin_respuesta", Level);
        }

        private void OnAttemptResolved(SkillcheckOutcome outcome)
        {
            switch (outcome)
            {
                case SkillcheckOutcome.Perfecto: Play("perfecto", Level); break;
                case SkillcheckOutcome.Bueno: Play("bueno", Level); break;
                case SkillcheckOutcome.Fallado: Play("fallo", Level); break;
            }
        }

        /// <summary>
        /// El cierre de la puerta suena un instante DESPUÉS del golpe del último
        /// eslabón, para que se oigan por separado y no como un solo ruido.
        /// </summary>
        private void OnDoorFinished(SkillcheckResult result)
        {
            if (result.State == SkillcheckSessionState.Convertido) Play("convierte", Level, 0f, 0.22f);
            else Play("rechazo", Level, 0f, 0.3f);
        }

        /// <summary>
        /// Cada perfect seguido suena un poco más agudo (la racha se OYE subir); al
        /// armarse el power up suena el aviso grande.
        /// </summary>
        private void OnPowerUpChanged(int streak, bool armed)
        {
            if (armed) { Play("powerup_listo", Level, 0f, 0.2f); return; }
            if (streak > 0) Play("racha", Level, 0f, 0.12f, 1f + 0.12f * (streak - 1));
        }

        private void OnPowerUpConsumed() => Play("powerup_usa", Level);

        private void OnEnding(EndingKind kind, int followers)
        {
            Play("fin", Level, 0f, 0.3f);

            bool win = kind == EndingKind.Ascension
                       || (kind == EndingKind.SeHizoDeNoche && middleEndingCountsAsWin);
            musicWanted = win ? MusicWin : MusicLose;
        }

        private void OnHighscoreStage(HighscoreStage stage)
        {
            if (stage == HighscoreStage.Iniciales || stage == HighscoreStage.Tabla) Play("tabla", Level);
        }

        private void OnInitialsStep(InitialsStep step)
        {
            if (step == InitialsStep.CambioLetra) Play("tecla", Level);
            else if (step == InitialsStep.LetraFijada || step == InitialsStep.Completo) Play("fijar", Level);
        }

        /// <summary>
        /// Reproduce un clip por nombre. <paramref name="jitter"/> suma variación de
        /// tono propia a la general; <paramref name="delay"/> lo retrasa en segundos
        /// sin trabar nada; <paramref name="pitch"/> lo transpone.
        /// </summary>
        private void Play(string clipName, float volume = 1f, float jitter = 0f,
                          float delay = 0f, float pitch = 1f)
        {
            AudioClip clip = Clip(clipName);
            if (clip == null || voices == null) return;

            AudioSource voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;

            float spread = pitchVariation + jitter;
            voice.pitch = pitch * (1f + Random.Range(-spread, spread));
            voice.volume = Mathf.Clamp01(volume * masterVolume);
            voice.clip = clip;

            if (delay > 0f) voice.PlayDelayed(delay);
            else voice.Play();
        }

        private AudioClip Clip(string clipName)
        {
            if (clips.TryGetValue(clipName, out AudioClip cached)) return cached;

            AudioClip loaded = Resources.Load<AudioClip>(Folder + clipName);
            if (loaded == null && warned.Add(clipName))
                Debug.LogWarning($"[GameAudio] Falta el clip 'Resources/{Folder}{clipName}'. " +
                                 "Ese sonido no suena.", this);

            clips[clipName] = loaded;
            return loaded;
        }
    }
}
