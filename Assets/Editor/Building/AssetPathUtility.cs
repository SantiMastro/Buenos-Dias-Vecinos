using UnityEditor;

namespace BuenosDias.EditorTools.Building
{
    /// <summary>Ayudas de rutas para los builders de assets.</summary>
    public static class AssetPathUtility
    {
        /// <summary>
        /// Crea la carpeta y todas sus padres si no existen.
        /// <c>AssetDatabase.CreateFolder</c> solo crea un nivel por vez, así que
        /// sin esto hay que acordarse del orden a mano.
        ///
        /// La existencia se pregunta al DISCO y no a <c>IsValidFolder</c>: dentro
        /// de un <c>StartAssetEditing</c> el importador está en pausa y la
        /// AssetDatabase sigue sin ver una carpeta recién creada. Como
        /// <c>CreateFolder</c> ante un nombre tomado no falla sino que inventa uno
        /// libre, cada llamada dejaba una "Vecinos 1", "Vecinos 2"... vacía.
        /// </summary>
        public static void EnsureFolder(string folderPath)
        {
            if (System.IO.Directory.Exists(folderPath)) return;

            string parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folderPath);

            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
