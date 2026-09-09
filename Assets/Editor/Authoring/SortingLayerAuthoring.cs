using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BuenosDias.EditorTools.Authoring
{
    /// <summary>
    /// Crea los sorting layers del proyecto si faltan, en el orden correcto.
    /// El orden de dibujo en 2D lo gobiernan estos layers, no la Z.
    /// </summary>
    public static class SortingLayerAuthoring
    {
        /// <summary>Skyline lejano.</summary>
        public const string Background = "Background";

        /// <summary>Árboles y postes del fondo medio.</summary>
        public const string Far = "Far";

        /// <summary>Vereda y calle.</summary>
        public const string Ground = "Ground";

        /// <summary>Paredes, techos, puertas y ventanas.</summary>
        public const string Houses = "Houses";

        /// <summary>Rejas, timbres, señales y props de las casas.</summary>
        public const string HouseDetails = "HouseDetails";

        /// <summary>Predicador, seguidores y vecinos.</summary>
        public const string Characters = "Characters";

        /// <summary>Arbustos que tapan el borde inferior del cuadro.</summary>
        public const string Foreground = "Foreground";

        /// <summary>Ondas de timbre, halos de luz y demás efectos.</summary>
        public const string Fx = "FX";

        /// <summary>Los layers del proyecto, del fondo hacia el frente.</summary>
        public static readonly string[] Ordered =
        {
            Background, Far, Ground, Houses, HouseDetails, Characters, Foreground, Fx
        };

        private const string TagManagerPath = "ProjectSettings/TagManager.asset";

        /// <summary>
        /// Agrega los layers que falten, conservando los existentes. Devuelve
        /// cuántos creó.
        /// </summary>
        public static int EnsureLayers()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(TagManagerPath);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError($"[SortingLayerAuthoring] No pude abrir {TagManagerPath}.");
                return 0;
            }

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("m_SortingLayers");

            var existing = new HashSet<string>();
            var usedIds = new HashSet<int>();
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty element = layers.GetArrayElementAtIndex(i);
                existing.Add(element.FindPropertyRelative("name").stringValue);
                usedIds.Add(element.FindPropertyRelative("uniqueID").intValue);
            }

            int created = 0;
            foreach (string layerName in Ordered)
            {
                if (existing.Contains(layerName)) continue;

                layers.InsertArrayElementAtIndex(layers.arraySize);
                SerializedProperty element = layers.GetArrayElementAtIndex(layers.arraySize - 1);
                element.FindPropertyRelative("name").stringValue = layerName;
                element.FindPropertyRelative("uniqueID").intValue = NextFreeId(layerName, usedIds);
                element.FindPropertyRelative("locked").boolValue = false;
                created++;
            }

            if (created > 0) tagManager.ApplyModifiedProperties();
            return created;
        }

        /// <summary>
        /// Deriva un id estable del nombre. Estable importa: si el id cambiara
        /// entre corridas, los SpriteRenderer ya guardados perderían su layer.
        /// </summary>
        private static int NextFreeId(string layerName, HashSet<int> usedIds)
        {
            int id = StableHash(layerName);
            while (id == 0 || usedIds.Contains(id)) id++;
            usedIds.Add(id);
            return id;
        }

        private static int StableHash(string text)
        {
            // FNV-1a de 32 bits. No usamos string.GetHashCode porque no está
            // garantizado que sea igual entre versiones del runtime.
            unchecked
            {
                const uint offsetBasis = 2166136261;
                const uint prime = 16777619;

                uint hash = offsetBasis;
                foreach (char c in text)
                {
                    hash ^= c;
                    hash *= prime;
                }
                return (int)(hash & 0x7FFFFFFF);
            }
        }
    }
}
