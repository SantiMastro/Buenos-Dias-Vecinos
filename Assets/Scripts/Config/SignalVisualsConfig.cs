using UnityEngine;

namespace BuenosDias.Config
{
    /// <summary>
    /// Cómo se ven las señales que se dibujan por código: la silueta de la
    /// ventana y el humo de la chimenea.
    ///
    /// Son números de LECTURA, no de balance: cuánto se nota una pista, no cuánto
    /// vale. El peso de cada señal sigue en su propio asset. Viven acá para poder
    /// tunearlos mirando el juego en Play, y que el ajuste no se pierda al salir.
    ///
    /// Se leen cada cuadro, así que un cambio en Play se ve en el acto.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SignalVisualsConfig", menuName = "Buenos Días/Config/Señales dibujadas", order = 28)]
    public sealed class SignalVisualsConfig : ScriptableObject
    {
        [Header("Silueta en la ventana")]
        [Tooltip("Segundos que tarda la sombra en cruzar la ventana.")]
        [SerializeField, Range(0.3f, 5f)] private float crossSeconds = 1.8f;

        [Tooltip("Pausa MÍNIMA entre dos cruces, en segundos.")]
        [SerializeField, Range(0f, 10f)] private float minPauseSeconds = 1.2f;

        [Tooltip("Pausa MÁXIMA entre dos cruces, en segundos. Más larga = una pista " +
                 "que hay que estar mirando para verla.")]
        [SerializeField, Range(0f, 10f)] private float maxPauseSeconds = 3.5f;

        [Tooltip("Opacidad de la sombra en el medio del cruce. Más alta se lee " +
                 "mejor de lejos, pero pasada 0.7 deja de parecer una sombra detrás " +
                 "de la cortina y parece un muñeco pegado al vidrio.")]
        [SerializeField, Range(0f, 1f)] private float silhouetteMaxAlpha = 0.55f;

        [Tooltip("Altura de los pies de la sombra sobre el borde de abajo de la " +
                 "ventana, en píxeles.")]
        [SerializeField, Range(0f, 30f)] private float sillPixels = 6f;

        [Tooltip("Aire entre la sombra y el marco de la ventana, en píxeles.")]
        [SerializeField, Range(0f, 15f)] private float frameMarginPixels = 4f;

        [Header("Humo de chimenea")]
        [Tooltip("Cada cuántos segundos sale una bocanada.")]
        [SerializeField, Range(0.1f, 3f)] private float puffEverySeconds = 0.75f;

        [Tooltip("Segundos que vive cada bocanada antes de desaparecer.")]
        [SerializeField, Range(0.3f, 6f)] private float puffLifeSeconds = 2.6f;

        [Tooltip("Cuánto sube una bocanada en toda su vida, en píxeles. El cielo " +
                 "mide 40: más que eso y el humo se sale del cuadro.")]
        [SerializeField, Range(0f, 60f)] private float puffRisePixels = 24f;

        [Tooltip("Cuánto la corre el viento en toda su vida, en píxeles.")]
        [SerializeField, Range(0f, 30f)] private float puffDriftPixels = 7f;

        [Tooltip("Opacidad de la bocanada al salir.")]
        [SerializeField, Range(0f, 1f)] private float smokeMaxAlpha = 0.8f;

        [Header("Fundidos")]
        [Tooltip("Escalones de los fundidos. Pocos escalones se leen como parte del " +
                 "pixel art; muchos vuelven a ser un degradado pegado encima.")]
        [SerializeField, Range(1, 16)] private int fadeSteps = 4;

        [Tooltip("Qué parte del cruce de la silueta se usa para aparecer y para " +
                 "irse, en cada punta.")]
        [SerializeField, Range(0.01f, 0.5f)] private float fadeFraction = 0.2f;

        private static SignalVisualsConfig defaults;

        /// <summary>
        /// La config pedida o, si falta, una con los valores por defecto. Sin esto,
        /// una casa sin asset asignado no dibujaría sus señales, que es un error
        /// que se ve como "esta casa no tiene pistas".
        /// </summary>
        public static SignalVisualsConfig OrDefault(SignalVisualsConfig config)
        {
            if (config != null) return config;

            if (defaults == null)
            {
                defaults = CreateInstance<SignalVisualsConfig>();
                defaults.hideFlags = HideFlags.HideAndDontSave;
            }

            return defaults;
        }

        private void OnValidate()
        {
            if (maxPauseSeconds < minPauseSeconds) maxPauseSeconds = minPauseSeconds;
        }

        /// <summary>Segundos que tarda la sombra en cruzar.</summary>
        public float CrossSeconds => crossSeconds;

        /// <summary>Pausa mínima entre cruces.</summary>
        public float MinPauseSeconds => minPauseSeconds;

        /// <summary>Pausa máxima entre cruces.</summary>
        public float MaxPauseSeconds => maxPauseSeconds;

        /// <summary>Opacidad máxima de la sombra.</summary>
        public float SilhouetteMaxAlpha => silhouetteMaxAlpha;

        /// <summary>Altura de la sombra sobre el alféizar, en píxeles.</summary>
        public float SillPixels => sillPixels;

        /// <summary>Aire contra el marco, en píxeles.</summary>
        public float FrameMarginPixels => frameMarginPixels;

        /// <summary>Segundos entre bocanadas.</summary>
        public float PuffEverySeconds => puffEverySeconds;

        /// <summary>Vida de una bocanada, en segundos.</summary>
        public float PuffLifeSeconds => puffLifeSeconds;

        /// <summary>Cuánto sube una bocanada, en píxeles.</summary>
        public float PuffRisePixels => puffRisePixels;

        /// <summary>Cuánto la corre el viento, en píxeles.</summary>
        public float PuffDriftPixels => puffDriftPixels;

        /// <summary>Opacidad inicial de una bocanada.</summary>
        public float SmokeMaxAlpha => smokeMaxAlpha;

        /// <summary>Escalones de los fundidos.</summary>
        public int FadeSteps => fadeSteps;

        /// <summary>Fracción del cruce usada para aparecer e irse.</summary>
        public float FadeFraction => fadeFraction;

        /// <summary>Redondea una opacidad a los escalones configurados.</summary>
        public float Step(float alpha)
        {
            return Mathf.Round(Mathf.Clamp01(alpha) * fadeSteps) / fadeSteps;
        }
    }
}
