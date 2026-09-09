using BuenosDias.Config;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// Resuelve dónde va cada señal dentro del terreno.
    ///
    /// Las posiciones se calculan a partir del ancho del terreno y del
    /// corrimiento de la puerta, no de transforms puestos a mano: el ancho
    /// cambia casa por casa, así que anclas fijas quedarían descolocadas.
    /// </summary>
    [System.Serializable]
    public sealed class HouseAnchors
    {
        [Header("Altura")]
        [Tooltip("Altura de los props de pared, en píxeles desde la base de la " +
                 "pared. 34 deja el farol a la altura de la cabeza.")]
        [SerializeField, Min(0f)] private float wallPropHeightPixels = 34f;

        [Tooltip("Altura de los props de suelo. 0 los apoya en la línea de caminata.")]
        [SerializeField] private float groundPropHeightPixels;

        [Header("Separación")]
        [Tooltip("A qué distancia del borde del marco de la puerta se cuelga el " +
                 "prop que reclama ese ancla, en píxeles.")]
        [SerializeField, Min(0f)] private float doorSideOffsetPixels = 26f;

        [Tooltip("Aire entre el borde del terreno y el prop lateral, en píxeles.\n" +
                 "Es MARGEN, no posición: el ancho del prop se descuenta aparte a " +
                 "partir de su sprite. Un margen fijo dejaba al auto de 80 px " +
                 "asomando 10 px fuera de la reja.")]
        [SerializeField, Min(0f)] private float lateralMarginPixels = 4f;

        /// <summary>Posición local de una señal ya colocada.</summary>
        public Vector3 PositionFor(PlacedSignal placed, HouseLayout layout, HouseGenConfig config)
        {
            bool onWall = placed.Definition.MountMode == SignalMountMode.PropEnAnclaDePared;
            float y = ProjectConstants.ToUnits(
                onWall ? wallPropHeightPixels : groundPropHeightPixels);

            return new Vector3(HorizontalFor(placed, layout, config), y, 0f);
        }

        private float HorizontalFor(PlacedSignal placed, HouseLayout layout, HouseGenConfig config)
        {
            float doorCenter = ProjectConstants.ToUnits(layout.DoorOffsetPixels);

            if (placed.DoorSide)
            {
                // Pegado al marco de la puerta, del lado que tenga más pared.
                float side = layout.WiderSideIsRight ? 1f : -1f;
                return doorCenter + side * ProjectConstants.ToUnits(doorSideOffsetPixels);
            }

            // Contra el borde del terreno, descontando SU PROPIO medio ancho: con
            // un margen fijo el auto de 80 px asomaba fuera de la reja.
            float halfLot = layout.LotWidthUnits * 0.5f;
            float halfSprite = HalfWidthOf(placed.Definition);
            float margin = ProjectConstants.ToUnits(lateralMarginPixels);
            float outer = Mathf.Max(0f, halfLot - halfSprite - margin);

            // Los props de PARED cuelgan a la altura de la cabeza: el camino de
            // entrada no los estorba y no hay que apartarlos de él.
            if (placed.Definition.MountMode == SignalMountMode.PropEnAnclaDePared)
                return placed.RightSide ? outer : -outer;

            return GroundHorizontal(placed, layout, config, outer, halfSprite);
        }

        /// <summary>
        /// Los props de suelo sí tienen que dejar libre el camino de entrada.
        /// Se prueba el lado preferido y, si ahí no entran, el otro; nunca se los
        /// empuja fuera del terreno para hacerles lugar.
        /// </summary>
        private static float GroundHorizontal(
            PlacedSignal placed, HouseLayout layout, HouseGenConfig config,
            float outer, float halfSprite)
        {
            float doorCenter = ProjectConstants.ToUnits(layout.DoorOffsetPixels);
            float clearance = config.DoorPathWidth * 0.5f + halfSprite;

            float preferred = placed.RightSide ? outer : -outer;
            if (Mathf.Abs(preferred - doorCenter) >= clearance) return preferred;

            float other = -preferred;
            if (Mathf.Abs(other - doorCenter) >= clearance) return other;

            // No entra de ningún lado: se lo deja contra el borde más lejano a la
            // puerta. Con minimumLotWidthPixels bien puesto no debería pasar.
            return doorCenter > 0f ? -outer : outer;
        }

        /// <summary>
        /// Medio ancho del sprite en unidades. Los props importan con pivot
        /// BottomCenter, así que el sprite se extiende media anchura a cada lado
        /// del transform.
        /// </summary>
        private static float HalfWidthOf(HouseSignalDefinition signal)
        {
            Sprite sprite = signal.Sprite;
            if (sprite == null) return 0f;
            return sprite.rect.width * 0.5f / sprite.pixelsPerUnit;
        }
    }
}
