using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// Reparte las dos ventanas a los costados de la puerta.
    ///
    /// Vive aparte de <see cref="HouseInstance"/> porque es la geometría más
    /// delicada de la casa y ya se rompió una vez: es la que decide si la fachada
    /// se lee simétrica o torcida.
    /// </summary>
    public static class HouseWindowLayout
    {
        /// <summary>
        /// Coloca la ventana de tell y la de señal a cada lado de la puerta.
        ///
        /// La separación se mide desde el BORDE DEL CAMINO hasta el BORDE DE LA
        /// VENTANA, igual de los dos lados, y la configurada actúa como TECHO y no
        /// como valor fijo. Con 60 px no entra en ningún terreno (22 + 60 + 50 por
        /// lado supera los 270 px del lote más ancho), así que las ventanas
        /// terminaban siempre clampeadas y, con la puerta corrida por el jitter,
        /// clampeadas distinto a cada lado. Tomando el mínimo, la separación al
        /// camino queda igual de los dos lados y el sobrante se va a los bordes.
        /// </summary>
        public static void Apply(
            HouseGenConfig config, Transform tellWindow, Transform signalWindow,
            float lotWidth, float doorX)
        {
            if (config == null) return;

            float windowWidth = WidthOf(tellWindow);
            float half = lotWidth * 0.5f;
            float pathHalf = config.DoorPathWidth * 0.5f;

            float leftRoom = doorX - pathHalf + half - windowWidth;
            float rightRoom = half - doorX - pathHalf - windowWidth;

            float gap = Mathf.Max(0f, Mathf.Min(config.WindowSeparation,
                Mathf.Min(leftRoom, rightRoom)));

            // La de TELL va del lado por el que llega el jugador, para que el asomo
            // de la cortina entre en el mismo golpe de vista que la puerta.
            float tellSide = config.TellWindowNearApproach ? -1f : 1f;
            Place(tellWindow, doorX, pathHalf + gap, tellSide, windowWidth);
            Place(signalWindow, doorX, pathHalf + gap, -tellSide, windowWidth);
        }

        /// <summary>
        /// Ubica una ventana a <paramref name="clearance"/> del centro de la puerta,
        /// del lado indicado, midiendo hasta su borde más cercano.
        /// </summary>
        private static void Place(
            Transform target, float doorX, float clearance, float side, float windowWidth)
        {
            if (target == null) return;

            // Con pivot BottomLeft el transform marca el borde izquierdo, así que
            // del lado izquierdo hay que restar además el ancho de la ventana.
            float x = side > 0f ? doorX + clearance : doorX - clearance - windowWidth;
            Vector3 local = target.localPosition;
            target.localPosition = new Vector3(x, local.y, local.z);
        }

        private static float WidthOf(Transform window)
        {
            if (window == null) return 0f;
            var renderer = window.GetComponent<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null) return 0f;
            return renderer.sprite.rect.width / renderer.sprite.pixelsPerUnit;
        }
    }
}
