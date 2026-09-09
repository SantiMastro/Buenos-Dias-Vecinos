using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BuenosDias.EditorTools.Building
{
    /// <summary>
    /// Agrupa los PNG de una carpeta en secuencias de animación.
    ///
    /// La cantidad de frames se detecta leyendo los archivos, no se configura:
    /// la convención "_00 a _05" de la spec no se cumple en el arte real
    /// (walk 6, espera 4, timbre 3, vecinos 2), así que confiar en ella
    /// generaría clips truncados sin avisar.
    /// </summary>
    public static class SpriteSequenceScanner
    {
        /// <summary>Una secuencia detectada: un grupo de sprites ordenados.</summary>
        public sealed class Sequence
        {
            /// <summary>Nombre común sin el sufijo numérico. Ej: chr_predicador_walk.</summary>
            public string GroupName { get; }

            /// <summary>Frames en orden de índice.</summary>
            public IReadOnlyList<Sprite> Frames { get; }

            /// <summary>Crea una secuencia ya ordenada.</summary>
            public Sequence(string groupName, IReadOnlyList<Sprite> frames)
            {
                GroupName = groupName;
                Frames = frames;
            }

            /// <summary>Cantidad de frames encontrados en disco.</summary>
            public int FrameCount => Frames.Count;
        }

        /// <summary>
        /// Escanea las carpetas indicadas y devuelve las secuencias encontradas,
        /// ordenadas por nombre.
        /// </summary>
        public static List<Sequence> Scan(params string[] folders)
        {
            var buckets = new Dictionary<string, List<(int index, Sprite sprite)>>();

            string[] guids = AssetDatabase.FindAssets("t:Sprite", folders);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;

                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                (string group, int index) = SplitTrailingIndex(fileName);

                if (!buckets.TryGetValue(group, out var list))
                {
                    list = new List<(int, Sprite)>();
                    buckets[group] = list;
                }
                list.Add((index, sprite));
            }

            return buckets
                .OrderBy(pair => pair.Key)
                .Select(pair => new Sequence(
                    pair.Key,
                    pair.Value.OrderBy(f => f.index).Select(f => f.sprite).ToList()))
                .ToList();
        }

        /// <summary>
        /// Separa el sufijo "_NN" del final. Sin sufijo, el archivo es una
        /// secuencia de un solo frame y conserva el nombre completo.
        ///
        /// Solo mira el final a propósito: chr_seguidor_01_walk_00 tiene que dar
        /// el grupo chr_seguidor_01_walk, no perder el 01 del medio.
        /// </summary>
        public static (string group, int index) SplitTrailingIndex(string fileName)
        {
            int underscore = fileName.LastIndexOf('_');
            if (underscore < 0 || underscore == fileName.Length - 1)
                return (fileName, 0);

            string tail = fileName.Substring(underscore + 1);
            if (tail.Length == 0 || !tail.All(char.IsDigit))
                return (fileName, 0);

            return (fileName.Substring(0, underscore), int.Parse(tail));
        }
    }
}
