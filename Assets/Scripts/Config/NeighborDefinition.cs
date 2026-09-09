using UnityEngine;

namespace BuenosDias.Config
{
    /// <summary>
    /// Un tipo de vecino: su animación y las réplicas con las que objeta.
    ///
    /// Como las religiones y las señales, es un asset: sumar un sexto vecino es
    /// tirar los PNG, reconstruir animaciones y crear este asset.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Neighbor", menuName = "Buenos Días/Vecino", order = 12)]
    public sealed class NeighborDefinition : ScriptableObject
    {
        [Header("Identidad")]
        [Tooltip("Nombre legible, solo para el Editor. Ej: Abuela.")]
        [SerializeField] private string displayName = "Vecino";

        [Header("Animación")]
        [Tooltip("Override que reemplaza los clips del Vecino.controller base. " +
                 "Lo genera la fase 2 en Assets/Animations/Vecinos.")]
        [SerializeField] private AnimatorOverrideController animatorOverride;

        [Header("Objeciones")]
        [Tooltip("Réplicas que dice al encadenar objeciones, una por eslabón.\n" +
                 "Si la cadena es más larga que esta lista, se cicla desde el principio.")]
        [SerializeField, TextArea(1, 3)]
        private string[] objections =
        {
            "No me interesa, gracias.",
            "Estoy por salir.",
            "Ya tengo mi religión.",
            "Mi marido no está."
        };

        /// <summary>Nombre legible del tipo de vecino.</summary>
        public string DisplayName => displayName;

        /// <summary>Override de animación de este tipo.</summary>
        public AnimatorOverrideController AnimatorOverride => animatorOverride;

        /// <summary>Cantidad de réplicas distintas cargadas.</summary>
        public int ObjectionCount => objections?.Length ?? 0;

        /// <summary>
        /// Réplica para el eslabón indicado. Cicla si la cadena es más larga que
        /// la lista, así una cadena de 4 nunca se queda sin texto que mostrar.
        /// </summary>
        public string ObjectionFor(int linkIndex)
        {
            if (ObjectionCount == 0) return string.Empty;
            return objections[((linkIndex % ObjectionCount) + ObjectionCount) % ObjectionCount];
        }
    }
}
