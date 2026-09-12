using BuenosDias.Config;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// El run loop del predicador, como máquina de estados explícita.
    ///
    /// Cada estado consume las ACCIONES que le corresponden. Con flags, "tocar el
    /// timbre mientras se espera" o "insistir mientras se camina" serían estados
    /// alcanzables que hay que acordarse de excluir; acá son inexpresables.
    ///
    /// Además de leer el input, la FSM le DECLARA al <see cref="GameInput"/> qué
    /// está esperando: es lo que le da significado al timbre, que toca en la
    /// puerta y le pega al QTE con la puerta abierta.
    ///
    /// No dibuja nada: avisa por eventos y quien dibuja se suscribe.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PreacherController : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Asset raíz de balance. De acá salen caminata, timbre y espera.")]
        [SerializeField] private GameConfig gameConfig;

        [Tooltip("Spawner de casas: de él salen las puertas y la memoria del pity.")]
        [SerializeField] private HouseSpawner houseSpawner;

        [Tooltip("La entrada del juego. Entrega acciones (timbre, libro, felpudo), " +
                 "no botones, y esconde con cuál de las dos modalidades se juega.")]
        [SerializeField] private GameInput input;

        [Tooltip("Quien corre la cadena de objeciones cuando abren la puerta.")]
        [SerializeField] private SkillcheckRunner skillcheck;

        [Tooltip("Reloj del día. De acá sale cuánto avanzó el atardecer, que es lo " +
                 "único que este componente le pregunta: el día acelera y la " +
                 "caminata acelera con él.")]
        [SerializeField] private DayDirector day;

        [Header("Religión")]
        [Tooltip("Religión con la que arranca. La partida la pisa con la que eligió " +
                 "el jugador; esto es lo que vale si se corre la escena salteando la " +
                 "selección, y evita que probar a mano sea jugar sin multiplicadores.")]
        [SerializeField] private ReligionDefinition religion;

        [Header("Semilla")]
        [Tooltip("Semilla de esperas y ocupación. Aparte de la del spawner para " +
                 "poder recorrer la misma cuadra con otra suerte.")]
        [SerializeField] private int seed = 5150;

        private HouseOccupancyResolver resolver;
        private System.Random random;
        private DoorApproach approach;
        private DoorAttempt attempt;
        private ActionBuffer ringBuffer;
        private float stateTime;

        /// <summary>Estado actual.</summary>
        public PreacherState State { get; private set; } = PreacherState.Caminando;

        /// <summary>Casa cuya puerta se está atendiendo. <c>null</c> mientras camina.</summary>
        public HouseInstance Target => attempt?.House;

        /// <summary>
        /// Cuánto más rápido se camina AHORA, por el avance del día.
        ///
        /// Es público porque el indicador de timbre tiene que medir el alcance con
        /// el MISMO número: si lo calculara por su cuenta, alcanzaría con que a
        /// alguien se le pase una llamada para que el cartelito prometa una puerta
        /// que el felpudo no agarra. Sin reloj vale 1, que es el día sin acelerar.
        /// </summary>
        public float Pace => day != null
            ? gameConfig.Walk.PaceAt(day.SunsetProgress)
            : 1f;

        /// <summary>Avisa cada vez que cambia de estado.</summary>
        public event System.Action<PreacherState> StateChanged;

        /// <summary>Avisa al tocar el timbre, con la precisión 0..1 del timbrazo.</summary>
        public event System.Action<float> DoorbellRung;

        /// <summary>Avisa que el vecino asomó la cortina.</summary>
        public event System.Action TellFired;

        /// <summary>Avisa cada paso del vecino acercándose a la puerta.</summary>
        public event System.Action StepFired;

        /// <summary>Avisa cómo se resolvió la puerta.</summary>
        public event System.Action<HouseOccupancy> Resolved;

        /// <summary>
        /// Religión de la partida. La escribe quien maneja la selección ANTES de
        /// encender este componente: cambiarla con el día empezado dejaría media
        /// partida corrida con una velocidad y media con otra. Un null se ignora.
        /// </summary>
        public ReligionDefinition Religion
        {
            get => religion;
            set { if (value != null) religion = value; }
        }

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }
            random = new System.Random(gameConfig.SeedFor(seed));
            ringBuffer = new ActionBuffer(input.RingBufferSeconds);
        }

        /// <summary>
        /// El resolver se arma en Start y NO en Awake porque necesita el
        /// <c>PityTracker</c>, que el spawner crea en su propio Awake, y el orden
        /// de Awake entre dos GameObjects no está definido. Armarlo en Awake se
        /// guardaba un null la mitad de las veces y reventaba recién al tocar el
        /// primer timbre. Unity sí garantiza que todos los Awake corren antes que
        /// cualquier Start.
        /// </summary>
        private void Start()
        {
            if (houseSpawner.Pity == null)
            {
                Debug.LogError(
                    $"[PreacherController] '{name}': el spawner de casas no tiene " +
                    "PityTracker. ¿Se desactivó por referencias sin asignar?", this);
                enabled = false;
                return;
            }

            resolver = new HouseOccupancyResolver(
                houseSpawner.Pity, gameConfig.Pity, gameConfig.Neighbors);
        }

        private void OnEnable()
        {
            Declare(State);
        }

        /// <summary>
        /// Se hizo de noche. Si había una puerta abierta, se corta ahí mismo, sin
        /// resolverla: con la FSM apagada nadie más iba a avanzar la tirada, y el
        /// aro quedaba dibujado encima del final.
        ///
        /// La llama quien maneja la partida ANTES de apagar este componente.
        /// </summary>
        public void EndDay()
        {
            if (skillcheck != null) skillcheck.Cancel();
        }

        private void Update()
        {
            stateTime += Time.deltaTime;
            BufferRing();

            switch (State)
            {
                case PreacherState.Caminando: Walk(); break;
                case PreacherState.Acercandose: Approach(); break;
                case PreacherState.EnElFelpudo: OnMat(); break;
                case PreacherState.Esperando: Wait(); break;
                case PreacherState.Atendido: Attend(); break;
                case PreacherState.SinRespuesta: Undecided(); break;
                case PreacherState.Resuelto: Settled(); break;
            }
        }

        /// <summary>
        /// Patrulla la vereda hasta que el jugador pisa la plancha. Pisar SIEMPRE
        /// frena, esté donde esté: subirse lejos de una puerta cuesta el tiempo
        /// perdido, y ese es el castigo por bajarse mal.
        /// </summary>
        private void Walk()
        {
            float speed = gameConfig.Walk.UnitsPerSecondFor(religion, Pace);
            transform.position += Vector3.right * (speed * Time.deltaTime);

            if (!input.Held(GameAction.Felpudo)) return;

            StepOn();
        }

        /// <summary>
        /// El frenazo. La puntería se mide ACÁ, con el pie tocando la plancha, y no
        /// al llegar a la puerta: si se midiera al llegar, todas las puertas darían
        /// puntería perfecta.
        /// </summary>
        private void StepOn()
        {
            float x = transform.position.x;
            HouseInstance house = houseSpawner.NearestDoor(x, out float signedDistance);

            approach = DoorApproach.At(house, x, signedDistance, gameConfig.Doorbell, Pace);

            Enter(approach.Arrived(x, gameConfig.Doorbell.ArrivalTolerance)
                ? PreacherState.EnElFelpudo
                : PreacherState.Acercandose);
        }

        /// <summary>
        /// Camina hasta la puerta. Si se pasó, vuelve para atrás: que se vea que se
        /// dio cuenta. El viaje es más largo cuanto peor frenó, y ese tiempo es un
        /// castigo por sí solo, aparte del que le hace al skillcheck.
        /// </summary>
        private void Approach()
        {
            if (SteppedOff()) return;

            // También va con el ritmo: si el viaje hasta la puerta no acelerara con
            // el resto, frenar mal costaría cada vez MÁS segundos a medida que
            // avanza el día, que es un castigo que nadie diseñó.
            float speed = gameConfig.Doorbell.ApproachSpeed(
                gameConfig.Walk.UnitsPerSecondFor(religion, Pace));

            Vector3 position = transform.position;
            position.x = approach.Step(position.x, speed, Time.deltaTime);
            transform.position = position;

            if (approach.Arrived(position.x, gameConfig.Doorbell.ArrivalTolerance))
                Enter(PreacherState.EnElFelpudo);
        }

        /// <summary>
        /// Parado en la plancha. Si no hay puerta cerca no hay timbre que tocar, y
        /// lo único que queda es bajarse.
        /// </summary>
        private void OnMat()
        {
            if (SteppedOff()) return;
            if (!approach.HasDoor || !ringBuffer.TryConsume(Time.time)) return;

            Ring();
        }

        /// <summary>
        /// Guarda el timbrazo que llega un poco antes de poder usarse: justo antes
        /// de que llegue la señal del felpudo, mientras camina hasta la puerta
        /// después de frenar, o en los últimos instantes de la espera, donde se va
        /// a leer como insistir.
        ///
        /// Caminando SÍ se guarda, y es por el control físico: la plancha de la
        /// Raspberry Pi llega hasta ~190 ms tarde —revisa el sensor unas cinco
        /// veces por segundo— y el click del mouse llega al instante. En "pisar y
        /// tocar" el click llega PRIMERO; sin guardarlo, se perdía. Un click al aire
        /// sin pisar no toca nada: el buffer vence solo.
        ///
        /// Con la puerta abierta no se guarda: ahí el botón es del skillcheck, y un
        /// golpe no se guarda para después.
        /// </summary>
        private void BufferRing()
        {
            if (State == PreacherState.Atendido || State == PreacherState.Resuelto) return;

            if (input.Pressed(GameAction.Timbre)) ringBuffer.Record(Time.time);
        }

        private void Ring()
        {
            DoorbellRung?.Invoke(approach.Precision);

            attempt = DoorAttempt.Ring(
                approach.House, approach.Precision, resolver, random,
                gameConfig.Wait, gameConfig.Doorbell, religion);

            Enter(PreacherState.Esperando);
        }

        private void Wait()
        {
            // El bloqueo evita que soltar la plancha un instante por acomodar el pie
            // cancele la espera recién empezada, que sería indistinguible de haberse
            // arrepentido.
            if (stateTime >= gameConfig.Doorbell.AbortLockoutSeconds && SteppedOff()) return;

            WaitTick tick = attempt.Advance(Time.deltaTime);
            if (tick.TellFired) TellFired?.Invoke();
            if (tick.StepFired) StepFired?.Invoke();
            if (!tick.Finished) return;

            Resolved?.Invoke(attempt.Occupancy);

            // No abrieron. Puede ser que no haya nadie o que haya alguien
            // ignorando el timbre, y el jugador ya tiene con qué distinguirlo: si
            // vio moverse la cortina, hay alguien. La FSM no necesita saber cuál de
            // las dos es, solo que la puerta no se abrió.
            if (!attempt.WillOpen) { Enter(PreacherState.SinRespuesta); return; }

            // La cadena arranca en el mismo cuadro en que se abre la puerta: si
            // esperara al Update siguiente, el aro aparecería un cuadro después
            // que el vecino y se vería como un parpadeo.
            skillcheck.Begin(
                gameConfig.Doorbell.SkillcheckPrecisionFor(attempt.Precision),
                attempt.Neighbor, religion,
                gameConfig.Doorbell.ZoneScaleAt(attempt.Insists),
                day != null ? day.TimeUsed : 0f);

            Enter(PreacherState.Atendido);
        }

        /// <summary>
        /// Nadie abrió. Acá está la única decisión abierta del juego: insistir o
        /// irse. No hay salida automática a propósito — el que decide es el
        /// jugador, y lo que le cobra la indecisión es el reloj del día.
        /// </summary>
        private void Undecided()
        {
            if (SteppedOff()) return;

            if (!attempt.CanInsist || !ringBuffer.TryConsume(Time.time)) return;

            attempt.Insist();
            DoorbellRung?.Invoke(attempt.Precision);
            Enter(PreacherState.Esperando);
        }

        /// <summary>
        /// Corre la puerta abierta. El libro va entero al skillcheck y de acá no se
        /// puede salir: si abandonar fuera gratis y fallar costara tiempo y
        /// seguidores, el jugador abandonaría siempre que viera que iba a errar, y
        /// eso mataría el sistema de fallo entero junto con el castigo de tramo.
        /// </summary>
        private void Attend()
        {
            // Bajarse acá NO hace nada: el vecino ya abrió. Es una decisión de
            // balance y no de tono — si abandonar fuera gratis y fallar costara
            // tiempo y seguidores, el jugador abandonaría siempre que viera que iba
            // a errar, y eso mataría el sistema de fallo entero junto con el
            // castigo de tramo.
            skillcheck.Tick(Time.deltaTime, input.Pressed(GameAction.Libro));
            if (skillcheck.IsBusy) return;

            attempt = null;

            // ⚠️ Se lee el estado REAL de la plancha, no el que había al abrirse la
            // puerta. Quien se bajó por reflejo a mitad del skillcheck arranca a
            // caminar en este mismo instante, en vez de quedar trabado teniendo que
            // volver a subirse solo para poder bajarse.
            Enter(input.Held(GameAction.Felpudo)
                ? PreacherState.Resuelto
                : PreacherState.Caminando);
        }

        /// <summary>
        /// La puerta terminó y hay que bajarse a mano para seguir. El tiempo muerto
        /// es deliberado: convertir cuesta algo más que ganar el skillcheck.
        /// </summary>
        private void Settled()
        {
            SteppedOff();
        }

        /// <summary>
        /// Si el pie ya no está en la plancha, vuelve a la vereda y avisa que sí.
        /// Devolver el bool deja a cada estado cortar su propio <c>Update</c> con
        /// una sola línea, que es lo que evita que alguno siga corriendo lógica de
        /// puerta después de haber vuelto a caminar.
        /// </summary>
        private bool SteppedOff()
        {
            if (input.Held(GameAction.Felpudo)) return false;

            Abandon();
            return true;
        }

        /// <summary>Suelta la puerta y vuelve a la vereda. Todos los finales pasan por acá.</summary>
        private void Abandon()
        {
            attempt = null;
            approach = DoorApproach.Nowhere;
            Enter(PreacherState.Caminando);
        }

        private void Enter(PreacherState next)
        {
            State = next;
            stateTime = 0f;

            // Lo guardado no sobrevive a volver a la vereda ni a que abran: en los
            // dos casos ya no hay timbre para el que se estaba guardando.
            if (next == PreacherState.Caminando || next == PreacherState.Atendido)
                ringBuffer?.Clear();

            Declare(next);
            StateChanged?.Invoke(next);
        }

        /// <summary>
        /// Le dice a la entrada qué se está esperando. Se declara al ENTRAR al
        /// estado y no en cada <c>Update</c> porque el apretón de este cuadro es
        /// físicamente anterior al cambio de estado: interpretarlo con el contexto
        /// nuevo sería adelantarse a la mano del jugador.
        /// </summary>
        private void Declare(PreacherState state)
        {
            input.Context = PreacherInputContext.For(state);
        }

        private bool ValidateSetup()
        {
            if (gameConfig == null || houseSpawner == null
                || input == null || skillcheck == null)
            {
                Debug.LogError(
                    $"[PreacherController] '{name}' tiene referencias sin asignar " +
                    "(GameConfig, spawner de casas, input o skillcheck).", this);
                return false;
            }

            if (religion == null)
            {
                Debug.LogError($"[PreacherController] '{name}' no tiene religión.", this);
                return false;
            }

            return true;
        }
    }
}
