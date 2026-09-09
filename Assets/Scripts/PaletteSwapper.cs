using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Intercambia colores de un SpriteRenderer en runtime.
/// Un solo set de PNG sirve para todas las variantes de color.
///
/// USO
///  1. Poné este script en el GameObject que tiene el SpriteRenderer.
///  2. Cargá los pares de color en la lista "swaps" desde el inspector,
///     o llamá a ApplyPalette() desde código.
///  3. Listo. Funciona con Animator: detecta el cambio de sprite y cachea.
///
/// REQUISITO DE IMPORTACIÓN
///  En el .png: marcá "Read/Write Enabled" en el Inspector de la textura.
///  Sin eso, GetPixels() tira excepción.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PaletteSwapper : MonoBehaviour
{
    [System.Serializable]
    public struct ColorSwap
    {
        public Color from;
        public Color to;
    }

    [Tooltip("Colores a reemplazar. El match es exacto salvo por la tolerancia.")]
    public List<ColorSwap> swaps = new List<ColorSwap>();

    [Tooltip("Margen de comparación por canal (0-1). 0.02 alcanza para pixel art limpio.")]
    [Range(0f, 0.25f)] public float tolerance = 0.02f;

    [Tooltip("Si está activo, recalcula al cambiar la lista en Play Mode.")]
    public bool liveUpdate = false;

    private SpriteRenderer sr;
    private Sprite lastSource;
    private string paletteKey;

    // cache global: una textura remapeada por (sprite original + paleta).
    // Usamos el propio Sprite como parte de la clave en vez de su ID numérico:
    // GetInstanceID quedó deprecado en Unity 6 y la referencia directa es más segura.
    private static readonly Dictionary<(Sprite source, string palette), Sprite> cache
        = new Dictionary<(Sprite, string), Sprite>();

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        RebuildKey();
    }

    void OnEnable() { lastSource = null; }

    void LateUpdate()
    {
        if (sr == null) return;
        if (liveUpdate) RebuildKey();

        // el Animator ya escribió el sprite de este frame
        Sprite current = sr.sprite;
        if (current == null || current == lastSource) return;

        Sprite swapped = GetSwapped(current);
        if (swapped != null)
        {
            sr.sprite = swapped;
            lastSource = swapped;   // evitamos reprocesar lo que ya generamos
        }
    }

    /// <summary>Cambia la paleta en caliente. Llamalo al elegir religión.</summary>
    public void ApplyPalette(IEnumerable<ColorSwap> newSwaps)
    {
        swaps.Clear();
        swaps.AddRange(newSwaps);
        RebuildKey();
        lastSource = null;          // fuerza el reprocesado en el próximo LateUpdate
    }

    void RebuildKey()
    {
        var sb = new System.Text.StringBuilder(64);
        foreach (var s in swaps)
        {
            sb.Append(ColorUtility.ToHtmlStringRGBA(s.from));
            sb.Append('>');
            sb.Append(ColorUtility.ToHtmlStringRGBA(s.to));
            sb.Append('|');
        }
        paletteKey = sb.ToString();
    }

    Sprite GetSwapped(Sprite source)
    {
        if (swaps.Count == 0 || string.IsNullOrEmpty(paletteKey)) return null;

        var key = (source, paletteKey);
        if (cache.TryGetValue(key, out Sprite hit)) return hit;

        Sprite built = BuildSwapped(source);
        cache[key] = built;
        return built;
    }

    Sprite BuildSwapped(Sprite source)
    {
        Texture2D src = source.texture;

        if (!src.isReadable)
        {
            Debug.LogError(
                $"[PaletteSwapper] La textura '{src.name}' no es legible. " +
                "Marcá 'Read/Write Enabled' en el Inspector del PNG.", this);
            return null;
        }

        // solo la región de este sprite (sirve para hojas en modo Multiple)
        Rect r = source.textureRect;
        int x = Mathf.RoundToInt(r.x), y = Mathf.RoundToInt(r.y);
        int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);

        Color[] px = src.GetPixels(x, y, w, h);

        for (int i = 0; i < px.Length; i++)
        {
            if (px[i].a < 0.01f) continue;            // no tocamos el alpha

            for (int s = 0; s < swaps.Count; s++)
            {
                Color from = swaps[s].from;
                if (Mathf.Abs(px[i].r - from.r) <= tolerance &&
                    Mathf.Abs(px[i].g - from.g) <= tolerance &&
                    Mathf.Abs(px[i].b - from.b) <= tolerance)
                {
                    Color to = swaps[s].to;
                    px[i] = new Color(to.r, to.g, to.b, px[i].a);   // conservamos el alpha original
                    break;
                }
            }
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,      // pixel art: nunca bilinear
            wrapMode = TextureWrapMode.Clamp,
            name = src.name + "_swap"
        };
        tex.SetPixels(px);
        tex.Apply();

        // el pivot viene en píxeles absolutos: lo pasamos a normalizado
        Vector2 pivot = new Vector2(
            (source.pivot.x) / w,
            (source.pivot.y) / h);

        var sprite = Sprite.Create(
            tex,
            new Rect(0, 0, w, h),
            pivot,
            source.pixelsPerUnit,
            0,
            SpriteMeshType.FullRect);

        sprite.name = source.name + "_swap";
        return sprite;
    }

    /// <summary>Limpia el cache. Llamalo al cambiar de escena si generaste muchas variantes.</summary>
    public static void ClearCache()
    {
        foreach (var kv in cache)
        {
            if (kv.Value == null) continue;
            var tex = kv.Value.texture;
            if (tex == null) continue;

            // Destroy solo funciona en Play Mode; fuera de eso hay que usar DestroyImmediate
            if (Application.isPlaying) Destroy(tex);
            else DestroyImmediate(tex);
        }
        cache.Clear();
    }
}
