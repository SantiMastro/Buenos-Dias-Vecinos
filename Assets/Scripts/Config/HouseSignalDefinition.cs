using UnityEngine;

namespace BuenosDias.Config
{
    /// <summary>
    /// Cómo se monta una señal dentro del terreno.
    ///
    /// Existe porque las siete señales de la spec no se montan igual: cuatro de
    /// ellas necesitan algo más que pegar un sprite en un ancla. Sin este enum,
    /// esos cuatro casos terminarían como <c>if</c> especiales adentro de
    /// HouseInstance, que es exactamente lo que el diseño quiere evitar.
    /// </summary>
    public enum SignalMountMode
    {
        /// <summary>Sprite colgado de la pared. Buzón, ropa tendida.</summary>
        PropEnAnclaDePared,

        /// <summary>Sprite apoyado en el suelo del terreno. Auto, farol de entrada.</summary>
        PropEnAnclaDeSuelo,

        /// <summary>
        /// Franja repetida a lo ancho del terreno, con draw mode Tiled.
        /// Pasto crecido: no es un prop, es una superficie.
        /// </summary>
        FranjaTileada,

        /// <summary>
        /// Reemplaza el sprite de la ventana de señal (WindowA).
        /// Persianas bajas y TV, que son los dos pesos más altos del sistema en
        /// cada dirección.
        /// </summary>
        VarianteDeVentana
    }

    /// <summary>
    /// Una señal que modifica la probabilidad de que haya alguien en la casa.
    ///
    /// Es un asset para cumplir el criterio 9: agregar una señal es crear este
    /// asset, sin tocar código. Ninguna clase enumera señales; el generador
    /// recibe la lista que tenga <see cref="HouseGenConfig"/> y
    /// <c>HouseInstance</c> se limita a leer el modo de montaje y aplicarlo.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Signal", menuName = "Buenos Días/Señal de casa", order = 11)]
    public sealed class HouseSignalDefinition : ScriptableObject
    {
        [Header("Identidad")]
        [Tooltip("Nombre legible, solo para el Editor. Ej: Luz de entrada prendida.")]
        [SerializeField] private string displayName = "Señal";

        [Header("Lectura")]
        [Tooltip("Cuánto mueve la probabilidad de que haya alguien.\n" +
                 "POSITIVO = pinta habitada (luz +0.30, TV +0.35, auto +0.25, ropa +0.15).\n" +
                 "NEGATIVO = pinta vacía (persianas −0.35, buzón −0.40, pasto −0.22).\n" +
                 "La probabilidad final se clampea entre 0.05 y 0.95.")]
        [SerializeField, Range(-1f, 1f)] private float weight = 0.3f;

        [Header("Montaje")]
        [Tooltip("Cómo se compone dentro del terreno.\n" +
                 "REGLA: ninguna señal se dibuja fuera del footprint de su casa. " +
                 "Por eso el auto va en la entrada y no en la calle.")]
        [SerializeField] private SignalMountMode mountMode = SignalMountMode.PropEnAnclaDePared;

        [Tooltip("Arte principal de la señal. Con VarianteDeVentana es el sprite " +
                 "que pisa a env_ventana_normal en WindowA.")]
        [SerializeField] private Sprite sprite;

        [Header("Capa de luz (opcional)")]
        [Tooltip("Sprite que se dibuja ENCIMA con blending aditivo: fx_farol_luz " +
                 "para la luz de entrada, fx_ventana_luz para el TV. Vacío en el resto.")]
        [SerializeField] private Sprite additiveLayer;

        [Tooltip("Animación de la capa aditiva. El parpadeo del TV sale de animar " +
                 "el alpha de esta capa, NO de cambiar sprites: env_ventana_tv es " +
                 "un sprite único y no tiene frames propios.")]
        [SerializeField] private AnimationClip additiveLayerAnimation;

        [Header("Colocación")]
        [Tooltip("Solo para PropEnAnclaDePared. Si está activo, esta señal reclama el " +
                 "ancla pegada al marco de la puerta y desplaza a las demás a los " +
                 "anclas laterales.\n" +
                 "Lo usa el farol de entrada: es un aplique montado al lado de la " +
                 "puerta, no un prop que pueda ir en cualquier punto de la fachada.")]
        [SerializeField] private bool claimsDoorSideAnchor;

        [Header("Restricciones")]
        [Tooltip("Ancho mínimo de terreno, en píxeles, para que esta señal pueda " +
                 "aparecer. 0 = sin restricción.\n" +
                 "El auto necesita 216: mide 80 px y el camino de la puerta ocupa " +
                 "44 centrados, así que el lado libre es ancho/2 − 22 y hacen falta " +
                 "86 para que entre con margen.")]
        [SerializeField, Min(0f)] private float minimumLotWidthPixels;

        /// <summary>Si reclama el ancla pegada al marco de la puerta.</summary>
        public bool ClaimsDoorSideAnchor => claimsDoorSideAnchor;

        /// <summary>Nombre legible de la señal.</summary>
        public string DisplayName => displayName;

        /// <summary>Cuánto mueve la probabilidad de ocupación.</summary>
        public float Weight => weight;

        /// <summary>Si empuja hacia "hay alguien".</summary>
        public bool IsPositive => weight > 0f;

        /// <summary>Cómo se monta dentro del terreno.</summary>
        public SignalMountMode MountMode => mountMode;

        /// <summary>Arte principal.</summary>
        public Sprite Sprite => sprite;

        /// <summary>Capa aditiva, o <c>null</c> si esta señal no tiene luz.</summary>
        public Sprite AdditiveLayer => additiveLayer;

        /// <summary>Animación de la capa aditiva, o <c>null</c>.</summary>
        public AnimationClip AdditiveLayerAnimation => additiveLayerAnimation;

        /// <summary>Si esta señal dibuja una capa de luz encima.</summary>
        public bool HasAdditiveLayer => additiveLayer != null;

        /// <summary>Ancho mínimo de terreno en píxeles. 0 = sin restricción.</summary>
        public float MinimumLotWidthPixels => minimumLotWidthPixels;

        /// <summary>Indica si esta señal cabe en un terreno del ancho indicado.</summary>
        public bool FitsLot(float lotWidthPixels)
        {
            return minimumLotWidthPixels <= 0f || lotWidthPixels >= minimumLotWidthPixels;
        }
    }
}
