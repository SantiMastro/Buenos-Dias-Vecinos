using System.Collections.Generic;
using BuenosDias.Config;
using BuenosDias.Core;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// Mantiene la cuadra infinita: genera casas por delante de la cámara y
    /// recicla las que quedaron atrás.
    ///
    /// El reciclado se decide por DISTANCIA y no por cantidad de casas: con
    /// anchos de 190 a 270 px y separaciones variables, un tope de "N casas"
    /// cubre un tramo de calle distinto en cada partida.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HouseSpawner : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Asset raíz de balance. De acá salen la generación y el anti-racha.")]
        [SerializeField] private GameConfig gameConfig;

        [Tooltip("Prefab de casa. Se instancia una sola vez al armar el pool.")]
        [SerializeField] private HouseInstance housePrefab;

        [Tooltip("Cámara que define qué hay que cubrir. Se asigna desde el " +
                 "Inspector; nunca se busca en runtime.")]
        [SerializeField] private Camera targetCamera;

        [Header("Cobertura")]
        [Tooltip("Unidades por delante del borde derecho de la cámara que se " +
                 "mantienen siempre generadas.")]
        [SerializeField, Min(0f)] private float aheadUnits = 8f;

        [Tooltip("Unidades por detrás del borde izquierdo antes de reciclar una " +
                 "casa. Con margen de sobra: reciclar algo que todavía se ve es " +
                 "un parpadeo muy visible.")]
        [SerializeField, Min(0f)] private float behindUnits = 6f;

        [Tooltip("Tamaño del pool. Tiene que cubrir el peor caso: pantalla llena " +
                 "de casas angostas más los márgenes.")]
        [SerializeField, Min(2)] private int poolSize = 10;

        [Header("Semilla")]
        [Tooltip("Semilla de generación. Solo sale la MISMA cuadra si el GameConfig " +
                 "está en modo verificación; si no, esto es el punto de partida que " +
                 "se mezcla con el reloj para que cada partida traiga otro barrio.")]
        [SerializeField] private int seed = 1234;

        private readonly List<HouseInstance> active = new List<HouseInstance>();
        private ComponentPool<HouseInstance> pool;
        private HouseLayoutGenerator generator;
        private float nextSpawnX;

        /// <summary>
        /// Memoria del anti-racha de la partida.
        ///
        /// Vive acá y no en un dueño aparte porque el pity es, ante todo, una
        /// entrada de GENERACIÓN: sesga las casas que se van creando por delante
        /// del jugador. Quien resuelve el timbre la lee desde acá por Inspector, y
        /// así no hace falta ni un locator ni un orden de Awake garantizado.
        /// </summary>
        public PityTracker Pity { get; private set; }

        /// <summary>Casas visibles ahora mismo.</summary>
        public IReadOnlyList<HouseInstance> Active => active;

        /// <summary>
        /// Se dispara al abrir un hueco entre dos casas. Entrega la X mundial del
        /// borde izquierdo del hueco y su ancho, en unidades.
        ///
        /// Existe para que el poste de luz sepa dónde puede plantarse sin taparle
        /// al jugador una fachada, sin que el spawner tenga que conocer al poste.
        /// </summary>
        public event System.Action<float, float> GapOpened;

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }

            pool = new ComponentPool<HouseInstance>(housePrefab, poolSize, transform);
            generator = new HouseLayoutGenerator(
                gameConfig.HouseGeneration, gameConfig.Pity, gameConfig.SeedFor(seed));
            Pity = new PityTracker(gameConfig.Pity);

            nextSpawnX = targetCamera.transform.position.x;
        }

        private void LateUpdate()
        {
            RecycleBehind();
            SpawnAhead();
        }

        private void SpawnAhead()
        {
            float limit = targetCamera.transform.position.x + HalfView() + aheadUnits;

            while (nextSpawnX < limit)
            {
                HouseInstance house = pool.Get();
                if (house == null) return;   // pool agotado: se corta hasta reciclar

                HouseLayout layout = generator.Next(EmptyRun);
                house.Initialize(gameConfig.HouseGeneration);

                float half = layout.LotWidthUnits * 0.5f;
                house.transform.position = new Vector3(nextSpawnX + half, 0f, 0f);
                house.Apply(layout);

                active.Add(house);

                GapOpened?.Invoke(nextSpawnX + layout.LotWidthUnits, layout.GapUnits);
                nextSpawnX += layout.LotWidthUnits + layout.GapUnits;
            }
        }

        private void RecycleBehind()
        {
            float limit = targetCamera.transform.position.x - HalfView() - behindUnits;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                HouseInstance house = active[i];
                if (house.transform.position.x + house.WidthUnits * 0.5f > limit) continue;

                active.RemoveAt(i);
                pool.Release(house);
            }
        }

        private float HalfView()
        {
            return targetCamera.orthographicSize * targetCamera.aspect;
        }

        /// <summary>
        /// Casa con la puerta más cercana a esa X, y a qué distancia con signo
        /// quedó (negativo si todavía no se llegó). Devuelve <c>null</c> si no hay
        /// ninguna activa.
        ///
        /// La consulta vive con la lista y no en quien pregunta: el spawner es el
        /// único que sabe cuántas casas hay y cuándo se reciclan.
        /// </summary>
        public HouseInstance NearestDoor(float worldX, out float signedDistance)
        {
            HouseInstance best = null;
            signedDistance = 0f;
            float bestAbs = float.MaxValue;

            foreach (HouseInstance house in active)
            {
                float delta = worldX - house.DoorPosition.x;
                float abs = Mathf.Abs(delta);
                if (abs >= bestAbs) continue;

                bestAbs = abs;
                signedDistance = delta;
                best = house;
            }

            return best;
        }

        /// <summary>
        /// Racha de vacías que alimenta el sesgo de generación.
        ///
        /// Es el mecanismo PRINCIPAL del anti-racha: con la racha alta, las casas
        /// que se generan por delante del jugador salen sesgadas hacia señales
        /// positivas. El barrio se pone más amable, pero las señales siguen
        /// diciendo la verdad — que es la regla de §3.3.
        /// </summary>
        private int EmptyRun => Pity?.EmptyRun ?? 0;

        private bool ValidateSetup()
        {
            if (gameConfig == null || housePrefab == null || targetCamera == null)
            {
                Debug.LogError(
                    $"[HouseSpawner] '{name}' tiene referencias sin asignar " +
                    "(GameConfig, prefab de casa o cámara).", this);
                return false;
            }

            if (gameConfig.HouseGeneration == null || gameConfig.Pity == null)
            {
                Debug.LogError("[HouseSpawner] El GameConfig no tiene HouseGen o Pity.", this);
                return false;
            }

            return true;
        }
    }
}
