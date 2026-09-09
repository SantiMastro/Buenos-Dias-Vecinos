using UnityEditor;
using UnityEngine;

namespace BuenosDias.EditorTools.Authoring
{
    /// <summary>
    /// Escribe campos privados marcados con <c>[SerializeField]</c> desde código
    /// de Editor.
    ///
    /// Hace falta porque las reglas del proyecto piden campos privados en vez de
    /// públicos: sin esto, un builder tendría que exponer setters públicos que
    /// solo existirían para el builder, que es exactamente el acoplamiento que
    /// se quiere evitar.
    /// </summary>
    public static class SerializedFieldUtility
    {
        /// <summary>Asigna una referencia a objeto (componente, transform, asset).</summary>
        public static void SetReference(Object target, string fieldName, Object value)
        {
            Apply(target, fieldName, p => p.objectReferenceValue = value);
        }

        /// <summary>Asigna un valor numérico de punto flotante.</summary>
        public static void SetFloat(Object target, string fieldName, float value)
        {
            Apply(target, fieldName, p => p.floatValue = value);
        }

        /// <summary>Asigna un entero, o el índice de un enum.</summary>
        public static void SetInt(Object target, string fieldName, int value)
        {
            Apply(target, fieldName, p => p.intValue = value);
        }

        /// <summary>Asigna un booleano.</summary>
        public static void SetBool(Object target, string fieldName, bool value)
        {
            Apply(target, fieldName, p => p.boolValue = value);
        }

        /// <summary>Asigna un enum por su valor entero.</summary>
        public static void SetEnum(Object target, string fieldName, int enumValue)
        {
            Apply(target, fieldName, p => p.enumValueIndex = enumValue);
        }

        /// <summary>
        /// Llena una lista o array de referencias. Reemplaza lo que hubiera: los
        /// builders son idempotentes, así que correrlos dos veces no puede dejar
        /// la lista con el doble de entradas.
        /// </summary>
        public static void SetReferenceList(Object target, string fieldName, Object[] values)
        {
            Apply(target, fieldName, p =>
            {
                p.ClearArray();
                for (int i = 0; i < values.Length; i++)
                {
                    p.InsertArrayElementAtIndex(i);
                    p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                }
            });
        }

        private static void Apply(Object target, string fieldName, System.Action<SerializedProperty> write)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);

            if (prop == null)
            {
                Debug.LogError(
                    $"[SerializedFieldUtility] '{target.GetType().Name}' no tiene un campo " +
                    $"serializado llamado '{fieldName}'. ¿Lo renombraste?", target);
                return;
            }

            write(prop);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
