using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuenosDias.EditorTools.Building
{
    /// <summary>
    /// Regenera el halo del farol de entrada.
    ///
    /// **Por qué existía un problema.** El halo que había era un DISCO DURO:
    /// medido píxel por píxel, alfa 255 hasta el radio 7 y alfa 0 desde el 9, con
    /// el color constante en (255, 196, 107). Sumado con <c>Blend One One</c> sobre
    /// la pared, un disco opaco y saturado se satura a blanco y deja de leerse como
    /// luz: se lee como una calcomanía pegada al lado de la puerta, más grande que
    /// la cabeza del predicador.
    ///
    /// El diagnóstico descarta las otras dos sospechas. **No es orden de dibujo**:
    /// el halo está en <c>HouseDetails/7</c> y la reja en 10 y el portón en 11, o
    /// sea que ya se dibuja detrás —el arreglo de la fase 4 sigue puesto—. **Y no es
    /// que el sprite sea de 60×60**: de esos 60 px solo 16 tenían tinta. Lo que
    /// estaba mal era la FORMA del degradé, que no era degradé.
    ///
    /// El shader premultiplica por alfa (<c>rgb * a * intensidad</c>), así que un
    /// alfa que baja SÍ apaga la suma. Con un núcleo chico y una caída cuadrática,
    /// el mismo color pasa de bola a resplandor.
    ///
    /// Se escribe un PNG y no se genera en runtime porque la capa aditiva la
    /// referencia un <c>HouseSignalDefinition</c>, que necesita un asset.
    /// </summary>
    public static class GlowSpriteFactory
    {
        /// <summary>Halo del farol de entrada. Se sobrescribe.</summary>
        public const string LampGlowPath = "Assets/Sprites/FX/fx_farol_luz.png";

        /// <summary>
        /// Lado del lienzo. El pivot de los <c>fx_</c> es Center, así que el lado
        /// no cambia dónde cae el halo: solo tiene que dar de sobra para la caída.
        /// </summary>
        private const int CanvasSize = 32;

        /// <summary>Radio del núcleo lleno, en píxeles. Es el tamaño de la bombita.</summary>
        private const float CoreRadius = 2f;

        /// <summary>Radio donde la luz ya no llega.</summary>
        private const float FalloffRadius = 11f;

        /// <summary>
        /// Alfa del núcleo. NO es 255 a propósito: en aditivo, 255 sobre una pared
        /// clara satura a blanco y el halo vuelve a ser un disco.
        /// </summary>
        private const byte CoreAlpha = 160;

        /// <summary>El color del farol, el mismo que ya tenía el halo viejo.</summary>
        private static readonly Color32 LampColor = new Color32(255, 196, 107, 255);

        [MenuItem("Tools/Buenos Días/Arte · Regenerar halo del farol", priority = 400)]
        public static void BuildFromMenu()
        {
            Debug.Log(Build());
        }

        /// <summary>Escribe el PNG y lo reimporta. Devuelve un reporte legible.</summary>
        public static string Build()
        {
            var texture = new Texture2D(CanvasSize, CanvasSize, TextureFormat.RGBA32, false);
            float centre = (CanvasSize - 1) * 0.5f;

            for (int y = 0; y < CanvasSize; y++)
                for (int x = 0; x < CanvasSize; x++)
                {
                    float distance = Vector2.Distance(
                        new Vector2(x, y), new Vector2(centre, centre));

                    texture.SetPixel(x, y, WithAlpha(AlphaAt(distance)));
                }

            texture.Apply();
            File.WriteAllBytes(LampGlowPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(LampGlowPath, ImportAssetOptions.ForceUpdate);

            return $"[Arte] Halo del farol regenerado en {LampGlowPath}: " +
                   $"lienzo {CanvasSize}×{CanvasSize}, núcleo {CoreRadius} px a " +
                   $"alfa {CoreAlpha}, caída hasta {FalloffRadius} px.";
        }

        /// <summary>
        /// La caída. Es cuadrática y no lineal: una rampa lineal deja un borde
        /// visible donde llega a cero, y en pixel art ese borde se ve como un
        /// círculo dibujado alrededor de la luz.
        /// </summary>
        private static byte AlphaAt(float distance)
        {
            if (distance <= CoreRadius) return CoreAlpha;
            if (distance >= FalloffRadius) return 0;

            float t = (distance - CoreRadius) / (FalloffRadius - CoreRadius);
            float remaining = 1f - t;

            return (byte)Mathf.RoundToInt(CoreAlpha * remaining * remaining);
        }

        /// <summary>
        /// El RGB se deja CONSTANTE y lo que baja es el alfa. Oscurecer el color
        /// hacia el negro daría tonos que no están en la paleta; el shader ya
        /// premultiplica, así que bajar el alfa alcanza para apagar la suma.
        /// </summary>
        private static Color32 WithAlpha(byte alpha)
        {
            return new Color32(LampColor.r, LampColor.g, LampColor.b, alpha);
        }
    }
}
