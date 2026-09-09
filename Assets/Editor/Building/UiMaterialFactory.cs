using UnityEditor;
using UnityEngine;

namespace BuenosDias.EditorTools.Building
{
    /// <summary>
    /// El material de la UI y de las cinemáticas: sprites SIN iluminar.
    ///
    /// ⚠️ El default de un <c>SpriteRenderer</c> en URP 2D es
    /// <c>Sprite-Lit-Default</c>, así que **la Global Light 2D multiplica también a
    /// la interfaz**. Con el ciclo de día de la fase 7, eso hacía que al anochecer
    /// se apagaran la barra de tiempo, el aro del skillcheck y los carteles junto
    /// con el barrio: el jugador perdía la lectura justo cuando más la necesita, y
    /// un color elegido de la paleta dejaba de ser ese color en pantalla.
    ///
    /// La regla es simple: **la UI no es parte del mundo y no la ilumina el sol.**
    /// Todo lo que se dibuja para que el jugador lea —y no para que exista dentro
    /// de la calle— usa este material.
    /// </summary>
    public static class UiMaterialFactory
    {
        private const string Folder = "Assets/Materials";
        private const string Path = Folder + "/UI_Unlit.mat";
        private const string ShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";

        /// <summary>Devuelve el material, creándolo la primera vez.</summary>
        public static Material Load()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(Path);
            if (existing != null) return existing;

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[UiMaterialFactory] No encontré el shader '{ShaderName}'.");
                return null;
            }

            AssetPathUtility.EnsureFolder(Folder);

            var material = new Material(shader) { name = "UI_Unlit" };
            AssetDatabase.CreateAsset(material, Path);

            return material;
        }

        /// <summary>Se lo pone a todos los renderers que le pasen, salteando nulos.</summary>
        public static void Apply(params SpriteRenderer[] renderers)
        {
            Material material = Load();
            if (material == null) return;

            foreach (SpriteRenderer renderer in renderers)
                if (renderer != null) renderer.sharedMaterial = material;
        }
    }
}
