using System.Collections.Generic;
using BuenosDias.Config;
using BuenosDias.Gameplay;
using BuenosDias.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BuenosDias.EditorTools.Authoring
{
    /// <summary>
    /// Arma lo que corre durante la partida y no es ni mundo ni skillcheck: el
    /// reloj del día, el pintor del cielo y la fila de seguidores.
    /// </summary>
    public static class RunRigBuilder
    {
        private const string ConfigPath = "Assets/ScriptableObjects/Config/GameConfig.asset";
        private const string FollowerControllerPath =
            "Assets/Animations/Seguidores/Seguidor.controller";
        private const string LooksFolder = "Assets/Animations/Seguidores";

        /// <summary>Cuántos tipos de seguidor buscar. Hoy son cinco.</summary>
        private const int LookCount = 5;

        /// <summary>
        /// El seguidor va DETRÁS del predicador, que dibuja en 10. Con el mismo
        /// orden, quién queda encima lo decidiría el orden de creación.
        /// </summary>
        private const int FollowerSortingOrder = 8;

        /// <summary>Z de la barra: la misma del skillcheck, por delante del mundo.</summary>
        private const float RingZ = -5f;

        /// <summary>
        /// Orden de la barra dentro de FX. Por encima del skillcheck (100–102): si
        /// el aro la tapara, el jugador perdería el reloj justo en los segundos en
        /// que más lo necesita.
        /// </summary>
        private const int BarOrder = 110;

        /// <summary>
        /// Orden del indicador de timbre. Muy por debajo del skillcheck y de la
        /// barra: es una ayuda de la calle, no lectura de pantalla, y no tiene por
        /// qué taparle nada a nadie.
        /// </summary>
        private const int PromptOrder = 50;

        /// <summary>
        /// Cuánto se corren el sol y la luna desde el centro. El marco mide 128, o
        /// sea que termina en 64; más 2 de aire y 8 de medio ícono dan 74.
        /// </summary>
        private const float AstroOffsetPixels = 74f;

        /// <summary>
        /// Arma el reloj, el ciclo de día, las luces y la comitiva. Devuelve el
        /// reloj para que el dueño de la partida pueda cerrar su lazo.
        /// </summary>
        public static DayDirector Build(
            Camera camera, Light2D globalLight, Transform preacher,
            SkillcheckRunner runner, HouseSpawner spawner, RunDirector run)
        {
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);

            var director = preacher.gameObject.AddComponent<DayDirector>();
            SerializedFieldUtility.SetReference(director, "gameConfig", config);
            SerializedFieldUtility.SetReference(director, "skillcheck", runner);

            // El predicador necesita el reloj para saber cuánto aceleró el día, y
            // el reloj se crea recién acá. Es el mismo lazo en dos pasos que ya
            // tienen la cámara y el dueño de la partida.
            SerializedFieldUtility.SetReference(
                preacher.GetComponent<PreacherController>(), "day", director);

            BuildCycleView(camera, globalLight, director, config, spawner);
            BuildParade(preacher, runner, config);
            BuildTimeBar(camera, director, config, run);
            BuildDoorPrompt(preacher, spawner, run, config);

            return director;
        }

        /// <summary>
        /// La barra cuelga de la cámara, como el skillcheck: es lectura del
        /// jugador, no un objeto de la calle.
        /// </summary>
        private static void BuildTimeBar(
            Camera camera, DayDirector director, GameConfig config, RunDirector run)
        {
            var root = new GameObject("Barra de tiempo");
            root.transform.SetParent(camera.transform, false);

            // La Y la fija el propio componente cada cuadro, anclada al borde de
            // pantalla. Acá solo importa la Z: local a la cámara, que está en -10.
            root.transform.localPosition = new Vector3(0f, 0f, RingZ - SceneLayout.CameraZ);

            SpriteRenderer track = AddBarLayer(root.transform, "Riel", 0,
                new Color32(0x1D, 0x16, 0x38, 0xFF));
            SpriteRenderer fill = AddBarLayer(root.transform, "Relleno", 1,
                new Color32(0xF3, 0xEC, 0xE0, 0xFF));

            // El marco va ENCIMA del relleno: el relleno llena el hueco de 124×8 y
            // el marco le dibuja el borde de 128×12 alrededor. Al revés, el relleno
            // taparía el borde justo cuando la barra está llena.
            SpriteRenderer frame = AddBarSprite(root.transform, "Marco", "ui_marco_barra", 2, 0f);
            SpriteRenderer sun = AddBarSprite(
                root.transform, "Sol", "ui_icono_sol", 3, -AstroOffsetPixels);
            SpriteRenderer moon = AddBarSprite(
                root.transform, "Luna", "ui_icono_luna", 3, AstroOffsetPixels);

            var view = root.AddComponent<TimeBarView>();
            SerializedFieldUtility.SetReference(view, "director", director);
            SerializedFieldUtility.SetReference(view, "runDirector", run);
            SerializedFieldUtility.SetReference(view, "gameConfig", config);
            SerializedFieldUtility.SetReference(view, "targetCamera", camera);
            SerializedFieldUtility.SetReference(view, "track", track);
            SerializedFieldUtility.SetReference(view, "fill", fill);
            SerializedFieldUtility.SetReference(view, "frame", frame);
            SerializedFieldUtility.SetReference(view, "sunIcon", sun);
            SerializedFieldUtility.SetReference(view, "moonIcon", moon);
        }

        /// <summary>
        /// Una pieza de la barra que SÍ es un sprite dibujado, a diferencia del riel
        /// y el relleno, que son rectángulos planos generados en runtime.
        ///
        /// La posición vive en el transform del hijo y no en el componente: es fija
        /// respecto de la barra, así que recalcularla cada cuadro sería inventar un
        /// número que ya está en la escena.
        /// </summary>
        private static SpriteRenderer AddBarSprite(
            Transform parent, string name, string spriteName, int orderOffset, float xPixels)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(
                ProjectConstants.ToUnits(xPixels), 0f, 0f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteLibrary.Load(spriteName);
            renderer.sortingLayerName = SortingLayerAuthoring.Fx;
            renderer.sortingOrder = BarOrder + orderOffset;
            Building.UiMaterialFactory.Apply(renderer);

            return renderer;
        }

        /// <summary>
        /// El chevrón de "acá podés tocar". Va suelto en el mundo y NO colgado de
        /// la casa: la casa vive en un pool y se recicla, así que un indicador que
        /// fuera hijo suyo tendría que apagarse y encenderse en cada reciclado.
        /// </summary>
        private static void BuildDoorPrompt(
            Transform preacher, HouseSpawner spawner, RunDirector run, GameConfig config)
        {
            var go = new GameObject("Indicador de timbre");

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = SortingLayerAuthoring.Fx;
            renderer.sortingOrder = PromptOrder;
            Building.UiMaterialFactory.Apply(renderer);

            var view = go.AddComponent<DoorPromptView>();
            SerializedFieldUtility.SetReference(view, "preacher",
                preacher.GetComponent<PreacherController>());
            SerializedFieldUtility.SetReference(view, "houseSpawner", spawner);
            SerializedFieldUtility.SetReference(view, "runDirector", run);
            SerializedFieldUtility.SetReference(view, "gameConfig", config);
            SerializedFieldUtility.SetReference(view, "icon", renderer);
        }

        private static SpriteRenderer AddBarLayer(
            Transform parent, string name, int orderOffset, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = SortingLayerAuthoring.Fx;
            renderer.sortingOrder = BarOrder + orderOffset;
            renderer.color = color;
            Building.UiMaterialFactory.Apply(renderer);

            return renderer;
        }

        private static void BuildCycleView(
            Camera camera, Light2D globalLight, DayDirector director,
            GameConfig config, HouseSpawner spawner)
        {
            var go = new GameObject("Ciclo de día");

            var view = go.AddComponent<DayCycleView>();
            SerializedFieldUtility.SetReference(view, "director", director);
            SerializedFieldUtility.SetReference(view, "gameConfig", config);
            SerializedFieldUtility.SetReference(view, "targetCamera", camera);
            SerializedFieldUtility.SetReference(view, "globalLight", globalLight);

            // Las ventanas van en su propio componente: el que pinta el cielo no
            // tiene por qué conocer al spawner de casas.
            var windows = go.AddComponent<WindowLightView>();
            SerializedFieldUtility.SetReference(windows, "director", director);
            SerializedFieldUtility.SetReference(windows, "gameConfig", config);
            SerializedFieldUtility.SetReference(windows, "houseSpawner", spawner);
        }

        private static void BuildParade(
            Transform preacher, SkillcheckRunner runner, GameConfig config)
        {
            var go = new GameObject("Comitiva");
            var parade = go.AddComponent<FollowerParade>();

            SpriteRenderer template = BuildTemplate(go.transform);

            SerializedFieldUtility.SetReference(parade, "skillcheck", runner);
            SerializedFieldUtility.SetReference(parade, "gameConfig", config);
            SerializedFieldUtility.SetReference(parade, "preacher", preacher);
            SerializedFieldUtility.SetReference(parade, "followerPrefab", template);
            SerializedFieldUtility.SetReferenceList(parade, "looks", LoadLooks());
        }

        /// <summary>
        /// La plantilla del pool. Queda APAGADA en la escena: el pool clona desde
        /// ella y prende las copias, así que si quedara prendida se vería un
        /// seguidor de más, parado en el origen y sin seguir a nadie.
        /// </summary>
        private static SpriteRenderer BuildTemplate(Transform parent)
        {
            var go = new GameObject("Seguidor (plantilla)");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, 0f, SceneLayout.CharactersZ);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteLibrary.Load("chr_seguidor_01_walk_00");
            renderer.sortingLayerName = SortingLayerAuthoring.Characters;
            renderer.sortingOrder = FollowerSortingOrder;

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                FollowerControllerPath);
            if (controller != null)
                go.AddComponent<Animator>().runtimeAnimatorController = controller;

            go.SetActive(false);
            return renderer;
        }

        /// <summary>
        /// Los overrides de cada tipo de seguidor. Si falta alguno se sigue sin
        /// él: la fila anda igual, solo que con menos variedad de caras.
        /// </summary>
        private static Object[] LoadLooks()
        {
            var found = new List<Object>();

            for (int i = 1; i <= LookCount; i++)
            {
                string path = $"{LooksFolder}/Seguidor_{i:00}.overrideController";
                var look = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
                if (look != null) found.Add(look);
            }

            return found.ToArray();
        }
    }
}
