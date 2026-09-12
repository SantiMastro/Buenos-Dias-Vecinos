using System.Collections.Generic;
using UnityEngine;

namespace BuenosDias.Config
{
    /// <summary>Cómo se resuelve el techo de una casa.</summary>
    public enum RoofStyle
    {
        /// <summary>Solo losa tileada en todo el ancho.</summary>
        LosaCompleta,

        /// <summary>
        /// Gable de 128 px a TAMAÑO NATIVO centrado sobre la puerta, con losa
        /// tileada a los costados. Es cómo se ven las casas del conurbano y
        /// evita estirar el techo a dos aguas, que deforma los aleros.
        /// </summary>
        FrenteDosAguas,

        /// <summary>Dos aguas en todo el ancho. Solo para casas de 128 px o menos.</summary>
        DosAguasCompleto
    }

    /// <summary>
    /// Generación procedural de la cuadra: medidas del terreno, módulos
    /// disponibles y dónde se monta cada cosa.
    /// </summary>
    [CreateAssetMenu(
        fileName = "HouseGenConfig", menuName = "Buenos Días/Config/Generación de casas", order = 27)]
    public sealed class HouseGenConfig : ScriptableObject
    {
        [Header("Terreno")]
        [Tooltip("Ancho mínimo del terreno, en píxeles.")]
        [SerializeField, Min(32f)] private float lotWidthMinPixels = 190f;

        [Tooltip("Ancho máximo del terreno, en píxeles.")]
        [SerializeField, Min(32f)] private float lotWidthMaxPixels = 270f;

        [Tooltip("Separación mínima entre casas, en píxeles.")]
        [SerializeField, Min(0f)] private float gapMinPixels = 85f;

        [Tooltip("Separación máxima entre casas, en píxeles.")]
        [SerializeField, Min(0f)] private float gapMaxPixels = 175f;

        [Header("Módulos")]
        [Tooltip("Paredes posibles. Se sortea una por casa.")]
        [SerializeField] private List<Sprite> wallSprites = new List<Sprite>();

        [Tooltip("Ancho, en píxeles, por debajo del cual una casa puede llevar el " +
                 "techo a dos aguas completo. Es el ancho nativo del sprite: por " +
                 "encima de esto habría que estirarlo, y en pixel art no se estira.")]
        [SerializeField, Min(32f)] private float fullGableMaxWidthPixels = 128f;

        [Tooltip("Si en el frente a dos aguas el gable se apoya ENCIMA de la losa " +
                 "(prendido) o arranca en el tope de la pared (apagado).\n\n" +
                 "Apagado, la losa se veía por detrás de las pendientes del gable y el " +
                 "techo rojo quedaba hundido 28 px debajo del borde de la terraza, " +
                 "como un alero de porche. Con techo a dos aguas completo no hay losa, " +
                 "así que ahí el gable siempre va sobre la pared.")]
        [SerializeField] private bool gableSitsOnSlab = true;

        [Header("Ventanas")]
        [Tooltip("Distancia desde el BORDE SUPERIOR de la pared hasta el tope de la " +
                 "ventana, en píxeles. 4 deja alféizar visible abajo; centrarla la " +
                 "dejaría flotando.")]
        [SerializeField, Min(0f)] private float windowTopMarginPixels = 4f;

        [Tooltip("Separación horizontal entre la ventana de señal (WindowA) y la de " +
                 "tell (WindowB), en píxeles.")]
        [SerializeField, Min(0f)] private float windowSeparationPixels = 60f;

        [Header("Ventana de tell")]
        [Tooltip("Corrimiento X de la cortina respecto del ORIGEN de la ventana " +
                 "(esquina inferior izquierda), en píxeles.\n" +
                 "OJO: la cortina importa con pivot Center y la ventana con " +
                 "BottomLeft, así que al componer hay que sumarle media cortina.")]
        [SerializeField] private float curtainOffsetXPixels = 4f;

        [Tooltip("Corrimiento Y de la cortina respecto del origen de la ventana.")]
        [SerializeField] private float curtainOffsetYPixels = 2f;

        [Tooltip("Si WindowB (la de tell) va del lado de la puerta más cercano al " +
                 "jugador. Se quiere en true: el asomo tiene que entrar en el mismo " +
                 "golpe de vista que la puerta.")]
        [SerializeField] private bool tellWindowNearApproach = true;

        [Header("Camino de entrada")]
        [Tooltip("Ancho del camino que va del portón a la puerta, en píxeles, " +
                 "centrado en la puerta.\n" +
                 "Manda en dos cosas: el pasto se parte para no taparlo, y el auto " +
                 "necesita que el lado libre (ancho/2 − mitad del camino) le alcance.")]
        [SerializeField, Min(0f)] private float doorPathWidthPixels = 44f;

        [Header("Señales")]
        [Tooltip("Máximo de señales por casa.")]
        [SerializeField, Range(0, 4)] private int maximumSignals = 2;

        [Header("Reglas de exclusión")]
        [Tooltip("Cuántas señales pueden pisar el sprite de WindowA.\n" +
                 "Tiene que ser 1: persianas bajas y TV parpadeando compiten por la " +
                 "misma ventana. Con las dos, una pisaría a la otra en silencio, y " +
                 "además es absurdo — la tele prendida detrás de la persiana baja.")]
        [SerializeField, Range(0, 2)] private int maximumWindowVariants = 1;

        [Tooltip("Anclas disponibles en la pared. Con 2 entran buzón y ropa tendida " +
                 "a la vez, cada uno en el suyo.")]
        [SerializeField, Range(0, 4)] private int wallAnchorCount = 2;

