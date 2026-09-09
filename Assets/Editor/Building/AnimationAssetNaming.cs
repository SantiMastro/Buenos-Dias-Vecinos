using System.Linq;

namespace BuenosDias.EditorTools.Building
{
    /// <summary>
    /// Traduce el nombre de un grupo de sprites al de su clip y su carpeta.
    /// Ej: <c>chr_vecino_abuela_si</c> → <c>Animations/Vecinos/Vecino_Abuela_Si.anim</c>
    /// </summary>
    public static class AnimationAssetNaming
    {
        /// <summary>Carpeta raíz de las animaciones generadas.</summary>
        public const string Root = "Assets/Animations";

        /// <summary>Devuelve la carpeta que le corresponde a un grupo.</summary>
        public static string FolderFor(string groupName)
        {
            if (groupName.StartsWith("chr_predicador")) return $"{Root}/Predicador";
            if (groupName.StartsWith("chr_seguidor")) return $"{Root}/Seguidores";
            if (groupName.StartsWith("chr_vecino")) return $"{Root}/Vecinos";
            if (groupName.StartsWith("fx_")) return $"{Root}/FX";
            return $"{Root}/Otros";
        }

        /// <summary>
        /// Convierte el nombre del grupo en nombre de clip: saca el prefijo de
        /// familia y pasa cada token a mayúscula inicial.
        /// </summary>
        public static string ClipNameFor(string groupName)
        {
            string withoutFamily = StripFamilyPrefix(groupName);

            return string.Join("_", withoutFamily
                .Split('_')
                .Where(token => token.Length > 0)
                .Select(Capitalize));
        }

        /// <summary>Ruta completa del asset de clip para un grupo.</summary>
        public static string ClipPathFor(string groupName)
        {
            return $"{FolderFor(groupName)}/{ClipNameFor(groupName)}.anim";
        }

        private static string StripFamilyPrefix(string groupName)
        {
            // 'chr_' y 'fx_' solo dicen de qué carpeta de arte salió el sprite:
            // no aportan nada al nombre del clip. El resto se conserva entero,
            // incluido el número de variante de chr_seguidor_01_walk.
            if (groupName.StartsWith("chr_")) return groupName.Substring(4);
            if (groupName.StartsWith("fx_")) return groupName.Substring(3);
            return groupName;
        }

        private static string Capitalize(string token)
        {
            if (token.Length == 0) return token;
            return char.ToUpperInvariant(token[0]) + token.Substring(1);
        }
    }
}
