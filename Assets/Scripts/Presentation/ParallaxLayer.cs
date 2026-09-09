using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Desplaza una capa de fondo en horizontal para simular profundidad.
    /// Solo toca el eje X: la altura y el Z de la capa se respetan tal cual
    /// quedaron en la escena.
    /// </summary>
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
            // La capa se mueve JUNTO con la cámara en proporción (1 - factor).
            // Así su velocidad aparente respecto del mundo termina siendo
            // exactamente 'factor': con 1 no se mueve y scrollea como el mundo,
            // con 0 quedaría clavada a la cámara y parecería infinitamente lejana.
            float cameraTravel = cameraTransform.position.x - cameraOriginX;
            float x = originPosition.x + cameraTravel * (1f - parallaxFactor);

            transform.position = new Vector3(x, originPosition.y, originPosition.z);
        }
    }
}
