using System.Collections.Generic;
using BuenosDias.Config;
using BuenosDias.Gameplay;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Dibuja la tabla de récords y la carga de iniciales, sobre un panel oscuro
    /// que la separa del final de atrás.
    ///
    /// No decide nada: lee al <see cref="HighscoreDirector"/> y se entera de los
    /// cambios por eventos. Las letras se redibujan solo cuando cambian; lo único
    /// que corre cada cuadro es el medidor del mantenido y el parpadeo del puesto
    /// nuevo, y ninguno de los dos genera basura.
    ///
    /// La letra que se está eligiendo va en un cartel APARTE, encimado sobre la
    /// palabra: la fuente es monoespaciada, así que dos cadenas del mismo largo
    /// caen letra sobre letra, y así la del cursor puede ir de otro color sin
    /// partir la palabra en tres carteles.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HighscoreView : MonoBehaviour
    {
        /// <summary>Lo que se muestra en los puestos de letras que todavía no se eligieron.</summary>
        private const char PendingGlyph = '-';

        [Header("Referencias")]
        [Tooltip("Dueño de la tabla. Se lee y se escucha; nunca se le pide nada.")]
        [SerializeField] private HighscoreDirector director;

        [Tooltip("Panel oscuro detrás de todo. Su sprite lo genera esta clase.")]
        [SerializeField] private SpriteRenderer panel;

        [Tooltip("Titular: 'nuevo récord' al cargar, el nombre de la tabla al mostrarla.")]
        [SerializeField] private TextLabel headerLabel;

        [Tooltip("Qué puesto se ganó. Solo al cargar.")]
        [SerializeField] private TextLabel detailLabel;

        [Tooltip("Las iniciales, en grande. Lleva las fijas y guiones en las que faltan.")]
        [SerializeField] private TextLabel wordLabel;

        [Tooltip("Solo la letra que se está eligiendo, encimada sobre la palabra.")]
        [SerializeField] private TextLabel cursorLabel;

        [Tooltip("Riel del medidor del mantenido, debajo de la letra del cursor.")]
        [SerializeField] private SpriteRenderer gaugeTrack;

        [Tooltip("Relleno del medidor del mantenido.")]
        [SerializeField] private SpriteRenderer gaugeFill;

        [Tooltip("Cómo se usa el click. Solo al cargar.")]
        [SerializeField] private TextLabel entryHintLabel;

        [Tooltip("Cartel de 'jugar de nuevo'. Aparece recién cuando el click reinicia.")]
        [SerializeField] private TextLabel restartHintLabel;

        [Tooltip("Un cartel por puesto, del primero al último.")]
        [SerializeField] private List<TextLabel> rowLabels = new List<TextLabel>();

        [Header("Textos")]
        [SerializeField] private string tableTitle = "MEJORES VENDEDORES";
        [SerializeField] private string newRecordTitle = "¡NUEVO RÉCORD!";

        [Tooltip("'{0}' es el puesto, contado desde 1.")]
        [SerializeField] private string detailFormat = "PUESTO {0}";

        [SerializeField] private string entryHint = "CLICK: LETRA   MANTENÉ: LISTO";
        [SerializeField] private string restartHint = "CLICK: JUGAR DE NUEVO";

        [Header("Colores")]
        [Tooltip("Tinta de los puestos. Hueso #F3ECE0.")]
        [SerializeField] private Color inkColor = new Color32(0xF3, 0xEC, 0xE0, 0xFF);

        [Tooltip("Titulares, letra del cursor y puesto nuevo. Naranja #FF9A5B.")]
        [SerializeField] private Color highlightColor = new Color32(0xFF, 0x9A, 0x5B, 0xFF);

        [Tooltip("Puestos vacíos. Verde salvia #6F8A5E: se leen, pero no compiten.")]
        [SerializeField] private Color emptyColor = new Color32(0x6F, 0x8A, 0x5E, 0xFF);

        [Tooltip("Panel. Violeta #1D1638 con transparencia: tapa lo justo para leer " +
                 "y deja ver que atrás sigue el final.")]
        [SerializeField] private Color panelColor = new Color(0.1137f, 0.0863f, 0.2196f, 0.82f);

        [Tooltip("Riel del medidor.")]
        [SerializeField] private Color trackColor = new Color32(0x1D, 0x16, 0x38, 0xFF);

        [Header("Medidas, en píxeles")]
        [SerializeField] private Vector2Int panelSizePixels = new Vector2Int(200, 124);

        [SerializeField, Min(1)] private int gaugeHeightPixels = 2;

        [Header("Tiempos")]
        [Tooltip("Mitad del ciclo de parpadeo del puesto nuevo.")]
        [SerializeField, Min(0.05f)] private float blinkSeconds = 0.3f;

        [Tooltip("Por debajo de este progreso el medidor no se dibuja: un toque corto " +
                 "no tiene que hacer flashear la barra.")]
        [SerializeField, Range(0f, 0.9f)] private float gaugeDeadZone = 0.15f;

        private Sprite panelSprite;
        private Sprite gaugeSprite;

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }

            panelSprite = SolidSprite.Create(Color.white, ProjectConstants.PixelsPerUnit);
            panel.sprite = panelSprite;
            panel.drawMode = SpriteDrawMode.Tiled;
            panel.color = panelColor;
            panel.size = new Vector2(
                ProjectConstants.ToUnits(panelSizePixels.x), ProjectConstants.ToUnits(panelSizePixels.y));

            gaugeSprite = SolidSprite.Create(Color.white, ProjectConstants.PixelsPerUnit);
            PrepareBar(gaugeTrack, trackColor);
            PrepareBar(gaugeFill, highlightColor);

            headerLabel.SetInkColor(highlightColor);
            cursorLabel.SetInkColor(highlightColor);
            entryHintLabel.SetText(entryHint);
            restartHintLabel.SetText(restartHint);

            Show(HighscoreStage.Apagado);
        }

        private void OnDestroy()
        {
            SolidSprite.Dispose(panelSprite);
            SolidSprite.Dispose(gaugeSprite);
        }

        private void OnEnable()
        {
            if (director == null) return;

            director.StageChanged += Show;
            director.EntryStepped += OnEntryStepped;
        }

        private void OnDisable()
        {
            if (director == null) return;

            director.StageChanged -= Show;
            director.EntryStepped -= OnEntryStepped;
        }

        private void LateUpdate()
        {
            if (director.Stage == HighscoreStage.Iniciales)
            {
                DrawGauge();
            }
            else if (director.Stage == HighscoreStage.Tabla)
            {
                BlinkNewRow();
                SetActive(restartHintLabel, director.RestartReady);
            }
        }

        private void Show(HighscoreStage stage)
        {
            bool typing = stage == HighscoreStage.Iniciales;
            bool listing = stage == HighscoreStage.Tabla;

            panel.enabled = typing || listing;
            SetActive(headerLabel, typing || listing);
            SetActive(detailLabel, typing);
            SetActive(wordLabel, typing);
            SetActive(cursorLabel, typing);
            SetActive(entryHintLabel, typing);
            SetActive(restartHintLabel, false);
            gaugeTrack.enabled = false;
            gaugeFill.enabled = false;

            foreach (TextLabel row in rowLabels) SetActive(row, false);

            if (typing) DrawEntry();
            if (listing) DrawTable();
        }

        private void OnEntryStepped(InitialsStep step)
        {
            if (director.Stage == HighscoreStage.Iniciales && !director.Entry.IsComplete) DrawEntry();
        }

        private void DrawEntry()
        {
            headerLabel.SetText(newRecordTitle);
            detailLabel.SetText(string.Format(detailFormat, director.PendingRank + 1));

            InitialsEntry entry = director.Entry;
            var word = new char[entry.Length];
            var cursor = new char[entry.Length];

            for (int i = 0; i < entry.Length; i++)
            {
                bool current = i == entry.Cursor;
                word[i] = i < entry.Cursor ? entry.LetterAt(i) : current ? ' ' : PendingGlyph;
                cursor[i] = current ? entry.LetterAt(i) : ' ';
            }

            wordLabel.SetText(new string(word));
            cursorLabel.SetText(new string(cursor));
        }

        private void DrawTable()
        {
            headerLabel.SetText(tableTitle);

            HighscoreTable table = director.Table;
            int width = director.Config.InitialsLength;
            int shown = Mathf.Min(table.Capacity, rowLabels.Count);

            for (int i = 0; i < shown; i++)
            {
                TextLabel row = rowLabels[i];
                bool filled = i < table.Entries.Count;

                row.SetText(filled ? RowText(i, table.Entries[i], width) : EmptyRowText(i, width));
                row.SetInkColor(filled ? inkColor : emptyColor);
                SetActive(row, true);
            }
        }

        /// <summary>
        /// Todas las filas miden lo mismo —puesto a 2, iniciales al ancho, puntaje a
        /// 4— y los carteles van centrados: con la fuente monoespaciada, eso alcanza
        /// para que las columnas queden alineadas sin alinear nada.
        /// </summary>
        private static string RowText(int rank, HighscoreEntry entry, int width)
        {
            return $"{rank + 1,2}. {Fit(entry.Initials, width)} {entry.Score,4}";
        }

        private static string EmptyRowText(int rank, int width)
        {
            return $"{rank + 1,2}. {new string(PendingGlyph, width)} {string.Empty,4}";
        }

        /// <summary>Corta o rellena las iniciales al ancho: un JSON editado a mano puede traer otro largo.</summary>
        private static string Fit(string initials, int width)
        {
            return initials.Length > width ? initials.Substring(0, width) : initials.PadRight(width);
        }

        /// <summary>
        /// El medidor cae debajo de la letra del cursor. La posición sale de la
        /// misma cuenta con que <see cref="TextLabel"/> centra: la palabra arranca
        /// en −ancho/2 redondeado, y cada letra avanza una celda.
        /// </summary>
        private void DrawGauge()
        {
            InitialsEntry entry = director.Entry;
            float progress = entry.HoldProgress(Time.unscaledTime);
            bool visible = !entry.IsComplete && progress > gaugeDeadZone;

            gaugeTrack.enabled = visible;
            gaugeFill.enabled = visible;
            if (!visible) return;

            int advance = wordLabel.AdvancePixels;
            int glyph = advance - wordLabel.PixelScale; // la celda trae un píxel de aire
            float left = -Mathf.Round(entry.Length * advance * 0.5f) + entry.Cursor * advance;

            PlaceBar(gaugeTrack, left, glyph);
            PlaceBar(gaugeFill, left, Mathf.RoundToInt(glyph * progress));
        }

        private void BlinkNewRow()
        {
            int rank = director.NewRank;
            if (rank < 0 || rank >= rowLabels.Count) return;

            bool lit = Mathf.FloorToInt(Time.unscaledTime / blinkSeconds) % 2 == 0;
            rowLabels[rank].SetInkColor(lit ? highlightColor : inkColor);
        }

        private void PrepareBar(SpriteRenderer bar, Color color)
        {
            bar.sprite = gaugeSprite;
            bar.drawMode = SpriteDrawMode.Tiled;
            bar.color = color;
            bar.enabled = false;
        }

        /// <summary>
        /// Coloca una barra por su borde IZQUIERDO. El sprite pivotea en el centro,
        /// así que el centro va a medio ancho; el borde cae siempre en píxel entero.
        /// </summary>
        private void PlaceBar(SpriteRenderer bar, float leftPixels, int widthPixels)
        {
            if (widthPixels <= 0)
            {
                bar.enabled = false;
                return;
            }

            bar.size = new Vector2(
                ProjectConstants.ToUnits(widthPixels), ProjectConstants.ToUnits(gaugeHeightPixels));

            Vector3 position = bar.transform.localPosition;
            position.x = ProjectConstants.ToUnits(leftPixels + widthPixels * 0.5f);
            bar.transform.localPosition = position;
        }

        private static void SetActive(TextLabel label, bool active)
        {
            if (label.gameObject.activeSelf != active) label.gameObject.SetActive(active);
        }

        private bool ValidateSetup()
        {
            if (director != null && panel != null && headerLabel != null && detailLabel != null
                && wordLabel != null && cursorLabel != null && gaugeTrack != null
                && gaugeFill != null && entryHintLabel != null && restartHintLabel != null
                && rowLabels.Count > 0 && !rowLabels.Contains(null)) return true;

            Debug.LogError(
                $"[HighscoreView] '{name}' tiene referencias sin asignar (tabla, panel, " +
                "carteles, medidor o puestos).", this);
            return false;
        }
    }
}
