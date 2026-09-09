using System.Collections.Generic;
using BuenosDias.Config;
using BuenosDias.Core;
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
