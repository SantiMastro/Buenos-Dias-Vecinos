using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BuenosDias.EditorTools.Building
{
    /// <summary>
    /// Construye AnimatorControllers con estados y transiciones visibles, y sus
    /// AnimatorOverrideControllers.
    ///
    /// El grafo se mantiene deliberadamente tonto: todas las transiciones salen
    /// de AnyState según un entero. Quién y cuándo cambia ese entero es
    /// responsabilidad de la FSM del juego. Meter tiempos de salida o encadenar
    /// estados acá metería lógica de juego en la vista.
    /// </summary>
    public static class AnimatorGraphFactory
    {
        /// <summary>Un estado del grafo y el valor de parámetro que lo activa.</summary>
        public readonly struct StateDefinition
        {
            /// <summary>Nombre visible del estado en el Animator.</summary>
            public string Name { get; }

            /// <summary>Clip que reproduce.</summary>
            public AnimationClip Clip { get; }

            /// <summary>Valor del parámetro entero que activa este estado.</summary>
            public int Value { get; }

            /// <summary>Define un estado del grafo.</summary>
            public StateDefinition(string name, AnimationClip clip, int value)
            {
                Name = name;
                Clip = clip;
                Value = value;
            }
        }

        /// <summary>
        /// Crea o reconstruye un controller. Si <paramref name="parameterName"/>
        /// viene vacío el grafo queda con un solo estado y sin parámetros, que es
        /// lo que necesitan seguidores, ondas y cortina.
        /// </summary>
        public static AnimatorController Build(
            string assetPath, string parameterName, IReadOnlyList<StateDefinition> states)
        {
            AnimatorController controller = LoadOrCreate(assetPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            ClearGraph(controller, machine);

            bool useParameter = !string.IsNullOrEmpty(parameterName);
            if (useParameter) controller.AddParameter(parameterName, AnimatorControllerParameterType.Int);

            for (int i = 0; i < states.Count; i++)
            {
                StateDefinition definition = states[i];
                AnimatorState state = machine.AddState(definition.Name);
                state.motion = definition.Clip;

                if (i == 0) machine.defaultState = state;
                if (!useParameter) continue;

                AnimatorStateTransition transition = machine.AddAnyStateTransition(state);
                transition.AddCondition(AnimatorConditionMode.Equals, definition.Value, parameterName);

                // Sin crossfade: mezclar dos sprites deja frames intermedios
                // fantasma. Y sin exit time, para que el cambio sea inmediato.
                transition.hasFixedDuration = true;
                transition.duration = 0f;
                transition.hasExitTime = false;
                transition.canTransitionToSelf = false;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// Grafo de reposo + golpe: un estado quieto por defecto y otro que se
        /// dispara con un Trigger y vuelve solo al terminar.
        ///
        /// Un entero no sirve para esto: como las transiciones no pueden ir a sí
        /// mismas, para repetir el golpe habría que bajar el valor y volver a
        /// subirlo desde el juego. El Trigger se consume solo, así que el juego
        /// solo dice "ahora" y el volver al reposo queda del lado de la vista,
        /// que es donde corresponde.
        /// </summary>
        public static AnimatorController BuildTriggeredOneShot(
            string assetPath, string triggerName,
            StateDefinition restState, StateDefinition oneShotState)
        {
            AnimatorController controller = LoadOrCreate(assetPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            ClearGraph(controller, machine);
            controller.AddParameter(triggerName, AnimatorControllerParameterType.Trigger);

            AnimatorState rest = machine.AddState(restState.Name);
            rest.motion = restState.Clip;
            machine.defaultState = rest;

            AnimatorState oneShot = machine.AddState(oneShotState.Name);
            oneShot.motion = oneShotState.Clip;

            AnimatorStateTransition fire = machine.AddAnyStateTransition(oneShot);
            fire.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
            fire.hasFixedDuration = true;
            fire.duration = 0f;
            fire.hasExitTime = false;
            fire.canTransitionToSelf = false;

            AnimatorStateTransition back = oneShot.AddTransition(rest);
            back.hasFixedDuration = true;
            back.duration = 0f;
            back.hasExitTime = true;
            back.exitTime = 1f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// Crea o reconstruye un override que reemplaza clips del controller base.
        /// Es el mecanismo para las variantes de seguidor y de vecino, y el hook
        /// por el que va a entrar la cortina real sin tocar código.
        /// </summary>
        public static AnimatorOverrideController BuildOverride(
            AnimatorController baseController,
            IReadOnlyDictionary<string, AnimationClip> replacementsByStateClipName,
            string assetPath)
        {
            var overrideController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(assetPath);
            bool isNew = overrideController == null;

            if (isNew) overrideController = new AnimatorOverrideController();
            overrideController.runtimeAnimatorController = baseController;

            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(pairs);

            for (int i = 0; i < pairs.Count; i++)
            {
                AnimationClip original = pairs[i].Key;
                if (original == null) continue;

                // Value en null significa "sin override": se usa el clip base.
                AnimationClip replacement =
                    replacementsByStateClipName.TryGetValue(original.name, out AnimationClip found)
                        ? found
                        : null;

                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, replacement);
            }

            overrideController.ApplyOverrides(pairs);

            if (isNew)
            {
                AssetPathUtility.EnsureFolder(
                    System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));
                AssetDatabase.CreateAsset(overrideController, assetPath);
            }
            else
            {
                EditorUtility.SetDirty(overrideController);
            }

            return overrideController;
        }

        private static AnimatorController LoadOrCreate(string assetPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);
            if (existing != null) return existing;

            AssetPathUtility.EnsureFolder(
                System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));
            return AnimatorController.CreateAnimatorControllerAtPath(assetPath);
        }

        /// <summary>
        /// Vacía el grafo conservando el asset. Reconstruir en el lugar mantiene
        /// el GUID, así los prefabs que ya apuntan al controller no se rompen.
        /// </summary>
        private static void ClearGraph(AnimatorController controller, AnimatorStateMachine machine)
        {
            foreach (AnimatorStateTransition transition in machine.anyStateTransitions.ToArray())
                machine.RemoveAnyStateTransition(transition);

            foreach (ChildAnimatorState child in machine.states.ToArray())
                machine.RemoveState(child.state);

            foreach (AnimatorControllerParameter parameter in controller.parameters.ToArray())
                controller.RemoveParameter(parameter);
        }
    }
}
