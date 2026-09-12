using BuenosDias.Config;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// Una casa en la escena. No decide nada: recibe una
    /// <see cref="HouseLayout"/> ya resuelta y la aplica.
    ///
    /// Toda la aleatoriedad vive en <c>HouseLayoutGenerator</c>, que es una
    /// clase plana y por eso se puede simular. Si la casa sorteara sus propias
    /// cosas, verificar el balance exigiría entrar en Play Mode.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HouseInstance : MonoBehaviour
    {
        [Header("Estructura")]
        [Tooltip("Pared, en modo Tiled: se estira al ancho del terreno.")]
        [SerializeField] private SpriteRenderer wall;

        [Tooltip("Losa del techo, tileada.")]
        [SerializeField] private SpriteRenderer roofSlab;

        [Tooltip("Techo a dos aguas. Va SIEMPRE a tamaño nativo de 128 px, " +
                 "centrado sobre la puerta: estirarlo deformaría los aleros.")]
        [SerializeField] private SpriteRenderer roofGable;

        [Tooltip("Reja del frente, tileada.")]
        [SerializeField] private SpriteRenderer fence;

        [Header("Puerta y ventanas")]
        [Tooltip("Puerta. Se corre con el jitter del terreno.")]
        [SerializeField] private Transform door;

        [Tooltip("Renderer de la HOJA. Es hijo de la puerta y no la puerta misma: " +
                 "del transform de la puerta cuelgan también el timbre, el portón y " +
                 "el ancla del vecino, que no cambian cuando se abre.")]
        [SerializeField] private SpriteRenderer doorLeaf;

        [Tooltip("Hoja abierta, con el interior oscuro. Se CAMBIA el sprite en vez " +
                 "de encender un segundo objeto: la hoja abierta ocupa exactamente " +
                 "el mismo lugar que la cerrada.")]
        [SerializeField] private Sprite openDoorSprite;

        [Tooltip("Ventana de TELL: lleva cortina en TODAS las casas, ocupadas o " +
                 "no. Que la cortina esté siempre es lo que hace que no delate nada.")]
        [SerializeField] private Transform tellWindow;

        [Tooltip("Animator de la cortina. El asomo es un disparo, no un loop.")]
        [SerializeField] private Animator curtainAnimator;

        [Tooltip("Trigger que dispara el asomo en el controller de la cortina.")]
        [SerializeField] private string tellTrigger = "Asomar";

        [Tooltip("Halo aditivo de ventana prendida. Lo enciende el anochecer, no " +
                 "la casa: la casa no sabe qué hora es.")]
        [SerializeField] private SpriteRenderer windowGlow;

        [Header("Composición")]
        [SerializeField] private HouseAnchors anchors;
        [SerializeField] private HouseSignalMounter signals;

        private HouseGenConfig config;
        private int tellTriggerHash;
        private Sprite closedDoorSprite;

        private bool generatedBuilt;
        private SpriteRenderer signalGlow;
        private bool signalWindowFree = true;

        /// <summary>
        /// Altura del gable tal como viene en el prefab: el tope de la pared. Se
        /// guarda antes de tocarla, porque la casa vuelve al pool con el gable
        /// subido y el próximo terreno puede necesitarlo abajo.
        /// </summary>
        private float gableWallY;

        /// <summary>Receta que está mostrando ahora mismo.</summary>
        public HouseLayout Layout { get; private set; }

        /// <summary>Ancho del terreno en unidades.</summary>
        public float WidthUnits => Layout?.LotWidthUnits ?? 0f;

        /// <summary>Posición mundial de la puerta. La usa el rango del timbre.</summary>
        public Vector3 DoorPosition => door != null ? door.position : transform.position;

        /// <summary>Inyecta la config. La llama el spawner al crear el pool.</summary>
        public void Initialize(HouseGenConfig houseGenConfig)
        {
            config = houseGenConfig;
            tellTriggerHash = Animator.StringToHash(tellTrigger);

            // La hoja cerrada se guarda de lo que traiga el prefab en vez de pedir
            // un segundo campo: son el mismo dibujo, y dos campos para eso invitan
            // a que un día no coincidan.
            if (doorLeaf != null) closedDoorSprite = doorLeaf.sprite;

            // El spawner llama a esto cada vez que saca la casa del pool, pero las
            // piezas dibujadas por código se arman una sola vez por casa.
            if (generatedBuilt) return;

            BuildGeneratedPieces();
            generatedBuilt = true;
        }

        /// <summary>
        /// Arma lo que no viene en el prefab: la silueta de la ventana de tell, la
        /// chimenea con su humo, y un segundo halo para la ventana de señal.
        ///
        /// Se arma por código y no en el prefab para no tener que reconstruirlo:
        /// el prefab lo genera un builder de Editor, y volver a correrlo pisaría
        /// cualquier ajuste hecho a mano. Capa, orden y material se copian de las
        /// piezas vecinas, así quedan en el mismo plano y con la misma luz.
        /// </summary>
        private void BuildGeneratedPieces()
        {
            if (roofGable != null) gableWallY = roofGable.transform.localPosition.y;

            SignalVisualsConfig visuals = config != null
                ? config.SignalVisuals
                : SignalVisualsConfig.OrDefault(null);

            WindowSilhouette silhouette = BuildSilhouette(visuals);

            ChimneySmoke chimney = null;
            float roofTop = 0f;
            if (roofSlab != null && roofSlab.sprite != null)
            {
                roofTop = roofSlab.transform.localPosition.y
                          + roofSlab.sprite.rect.height / roofSlab.sprite.pixelsPerUnit;
                chimney = ChimneySmoke.Create(transform, roofSlab, visuals);
            }

            float gableHalf = roofGable != null && roofGable.sprite != null
                ? roofGable.sprite.rect.width * 0.5f / roofGable.sprite.pixelsPerUnit
                : 0f;

            signals.AttachGenerated(silhouette, chimney, roofTop, gableHalf);

            // El segundo halo es una copia del de la ventana de tell: mismo sprite,
            // mismo material aditivo y mismo corrimiento, porque las dos ventanas
            // son el mismo dibujo con el mismo pivot.
            if (windowGlow != null && signals.SignalWindowTransform != null)
            {
                signalGlow = Instantiate(windowGlow, signals.SignalWindowTransform, false);
                signalGlow.name = windowGlow.name;
                signalGlow.enabled = false;
            }
        }

        /// <summary>
        /// La silueta va ENCIMA de la cortina y translúcida: así se lee como una
        /// sombra del otro lado. Si el halo quedaba en el mismo orden, se lo sube
        /// uno para que la luz siga por encima de todo.
        /// </summary>
        private WindowSilhouette BuildSilhouette(SignalVisualsConfig visuals)
        {
            if (tellWindow == null) return null;

            var windowRenderer = tellWindow.GetComponent<SpriteRenderer>();
            SpriteRenderer curtain = curtainAnimator != null
                ? curtainAnimator.GetComponent<SpriteRenderer>()
                : null;

            SpriteRenderer reference = curtain != null ? curtain : windowRenderer;
            if (reference == null || windowRenderer == null || windowRenderer.sprite == null)
                return null;

            int order = reference.sortingOrder + 1;
            if (windowGlow != null && windowGlow.sortingOrder <= order)
                windowGlow.sortingOrder = order + 1;

            float windowWidth = windowRenderer.sprite.rect.width / windowRenderer.sprite.pixelsPerUnit;
            return WindowSilhouette.Create(tellWindow, windowWidth, reference, order, visuals);
        }

        /// <summary>Aplica una receta. Es lo único que hace esta clase.</summary>
        public void Apply(HouseLayout layout)
        {
            Layout = layout;

            float width = layout.LotWidthUnits;
            float doorX = ProjectConstants.ToUnits(layout.DoorOffsetPixels);

            ApplyWall(layout, width);
            ApplyRoof(layout, width, doorX);
            ApplyFence(width);

            if (door != null) door.localPosition = WithX(door.localPosition, doorX);
            HouseWindowLayout.Apply(
                config, tellWindow, signals.SignalWindowTransform, width, doorX);

            signals.Mount(layout, anchors, config);
            signalWindowFree = !HasWindowVariant(layout);

            // ⚠️ La casa vive en un pool. Sin este cierre, una puerta que quedó
            // abierta vuelve a aparecer más adelante en la cuadra como una casa que
            // abre sola, y sería carísimo de rastrear: el síntoma aparece a treinta
            // metros del bug.
            SetDoorOpen(false);
        }

        /// <summary>
        /// Abre o cierra la hoja de la puerta. Lo llama la vista al atender; la casa
        /// no sabe por qué se abre, igual que no sabe si hay alguien.
        /// </summary>
        public void SetDoorOpen(bool open)
        {
            if (doorLeaf == null || openDoorSprite == null || closedDoorSprite == null) return;

            doorLeaf.sprite = open ? openDoorSprite : closedDoorSprite;
        }

        private void ApplyWall(HouseLayout layout, float width)
        {
            if (wall == null) return;
            if (layout.WallSprite != null) wall.sprite = layout.WallSprite;

            float height = wall.sprite.rect.height / wall.sprite.pixelsPerUnit;
            wall.size = new Vector2(width, height);
            wall.transform.localPosition = WithX(wall.transform.localPosition, -width * 0.5f);
        }

        private void ApplyRoof(HouseLayout layout, float width, float doorX)
        {
            bool hasGable = layout.Roof != RoofStyle.LosaCompleta;
            bool gableOnly = layout.Roof == RoofStyle.DosAguasCompleto;

            if (roofGable != null)
            {
                roofGable.enabled = hasGable;
                if (hasGable)
                {
                    float gableWidth = roofGable.sprite.rect.width / roofGable.sprite.pixelsPerUnit;
                    roofGable.transform.localPosition = new Vector3(
                        doorX - gableWidth * 0.5f, GableY(gableOnly), roofGable.transform.localPosition.z);
                }
            }

            if (roofSlab == null) return;

            // La losa cubre todo el ancho. Con el gable apoyado encima no hay
            // nada que recortar; con el gable sobre la pared, el gable se dibuja
            // delante y tapa la losa donde se pisan.
            roofSlab.enabled = !gableOnly;
            if (gableOnly) return;

            float slabHeight = roofSlab.sprite.rect.height / roofSlab.sprite.pixelsPerUnit;
            roofSlab.size = new Vector2(width, slabHeight);
            roofSlab.transform.localPosition = WithX(roofSlab.transform.localPosition, -width * 0.5f);
        }

        /// <summary>
        /// Dónde arranca el gable. En el frente a dos aguas se apoya en el TOPE de
        /// la losa, así se lee como un techo sobre la terraza y no como un alero
        /// hundido detrás del borde. Con dos aguas completo no hay losa y va sobre
        /// la pared, como vino del prefab.
        /// </summary>
        private float GableY(bool gableOnly)
        {
            bool onSlab = !gableOnly && config != null && config.GableSitsOnSlab
                          && roofSlab != null && roofSlab.sprite != null;

            if (!onSlab) return gableWallY;

            return roofSlab.transform.localPosition.y
                   + roofSlab.sprite.rect.height / roofSlab.sprite.pixelsPerUnit;
        }

        private void ApplyFence(float width)
        {
            if (fence == null || fence.sprite == null) return;
            float height = fence.sprite.rect.height / fence.sprite.pixelsPerUnit;
            fence.size = new Vector2(width, height);
            fence.transform.localPosition = WithX(fence.transform.localPosition, -width * 0.5f);
        }

        /// <summary>
        /// Hace que el vecino asome la cortina. Lo llama la espera, no la casa:
        /// la casa no sabe si hay alguien y no tiene por qué saberlo.
        /// </summary>
        public void PlayTell()
        {
            if (curtainAnimator != null) curtainAnimator.SetTrigger(tellTriggerHash);
        }

        /// <summary>
        /// Enciende la ventana, de 0 a 1. El sprite ya trae su opacidad máxima
        /// horneada, así que acá solo se escala.
        ///
        /// ⚠️ Va en la ventana del TELL y no en la de señales a propósito: la de
        /// señales puede convertirse en <c>env_ventana_tv</c>, que ES una señal
        /// con su propia capa aditiva. Un halo que se enciende con la hora encima
        /// de una señal la haría parecer que cambia sola, y las señales no mienten
        /// (§3.3).
        /// </summary>
        public void SetWindowLight(float amount)
        {
            SetGlow(windowGlow, amount);
        }

        /// <summary>
        /// Enciende la ventana de SEÑAL, de 0 a 1. Es la segunda luz de las casas
        /// que se ven más habitadas.
        ///
        /// ⚠️ Solo si la ventana está libre. Con persianas bajas no hay luz que
        /// mostrar, y el TV ya trae su propia capa: un halo que se enciende con la
        /// hora encima de una señal la haría parecer que cambia sola (§3.3).
        /// </summary>
        public void SetSignalWindowLight(float amount)
        {
            SetGlow(signalGlow, signalWindowFree ? amount : 0f);
        }

        private static void SetGlow(SpriteRenderer glow, float amount)
        {
            if (glow == null) return;

            float clamped = Mathf.Clamp01(amount);
            glow.enabled = clamped > 0f;
            glow.color = new Color(1f, 1f, 1f, clamped);
        }

        private static bool HasWindowVariant(HouseLayout layout)
        {
            for (int i = 0; i < layout.Signals.Count; i++)
                if (layout.Signals[i].Definition.MountMode == SignalMountMode.VarianteDeVentana)
                    return true;

            return false;
        }

        private static Vector3 WithX(Vector3 value, float x) => new Vector3(x, value.y, value.z);
    }
}
