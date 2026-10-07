using System.Collections.Generic;
using BuenosDias.Config;
using BuenosDias.Core;
using BuenosDias.Presentation;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// Planta un decorado en los huecos entre terrenos: poste de luz o árbol de
    /// vereda, uno solo por hueco.
    ///
    /// Todo esto vive a parallax 1, en el mismo plano que el jugador. El poste
    /// porque en la fase 7 es una FUENTE DE LUZ y tiene que iluminar la vereda por
    /// la que camina el predicador. El árbol porque el sprite es mobiliario de
    /// vereda —cazuela de tierra en la base, la altura de una casa— y en una capa
    /// de parallax distinto la alineación con las casas deriva sola: tarde o
    /// temprano queda sobre un hueco, y ahí no hay altura que lo salve.
    ///
    /// Solo en los huecos, nunca frente a una fachada: es la misma regla que las
    /// señales. Un decorado delante de un buzón desbordado rompe el sistema de
    /// lectura, que es de lo único que depende el juego.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GapPropPlacer : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Spawner de casas. De él salen los huecos: esto no sabe generar " +
                 "cuadra, solo dónde se puede plantar.")]
        [SerializeField] private HouseSpawner houseSpawner;

        [Tooltip("Qué puede tocar en un hueco. Cada asset trae su propia " +
                 "probabilidad y compiten en una sola tirada; agregar un decorado " +
                 "nuevo es sumar un asset a esta lista.")]
        [SerializeField] private List<SceneryPropSet> candidates = new List<SceneryPropSet>();

        [Tooltip("Cámara que define cuándo reciclar. Se asigna desde el Inspector; " +
                 "nunca se busca en runtime.")]
        [SerializeField] private Camera targetCamera;

        [Header("Luz de farolas")]
        [Tooltip("De dónde sale la hora del día. Opcional: sin él las farolas " +
                 "quedan apagadas.")]
        [SerializeField] private DayDirector director;

        [Tooltip("Asset raíz de balance. De acá sale a qué hora se prende la luz.")]
        [SerializeField] private GameConfig gameConfig;

        [Tooltip("Material aditivo, el mismo de las ventanas encendidas. Sin él " +
                 "la luz no se vería sobre la noche.")]
        [SerializeField] private Material lightMaterial;

        [Header("Cobertura")]
        [Tooltip("Unidades por detrás del borde izquierdo antes de reciclar.")]
        [SerializeField, Min(0f)] private float behindUnits = 6f;

        [Tooltip("Tamaño del pool. Con un decorado cada dos huecos alcanza con pocos.")]
        [SerializeField, Min(2)] private int poolSize = 8;

        [Header("Semilla")]
        [Tooltip("Semilla del sorteo por hueco. Va aparte de la del spawner de casas " +
                 "para poder cambiar la densidad de decorados sin mover la cuadra.")]
        [SerializeField] private int seed = 4242;

        private readonly List<SpriteRenderer> active = new List<SpriteRenderer>();
        private ComponentPool<SpriteRenderer> pool;
        private GapPropChooser chooser;
        private System.Random random;
        private Sprite[] sprites;
        private float[] widths;

        private sealed class LampRig
        {
            public GameObject Root;
            public SpriteRenderer Halo;
            public SpriteRenderer Cone;
            public SpriteRenderer Pool;
        }

        private readonly Dictionary<SpriteRenderer, LampRig> rigs =
            new Dictionary<SpriteRenderer, LampRig>();
        private DayCycleConfig cycle;

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }

            sprites = new Sprite[candidates.Count];
            widths = new float[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                // Los decorados de hueco tienen una variante cada uno. Si algún día
                // tienen más, acá se toma la primera y el ancho se mide sobre ella;
                // variantes de distinto ancho pedirían medir la más ancha para que
                // el hueco mínimo siga siendo válido para cualquier sorteo.
                sprites[i] = candidates[i].CreateSprites()[0];
                widths[i] = sprites[i].rect.width / sprites[i].pixelsPerUnit;
            }

            chooser = new GapPropChooser(candidates, widths);
            random = new System.Random(seed);
            pool = BuildPool();
            if (gameConfig != null) cycle = gameConfig.DayCycle;
        }

        private void OnEnable() => houseSpawner.GapOpened += OnGapOpened;

        private void OnDisable() => houseSpawner.GapOpened -= OnGapOpened;

        private void LateUpdate()
        {
            float limit = targetCamera.transform.position.x
                          - targetCamera.orthographicSize * targetCamera.aspect
                          - behindUnits;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                SpriteRenderer prop = active[i];
                if (prop.bounds.max.x > limit) continue;

                active.RemoveAt(i);
                pool.Release(prop);
            }

            UpdateLampLights();
        }

        /// <summary>
        /// Cuánta luz tienen las farolas ahora, de 0 a 1. Sigue la misma curva que
        /// las ventanas, pero sube un poco más rápido: una farola apagada con el
        /// cielo ya oscuro se ve rota.
        /// </summary>
        private float LampAmount()
        {
            if (director == null || cycle == null) return 0f;

            float windows = cycle.WindowLightAt(director.SunsetProgress);
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(windows * 1.6f));
        }

        private void UpdateLampLights()
        {
            float amount = LampAmount();

            for (int i = 0; i < active.Count; i++)
            {
                if (!rigs.TryGetValue(active[i], out LampRig rig) || !rig.Root.activeSelf) continue;

                bool visible = amount > 0.01f;
                rig.Halo.enabled = visible;
                rig.Cone.enabled = visible;
                rig.Pool.enabled = visible;
                if (!visible) continue;

                rig.Halo.color = new Color(1f, 1f, 1f, amount);
                rig.Cone.color = new Color(1f, 1f, 1f, amount);
                rig.Pool.color = new Color(1f, 1f, 1f, amount);
            }
        }

        /// <summary>
        /// Arma (una sola vez por renderer del pool) la luz de una farola y la
        /// enciende o apaga según el decorado que le tocó. El renderer se recicla
        /// entre farolas y árboles, así que la luz se queda colgada y se esconde.
        /// </summary>
        private void ConfigureLampLight(SpriteRenderer prop, SceneryPropSet set)
        {
            bool wantsLight = set.EmitsLight && lightMaterial != null;

            if (!rigs.TryGetValue(prop, out LampRig rig))
            {
                if (!wantsLight) return;
                rig = BuildRig(prop);
                rigs.Add(prop, rig);
            }

            rig.Root.SetActive(wantsLight);
            if (!wantsLight) return;

            Vector2 origin = set.LightOffset;
            rig.Root.transform.localPosition = new Vector3(origin.x, origin.y, 0f);

            // el charco va en el piso: la lámpara está a 'origin.y' sobre él
            rig.Pool.transform.localPosition = new Vector3(0f, -origin.y - 0.15f, 0f);

            int order = set.SortingOrder;
            rig.Halo.sortingLayerName = set.SortingLayer;
            rig.Cone.sortingLayerName = set.SortingLayer;
            rig.Pool.sortingLayerName = set.SortingLayer;
            rig.Cone.sortingOrder = order + 1;
            rig.Pool.sortingOrder = order + 1;
            rig.Halo.sortingOrder = order + 2;
        }

        private LampRig BuildRig(SpriteRenderer prop)
        {
            var root = new GameObject("Luz");
            root.transform.SetParent(prop.transform, false);

            return new LampRig
            {
                Root = root,
                Halo = MakeLightRenderer(root.transform, "Halo", LampLightSprites.Halo),
                Cone = MakeLightRenderer(root.transform, "Cono", LampLightSprites.Cone),
                Pool = MakeLightRenderer(root.transform, "Charco", LampLightSprites.Pool),
            };
        }

        private SpriteRenderer MakeLightRenderer(Transform parent, string objectName, Sprite sprite)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = lightMaterial;
            renderer.enabled = false;
            return renderer;
        }

        /// <summary>
        /// Planta el decorado centrado en el hueco. Centrado y no corrido al azar:
        /// es lo único que garantiza que no invada ninguna de las dos fachadas con
        /// cualquier ancho de hueco.
        /// </summary>
        private void OnGapOpened(float gapStartX, float gapWidth)
        {
            int pick = chooser.Choose(random, gapWidth);
            if (pick < 0) return;

            SpriteRenderer prop = pool.Get();
            if (prop == null) return;

            SceneryPropSet set = candidates[pick];
            prop.sprite = sprites[pick];
            prop.sortingLayerName = set.SortingLayer;
            prop.sortingOrder = set.SortingOrder;
            prop.color = set.Tint;

            // Se centra el BORDE IZQUIERDO en el hueco y recién ahí se corrige por el
            // pivot, que no está del mismo lado en todos los sprites del proyecto.
            float left = gapStartX + (gapWidth - widths[pick]) * 0.5f;
            float pivotX = sprites[pick].pivot.x / sprites[pick].rect.width;
            prop.transform.position = new Vector3(
                left + pivotX * widths[pick], set.GroundOffset, transform.position.z);

            ConfigureLampLight(prop, set);
            active.Add(prop);
        }

        private ComponentPool<SpriteRenderer> BuildPool()
        {
            // El molde va sin sprite: cada hueco decide el suyo al plantarlo. Se
            // desactiva antes de copiarlo para que no llegue a dibujarse en el
            // origen durante el cuadro en que se lo destruye.
            var template = new GameObject("Decorado");
            template.transform.SetParent(transform, false);
            var renderer = template.AddComponent<SpriteRenderer>();

            template.SetActive(false);
            var built = new ComponentPool<SpriteRenderer>(renderer, poolSize, transform);
            Destroy(template);
            return built;
        }

        private bool ValidateSetup()
        {
            if (houseSpawner == null || targetCamera == null)
            {
                Debug.LogError(
                    $"[GapPropPlacer] '{name}' no tiene spawner de casas o cámara.", this);
                return false;
            }

            float total = 0f;
            foreach (SceneryPropSet set in candidates)
            {
                if (set == null || !set.IsUsable)
                {
                    Debug.LogError(
                        $"[GapPropPlacer] '{name}' tiene un candidato vacío o sin sprite.", this);
                    return false;
                }
                total += set.ChancePerGap;
            }

            if (candidates.Count == 0)
            {
                Debug.LogError($"[GapPropPlacer] '{name}' no tiene candidatos.", this);
                return false;
            }

            // No es fatal, pero conviene que se note: pasado 1, los últimos
            // candidatos de la lista no salen nunca y el Inspector miente.
            if (total > 1f)
            {
                Debug.LogWarning(
                    $"[GapPropPlacer] Las probabilidades por hueco suman {total:F2}. " +
                    "Pasado 1, los últimos candidatos de la lista no salen nunca.", this);
            }

            return true;
        }
    }
}
