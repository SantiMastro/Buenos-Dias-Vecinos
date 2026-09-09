using BuenosDias.Config;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// Monta las señales de una casa según su modo, sobre slots que ya existen
    /// en el prefab.
    ///
    /// No instancia nada en runtime: el prefab trae dos slots de prop y dos
    /// segmentos de franja, y acá solo se prenden, se apagan y se les cambia el
    /// sprite. Instanciar por casa haría trabajo de GC en cada scroll.
    /// </summary>
    [System.Serializable]
    public sealed class HouseSignalMounter
    {
        [Tooltip("Slots de prop reutilizables. Alcanzan dos: es el máximo de " +
                 "señales por casa.")]
        [SerializeField] private SignalPropSlot[] propSlots;

        [Tooltip("Los dos tramos del pasto, a cada lado del camino de entrada.")]
        [SerializeField] private SpriteRenderer[] stripSegments;

        [Tooltip("Ventana de señal: la que pisan las persianas y el TV.")]
        [SerializeField] private SpriteRenderer signalWindow;

        [Tooltip("Sprite por defecto de la ventana de señal, cuando ninguna señal " +
                 "la reemplaza.")]
        [SerializeField] private Sprite plainWindowSprite;

        /// <summary>
        /// Transform de la ventana de señal, para que <c>HouseInstance</c> la
        /// posicione junto con la de tell.
        /// </summary>
        public Transform SignalWindowTransform =>
            signalWindow != null ? signalWindow.transform : null;

        /// <summary>Apaga todo lo que haya quedado de la casa anterior.</summary>
        public void Clear()
        {
            foreach (SignalPropSlot slot in propSlots) slot.Hide();
            foreach (SpriteRenderer strip in stripSegments) strip.enabled = false;
            if (signalWindow != null) signalWindow.sprite = plainWindowSprite;
        }

        /// <summary>Coloca las señales de una receta ya resuelta.</summary>
        public void Mount(HouseLayout layout, HouseAnchors anchors, HouseGenConfig config)
        {
            Clear();
            int propIndex = 0;

            foreach (PlacedSignal placed in layout.Signals)
            {
                HouseSignalDefinition signal = placed.Definition;

                switch (signal.MountMode)
                {
                    case SignalMountMode.VarianteDeVentana:
                        if (signalWindow != null) signalWindow.sprite = signal.Sprite;
                        break;

                    case SignalMountMode.FranjaTileada:
                        MountStrip(signal, layout, config);
                        break;

                    default:
                        if (propIndex < propSlots.Length)
                        {
                            Vector3 position = anchors.PositionFor(placed, layout, config);
                            propSlots[propIndex++].Show(signal, position);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// El pasto va en DOS tramos, uno a cada lado del camino de entrada.
        /// Una franja entera al ancho del terreno le pasaría por encima al
        /// sendero, y un pasto que tapa el camino se lee como bug, no como
        /// abandono.
        /// </summary>
        private void MountStrip(
            HouseSignalDefinition signal, HouseLayout layout, HouseGenConfig config)
        {
            if (stripSegments.Length < 2 || signal.Sprite == null) return;

            float lotWidth = layout.LotWidthUnits;
            float pathHalf = config.DoorPathWidth * 0.5f;
            float doorCenter = ProjectConstants.ToUnits(layout.DoorOffsetPixels);
            float height = signal.Sprite.rect.height / signal.Sprite.pixelsPerUnit;

            float leftEnd = doorCenter - pathHalf;
            float rightStart = doorCenter + pathHalf;

            SetStrip(stripSegments[0], signal.Sprite, -lotWidth * 0.5f, leftEnd, height);
            SetStrip(stripSegments[1], signal.Sprite, rightStart, lotWidth * 0.5f, height);
        }

        private static void SetStrip(
            SpriteRenderer strip, Sprite sprite, float from, float to, float height)
        {
            float width = to - from;
            if (width <= 0.01f) { strip.enabled = false; return; }

            strip.enabled = true;
            strip.sprite = sprite;
            strip.drawMode = SpriteDrawMode.Tiled;
            strip.size = new Vector2(width, height);

            // El rect tileado se dibuja alrededor del pivot, no desde el borde
            // izquierdo. Hay que corregir por él: los sprites 'env_' importan con
            // pivot BottomLeft pero 'prop_pasto_alto' es 'prop_' y le toca
            // BottomCenter, así que sin esto la franja queda corrida media franja.
            float pivotX = sprite.pivot.x / sprite.rect.width;

            Vector3 local = strip.transform.localPosition;
            strip.transform.localPosition = new Vector3(from + pivotX * width, local.y, local.z);
        }
    }
}
