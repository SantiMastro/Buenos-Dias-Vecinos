using System.Collections.Generic;
using BuenosDias.Config;
using BuenosDias.Gameplay;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Reproduce la cinemática del final que salió.
    ///
    /// No decide cuál: escucha <see cref="RunDirector.EndingReached"/>, que ya
    /// trae el final resuelto y las conversiones. Acá solo se dibuja.
    ///
    /// El fondo se estira desde <c>orthographicSize</c> cada cuadro y no se deja
    /// de un tamaño fijo: la Pixel Perfect Camera lo cambia en runtime (§3.8) y un
    /// telón horneado deja ver la calle por los costados apenas cambia la ventana.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EndingView : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Dueño de la partida. Se escucha; nunca se le pide nada.")]
        [SerializeField] private RunDirector runDirector;

        [Tooltip("Cámara que enmarca la cinemática.")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Telón de fondo que tapa el mundo.")]
        [SerializeField] private SpriteRenderer backdrop;

        [Tooltip("Material de las piezas. Tiene que ser SIN iluminar: una cinemática " +
                 "no está dentro de la calle, así que la luz del atardecer no tiene " +
                 "nada que hacerle. Con el material iluminado, el naranja del " +
                 "atardecer salía marrón.")]
        [SerializeField] private Material spriteMaterial;

        [Tooltip("Titular, en ×2.")]
        [SerializeField] private TextLabel titleLabel;

        [Tooltip("Segunda línea, en ×1.")]
        [SerializeField] private TextLabel subtitleLabel;

        [Header("Catálogo")]
        [Tooltip("Los finales disponibles. Agregar uno es crear el asset y sumarlo " +
                 "acá; ninguna clase los enumera.")]
        [SerializeField] private List<EndingDefinition> endings = new List<EndingDefinition>();

        [Header("Tiempos")]
        [Tooltip("Segundos desde que cae la noche hasta que entra el titular. La " +
                 "imagen tiene que llegar antes que la frase: si entran juntas, se " +
                 "lee el cartel y no se mira el dibujo.")]
        [SerializeField, Min(0f)] private float titleDelaySeconds = 1.2f;

        [Tooltip("Segundos entre el titular y la segunda línea.")]
        [SerializeField, Min(0f)] private float subtitleDelaySeconds = 0.5f;

        private readonly List<SpriteRenderer> pieces = new List<SpriteRenderer>();
        private EndingDefinition playing;
        private Sprite backdropSprite;
        private float elapsed;

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }

            backdropSprite = SolidSprite.Create(Color.white, ProjectConstants.PixelsPerUnit);
            backdrop.sprite = backdropSprite;
            backdrop.drawMode = SpriteDrawMode.Tiled;

            Hide();
        }

        private void OnDestroy()
        {
            SolidSprite.Dispose(backdropSprite);
        }

        private void OnEnable()
        {
            if (runDirector != null) runDirector.EndingReached += Play;
        }

        private void OnDisable()
        {
            if (runDirector != null) runDirector.EndingReached -= Play;
        }

        private void Play(EndingKind kind, int followers)
        {
            playing = Find(kind);
            if (playing == null)
            {
                Debug.LogError($"[EndingView] '{name}': no hay asset para el final {kind}.", this);
                return;
            }

            elapsed = 0f;
            backdrop.color = playing.Backdrop;
            backdrop.enabled = playing.HideWorld;

            titleLabel.SetText(playing.Title);
            subtitleLabel.SetText(playing.SubtitleFor(followers));

            Build(playing);
        }

        private void LateUpdate()
        {
            if (playing == null) return;

            elapsed += Time.deltaTime;
            StretchBackdrop();

            IReadOnlyList<EndingLayer> layers = playing.Layers;
            for (int i = 0; i < layers.Count; i++)
            {
                float since = elapsed - layers[i].AppearAtSeconds;
                pieces[i].enabled = since >= 0f;
                if (since < 0f) continue;

                pieces[i].sprite = layers[i].FrameAt(since);
                pieces[i].transform.localPosition = PositionOf(layers[i], since);
                pieces[i].transform.localRotation = Quaternion.Euler(
                    0f, 0f, layers[i].DegreesPerSecond * since);
            }

            titleLabel.gameObject.SetActive(elapsed >= titleDelaySeconds);
            subtitleLabel.gameObject.SetActive(
                elapsed >= titleDelaySeconds + subtitleDelaySeconds);
        }

        /// <summary>
        /// El telón se estira a la vista real de la cámara, con un margen de sobra:
        /// más vale que sobre tapado a que se vea un borde de calle.
        /// </summary>
        private void StretchBackdrop()
        {
            float height = targetCamera.orthographicSize * 2f;
            backdrop.size = new Vector2(height * targetCamera.aspect + 1f, height + 1f);
        }

        private EndingDefinition Find(EndingKind kind)
        {
            foreach (EndingDefinition ending in endings)
                if (ending != null && ending.Kind == kind) return ending;

            return null;
        }

        private void Build(EndingDefinition ending)
        {
            IReadOnlyList<EndingLayer> layers = ending.Layers;

            for (int i = 0; i < layers.Count; i++)
            {
                SpriteRenderer piece = PieceAt(i);
                piece.sortingOrder = backdrop.sortingOrder + 1 + layers[i].SortingOrder;
                piece.color = layers[i].Tint;
                piece.enabled = false;
                piece.gameObject.name = layers[i].Label;
                piece.transform.localPosition = PositionOf(layers[i], 0f);
                piece.transform.localRotation = Quaternion.identity;
            }

            for (int i = layers.Count; i < pieces.Count; i++) pieces[i].enabled = false;
        }

        /// <summary>
        /// Dónde va una pieza a los <paramref name="since"/> segundos de haber
        /// aparecido. El corrimiento se cuenta desde que ENTRA y no desde que
        /// arranca el final: si contara desde el principio, una pieza que aparece
        /// tarde entraría ya corrida, o sea de la nada y en el aire.
        /// </summary>
        private static Vector3 PositionOf(EndingLayer layer, float since)
        {
            Vector2 drift = layer.DriftPixelsPerSecond * Mathf.Max(0f, since);

            return new Vector3(
                ProjectConstants.ToUnits(layer.PositionPixels.x + drift.x),
                ProjectConstants.ToUnits(layer.PositionPixels.y + drift.y),
                0f);
        }

        private SpriteRenderer PieceAt(int index)
        {
            while (pieces.Count <= index)
            {
                var go = new GameObject("pieza");
                go.transform.SetParent(transform, false);

                var created = go.AddComponent<SpriteRenderer>();
                created.sortingLayerName = backdrop.sortingLayerName;
                if (spriteMaterial != null) created.sharedMaterial = spriteMaterial;
                pieces.Add(created);
            }

            return pieces[index];
        }

        private void Hide()
        {
            backdrop.enabled = false;
            titleLabel.gameObject.SetActive(false);
            subtitleLabel.gameObject.SetActive(false);
        }

        private bool ValidateSetup()
        {
            if (runDirector != null && targetCamera != null && backdrop != null
                && titleLabel != null && subtitleLabel != null) return true;

            Debug.LogError(
                $"[EndingView] '{name}' tiene referencias sin asignar " +
                "(partida, cámara, telón, titular o segunda línea).", this);
            return false;
        }
    }
}
