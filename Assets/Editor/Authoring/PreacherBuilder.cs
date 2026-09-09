using BuenosDias.Config;
using BuenosDias.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BuenosDias.EditorTools.Authoring
{
    /// <summary>
    /// Arma el predicador en la escena y lo cablea al mundo.
    ///
    /// Va en dos pasos porque hay una dependencia circular de armado: la cámara
    /// necesita seguir al predicador, y el predicador necesita el spawner de
    /// casas, que se crea junto con la cámara. Primero el transform, después el
    /// cableado.
    /// </summary>
    public static class PreacherBuilder
    {
        private const string ControllerPath = "Assets/Animations/Predicador/Predicador.controller";
        private const string ReligionPath = "Assets/ScriptableObjects/Religions/Testigos.asset";
        private const string ConfigPath = "Assets/ScriptableObjects/Config/GameConfig.asset";

        /// <summary>
        /// Crea el predicador con su sprite. Todavía sin cablear: en este punto el
        /// mundo no existe.
        /// </summary>
        public static Transform CreateRoot()
        {
            var root = new GameObject("Predicador");
            root.transform.position = new Vector3(0f, 0f, SceneLayout.CharactersZ);

            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteLibrary.Load("chr_predicador_walk_00");
            renderer.sortingLayerName = SortingLayerAuthoring.Characters;
            renderer.sortingOrder = 10;

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller != null)
                root.AddComponent<Animator>().runtimeAnimatorController = controller;

            // El lector crudo del boton y la capa de acciones que lo envuelve.
            // GameInput es lo que consume todo el juego; OneButtonInput ya no lo
            // lee nadie directo, solo la modalidad de teclado por dentro.
            var button = root.AddComponent<OneButtonInput>();
            var input = root.AddComponent<GameInput>();
            SerializedFieldUtility.SetReference(input, "button", button);

            root.AddComponent<SkillcheckRunner>();
            root.AddComponent<PreacherController>();

            return root.transform;
        }

        /// <summary>Conecta el predicador con el spawner de casas y los datos.</summary>
        public static void Wire(Transform preacher, GameObject world)
        {
            var controller = preacher.GetComponent<PreacherController>();
            var runner = preacher.GetComponent<SkillcheckRunner>();
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);

            SerializedFieldUtility.SetReference(controller, "gameConfig", config);
            SerializedFieldUtility.SetReference(controller, "houseSpawner",
                world.GetComponentInChildren<HouseSpawner>());
            SerializedFieldUtility.SetReference(controller, "input",
                preacher.GetComponent<GameInput>());
            SerializedFieldUtility.SetReference(controller, "skillcheck", runner);
            SerializedFieldUtility.SetReference(controller, "religion",
                AssetDatabase.LoadAssetAtPath<ReligionDefinition>(ReligionPath));

            SerializedFieldUtility.SetReference(runner, "gameConfig", config);
        }
    }
}
