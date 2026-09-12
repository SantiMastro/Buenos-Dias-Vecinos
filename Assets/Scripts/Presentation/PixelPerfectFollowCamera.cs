using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Sigue al objetivo en horizontal manteniendo la altura fija.
    ///
    /// A propósito NO redondea la posición a la grilla de píxeles: URP lo hace
    /// solo. <c>PixelPerfectCamera.PixelSnap()</c> corre en cada
    /// <c>OnBeginCameraRendering</c> y ajusta la <c>worldToCameraMatrix</c> sin
    /// mover el transform. Redondear acá además pelearía con ese ajuste y
    /// produciría el temblor que se quiere evitar.
    ///
    /// ⚠️ El orden −50 es para que la cámara quede quieta ANTES de que la lean
    /// las capas de parallax (−40), los sembradores (−30) y el resto de los
    /// <c>LateUpdate</c>. Con el orden librado a Unity, algunos leían la cámara del
    /// cuadro anterior.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class PixelPerfectFollowCamera : MonoBehaviour
    {
        [Header("Objetivo")]
        [Tooltip("Transform a seguir. Se asigna desde el Inspector; nunca se busca en runtime.")]
        [SerializeField] private Transform target;

        [Tooltip("Corrimiento horizontal en unidades. Positivo adelanta la cámara " +
                 "respecto del objetivo, dejando ver más de lo que viene. " +
                 "1 unidad = 32 px.")]
        [SerializeField] private float horizontalOffset = 2f;

        [Header("Suavizado")]
        [Tooltip("Si está activo, la cámara persigue con inercia en vez de pegarse. " +
                 "Apagalo si notás que arrastra al frenar de golpe al tocar el timbre.")]
        [SerializeField] private bool smoothing = true;

        [Tooltip("Tiempo aproximado que tarda en alcanzar al objetivo, en segundos.")]
        [SerializeField, Range(0.01f, 0.5f)] private float smoothTime = 0.12f;

        private float velocityX;
        private float fixedY;
        private float fixedZ;

        private void Awake()
        {
            fixedY = transform.position.y;
            fixedZ = transform.position.z;

            if (target == null)
            {
                Debug.LogError(
                    $"[PixelPerfectFollowCamera] '{name}' no tiene objetivo asignado.", this);
                enabled = false;
            }
        }

        /// <summary>Cambia el objetivo a seguir. Lo usa el arranque de partida.</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            velocityX = 0f;
        }

        private void LateUpdate()
        {
            float desiredX = target.position.x + horizontalOffset;

            float x = smoothing
                ? Mathf.SmoothDamp(transform.position.x, desiredX, ref velocityX, smoothTime)
                : desiredX;

            transform.position = new Vector3(x, fixedY, fixedZ);
        }
    }
}
