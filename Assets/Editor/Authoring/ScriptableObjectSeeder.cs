using BuenosDias.EditorTools.Building;
using UnityEditor;
using UnityEngine;

namespace BuenosDias.EditorTools.Authoring
{
    /// <summary>
    /// Crea o recupera assets de ScriptableObject en una ruta dada.
    ///
    /// Nunca pisa un asset existente: si ya está, lo devuelve tal cual. Eso es
    /// deliberado — el seeder se puede volver a correr para completar lo que
    /// falte sin borrar el balance que el diseñador ya ajustó a mano.
    /// </summary>
    public static class ScriptableObjectSeeder
    {
        /// <summary>Raíz de los assets de datos.</summary>
        public const string Root = "Assets/ScriptableObjects";

        /// <summary>
        /// Devuelve el asset de <paramref name="path"/>, creándolo si no existe.
        /// <paramref name="created"/> queda en <c>true</c> solo si lo tuvo que crear.
        /// </summary>
        public static T GetOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                created = false;
                return existing;
            }

            AssetPathUtility.EnsureFolder(
                System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            created = true;
            return asset;
        }

        /// <summary>Escribe un campo privado serializado del asset.</summary>
        public static void Set(ScriptableObject asset, string field, object value)
        {
            var so = new SerializedObject(asset);
            SerializedProperty property = so.FindProperty(field);

            if (property == null)
            {
                Debug.LogError(
                    $"[Seeder] '{asset.GetType().Name}' no tiene el campo '{field}'.", asset);
                return;
            }

            switch (value)
            {
                case string s: property.stringValue = s; break;
                case bool b: property.boolValue = b; break;
                case int i when property.propertyType == SerializedPropertyType.Enum:
                    property.enumValueIndex = i; break;
                case int i: property.intValue = i; break;
                case float f: property.floatValue = f; break;
                case Color c: property.colorValue = c; break;
                case Object o: property.objectReferenceValue = o; break;
                default:
                    Debug.LogError($"[Seeder] Tipo no soportado para '{field}': {value?.GetType()}");
                    break;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Llena una lista serializada de referencias a objeto.</summary>
        public static void SetList(ScriptableObject asset, string field, params Object[] items)
        {
            var so = new SerializedObject(asset);
            SerializedProperty list = so.FindProperty(field);

            if (list == null || !list.isArray)
            {
                Debug.LogError($"[Seeder] '{field}' no es una lista en {asset.name}.", asset);
                return;
            }

            list.ClearArray();
            for (int i = 0; i < items.Length; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
