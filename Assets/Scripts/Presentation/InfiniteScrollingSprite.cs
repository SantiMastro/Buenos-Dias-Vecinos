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
    ///
    /// Orden −30: después de la cámara (−50) y de la capa padre (−40), para leer
    /// las dos ya ubicadas en este cuadro.
    ///
    /// ⚠️ El ancho a cubrir se recalcula cuando cambia la vista. La Pixel Perfect
    /// Camera reescribe el <c>orthographicSize</c> en runtime según la ventana (se
    /// midió 3.688 donde el diseño dice 3.375), así que calcularlo una sola vez en
    /// <c>Awake</c> dejaba asomar el borde de la tira en ventanas que no son 16:9.
    /// </summary>
    [DefaultExecutionOrder(-30)]
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
        private float coveredOrthographicSize = -1f;
        private float coveredAspect = -1f;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            parentTransform = transform.parent;

            if (!ValidateSetup()) { enabled = false; return; }

            tileWidth = TileWidthOf(spriteRenderer.sprite);
            RefreshCover();
        }

        /// <summary>
        /// Rehace el ancho de la tira si la vista de la cámara cambió desde la
        /// última vez. La comparación es exacta a propósito: los dos valores los
        /// escribe la cámara de a saltos, no derivan solos.
        /// </summary>
        private void RefreshCover()
        {
            float orthographicSize = targetCamera.orthographicSize;
            float aspect = targetCamera.aspect;
            if (orthographicSize == coveredOrthographicSize && aspect == coveredAspect) return;

            coveredOrthographicSize = orthographicSize;
            coveredAspect = aspect;

            coverWidth = CalculateCoverWidth(tileWidth, orthographicSize * 2f * aspect, marginTiles);
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
            RefreshCover();

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
