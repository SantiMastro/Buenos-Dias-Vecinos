using BuenosDias.Config;
using BuenosDias.EditorTools.Building;
using BuenosDias.Gameplay;
using BuenosDias.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuenosDias.EditorTools.Authoring
{
    /// <summary>
    /// Arma la tabla de récords: el panel, los carteles y el dueño que la maneja.
    ///
    /// Va aparte de <see cref="EndingRigBuilder"/> por la misma costura: es otra
    /// pantalla, con su propio orden de dibujo —por encima del telón del final
    /// (300) y de sus carteles (400)— y su propio asset de perillas.
    ///
    /// Es idempotente: si ya hay una tabla colgada de la cámara, la rehace.
    /// </summary>
    public static class HighscoreRigBuilder
    {
        /// <summary>Asset de perillas de la tabla.</summary>
        public const string ConfigPath = "Assets/ScriptableObjects/Config/HighscoreConfig.asset";

        private const string RootName = "Highscore";

        /// <summary>El panel tapa los carteles del final (400), que caen debajo de él.</summary>
        private const int PanelOrder = 410;

        private const int LabelOrder = 420;

        private const int MaxRows = 10;

        /// <summary>Z de los carteles: la misma del final, por delante del mundo.</summary>
        private const float LabelZ = -5f;

        /// <summary>
        /// Alturas, en píxeles desde el CENTRO de la cámara, como el resto de los
        /// carteles (§3.8). Todo va por encima del titular del final, que está en
        /// −46: la tabla ocupa de +98 a −26 y deja leer el final debajo.
        /// </summary>
        private const float PanelCenterY = 36f;

        private const float HeaderY = 82f;
        private const float DetailY = 68f;
        private const float WordY = 30f;
        private const float GaugeY = 26f;
        private const float EntryHintY = 8f;
        private const float FirstRowY = 68f;
        private const float RowSpacing = 10f;

        /// <summary>Debajo de la segunda línea del final (−62), sobre la calle a oscuras.</summary>
        private const float RestartHintY = -80f;

        /// <summary>Arma la tabla y se la cuelga al dueño de la partida.</summary>
        public static HighscoreDirector Build(Camera camera, RunDirector run, GameInput input)
        {
            Transform previous = camera.transform.Find(RootName);
            if (previous != null) Object.DestroyImmediate(previous.gameObject);

            var root = new GameObject(RootName);
            root.transform.SetParent(camera.transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, LabelZ - SceneLayout.CameraZ);

            var director = root.AddComponent<HighscoreDirector>();
            SerializedFieldUtility.SetReference(director, "runDirector", run);
            SerializedFieldUtility.SetReference(director, "input", input);
            SerializedFieldUtility.SetReference(director, "config",
                AssetDatabase.LoadAssetAtPath<HighscoreConfig>(ConfigPath));
            SerializedFieldUtility.SetReference(run, "highscore", director);

            BuildView(root.transform, director);
            return director;
        }

        private static void BuildView(Transform root, HighscoreDirector director)
        {
            BitmapFontDefinition font = LabelFactory.Font;

            SpriteRenderer panel = AddRenderer(root, "Panel", PanelOrder, PanelCenterY);
            SpriteRenderer track = AddRenderer(root, "Medidor riel", LabelOrder + 1, GaugeY);
            SpriteRenderer fill = AddRenderer(root, "Medidor relleno", LabelOrder + 2, GaugeY);

            TextLabel header = LabelFactory.Create(root, "Titular", font, HeaderY, 1, LabelOrder);
            TextLabel detail = LabelFactory.Create(root, "Puesto ganado", font, DetailY, 1, LabelOrder);
            TextLabel word = LabelFactory.Create(root, "Iniciales", font, WordY, 3, LabelOrder);
            TextLabel cursor = LabelFactory.Create(root, "Letra del cursor", font, WordY, 3, LabelOrder + 3);
            TextLabel entryHint = LabelFactory.Create(root, "Cómo cargar", font, EntryHintY, 1, LabelOrder);
            TextLabel restartHint = LabelFactory.Create(root, "Jugar de nuevo", font, RestartHintY, 1, LabelOrder);

            var rowsRoot = new GameObject("Puestos").transform;
            rowsRoot.SetParent(root, false);

            var rows = new Object[MaxRows];
            for (int i = 0; i < MaxRows; i++)
                rows[i] = LabelFactory.Create(
                    rowsRoot, $"Puesto {i + 1:00}", font, FirstRowY - RowSpacing * i, 1, LabelOrder);

            var view = root.gameObject.AddComponent<HighscoreView>();
            SerializedFieldUtility.SetReference(view, "director", director);
            SerializedFieldUtility.SetReference(view, "panel", panel);
            SerializedFieldUtility.SetReference(view, "headerLabel", header);
            SerializedFieldUtility.SetReference(view, "detailLabel", detail);
            SerializedFieldUtility.SetReference(view, "wordLabel", word);
            SerializedFieldUtility.SetReference(view, "cursorLabel", cursor);
            SerializedFieldUtility.SetReference(view, "gaugeTrack", track);
            SerializedFieldUtility.SetReference(view, "gaugeFill", fill);
            SerializedFieldUtility.SetReference(view, "entryHintLabel", entryHint);
            SerializedFieldUtility.SetReference(view, "restartHintLabel", restartHint);
            SerializedFieldUtility.SetReferenceList(view, "rowLabels", rows);
        }

        private static SpriteRenderer AddRenderer(Transform parent, string name, int order, float yPixels)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, ProjectConstants.ToUnits(yPixels), 0f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = SortingLayerAuthoring.Fx;
            renderer.sortingOrder = order;
            UiMaterialFactory.Apply(renderer);

            return renderer;
        }

        /// <summary>
        /// Agrega la tabla a la escena abierta SIN reconstruirla. Reconstruir borra
        /// lo que se haya tildado a mano (ver <see cref="GameSceneBuilder"/>); esto
        /// solo toca la tabla y la referencia del dueño de la partida.
        /// </summary>
        [MenuItem("Tools/Buenos Días/Highscore · Agregar a la escena abierta", priority = 120)]
        public static void AddToOpenScene()
        {
            var run = Object.FindAnyObjectByType<RunDirector>();
            var input = Object.FindAnyObjectByType<GameInput>();
            Camera camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();

            if (run == null || input == null || camera == null)
            {
                Debug.LogError(
                    "[HighscoreRigBuilder] La escena abierta no tiene partida, input o " +
                    "cámara. ¿Es Game.unity?");
                return;
            }

            Build(camera, run, input);
            EditorSceneManager.MarkSceneDirty(run.gameObject.scene);
            Debug.Log("[HighscoreRigBuilder] Tabla de récords agregada. Guardá la escena.");
        }

        /// <summary>Abre la carpeta donde vive el JSON de récords.</summary>
        [MenuItem("Tools/Buenos Días/Highscore · Abrir carpeta del JSON", priority = 121)]
        public static void RevealSaveFolder()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }
    }
}