        [Tooltip("Anclas disponibles en el suelo del terreno.")]
        [SerializeField, Range(0, 4)] private int groundAnchorCount = 1;

        [Tooltip("Cuántas franjas tileadas pueden convivir. El pasto es una " +
                 "superficie: dos superpuestas no se leerían.")]
        [SerializeField, Range(0, 2)] private int maximumTiledStrips = 1;

        [Tooltip("Cuántas siluetas puede llevar una casa. Hay una sola ventana de " +
                 "tell, así que el techo es 1.")]
        [SerializeField, Range(0, 1)] private int silhouetteCapacity = 1;

        [Tooltip("Cuántas chimeneas puede llevar una casa.")]
        [SerializeField, Range(0, 1)] private int chimneyCapacity = 1;

        [Tooltip("Cómo se ven la silueta y el humo, que se dibujan por código. Son " +
                 "números de lectura, no de balance. Vacío = valores por defecto.")]
        [SerializeField] private SignalVisualsConfig signalVisuals;

        [Tooltip("Catálogo de señales disponibles. El generador NO enumera señales " +
                 "en código: usa esta lista, así que agregar una es crear el asset " +
                 "y sumarlo acá.")]
        [SerializeField] private List<HouseSignalDefinition> availableSignals = new List<HouseSignalDefinition>();

        /// <summary>Ancho mínimo del terreno, en píxeles.</summary>
        public float LotWidthMinPixels => lotWidthMinPixels;

        /// <summary>Ancho máximo del terreno, en píxeles.</summary>
        public float LotWidthMaxPixels => lotWidthMaxPixels;

        /// <summary>Paredes disponibles.</summary>
        public IReadOnlyList<Sprite> WallSprites => wallSprites;

        /// <summary>Catálogo de señales.</summary>
        public IReadOnlyList<HouseSignalDefinition> AvailableSignals => availableSignals;

        /// <summary>Máximo de señales por casa.</summary>
        public int MaximumSignals => maximumSignals;

        /// <summary>Si el gable del frente a dos aguas se apoya sobre la losa.</summary>
        public bool GableSitsOnSlab => gableSitsOnSlab;

        /// <summary>Cómo se ven las señales dibujadas por código. Nunca null.</summary>
        public SignalVisualsConfig SignalVisuals => SignalVisualsConfig.OrDefault(signalVisuals);

        /// <summary>Ancho del camino de entrada, en unidades.</summary>
        public float DoorPathWidth => ProjectConstants.ToUnits(doorPathWidthPixels);

        /// <summary>Ancho del camino de entrada, en píxeles.</summary>
        public float DoorPathWidthPixels => doorPathWidthPixels;

        /// <summary>
        /// Cuántas señales de ese modo de montaje entran en una casa. Es la tabla
        /// de exclusión: vive acá y no en el generador, así cambiar la regla es
        /// editar el asset.
        /// </summary>
        public int CapacityFor(SignalMountMode mode)
        {
            return mode switch
            {
                SignalMountMode.VarianteDeVentana => maximumWindowVariants,
                SignalMountMode.PropEnAnclaDePared => wallAnchorCount,
                SignalMountMode.PropEnAnclaDeSuelo => groundAnchorCount,
                SignalMountMode.FranjaTileada => maximumTiledStrips,
                SignalMountMode.SiluetaEnVentana => silhouetteCapacity,
                SignalMountMode.ChimeneaEnTecho => chimneyCapacity,
                _ => 0
            };
        }

        /// <summary>Separación entre ventanas, en unidades.</summary>
        public float WindowSeparation => ProjectConstants.ToUnits(windowSeparationPixels);

        /// <summary>Si la ventana de tell va del lado por el que llega el jugador.</summary>
        public bool TellWindowNearApproach => tellWindowNearApproach;

        /// <summary>
        /// Y local de la ventana dentro de la pared, en unidades, con pivot
        /// BottomLeft. Sale de anclar el tope de la ventana bajo el borde de la pared.
        /// </summary>
        public float WindowLocalY(float wallHeightPixels, float windowHeightPixels)
        {
            float top = wallHeightPixels - windowTopMarginPixels;
            return ProjectConstants.ToUnits(top - windowHeightPixels);
        }

        /// <summary>
        /// Posición local de la cortina respecto del origen de la ventana, ya
        /// corregida por el pivot Center de la cortina.
        /// </summary>
        public Vector2 CurtainLocalPosition(Vector2 curtainSizePixels)
        {
            float x = curtainOffsetXPixels + curtainSizePixels.x * 0.5f;
            float y = curtainOffsetYPixels + curtainSizePixels.y * 0.5f;
            return new Vector2(ProjectConstants.ToUnits(x), ProjectConstants.ToUnits(y));
        }

        /// <summary>Sortea un ancho de terreno, en píxeles.</summary>
        public float RollLotWidth(System.Random random)
        {
            return Mathf.Lerp(lotWidthMinPixels, lotWidthMaxPixels, (float)random.NextDouble());
        }

        /// <summary>Sortea una separación entre casas, en unidades.</summary>
        public float RollGap(System.Random random)
        {
            float pixels = Mathf.Lerp(gapMinPixels, gapMaxPixels, (float)random.NextDouble());
            return ProjectConstants.ToUnits(pixels);
        }

        /// <summary>Estilo de techo posible para ese ancho de terreno.</summary>
        public RoofStyle RoofStyleFor(float lotWidthPixels, System.Random random)
        {
            if (lotWidthPixels <= fullGableMaxWidthPixels) return RoofStyle.DosAguasCompleto;
            return random.NextDouble() < 0.5 ? RoofStyle.FrenteDosAguas : RoofStyle.LosaCompleta;
        }
    }
}
