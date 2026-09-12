using System.Collections.Generic;
using BuenosDias.Config;
using BuenosDias.Core;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Siembra props de decorado a lo largo de la calle, sueltos y con huecos
    /// entre uno y otro, y los recicla cuando quedan atrás.
    ///
    /// Trabaja en el espacio LOCAL de su capa: el padre puede llevar un
    /// <see cref="ParallaxLayer"/>, así el desplazamiento lo resuelve la capa y
    /// acá solo se decide la cobertura, sin que un componente pise al otro.
    ///
    /// Orden −30: después de la cámara (−50) y de su capa (−40), para leer las
    /// dos ya ubicadas en este cuadro.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    [DisallowMultipleComponent]
    public sealed class ScenerySpawner : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Qué se siembra: sprite, recorte, altura, tinte y separaciones.")]
        [SerializeField] private SceneryPropSet propSet;

        [Tooltip("Cámara que define qué tramo hay que cubrir. Se asigna desde el " +
                 "Inspector; nunca se busca en runtime.")]
        [SerializeField] private Camera targetCamera;

        [Header("Cobertura")]
        [Tooltip("Unidades de margen a cada lado de la cámara. Con margen de sobra " +
                 "ningún prop aparece ni desaparece dentro del cuadro.")]
        [SerializeField, Min(0f)] private float marginUnits = 4f;

        [Tooltip("Tamaño del pool. Tiene que cubrir el peor caso: la pantalla llena " +
                 "de props con la separación mínima, más los márgenes.")]
        [SerializeField, Min(2)] private int poolSize = 12;

        [Header("Semilla")]
        [Tooltip("Semilla de siembra. Con la misma semilla sale la misma cuadra, " +
                 "lo que permite reproducir una composición fea.")]
        [SerializeField] private int seed = 7788;

        private readonly List<SpriteRenderer> active = new List<SpriteRenderer>();
        private ComponentPool<SpriteRenderer> pool;
        private System.Random random;
        private Sprite[] variants;
        private float nextX;

        /// <summary>Props visibles ahora mismo.</summary>
        public IReadOnlyList<SpriteRenderer> Active => active;

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }

            variants = propSet.CreateSprites();
            random = new System.Random(seed);
            pool = BuildPool();

            // Se arranca un margen por detrás para que el primer cuadro ya tenga
            // sembrado todo lo que se ve, sin un tramo pelado a la izquierda.
            nextX = CameraLocalX() - HalfView() - marginUnits;
        }

        private void LateUpdate()
        {
            RecycleBehind();
            SpawnAhead();
        }

        private void SpawnAhead()
        {
            float limit = CameraLocalX() + HalfView() + marginUnits;

            while (nextX < limit)
            {
                SpriteRenderer prop = pool.Get();
                if (prop == null) return;   // pool agotado: se corta hasta reciclar

                Sprite variant = variants[random.Next(variants.Length)];
                prop.sprite = variant;

                // nextX es el BORDE IZQUIERDO del prop, pero el transform apunta al
                // pivot, y no todos los sprites lo tienen en el mismo lado: las matas
                // importan BottomCenter (prefijo prop_) y las tiras BottomLeft (bg_).
                // Sin esta corrección, media capa aparece corrida media mata.
                float width = variant.rect.width / variant.pixelsPerUnit;
                float pivotX = variant.pivot.x / variant.rect.width;
                // A la grilla de píxeles, igual que la capa: un prop a medio píxel
                // se rasteriza distinto de un cuadro al otro.
                float x = ParallaxLayer.Snap(nextX + pivotX * width, ProjectConstants.PixelsPerUnit);
                prop.transform.localPosition = new Vector3(x, propSet.GroundOffset, 0f);

                active.Add(prop);
                nextX += width + propSet.RollSpacing(random);
            }
        }

        private void RecycleBehind()
        {
            float limit = CameraLocalX() - HalfView() - marginUnits;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                SpriteRenderer prop = active[i];

                // Todo en espacio LOCAL de la capa. Usar prop.bounds acá estaría mal:
                // bounds es espacio de mundo, y en una capa con parallax el mundo y lo
                // local no coinciden, así que el reciclado se correría con la cámara.
                Sprite variant = prop.sprite;
                float width = variant.rect.width / variant.pixelsPerUnit;
                float pivotX = variant.pivot.x / variant.rect.width;
                float rightEdge = prop.transform.localPosition.x + (1f - pivotX) * width;

                if (rightEdge > limit) continue;

                active.RemoveAt(i);
                pool.Release(prop);
            }
        }

        /// <summary>
        /// Arma el pool a partir de un molde desactivado. El molde se desactiva
        /// ANTES de copiarlo para que no llegue a dibujarse en el origen durante
        /// el cuadro en que se lo destruye.
        /// </summary>
        private ComponentPool<SpriteRenderer> BuildPool()
        {
            var template = new GameObject(propSet.name);
            template.transform.SetParent(transform, false);

            var renderer = template.AddComponent<SpriteRenderer>();
            renderer.sprite = variants[0];
            renderer.sortingLayerName = propSet.SortingLayer;
            renderer.sortingOrder = propSet.SortingOrder;
            renderer.color = propSet.Tint;

            template.SetActive(false);
            var built = new ComponentPool<SpriteRenderer>(renderer, poolSize, transform);
            Destroy(template);
            return built;
        }

        /// <summary>X de la cámara en el espacio local de esta capa.</summary>
        private float CameraLocalX()
        {
            return transform.InverseTransformPoint(
                new Vector3(targetCamera.transform.position.x, 0f, 0f)).x;
        }

        private float HalfView() => targetCamera.orthographicSize * targetCamera.aspect;

        private bool ValidateSetup()
        {
            if (targetCamera == null)
            {
                Debug.LogError($"[ScenerySpawner] '{name}' no tiene cámara asignada.", this);
                return false;
            }

            if (propSet == null || !propSet.IsUsable)
            {
                Debug.LogError(
                    $"[ScenerySpawner] '{name}' no tiene decorado asignado, o el " +
                    "asset no tiene sprite.", this);
                return false;
            }

            return true;
        }
    }
}
