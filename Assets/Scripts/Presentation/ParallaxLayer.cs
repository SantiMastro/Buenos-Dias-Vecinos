using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Desplaza una capa de fondo en horizontal para simular profundidad.
    /// Solo toca el eje X: la altura y el Z de la capa se respetan tal cual
    /// quedaron en la escena.
    ///
    /// ⚠️ Corre DESPUÉS de la cámara y ANTES de sus hijos (orden −40, entre la
    /// cámara en −50 y los sembradores en −30). Con el orden librado a Unity, la
    /// capa leía a veces la cámara del cuadro anterior: en el skyline eso son unos
    /// dos píxeles de error que cambian con el <c>deltaTime</c>, y la capa temblaba.
    ///
    /// ⚠️ Y trabaja en la GRILLA DE PÍXELES. La Pixel Perfect Camera redondea la
    /// vista a la grilla; si la capa quedaba en una X sub-píxel calculada con la
    /// cámara sin redondear, el corrimiento en pantalla oscilaba medio píxel y la
    /// capa saltaba uno para atrás y para adelante.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public sealed class ParallaxLayer : MonoBehaviour
    {
        [Header("Profundidad")]
        [Tooltip("Velocidad aparente de scroll respecto del mundo.\n" +
                 "1 = a la par de las casas (sin parallax).\n" +
                 "Menor a 1 = más lejos: skyline 0.18, árboles y postes 0.55.\n" +
                 "Mayor a 1 = más cerca que el jugador: arbustos de frente 1.25.")]
        [SerializeField, Range(0f, 3f)] private float parallaxFactor = 1f;

        [Header("Referencias")]
        [Tooltip("Cámara que define el desplazamiento. Se asigna desde el Inspector; " +
                 "nunca se busca en runtime.")]
        [SerializeField] private Transform cameraTransform;

        private Vector3 originPosition;
        private float cameraOriginX;

        /// <summary>Velocidad aparente de scroll de esta capa respecto del mundo.</summary>
        public float ParallaxFactor => parallaxFactor;

        /// <summary>Redondea a la grilla de píxeles. Con un PPU no positivo no toca nada.</summary>
        public static float Snap(float value, float pixelsPerUnit)
        {
            return pixelsPerUnit > 0f ? Mathf.Round(value * pixelsPerUnit) / pixelsPerUnit : value;
        }

        /// <summary>
        /// X de la capa para esa posición de cámara.
        ///
        /// La capa se mueve JUNTO con la cámara en proporción (1 − factor). Así su
        /// velocidad aparente respecto del mundo termina siendo exactamente el
        /// factor: con 1 no se mueve y scrollea como el mundo, con 0 queda clavada
        /// a la cámara y parece infinitamente lejana.
        ///
        /// Las dos X de cámara se redondean ANTES, igual que lo hace la Pixel
        /// Perfect Camera, y el resultado DESPUÉS: así el corrimiento en pantalla
        /// siempre es un número entero de píxeles y nunca retrocede.
        /// </summary>
        public static float LayerX(
            float originX, float cameraX, float cameraOriginX, float factor, float pixelsPerUnit)
        {
            float travel = Snap(cameraX, pixelsPerUnit) - Snap(cameraOriginX, pixelsPerUnit);
            return Snap(originX + travel * (1f - factor), pixelsPerUnit);
        }

        private void Awake()
        {
            if (cameraTransform == null)
            {
                Debug.LogError(
                    $"[ParallaxLayer] '{name}' no tiene cámara asignada. " +
                    "Arrastrá la Main Camera al campo 'Camera Transform'.", this);
                enabled = false;
                return;
            }

            originPosition = transform.position;
            cameraOriginX = cameraTransform.position.x;
        }

        private void LateUpdate()
        {
            float x = LayerX(
                originPosition.x, cameraTransform.position.x, cameraOriginX,
                parallaxFactor, ProjectConstants.PixelsPerUnit);

            transform.position = new Vector3(x, originPosition.y, originPosition.z);
        }
    }
}
