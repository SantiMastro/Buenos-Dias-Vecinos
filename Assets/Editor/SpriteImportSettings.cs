using UnityEditor;
using UnityEngine;

/// <summary>
/// Configura solo todos los PNG que caigan dentro de Assets/Sprites/.
/// Point filter, sin compresión, PPU 32, pivot según el prefijo del archivo
/// y Read/Write activado (que lo necesita PaletteSwapper).
///
/// INSTALACIÓN
///   Poné este archivo en Assets/Editor/SpriteImportSettings.cs
///   Después arrastrá la carpeta Sprites a Assets/.
///   Se aplica solo. No tenés que tocar el Inspector.
///
/// Si ya importaste los sprites antes de poner este script:
///   Click derecho sobre la carpeta Sprites > Reimport
/// </summary>
public class SpriteImportSettings : AssetPostprocessor
{
    const string ROOT = "Assets/Sprites/";
    const float PPU = 32f;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(ROOT)) return;

        var ti = (TextureImporter)assetImporter;

        ti.textureType         = TextureImporterType.Sprite;
        ti.spriteImportMode    = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = PPU;
        ti.filterMode          = FilterMode.Point;      // pixel art: jamás bilinear
        ti.mipmapEnabled       = false;
        ti.alphaIsTransparency = true;
        ti.isReadable          = true;                  // lo pide PaletteSwapper
        ti.wrapMode            = TextureWrapMode.Clamp;
        ti.maxTextureSize      = 2048;

        var platform = ti.GetDefaultPlatformTextureSettings();
        platform.textureCompression = TextureImporterCompression.Uncompressed;
        platform.format             = TextureImporterFormat.RGBA32;
        ti.SetPlatformTextureSettings(platform);

        string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);

        // los tiles se repiten en horizontal
        if (file.StartsWith("env_pared")   || file.StartsWith("env_vereda") ||
            file.StartsWith("env_calle")   || file.StartsWith("env_reja_segmento") ||
            file.StartsWith("bg_")         || file.StartsWith("prop_pasto"))
        {
            ti.wrapMode = TextureWrapMode.Repeat;
        }

        // spriteMeshType y spriteAlignment viven en TextureImporterSettings,
        // no en TextureImporter. Se leen, se modifican y se vuelven a escribir.
        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);

        s.spriteMeshType = SpriteMeshType.FullRect;

        if (file.StartsWith("ui_skillcheck_aguja"))
        {
            // rota sobre su propio centro
            s.spriteAlignment = (int)SpriteAlignment.Center;
        }
        else if (file.StartsWith("chr_") || file.StartsWith("prop_") || file.StartsWith("fx_sombra"))
        {
            // apoyados en el piso
            s.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        }
        else if (file.StartsWith("env_") || file.StartsWith("bg_"))
        {
            // módulos que se componen desde la esquina
            s.spriteAlignment = (int)SpriteAlignment.BottomLeft;
        }
        else
        {
            s.spriteAlignment = (int)SpriteAlignment.Center;
        }

        ti.SetTextureSettings(s);
    }
}
