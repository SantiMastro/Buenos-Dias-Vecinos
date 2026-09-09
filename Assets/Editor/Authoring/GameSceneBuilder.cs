using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuenosDias.EditorTools.Authoring
{
    /// <summary>
    /// Construye <c>Assets/Scenes/Game.unity</c> desde cero: sorting layers,
    /// cámara pixel-perfect, luz global y el árbol de capas del mundo.
    ///
    /// Es idempotente: correrlo de nuevo rehace la escena entera. Eso es
    /// deliberado, así la escena siempre se puede reconstruir desde el repo en
    /// vez de depender de ediciones manuales que nadie documentó.
    /// </summary>
    public static class GameSceneBuilder
    {
        /// <summary>Ruta de la escena principal.</summary>
        public const string ScenePath = "Assets/Scenes/Game.unity";

        [MenuItem("Tools/Buenos Días/Fase 1 · Construir escena de juego", priority = 100)]
        public static void BuildFromMenu()
        {
            string report = Build();
            Debug.Log(report);
        }

        /// <summary>Construye y guarda la escena. Devuelve un reporte legible.</summary>
        public static string Build()
        {
            SpriteLibrary.Invalidate();

            int createdLayers = SortingLayerAuthoring.EnsureLayers();

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // El predicador se crea primero pero se cablea al final: la cámara lo
            // necesita para seguirlo, y él necesita el spawner de casas, que se
            // arma junto con la cámara.
            Transform preacher = PreacherBuilder.CreateRoot();
            Camera camera = CameraRigBuilder.BuildCamera(preacher);
            var globalLight = CameraRigBuilder.BuildGlobalLight();
            GameObject world = WorldLayerBuilder.Build(camera);
            PreacherBuilder.Wire(preacher, world);

            var runner = preacher.GetComponent<BuenosDias.Gameplay.SkillcheckRunner>();
            SkillcheckRigBuilder.Build(camera, runner);

            // La capa de partida se crea antes del reloj porque la barra de tiempo
            // necesita saber en qué etapa está, y se cierra después porque el dueño
            // de la partida necesita el reloj. Es el mismo lazo que el predicador.
            BuenosDias.Gameplay.RunDirector run = RunStateBuilder.CreateRun(preacher);
            BuenosDias.Gameplay.DayDirector day = RunRigBuilder.Build(
                camera, globalLight, preacher, runner,
                world.GetComponentInChildren<BuenosDias.Gameplay.HouseSpawner>(), run);
            RunStateBuilder.WireDay(run, day);
            RunStateBuilder.BuildHud(camera, run, day, runner);
            RunStateBuilder.BuildScreen(camera, run);
            EndingRigBuilder.Build(camera, run);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();

            AssetDatabase.Refresh();

            return BuildReport(createdLayers, camera);
        }

        private static void RegisterInBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);

            if (scenes.Exists(s => s.path == ScenePath)) return;

            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// Informa las banderas que la reconstrucción devolvió a su valor de
        /// fábrica.
        ///
        /// ⚠️ Existe porque reconstruir la escena **borra en silencio todo lo que
        /// alguien haya tildado a mano**, y las dos primeras veces se llevó puestas
        /// la modalidad de input —el que juega la había puesto en dos botones— y
        /// `acceptNoisyControls`, que estaba prendido a propósito para la prueba del
        /// gabinete (§13.8). Ninguna de las dos falla: el juego arranca igual, con
        /// otro esquema de control.
        ///
        /// No se hornean los valores "correctos" acá a propósito: no son del
        /// builder, son de quien está jugando ese día. Lo que sí es del builder es
        /// DECIR con qué quedaron, para que la diferencia se vea en el momento y no
        /// dos partidas después.
        /// </summary>
        private static void AppendResetFlags(StringBuilder sb)
        {
            var input = Object.FindAnyObjectByType<BuenosDias.Gameplay.GameInput>();
            var button = Object.FindAnyObjectByType<BuenosDias.Gameplay.OneButtonInput>();

            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("  ⚠ Banderas que volvieron a fábrica. Si alguna no es la");
            sb.AppendLine("    que querías, hay que tildarla de nuevo a mano:");
            sb.AppendLine($"      Modalidad de input.... {(input != null ? input.Mode.ToString() : "SIN GameInput")}");
            sb.Append($"      acceptNoisyControls... {(button != null ? Noisy(button).ToString() : "SIN OneButtonInput")}");
        }

        private static bool Noisy(BuenosDias.Gameplay.OneButtonInput button)
        {
            var so = new SerializedObject(button);
            return so.FindProperty("acceptNoisyControls").boolValue;
        }

        private static string BuildReport(int createdLayers, Camera camera)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Fase 1] Escena construida.");
            sb.AppendLine($"  Ruta................ {ScenePath}");
            sb.AppendLine($"  Sorting layers...... {createdLayers} creados de " +
                          $"{SortingLayerAuthoring.Ordered.Length}");
            sb.AppendLine($"  Orthographic size... {camera.orthographicSize} " +
                          $"(esperado {SceneLayout.OrthographicSize})");
            sb.AppendLine($"  Centro de cámara Y.. {camera.transform.position.y} " +
                          $"(esperado {SceneLayout.CameraCenterY})");
            sb.AppendLine($"  Borde inferior Y.... {SceneLayout.ScreenBottomY}");
            sb.Append("  Grid snapping....... UpscaleRenderTexture + filtro Point");
            AppendResetFlags(sb);
            return sb.ToString();
        }
    }
}
