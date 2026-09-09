using System.Collections.Generic;
using BuenosDias.Config;
using BuenosDias.Gameplay;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Le pone al predicador los colores de la religión apuntada.
    ///
    /// Es la única respuesta que da la pantalla de selección, así que no es
    /// decoración: mientras no haya nombres en pantalla, **el color ES la opción**.
    ///
    /// Los colores de origen —los que trae el PNG— son campos del Inspector y no
    /// constantes: son los del sprite, y el día que se redibuje al predicador con
    /// otra camisa, esto tiene que poder seguirlo sin abrir un <c>.cs</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReligionPreviewView : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Quien elige. Se lee y se escucha; nunca se le pide nada.")]
        [SerializeField] private ReligionSelector selector;

        [Tooltip("El intercambiador de paleta del predicador.")]
        [SerializeField] private PaletteSwapper swapper;

        [Header("Colores del sprite original")]
        [Tooltip("Color de la camisa tal como viene en el PNG del predicador.")]
        [SerializeField] private Color baseShirt = new Color32(0xF3, 0xEC, 0xE0, 0xFF);

        [Tooltip("Color de la corbata tal como viene en el PNG del predicador.")]
        [SerializeField] private Color baseTie = new Color32(0x7A, 0x2F, 0x3D, 0xFF);

        private readonly List<PaletteSwapper.ColorSwap> swaps =
            new List<PaletteSwapper.ColorSwap>(2);

        private void OnEnable()
        {
            if (selector != null) selector.Changed += Apply;
        }

        private void OnDisable()
        {
            if (selector != null) selector.Changed -= Apply;
        }

        /// <summary>
        /// La primera pintada va en Start y no en OnEnable: el selector arma su
        /// catálogo en Awake, y el orden de Awake entre dos GameObjects no está
        /// definido. Unity sí garantiza que todos los Awake corren antes que
        /// cualquier Start, así que acá la religión inicial ya existe.
        /// </summary>
        private void Start()
        {
            if (!ValidateSetup()) { enabled = false; return; }
            Apply(selector.Selected);
        }

        private void Apply(ReligionDefinition religion)
        {
            if (religion == null || swapper == null) return;

            swaps.Clear();
            swaps.Add(new PaletteSwapper.ColorSwap
            {
                from = baseShirt, to = religion.ShirtColor
            });
            swaps.Add(new PaletteSwapper.ColorSwap
            {
                from = baseTie, to = religion.TieColor
            });

            swapper.ApplyPalette(swaps);
        }

        private bool ValidateSetup()
        {
            if (selector != null && swapper != null) return true;

            Debug.LogError(
                $"[ReligionPreviewView] '{name}' tiene referencias sin asignar " +
                "(selector o PaletteSwapper).", this);
            return false;
        }
    }
}
