using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Mantiene un <see cref="SpriteRenderer"/> en modo Tiled cubriendo siempre
    /// el ancho de la cámara, saltando de a un ancho de tile entero.
    ///
    /// Va en un hijo de la capa; el padre puede llevar un <see cref="ParallaxLayer"/>.
    /// El salto se calcula en el espacio LOCAL del padre a propósito: así el
    /// parallax del padre desplaza la capa y acá solo resolvemos la cobertura,
    /// sin que un componente pise al otro.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class InfiniteScrollingSprite : MonoBehaviour
    {
        [Header("Cobertura")]
        [Tooltip("Tiles enteros de margen a cada lado de la cámara. 1 alcanza; " +
                 "subilo si ves un borde entrar en pantalla al moverse rápido.")]
        [SerializeField, Range(1, 4)] private int marginTiles = 1;

        [Header("Referencias")]
        [Tooltip("Cámara a cubrir. Se asigna desde el Inspector; nunca se busca en runtime.")]
        [SerializeField] private Camera targetCamera;

        private SpriteRenderer spriteRenderer;
        private Transform parentTransform;
        private float tileWidth;
        private float coverWidth;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            parentTransform = transform.parent;

            if (!ValidateSetup()) { enabled = false; return; }

            tileWidth = TileWidthOf(spriteRenderer.sprite);

            float viewWidth = targetCamera.orthographicSize * 2f * targetCamera.aspect;
            coverWidth = CalculateCoverWidth(tileWidth, viewWidth, marginTiles);

            spriteRenderer.size = new Vector2(coverWidth, spriteRenderer.size.y);
        }

        /// <summary>Ancho de un tile en unidades de mundo.</summary>
        public static float TileWidthOf(Sprite sprite)
        {
            return sprite.rect.width / sprite.pixelsPerUnit;
        }

        /// <summary>
        /// Ancho total a cubrir: el visible más el margen, redondeado hacia arriba
        /// a tiles enteros para que el patrón nunca corte a mitad de camino.
        /// El builder de escena usa esto mismo, así el aspecto en el Editor
        /// coincide con el de Play.
        /// </summary>
        public static float CalculateCoverWidth(float tileWidth, float viewWidth, int marginTiles)
        {
            int tilesNeeded = Mathf.CeilToInt(viewWidth / tileWidth) + marginTiles * 2;
            return tilesNeeded * tileWidth;
        }

        private void LateUpdate()
        {
            float cameraLocalX = ToParentSpaceX(targetCamera.transform.position.x);
            float desiredLeft = cameraLocalX - coverWidth * 0.5f;

            // Redondear hacia abajo a un múltiplo del ancho de tile deja la fase
            // del patrón clavada a la grilla local: la costura nunca se ve.
            float snappedLeft = Mathf.Floor(desiredLeft / tileWidth) * tileWidth;

            Vector3 local = transform.localPosition;
            transform.localPosition = new Vector3(snappedLeft, local.y, local.z);
        }

        private float ToParentSpaceX(float worldX)
        {
            if (parentTransform == null) return worldX;
            return parentTransform.InverseTransformPoint(new Vector3(worldX, 0f, 0f)).x;
        }

        private bool ValidateSetup()
        {
            if (targetCamera == null)
            {
                Debug.LogError($"[InfiniteScrollingSprite] '{name}' no tiene cámara asignada.", this);
                return false;
            }

            if (spriteRenderer.sprite == null)
            {
                Debug.LogError($"[InfiniteScrollingSprite] '{name}' no tiene sprite.", this);
                return false;
            }

            if (spriteRenderer.drawMode == SpriteDrawMode.Simple)
            {
                Debug.LogError(
                    $"[InfiniteScrollingSprite] '{name}' necesita Draw Mode = Tiled. " +
                    "En Simple no se puede setear el tamaño.", this);
                return false;
            }

            return true;
        }
    }
}
