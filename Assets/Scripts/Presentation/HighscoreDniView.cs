using System.Collections.Generic;
using BuenosDias.Config;
using BuenosDias.Gameplay;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// El "DNI" del vendedor: una tarjeta a un costado de la tabla de récords con la
    /// foto del predicador vestido como la religión con la que se jugó (camisa y
    /// corbata de su paleta), el nombre de la religión, las iniciales y el puntaje.
    ///
    /// Se arma ENTERA por código —rectángulos sólidos, el sprite del predicador y
    /// carteles clonados de uno que ya está en la escena—, así que no hace falta
    /// dibujar un asset nuevo ni agregar objetos a la escena. Todo va en escala
    /// entera y en posiciones de píxel entero para no romper la grilla.
    ///
    /// No decide nada: se suscribe al <see cref="HighscoreDirector"/> y aparece
    /// cuando la tabla se muestra.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HighscoreDniView : MonoBehaviour
    {
        private const int CardWidth = 80;
        private const int CardHeight = 64;

        [Header("Referencias")]
        [Tooltip("Dueño de la tabla. Se escucha; nunca se le pide nada.")]
        [SerializeField] private HighscoreDirector director;

        [Tooltip("El panel oscuro de la tabla. De ahí se copian material, capa y orden.")]
        [SerializeField] private SpriteRenderer panel;

        [Tooltip("Cartel que se CLONA para los textos de la tarjeta. Todos los carteles " +
                 "de la tabla son centrados y de escala 1, así que sirve cualquiera.")]
        [SerializeField] private TextLabel labelTemplate;

        [Tooltip("Sprite del predicador que sale en la foto (chr_predicador_idle).")]
        [SerializeField] private Sprite portraitSprite;

        [Header("Posición")]
        [Tooltip("Centro de la tarjeta, en píxeles, respecto del centro de la tabla. " +
                 "La tabla mide 200 de ancho, así que a +146 la tarjeta entra en los " +
                 "92 px libres de la derecha de una pantalla de 384.")]
        [SerializeField] private Vector2Int centerPixels = new Vector2Int(146, 6);

        [Header("Textos")]
        [SerializeField] private string cardTitle = "DNI VENDEDOR";
        [SerializeField] private string scoreCaption = "PTS";
        [SerializeField] private string pendingInitials = "---";

        [Header("Colores")]
        [Tooltip("Borde y tinta. Violeta #1D1638 de la paleta.")]
        [SerializeField] private Color inkColor = new Color32(0x1D, 0x16, 0x38, 0xFF);

        [Tooltip("Fondo del documento. Hueso #F3ECE0.")]
        [SerializeField] private Color paperColor = new Color32(0xF3, 0xEC, 0xE0, 0xFF);

        [Tooltip("Franja de arriba, el celeste del DNI.")]
        [SerializeField] private Color stripColor = new Color32(0x7F, 0xB2, 0xD6, 0xFF);

        [Tooltip("Fondo de la foto.")]
        [SerializeField] private Color photoColor = new Color32(0xA9, 0xC4, 0xD6, 0xFF);

        private static readonly Color BaseShirt = new Color32(0xF3, 0xEC, 0xE0, 0xFF);
        private static readonly Color BaseTie = new Color32(0x7A, 0x2F, 0x3D, 0xFF);

        private readonly Dictionary<ReligionDefinition, Sprite> portraits =
            new Dictionary<ReligionDefinition, Sprite>();

        private GameObject root;
        private Sprite solid;
        private SpriteRenderer portraitRenderer;
        private TextLabel initialsLabel;
        private TextLabel scoreLabel;
        private TextLabel nameLabel;

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }

            solid = SolidSprite.Create(Color.white, ProjectConstants.PixelsPerUnit);

            // Se arma INACTIVA: los carteles clonados no corren su Awake hasta que
            // la tarjeta se prende, y para entonces ya se les sacó lo que traían
            // copiado del original.
            root = new GameObject("DNI");
            root.SetActive(false);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(
                ProjectConstants.ToUnits(centerPixels.x), ProjectConstants.ToUnits(centerPixels.y), 0f);

            int order = panel.sortingOrder;

            // De atrás para adelante: borde, papel, franja, marco de la foto, foto.
            Rect("Borde", 0, 0, CardWidth + 4, CardHeight + 4, inkColor, order + 1);
            Rect("Papel", 0, 0, CardWidth, CardHeight, paperColor, order + 2);
            Rect("Franja", 0, 26.5f, CardWidth, 11, stripColor, order + 3);
            Rect("Marco foto", -18, 0, 42, 42, inkColor, order + 3);
            Rect("Fondo foto", -18, 0, 40, 40, photoColor, order + 4);

            var portraitObject = new GameObject("Foto");
            portraitObject.transform.SetParent(root.transform, false);
            portraitRenderer = portraitObject.AddComponent<SpriteRenderer>();
            Style(portraitRenderer, order + 5);

            Label(cardTitle, 0, 23);
            initialsLabel = Label(pendingInitials, 22, 8);
            Label(scoreCaption, 22, -1);
            scoreLabel = Label("0", 22, -10);
            nameLabel = Label(string.Empty, 0, -30);
        }

        private void OnEnable()
        {
            if (director != null) director.StageChanged += OnStageChanged;
        }

        private void OnDisable()
        {
            if (director != null) director.StageChanged -= OnStageChanged;
        }

        private void OnDestroy()
        {
            SolidSprite.Dispose(solid);

            foreach (Sprite sprite in portraits.Values)
            {
                if (sprite == null || sprite == portraitSprite) continue;
                Texture texture = sprite.texture;
                Destroy(sprite);
                if (texture != null) Destroy(texture);
            }
        }

        private void OnStageChanged(HighscoreStage stage)
        {
            bool listing = stage == HighscoreStage.Tabla;
            if (listing) Fill();

            if (root != null && root.activeSelf != listing) root.SetActive(listing);
        }

        /// <summary>Escribe los datos de la partida que se acaba de jugar.</summary>
        private void Fill()
        {
            ReligionDefinition religion = director.Religion;

            portraitRenderer.sprite = PortraitFor(religion);
            CenterPortrait(portraitRenderer, -18f, 0f);

            InitialsEntry entry = director.Entry;
            initialsLabel.SetText(entry != null && entry.IsComplete ? entry.Initials : pendingInitials);
            scoreLabel.SetText(director.Score.ToString());
            nameLabel.SetText(religion != null ? religion.DisplayName : string.Empty);
        }

        /// <summary>
        /// El predicador con la camisa y la corbata de la religión. Se arma una vez
        /// por religión y queda guardado: la tabla puede abrirse muchas veces.
        /// </summary>
        private Sprite PortraitFor(ReligionDefinition religion)
        {
            if (religion == null) return portraitSprite;
            if (portraits.TryGetValue(religion, out Sprite cached)) return cached;

            var swaps = new List<PaletteSwapper.ColorSwap>
            {
                new PaletteSwapper.ColorSwap { from = BaseShirt, to = religion.ShirtColor },
                new PaletteSwapper.ColorSwap { from = BaseTie, to = religion.TieColor }
            };
            foreach (ReligionDefinition.ColorPair extra in religion.ExtraSwaps)
                swaps.Add(new PaletteSwapper.ColorSwap { from = extra.from, to = extra.to });

            Sprite built = PaletteSwapper.Recolor(
                portraitSprite, swaps, 0.02f, religion.HatSprite, religion.HatSinkPixels,
                religion.SidelockSprite, religion.SidelockOffset);

            portraits[religion] = built;
            return built;
        }

        /// <summary>
        /// Centra el sprite en el marco sin importar dónde tenga el pivot: la
        /// posición sale del <c>bounds</c> y no se supone que el pivot es el centro.
        /// </summary>
        private static void CenterPortrait(SpriteRenderer renderer, float xPixels, float yPixels)
        {
            if (renderer.sprite == null) return;

            Vector3 center = renderer.sprite.bounds.center;
            renderer.transform.localPosition = new Vector3(
                ProjectConstants.ToUnits(xPixels) - center.x,
                ProjectConstants.ToUnits(yPixels) - center.y, 0f);
        }

        private void Rect(string name, float x, float y, int width, int height, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(
                ProjectConstants.ToUnits(x), ProjectConstants.ToUnits(y), 0f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = solid;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = new Vector2(ProjectConstants.ToUnits(width), ProjectConstants.ToUnits(height));
            renderer.color = color;
            Style(renderer, order);
        }

        private void Style(SpriteRenderer renderer, int order)
        {
            renderer.sharedMaterial = panel.sharedMaterial;
            renderer.sortingLayerID = panel.sortingLayerID;
            renderer.sortingOrder = order;
        }

        private TextLabel Label(string text, float x, float y)
        {
            TextLabel label = Instantiate(labelTemplate, root.transform);

            // El original ya dibujó sus letras como hijos: el clon las trae copiadas
            // y armaría las suyas encima. Se sacan antes de que corra su Awake.
            for (int i = label.transform.childCount - 1; i >= 0; i--)
                Destroy(label.transform.GetChild(i).gameObject);

            label.gameObject.SetActive(true);
            label.transform.localPosition = new Vector3(
                ProjectConstants.ToUnits(x), ProjectConstants.ToUnits(y), 0f);
            label.SetInkColor(inkColor);
            label.SetText(text);
            return label;
        }

        private bool ValidateSetup()
        {
            if (director != null && panel != null && labelTemplate != null
                && portraitSprite != null) return true;

            Debug.LogError(
                $"[HighscoreDniView] '{name}' tiene referencias sin asignar " +
                "(tabla, panel, cartel modelo o sprite del predicador).", this);
            return false;
        }
    }
}
