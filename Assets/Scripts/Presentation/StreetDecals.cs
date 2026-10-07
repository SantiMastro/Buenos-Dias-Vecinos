using System.Collections.Generic;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Detalles sueltos sobre el asfalto: tapas de alcantarilla, charcos y hojas.
    ///
    /// Van en el plano de juego (parallax 1, quietos en el mundo) y se reparten por
    /// delante de la cámara con huecos al azar. Los que quedan atrás se reciclan
    /// adelante, así nunca hay más de un puñado de objetos vivos.
    ///
    /// Sin simulación ni eventos: decorado puro, como <see cref="StreetTraffic"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StreetDecals : MonoBehaviour
    {
        private const float PixelsPerUnit = 32f;

        [Header("Referencias")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Material de sprite lit, el mismo del resto del mundo.")]
        [SerializeField] private Material spriteMaterial;

        [Tooltip("Detalles posibles: tapas, charcos, hojas. Pivot al centro.")]
        [SerializeField] private Sprite[] sprites;

        [Header("Reparto")]
        [Tooltip("Separación entre un detalle y el siguiente, en unidades (mín, máx).")]
        [SerializeField] private Vector2 gapRange = new Vector2(2.5f, 7f);

        [Tooltip("Banda de Y donde pueden aparecer (mín, máx). La calle va de -2 a -0.75.")]
        [SerializeField] private Vector2 yRange = new Vector2(-1.85f, -1.1f);

        [SerializeField] private string sortingLayerName = "Ground";

        [Tooltip("Entre la calle (0) y la vereda (10).")]
        [SerializeField] private int sortingOrder = 5;

        private readonly List<SpriteRenderer> active = new List<SpriteRenderer>();
        private readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        private float nextX;

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }

            float camX = targetCamera.transform.position.x;
            nextX = camX - HalfView() - 2f;
        }

        private void Update()
        {
            float camX = targetCamera.transform.position.x;
            float halfView = HalfView();

            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i].transform.position.x < camX - halfView - 3f)
                {
                    active[i].gameObject.SetActive(false);
                    pool.Push(active[i]);
                    active.RemoveAt(i);
                }
            }

            // se corre hacia la cámara si el jugador salta (checkpoint, reinicio)
            if (nextX < camX - halfView - 3f) nextX = camX - halfView - 2f;

            while (nextX < camX + halfView + 2f)
            {
                Spawn(nextX);
                nextX += Random.Range(gapRange.x, gapRange.y);
            }
        }

        private void Spawn(float x)
        {
            SpriteRenderer renderer = Rent();
            renderer.sprite = sprites[Random.Range(0, sprites.Length)];
            renderer.flipX = Random.value < 0.5f;

            float y = Random.Range(yRange.x, yRange.y);
            renderer.transform.position = new Vector3(Snap(x), Snap(y), 0f);
            renderer.gameObject.SetActive(true);
            active.Add(renderer);
        }

        private SpriteRenderer Rent()
        {
            if (pool.Count > 0) return pool.Pop();

            var go = new GameObject("Detalle");
            go.transform.SetParent(transform, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = spriteMaterial;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private float HalfView()
        {
            return targetCamera.orthographicSize * targetCamera.aspect;
        }

        private static float Snap(float value)
        {
            return Mathf.Round(value * PixelsPerUnit) / PixelsPerUnit;
        }

        private bool ValidateSetup()
        {
            if (targetCamera == null || spriteMaterial == null)
            {
                Debug.LogError(
                    $"[StreetDecals] '{name}' tiene referencias sin asignar (cámara o material).", this);
                return false;
            }

            if (sprites == null || sprites.Length == 0)
            {
                Debug.LogError($"[StreetDecals] '{name}' no tiene sprites asignados.", this);
                return false;
            }

            return true;
        }
    }
}
