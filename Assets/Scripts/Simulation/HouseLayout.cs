using System.Collections.Generic;
using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.Simulation
{
    /// <summary>Una señal ya colocada en un ancla concreta de la casa.</summary>
    public readonly struct PlacedSignal
    {
        /// <summary>Qué señal es.</summary>
        public HouseSignalDefinition Definition { get; }

        /// <summary>Índice del ancla dentro de su modo de montaje.</summary>
        public int SlotIndex { get; }

        /// <summary>Si ocupa el ancla pegada al marco de la puerta.</summary>
        public bool DoorSide { get; }

        /// <summary>Si va del lado derecho del terreno.</summary>
        public bool RightSide { get; }

        /// <summary>Coloca una señal en un ancla.</summary>
        public PlacedSignal(HouseSignalDefinition definition, int slotIndex, bool doorSide, bool rightSide)
        {
            Definition = definition;
            SlotIndex = slotIndex;
            DoorSide = doorSide;
            RightSide = rightSide;
        }
    }

    /// <summary>
    /// La receta de una casa: puros datos, sin nada de Unity que dependa de la
    /// escena.
    ///
    /// Existe separada de <c>HouseInstance</c> para que la generación se pueda
    /// simular miles de veces en milisegundos sin entrar en Play Mode. Es lo que
    /// permite verificar el balance del sistema de señales.
    /// </summary>
    public sealed class HouseLayout
    {
        /// <summary>Ancho del terreno, en píxeles.</summary>
        public float LotWidthPixels { get; }

        /// <summary>Separación hasta la casa siguiente, en unidades.</summary>
        public float GapUnits { get; }

        /// <summary>Pared elegida.</summary>
        public Sprite WallSprite { get; }

        /// <summary>Estilo de techo.</summary>
        public RoofStyle Roof { get; }

        /// <summary>
        /// Corrimiento de la puerta respecto del centro del terreno, en píxeles.
        /// El jitter evita que todas las casas se vean calcadas.
        /// </summary>
        public float DoorOffsetPixels { get; }

        /// <summary>Señales colocadas, con su ancla resuelta.</summary>
        public IReadOnlyList<PlacedSignal> Signals { get; }

        /// <summary>
        /// Probabilidad propia de que haya alguien, según las señales visibles.
        /// Es lo que el jugador puede leer mirando la casa.
        /// </summary>
        public float Chance { get; }

        /// <summary>Arma una receta de casa ya resuelta.</summary>
        public HouseLayout(
            float lotWidthPixels, float gapUnits, Sprite wallSprite, RoofStyle roof,
            float doorOffsetPixels, IReadOnlyList<PlacedSignal> signals, float chance)
        {
            LotWidthPixels = lotWidthPixels;
            GapUnits = gapUnits;
            WallSprite = wallSprite;
            Roof = roof;
            DoorOffsetPixels = doorOffsetPixels;
            Signals = signals;
            Chance = chance;
        }

        /// <summary>Ancho del terreno en unidades de mundo.</summary>
        public float LotWidthUnits => ProjectConstants.ToUnits(LotWidthPixels);

        /// <summary>Cuántas señales positivas lleva.</summary>
        public int PositiveSignalCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Signals.Count; i++)
                    if (Signals[i].Definition.IsPositive) count++;
                return count;
            }
        }

        /// <summary>
        /// Lado del terreno con más espacio libre a un costado del camino.
        /// El auto se coloca acá para que el jitter de la puerta no lo arruine.
        /// </summary>
        public bool WiderSideIsRight => DoorOffsetPixels < 0f;
    }
}
