using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BuenosDias.EditorTools.Authoring
{
    /// <summary>
    /// Busca sprites por nombre de archivo dentro de Assets/Sprites.
    ///
    /// Existe para que los builders no lleven rutas literales pegadas: si mañana
    /// se reordenan las subcarpetas, el índice se rearma solo y nada se rompe.
    /// Es una herramienta de Editor: nada de esto corre en el juego.
    /// </summary>
    public static class SpriteLibrary
    {
        private const string SpritesRoot = "Assets/Sprites";

        private static Dictionary<string, string> pathsByName;

        /// <summary>Fuerza a releer la carpeta en la próxima consulta.</summary>
        public static void Invalidate() => pathsByName = null;

        /// <summary>
        /// Devuelve el sprite cuyo archivo se llama <paramref name="spriteName"/>
        /// (sin extensión), o <c>null</c> si no existe, dejando un error en consola.
        /// </summary>
        public static Sprite Load(string spriteName)
        {
            BuildIndexIfNeeded();

            if (!pathsByName.TryGetValue(spriteName, out string path))
            {
                Debug.LogError(
                    $"[SpriteLibrary] No encontré '{spriteName}.png' bajo {SpritesRoot}.");
                return null;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError(
                    $"[SpriteLibrary] '{path}' existe pero no importó como Sprite. " +
                    "Reimportá la carpeta Sprites.");
            }
            return sprite;
        }

        /// <summary>Indica si existe un sprite con ese nombre.</summary>
        public static bool Exists(string spriteName)
        {
            BuildIndexIfNeeded();
            return pathsByName.ContainsKey(spriteName);
        }

        private static void BuildIndexIfNeeded()
        {
            if (pathsByName != null) return;

            pathsByName = new Dictionary<string, string>();

            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { SpritesRoot });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                pathsByName[name] = path;
            }
        }
    }
}
