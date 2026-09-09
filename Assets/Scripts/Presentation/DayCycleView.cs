using BuenosDias.Config;
using BuenosDias.Gameplay;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Baja el sol: pinta el cielo y tiñe el mundo según lo que diga el reloj.
    ///
    /// ⚠️ El cielo va sobre <c>Camera.backgroundColor</c> y NO sobre la luz
    /// global. La <c>Light2D</c> global no toca el clear color de la cámara:
    /// intentar el atardecer solo con la luz deja el cielo celeste de mediodía
    /// mientras todo lo demás oscurece. Está en §3.8 del plan y es el error que
    /// esta clase existe para no cometer.
    ///
    /// Lee <see cref="DayDirector.SunsetProgress"/>, que nunca baja, y no el
    /// progreso instantáneo: si usara el instantáneo, convertir aclararía el cielo
    /// y se leería como un bug.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DayCycleView : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("De dónde sale la hora del día. Se lee; nunca se le pide nada.")]
        [SerializeField] private DayDirector director;

        [Tooltip("Asset raíz de balance. De acá salen los gradientes del ciclo.")]
        [SerializeField] private GameConfig gameConfig;

        [Tooltip("Cámara cuyo color de fondo ES el cielo. Se asigna por Inspector; " +
                 "nunca se busca en runtime.")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Luz global 2D que tiñe el mundo.")]
        [SerializeField] private Light2D globalLight;

        private DayCycleConfig cycle;

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }
            cycle = gameConfig.DayCycle;
        }

        /// <summary>
        /// Se pinta en LateUpdate para leer el reloj que ya avanzó este cuadro. Al
        /// revés, el cielo iría siempre un cuadro atrasado respecto de la barra.
        /// </summary>
        private void LateUpdate()
        {
            float progress = director.SunsetProgress;

            targetCamera.backgroundColor = cycle.SkyColorAt(progress);
            globalLight.color = cycle.LightColorAt(progress);
            globalLight.intensity = cycle.LightIntensityAt(progress);
        }

        private bool ValidateSetup()
        {
            if (director == null || gameConfig == null
                || targetCamera == null || globalLight == null)
            {
                Debug.LogError(
                    $"[DayCycleView] '{name}' tiene referencias sin asignar " +
                    "(director, GameConfig, cámara o luz global).", this);
                return false;
            }

            if (gameConfig.DayCycle == null)
            {
                Debug.LogError("[DayCycleView] El GameConfig no tiene DayCycleConfig.", this);
                return false;
            }

            return true;
        }
    }
}
