using System.Collections.Generic;
using UnityEngine;

namespace BuenosDias.Config
{
    /// <summary>
    /// Un final: qué dice, sobre qué fondo y con qué piezas se arma.
    ///
    /// Es un asset por el mismo motivo que las religiones y las señales: agregar o
    /// retocar un final tiene que ser tocar datos, no código. Ninguna clase enumera
    /// finales; el catálogo vive en el componente que los reproduce.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Final", menuName = "Buenos Días/Final", order = 40)]
    public sealed class EndingDefinition : ScriptableObject
    {
        [Header("Cuál")]
        [Tooltip("Qué final de los tres es. Lo resuelve GameConfig.ResolveEnding.")]
        [SerializeField] private EndingKind kind = EndingKind.SeHizoDeNoche;

        [Header("Texto")]
        [Tooltip("Titular, en mayúsculas. Va en ×2: es un cartel corto y tiene que pegar.")]
        [SerializeField] private string title = "SE HIZO DE NOCHE";

        [Tooltip("Segunda línea, en ×1. '{0}' es la cantidad de conversiones. " +
                 "Vacío = no se dibuja.")]
        [SerializeField] private string subtitleFormat = string.Empty;

        [Tooltip("Igual que la anterior pero cuando hubo UNA sola conversión, para " +
                 "no escribir '1 ALMAS'.")]
        [SerializeField] private string subtitleSingular = string.Empty;

        [Header("Fondo")]
        [Tooltip("Si tapa el mundo con un color plano. Apagado deja ver la calle, " +
                 "que para el final de anochecer YA es la imagen que corresponde: " +
                 "el ciclo de día la dejó de noche y con las ventanas prendidas.")]
        [SerializeField] private bool hideWorld = true;

        [Tooltip("Color del fondo cuando tapa el mundo.")]
        [SerializeField] private Color backdrop = new Color32(0x1D, 0x16, 0x38, 0xFF);

        [Header("Piezas")]
        [Tooltip("De atrás hacia adelante según su orden de dibujo.")]
        [SerializeField] private List<EndingLayer> layers = new List<EndingLayer>();

        /// <summary>Qué final es.</summary>
        public EndingKind Kind => kind;

        /// <summary>Titular.</summary>
        public string Title => title;

        /// <summary>Si tapa el mundo.</summary>
        public bool HideWorld => hideWorld;

        /// <summary>Color del fondo.</summary>
        public Color Backdrop => backdrop;

        /// <summary>Piezas de la cinemática.</summary>
        public IReadOnlyList<EndingLayer> Layers => layers;

        /// <summary>
        /// Segunda línea ya armada con las conversiones. Cadena vacía si este final
        /// no lleva.
        /// </summary>
        public string SubtitleFor(int converts)
        {
            string format = converts == 1 && !string.IsNullOrEmpty(subtitleSingular)
                ? subtitleSingular
                : subtitleFormat;

            return string.IsNullOrEmpty(format) ? string.Empty : string.Format(format, converts);
        }
    }
}
