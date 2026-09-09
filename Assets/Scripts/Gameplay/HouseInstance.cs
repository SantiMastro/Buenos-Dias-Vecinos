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
                    roofGable.transform.localPosition = WithX(
                        roofGable.transform.localPosition, doorX - gableWidth * 0.5f);
                }
            }

            if (roofSlab == null) return;

            // La losa cubre todo el ancho también bajo el gable: el gable se
            // dibuja encima y el sobrante no se ve. Es más simple y más barato
            // que recortar la losa en dos tramos.
            roofSlab.enabled = !gableOnly;
            if (gableOnly) return;

            float slabHeight = roofSlab.sprite.rect.height / roofSlab.sprite.pixelsPerUnit;
            roofSlab.size = new Vector2(width, slabHeight);
            roofSlab.transform.localPosition = WithX(roofSlab.transform.localPosition, -width * 0.5f);
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
            if (windowGlow == null) return;

            float clamped = Mathf.Clamp01(amount);
            windowGlow.enabled = clamped > 0f;
            windowGlow.color = new Color(1f, 1f, 1f, clamped);
        }

        private static Vector3 WithX(Vector3 value, float x) => new Vector3(x, value.y, value.z);
    }
}
