using BuenosDias.Config;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Gameplay
{
    /// <summary>Cómo terminó una puerta, con lo que paga o cuesta en tiempo.</summary>
    public readonly struct SkillcheckResult
    {
        /// <summary>Si se convirtió o lo rechazaron.</summary>
        public SkillcheckSessionState State { get; }

        /// <summary>Aciertos perfectos de la cadena.</summary>
        public int PerfectHits { get; }

        /// <summary>Aciertos buenos de la cadena.</summary>
        public int GoodHits { get; }

        /// <summary>Segundos que gana el reloj del día. Negativo si los pierde.</summary>
        public float TimeDelta { get; }

        /// <summary>Arma el resultado de una puerta.</summary>
        public SkillcheckResult(
            SkillcheckSessionState state, int perfectHits, int goodHits, float timeDelta)
        {
            State = state;
            PerfectHits = perfectHits;
            GoodHits = goodHits;
            TimeDelta = timeDelta;
        }
    }

    /// <summary>
    /// Corre la cadena de objeciones de una puerta y lleva la cuenta de la partida:
    /// conversiones y tamaño de la comitiva.
    ///
    /// No se maneja solo: la FSM del predicador lo llama con
    /// <see cref="Tick"/>. Si tuviera su propio <c>Update</c>, el botón se leería
    /// en dos lugares y el resultado dependería del orden de ejecución.
    ///
    /// No dibuja: avisa por eventos y el aro se suscribe.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillcheckRunner : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Asset raíz de balance. De acá salen skillcheck, comitiva y bonos.")]
        [SerializeField] private GameConfig gameConfig;

        [Header("Cierre")]
        [Tooltip("Segundos que el predicador se queda en la puerta después de que " +
                 "la cadena se resolvió, para que el sí o el no se lea antes de " +
                 "volver a caminar.")]
        [SerializeField, Min(0f)] private float resolutionHoldSeconds = 0.6f;

        [Header("Semilla")]
        [Tooltip("Semilla de dónde aparece la zona. Aparte de las otras dos para " +
                 "poder repetir una cuadra con otras tiradas.")]
        [SerializeField] private int seed = 907;

        private SkillcheckDifficulty difficulty;
        private System.Random random;
        private SkillcheckSession session;
        private NeighborDefinition neighbor;
        private ReligionDefinition religion;
        private float holdLeft;

        /// <summary>Conversiones logradas en la partida.</summary>
        public int Converts { get; private set; }

        /// <summary>Seguidores en la fila. Sube al convertir y baja al fallar.</summary>
        public int Followers { get; private set; }

        /// <summary>Si la puerta todavía está en curso, incluido el cierre.</summary>
        public bool IsBusy => session != null;

        /// <summary>
        /// Eslabones que tendría la próxima puerta con la comitiva de ahora. Lo
        /// consulta el HUD.
        ///
        /// Se expone en vez de dejar que el HUD rehaga la cuenta con el
        /// <c>FollowerConfig</c>: la fórmula tiene tramos, techo y el extra de la
        /// religión, y con dos copias el cartel diría un número y el skillcheck
        /// daría otro.
        /// </summary>
        public int ChainLinksWith(ReligionDefinition activeReligion)
        {
            return difficulty != null ? difficulty.ChainLinksFor(Followers, activeReligion) : 1;
        }

        /// <summary>Arrancó un eslabón. Entrega la tirada que hay que dibujar.</summary>
        public event System.Action<SkillcheckAttempt> AttemptStarted;

        /// <summary>Se resolvió un eslabón.</summary>
        public event System.Action<SkillcheckOutcome> AttemptResolved;

        /// <summary>El vecino tira una réplica antes del eslabón siguiente.</summary>
        public event System.Action<string> Objected;

        /// <summary>La puerta terminó.</summary>
        public event System.Action<SkillcheckResult> Finished;

        /// <summary>Cambió el tamaño de la comitiva. Lo escucha la fila de la fase 7.</summary>
        public event System.Action<int> FollowersChanged;

        /// <summary>
        /// La puerta se cortó sin resolverse. Pasa cuando se hace de noche con la
        /// puerta abierta: quien dibuja el aro tiene que esconderlo.
        /// </summary>
        public event System.Action Cancelled;

        /// <summary>
        /// Corta la puerta en curso SIN resolverla: no paga ni cobra tiempo y no
        /// toca la comitiva, porque no hubo acierto ni falla. Sin puerta en curso
        /// no hace nada.
        ///
        /// ⚠️ Existe por el final del día: la FSM se apaga y deja de llamar a
        /// <see cref="Tick"/>, así que la tirada quedaba viva para siempre y el aro
        /// seguía dibujado encima de la cinemática.
        /// </summary>
        public void Cancel()
        {
            if (session == null) return;

            session = null;
            holdLeft = 0f;
            Cancelled?.Invoke();
        }

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }

            random = new System.Random(gameConfig.SeedFor(seed));
            difficulty = new SkillcheckDifficulty(
                gameConfig.Skillcheck, gameConfig.Followers, gameConfig.ConvertsForMaxDifficulty);
        }

        /// <summary>
        /// Abre una puerta. La dificultad se congela acá: la puntería del timbrazo,
        /// las conversiones, la comitiva y las insistencias de ESTE momento valen
        /// para toda la cadena, así que convertir en el eslabón 1 no endurece el 2.
        ///
        /// <paramref name="zoneScale"/> viene de cuántas veces hubo que insistirle
        /// al vecino: cuanto más tuvo que sonar el timbre, más chica la zona.
        ///
        /// <paramref name="timeUsed"/> es cuánto del día ya se consumió, 0..1: con
        /// poco tiempo por delante la zona también se achica.
        /// </summary>
        public void Begin(
            float precision, NeighborDefinition neighborAtDoor, ReligionDefinition activeReligion,
            float zoneScale = 1f, float timeUsed = 0f)
        {
            // Un componente apagado por validación igual recibe llamadas: los
            // métodos no dejan de existir porque enabled sea false. Sin esta
            // guarda, un cableado incompleto no da el error de Awake sino un
            // NullReference al abrirse la primera puerta, que es donde cuesta
            // diez veces más darse cuenta de qué faltaba.
            if (difficulty == null) return;

            neighbor = neighborAtDoor;
            religion = activeReligion;

            SkillcheckSetup setup =
                difficulty.Resolve(precision, Converts, Followers, activeReligion, zoneScale, timeUsed);

            session = new SkillcheckSession(gameConfig.Skillcheck, setup, random);
            holdLeft = 0f;

            AttemptStarted?.Invoke(session.Current);
        }

        /// <summary>Avanza la puerta un cuadro. La llama la FSM del predicador.</summary>
        public void Tick(float deltaTime, bool pressed)
        {
            if (session == null) return;

            if (session.State != SkillcheckSessionState.EnCurso)
            {
                holdLeft -= deltaTime;
                if (holdLeft <= 0f) session = null;
                return;
            }

            SkillcheckSessionTick tick = session.Advance(deltaTime, pressed);

            if (tick.LinkResolved) AttemptResolved?.Invoke(tick.LinkOutcome);
            if (tick.Finished) { Close(); return; }

            // La réplica presenta la objeción que VIENE, no la que se acaba de
            // ganar: para cuando suena, el eslabón siguiente ya está contado.
            if (tick.LinkResolved) Objected?.Invoke(neighbor?.ObjectionFor(session.LinkIndex));
            if (tick.LinkStarted) AttemptStarted?.Invoke(session.Current);
        }

        private void Close()
        {
            bool converted = session.State == SkillcheckSessionState.Convertido;
            float timeDelta = converted
                ? gameConfig.TimeBonusFor(session.PerfectHits, session.GoodHits, religion)
                : -gameConfig.Followers.TimePenalty(Followers);

            if (converted) { Converts++; SetFollowers(Followers + 1); }
            else SetFollowers(gameConfig.Followers.ApplyPenalty(Followers));

            holdLeft = resolutionHoldSeconds;
            Finished?.Invoke(new SkillcheckResult(
                session.State, session.PerfectHits, session.GoodHits, timeDelta));
        }

        /// <summary>
        /// El castigo de tiempo se calcula con la comitiva que HABÍA al fallar, no
        /// con la que queda: el recargo por multitud se cobra por el papelón, y el
        /// papelón ya ocurrió cuando los que se van todavía estaban ahí.
        /// </summary>
        private void SetFollowers(int count)
        {
            if (count == Followers) return;

            Followers = Mathf.Max(0, count);
            FollowersChanged?.Invoke(Followers);
        }

        private bool ValidateSetup()
        {
            if (gameConfig == null || gameConfig.Skillcheck == null || gameConfig.Followers == null)
            {
                Debug.LogError(
                    $"[SkillcheckRunner] '{name}' no llega al GameConfig, al " +
                    "SkillcheckConfig o al FollowerConfig.", this);
                return false;
            }

            return true;
        }
    }
}
