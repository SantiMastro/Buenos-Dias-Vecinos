using UnityEngine;

namespace BuenosDias.Config
{
    /// <summary>
    /// La caída del sol: color de cielo, luz global y encendido de ventanas.
    ///
    /// ⚠️ La Global Light 2D NO afecta al clear color de la cámara. El cielo es
    /// el <c>backgroundColor</c>, así que el gradiente de acá tiene que
    /// escribirse DIRECTO sobre la cámara. Intentar el atardecer solo con la luz
    /// deja el cielo celeste de mediodía mientras todo lo demás oscurece.
    /// </summary>
    [CreateAssetMenu(
        fileName = "DayCycleConfig", menuName = "Buenos Días/Config/Ciclo de día", order = 26)]
    public sealed class DayCycleConfig : ScriptableObject
    {
        [Header("Cielo")]
        [Tooltip("Color del cielo a lo largo del día. X = 0 es el arranque " +
                 "(mediodía) y X = 1 el final (noche).\n" +
                 "Se escribe sobre Camera.backgroundColor, NO sobre la luz global.")]
        [SerializeField] private Gradient skyGradient = DefaultSky();

        [Header("Luz global")]
        [Tooltip("Color con el que se tiñe el mundo. Mismo eje que el cielo.")]
        [SerializeField] private Gradient lightGradient = DefaultLight();

        [Tooltip("Intensidad de la luz global a lo largo del día.")]
        [SerializeField]
        private AnimationCurve lightIntensity = AnimationCurve.Linear(0f, 1f, 1f, 0.45f);

        [Header("Ventanas")]
        [Tooltip("Fracción del día pasada la cual las ventanas y faroles empiezan " +
                 "a encenderse progresivamente.")]
        [SerializeField, Range(0f, 1f)] private float lightsOnFraction = 0.60f;

        [Header("Presión")]
        [Tooltip("Segundos restantes por debajo de los cuales la barra parpadea en rojo.")]
        [SerializeField, Min(0f)] private float lowTimeWarningSeconds = 12f;

        /// <summary>Fracción del día a la que se empiezan a encender las luces.</summary>
        public float LightsOnFraction => lightsOnFraction;

        /// <summary>Umbral de parpadeo rojo, en segundos restantes.</summary>
        public float LowTimeWarningSeconds => lowTimeWarningSeconds;

        /// <summary>Color de cielo en ese punto del día (0 = mediodía, 1 = noche).</summary>
        public Color SkyColorAt(float dayProgress)
        {
            return skyGradient.Evaluate(Mathf.Clamp01(dayProgress));
        }

        /// <summary>Color de la luz global en ese punto del día.</summary>
        public Color LightColorAt(float dayProgress)
        {
            return lightGradient.Evaluate(Mathf.Clamp01(dayProgress));
        }

        /// <summary>Intensidad de la luz global en ese punto del día.</summary>
        public float LightIntensityAt(float dayProgress)
        {
            return lightIntensity.Evaluate(Mathf.Clamp01(dayProgress));
        }

        /// <summary>
        /// Cuánto están encendidas las ventanas, de 0 a 1. Antes del umbral vale 0
        /// y de ahí en más sube hasta 1 al final del día.
        /// </summary>
        public float WindowLightAt(float dayProgress)
        {
            if (dayProgress <= lightsOnFraction) return 0f;
            return Mathf.InverseLerp(lightsOnFraction, 1f, dayProgress);
        }

        // Los colores salen de la paleta del proyecto: #A8B4C6 mediodía,
        // #7F9BB5 tarde, #4A5D63 crepúsculo, #1D1638 noche.
        private static Gradient DefaultSky()
        {
            return BuildGradient(
                new Color32(0xA8, 0xB4, 0xC6, 0xFF),
                new Color32(0x7F, 0x9B, 0xB5, 0xFF),
                new Color32(0x4A, 0x5D, 0x63, 0xFF),
                new Color32(0x1D, 0x16, 0x38, 0xFF));
        }

        private static Gradient DefaultLight()
        {
            return BuildGradient(
                Color.white,
                new Color32(0xFF, 0xE6, 0xB0, 0xFF),
                new Color32(0xD3, 0xB2, 0xB0, 0xFF),
                new Color32(0x3A, 0x30, 0x50, 0xFF));
        }

        private static Gradient BuildGradient(params Color[] colors)
        {
            var gradient = new Gradient();
            var keys = new GradientColorKey[colors.Length];

            for (int i = 0; i < colors.Length; i++)
                keys[i] = new GradientColorKey(colors[i], i / (float)(colors.Length - 1));

            gradient.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f) });
            return gradient;
        }
    }
}
