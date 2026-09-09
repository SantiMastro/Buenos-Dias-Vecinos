using BuenosDias.Config;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// Dueño del reloj del día. Lo corre, le pasa lo que cada puerta gana o
    /// pierde, y avisa cuando se hizo de noche.
    ///
    /// La lógica del reloj no está acá sino en <see cref="DayClock"/>, que es
    /// plano y testeable. Esta clase es solo el enchufe: <c>Update</c>, la
    /// suscripción al skillcheck y los eventos.
    ///
    /// No dibuja. El cielo y la luz los pinta quien escuche.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DayDirector : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Asset raíz de balance. De acá sale la duración del día.")]
        [SerializeField] private GameConfig gameConfig;

        [Tooltip("Quien resuelve las puertas. De él sale el tiempo que se gana o " +
                 "se pierde en cada una.")]
        [SerializeField] private SkillcheckRunner skillcheck;

        private DayClock clock;

        /// <summary>Segundos que quedan de día.</summary>
        public float Remaining => clock?.Remaining ?? 0f;

        /// <summary>
        /// Lo más oscuro que llegó a estar el día, de 0 a 1. Es el que va a los
        /// gradientes: nunca baja, así que el cielo no se aclara al convertir.
        /// </summary>
        public float SunsetProgress => clock?.SunsetProgress ?? 0f;

        /// <summary>Si el día ya terminó.</summary>
        public bool IsOver => clock != null && clock.IsOver;

        /// <summary>
        /// Se dispara UNA vez, cuando cae la noche. Entrega la COMITIVA final —los
        /// seguidores que quedan caminando atrás— para que la fase 9 resuelva cuál
        /// de los tres finales corresponde.
        ///
        /// ⚠️ No entrega las conversiones acumuladas. Son dos números distintos:
        /// convertir 10 y perder 3 en el camino da 10 conversiones y 7 seguidores,
        /// y el que vale es el 7. El puntaje es la gente que te acompaña cuando se
        /// hace de noche, no la que alguna vez dijo que sí.
        /// </summary>
        public event System.Action<int> DayEnded;

        /// <summary>Avisa cada cuadro cuánto queda. Lo escucha la barra de la fase 8.</summary>
        public event System.Action<float> RemainingChanged;

        private void Awake()
        {
            if (gameConfig == null || skillcheck == null)
            {
                Debug.LogError(
                    $"[DayDirector] '{name}' tiene referencias sin asignar " +
                    "(GameConfig o skillcheck).", this);
                enabled = false;
                return;
            }

            clock = new DayClock(gameConfig.DayDurationSeconds, gameConfig.InfiniteTimeDebug);
        }

        private void OnEnable()
        {
            if (skillcheck != null) skillcheck.Finished += OnDoorFinished;
        }

        private void OnDisable()
        {
            if (skillcheck != null) skillcheck.Finished -= OnDoorFinished;
        }

        private void Update()
        {
            if (clock.IsOver) return;

            bool ended = clock.Advance(Time.deltaTime);
            RemainingChanged?.Invoke(clock.Remaining);

            if (ended) DayEnded?.Invoke(skillcheck.Followers);
        }

        /// <summary>
        /// El resultado ya trae el delta calculado y con el techo de la religión
        /// aplicado: acá no se vuelve a decidir cuánto vale una conversión.
        /// </summary>
        private void OnDoorFinished(SkillcheckResult result)
        {
            clock.Add(result.TimeDelta);
            RemainingChanged?.Invoke(clock.Remaining);

            if (clock.IsOver) DayEnded?.Invoke(skillcheck.Followers);
        }
    }
}
