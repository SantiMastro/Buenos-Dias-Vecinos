using BuenosDias.EditorTools.Building;
using BuenosDias.Gameplay;
using UnityEditor;
using UnityEngine;

namespace BuenosDias.EditorTools.Authoring
{
    /// <summary>
    /// Cablea las referencias serializadas de <see cref="HouseInstance"/> y de su
    /// montador de señales.
    ///
    /// Va aparte del builder porque son dos trabajos distintos: uno crea la
    /// jerarquía y el otro conecta campos privados con <c>SerializedObject</c>.
    /// </summary>
    public static class HousePrefabWiring
    {
        /// <summary>Las piezas de un slot de prop, antes de cablearse.</summary>
        public readonly struct PropSlotParts
        {
            /// <summary>Contenedor que se mueve al colocar la señal.</summary>
            public Transform Root { get; }

            /// <summary>Renderer del prop.</summary>
            public SpriteRenderer Prop { get; }

            /// <summary>Renderer de la capa de luz aditiva.</summary>
            public SpriteRenderer Glow { get; }

            /// <summary>Animator de la capa de luz.</summary>
            public Animator GlowAnimator { get; }

            /// <summary>Agrupa las piezas creadas por el builder.</summary>
            public PropSlotParts(
                Transform root, SpriteRenderer prop, SpriteRenderer glow, Animator animator)
            {
                Root = root;
                Prop = prop;
                Glow = glow;
                GlowAnimator = animator;
            }
        }

        /// <summary>Conecta las piezas estructurales de la casa.</summary>
        public static void WireStructure(
            HouseInstance instance, SpriteRenderer wall, SpriteRenderer slab,
            SpriteRenderer gable, SpriteRenderer fence, Transform door, Transform tellWindow)
        {
            var so = new SerializedObject(instance);
            so.FindProperty("wall").objectReferenceValue = wall;
            so.FindProperty("roofSlab").objectReferenceValue = slab;
            so.FindProperty("roofGable").objectReferenceValue = gable;
            so.FindProperty("fence").objectReferenceValue = fence;
            so.FindProperty("door").objectReferenceValue = door;
            so.FindProperty("tellWindow").objectReferenceValue = tellWindow;

            // La HOJA se busca por nombre entre los hijos de la puerta, como el
            // halo de la ventana: del transform de la puerta cuelgan también el
            // timbre y el portón, así que un GetComponentInChildren traería
            // cualquiera de los tres.
            Transform leaf = door.Find("DoorSprite");
            so.FindProperty("doorLeaf").objectReferenceValue =
                leaf != null ? leaf.GetComponent<SpriteRenderer>() : null;
            so.FindProperty("openDoorSprite").objectReferenceValue =
                SpriteLibrary.Load("env_puerta_abierta");

            // El animator de la cortina cuelga del hijo Curtain. Se busca acá y no
            // se pasa por parámetro porque es la única pieza de la ventana de tell
            // que el runtime necesita, y el builder ya devuelve la ventana entera.
            so.FindProperty("curtainAnimator").objectReferenceValue =
                tellWindow.GetComponentInChildren<Animator>();

            // El halo se busca POR NOMBRE y no con GetComponentInChildren: la
            // ventana tiene su propio SpriteRenderer y sería el primero en salir.
            Transform glow = tellWindow.Find("Glow");
            so.FindProperty("windowGlow").objectReferenceValue =
                glow != null ? glow.GetComponent<SpriteRenderer>() : null;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Conecta los slots de señal, las franjas y la ventana de señal.</summary>
        public static void WireMounter(
            HouseInstance instance, PropSlotParts[] slots,
            SpriteRenderer[] segments, Transform signalWindow)
        {
            var so = new SerializedObject(instance);
            SerializedProperty mounter = so.FindProperty("signals");

            SerializedProperty slotList = mounter.FindPropertyRelative("propSlots");
            slotList.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
            {
                SerializedProperty element = slotList.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("root").objectReferenceValue = slots[i].Root;
                element.FindPropertyRelative("prop").objectReferenceValue = slots[i].Prop;
                element.FindPropertyRelative("glow").objectReferenceValue = slots[i].Glow;
                element.FindPropertyRelative("glowAnimator").objectReferenceValue =
                    slots[i].GlowAnimator;
            }

            SerializedProperty stripList = mounter.FindPropertyRelative("stripSegments");
            stripList.arraySize = segments.Length;
            for (int i = 0; i < segments.Length; i++)
                stripList.GetArrayElementAtIndex(i).objectReferenceValue = segments[i];

            mounter.FindPropertyRelative("signalWindow").objectReferenceValue =
                signalWindow.GetComponent<SpriteRenderer>();
            mounter.FindPropertyRelative("plainWindowSprite").objectReferenceValue =
                SpriteLibrary.Load("env_ventana_normal");

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
