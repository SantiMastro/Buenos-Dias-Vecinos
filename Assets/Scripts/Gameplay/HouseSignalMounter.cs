using BuenosDias.Config;
using BuenosDias.Presentation;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// Monta las señales de una casa según su modo, sobre slots que ya existen
    /// en el prefab.
    ///
    /// No instancia nada al montar: el prefab trae dos slots de prop y dos
    /// segmentos de franja, y acá solo se prenden, se apagan y se les cambia el
    /// sprite. Instanciar por casa haría trabajo de GC en cada scroll.
    ///
    /// La silueta y la chimenea son la excepción de ORIGEN, no de costo: se
    /// dibujan por código y las arma <see cref="HouseInstance"/> una sola vez por
    /// casa del pool (<see cref="AttachGenerated"/>). De ahí en más se prenden y se
    /// apagan igual que los slots.
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

        private WindowSilhouette silhouette;
        private ChimneySmoke chimney;
        private float roofTopY;
        private float gableHalfWidth;

        /// <summary>
        /// Transform de la ventana de señal, para que <c>HouseInstance</c> la
        /// posicione junto con la de tell.
        /// </summary>
        public Transform SignalWindowTransform =>
            signalWindow != null ? signalWindow.transform : null;

        /// <summary>
        /// Recibe las piezas dibujadas por código y la geometría del techo que
        /// necesita la chimenea. Lo llama la casa una vez, al armarlas.
        /// </summary>
        public void AttachGenerated(
            WindowSilhouette generatedSilhouette, ChimneySmoke generatedChimney,
            float roofTopLocalY, float gableHalfWidthUnits)
        {
            silhouette = generatedSilhouette;
            chimney = generatedChimney;
            roofTopY = roofTopLocalY;
            gableHalfWidth = gableHalfWidthUnits;
        }

        /// <summary>Apaga todo lo que haya quedado de la casa anterior.</summary>
        public void Clear()
        {
            foreach (SignalPropSlot slot in propSlots) slot.Hide();
            foreach (SpriteRenderer strip in stripSegments) strip.enabled = false;
            if (signalWindow != null) signalWindow.sprite = plainWindowSprite;
            if (silhouette != null) silhouette.Hide();
            if (chimney != null) chimney.Hide();
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

                    case SignalMountMode.SiluetaEnVentana:
                        if (silhouette != null) silhouette.Show();
                        break;

                    case SignalMountMode.ChimeneaEnTecho:
                        MountChimney(layout, config);
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

        /// <summary>
        /// La chimenea va sobre la losa del lado más ancho de la casa, a mitad de
        /// camino entre el borde del terreno y lo que ocupa el centro: el techo a
        /// dos aguas si lo hay, el camino de entrada si no. Así no queda encima
        /// del gable ni pegada al borde.
        ///
        /// Con techo a dos aguas completo no hay losa donde apoyarla. El asset
        /// pide un terreno mínimo que deja afuera ese caso, así que acá no debería
        /// llegar; si llega, no se dibuja.
        /// </summary>
        private void MountChimney(HouseLayout layout, HouseGenConfig config)
        {
            if (chimney == null || layout.Roof == RoofStyle.DosAguasCompleto) return;

            float halfLot = layout.LotWidthUnits * 0.5f;
            float doorX = ProjectConstants.ToUnits(layout.DoorOffsetPixels);
            float side = layout.WiderSideIsRight ? 1f : -1f;

            float clearance = layout.Roof == RoofStyle.FrenteDosAguas
                ? gableHalfWidth
                : config.DoorPathWidth;

            float inner = doorX + side * clearance;
            float x = ParallaxLayer.Snap((inner + side * halfLot) * 0.5f, ProjectConstants.PixelsPerUnit);

            chimney.Show(new Vector3(x, roofTopY, 0f));
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
