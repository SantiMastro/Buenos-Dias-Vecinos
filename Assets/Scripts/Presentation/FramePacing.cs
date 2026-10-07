using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Fija el ritmo de cuadros apenas arranca el juego, sin tener que agregar nada
    /// a la escena.
    ///
    /// Por qué existe: el proyecto corre en la calidad "Very Low", que trae vSync
    /// apagado y ningún tope de FPS. Con eso el tiempo entre cuadros oscila, y como
    /// la cámara es pixel-perfect (cada movimiento se redondea a píxel entero), esa
    /// oscilación se ve como tirones aunque el juego vaya sobrado de rendimiento.
    ///
    /// En el build usa vSync, que es lo que mejor empareja el ritmo con el monitor.
    /// En el Editor el vSync del Game view es opcional y suele estar apagado, así
    /// que ahí se usa un tope de 60 cuadros por segundo.
    /// </summary>
    public static class FramePacing
    {
        private const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            if (Application.isEditor)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = TargetFrameRate;
                return;
            }

            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
        }
    }
}
