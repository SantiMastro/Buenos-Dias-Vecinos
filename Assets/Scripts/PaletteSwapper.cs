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

    // Sombrero opcional: se compone encima de la cabeza de cada cuadro.
    private Sprite hat;
    private int hatSink = 2;
    private Sprite sidelock;
    private Vector2Int sidelockOffset;

    // cache global: una textura remapeada por (sprite original + paleta).
    // Usamos el propio Sprite como parte de la clave en vez de su ID numérico:
    // GetInstanceID quedó deprecado en Unity 6 y la referencia directa es más segura.
    private static readonly Dictionary<(Sprite source, string palette), Sprite> cache
        = new Dictionary<(Sprite, string), Sprite>();

    // copia pintada -> sprite original, para poder repintar al cambiar de paleta
    private static readonly Dictionary<Sprite, Sprite> swappedToOriginal
        = new Dictionary<Sprite, Sprite>();

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

        // ⚠️ El Animator NO reescribe el sprite en un estado de cuadro único (el
        // Idle): ahí el renderer sigue con la copia pintada con la paleta ANTERIOR,
        // y repintar una copia no devuelve los colores de origen. Por eso se
        // vuelve siempre al sprite original antes de aplicar la paleta nueva.
        if (swappedToOriginal.TryGetValue(current, out Sprite original)) current = original;

        Sprite swapped = GetSwapped(current);
        if (swapped != null)
        {
            sr.sprite = swapped;
            lastSource = swapped;   // evitamos reprocesar lo que ya generamos
        }
        else if (current != sr.sprite)
        {
            // paleta vacía: se devuelve el original
            sr.sprite = current;
            lastSource = current;
        }
    }

    /// <summary>Cambia la paleta en caliente. Llamalo al elegir religión.</summary>
    public void ApplyPalette(
        IEnumerable<ColorSwap> newSwaps, Sprite newHat = null, int newHatSink = 2,
        Sprite newSidelock = null, Vector2Int newSidelockOffset = default)
    {
        swaps.Clear();
        swaps.AddRange(newSwaps);
        hat = newHat;
        hatSink = newHatSink;
        sidelock = newSidelock;
        sidelockOffset = newSidelockOffset;
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
        if (hat != null) sb.Append("hat:").Append(hat.name).Append(':').Append(hatSink);
        if (sidelock != null) sb.Append("side:").Append(sidelock.name).Append(':').Append(sidelockOffset);
        paletteKey = sb.ToString();
    }

    Sprite GetSwapped(Sprite source)
    {
        if ((swaps.Count == 0 && hat == null && sidelock == null) || string.IsNullOrEmpty(paletteKey)) return null;

        var key = (source, paletteKey);
        if (cache.TryGetValue(key, out Sprite hit)) return hit;

        Sprite built = BuildSwapped(source);
        cache[key] = built;
        if (built != null) swappedToOriginal[built] = source;
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

        px = ComposeHat(
            px, w, h, hat, hatSink, sidelock, sidelockOffset, out int newW, out int newH, out int padL);

        var tex = new Texture2D(newW, newH, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,      // pixel art: nunca bilinear
            wrapMode = TextureWrapMode.Clamp,
            name = src.name + "_swap"
        };
        tex.SetPixels(px);
        tex.Apply();

        // el pivot viene en píxeles absolutos: lo pasamos a normalizado. Si el
        // sombrero obligó a agrandar el lienzo, el agregado va ARRIBA, así que el
        // pivot (medido desde abajo) no cambia en píxeles.
        Vector2 pivot = new Vector2(
            (source.pivot.x + padL) / newW,
            (source.pivot.y) / newH);

        var sprite = Sprite.Create(
            tex,
            new Rect(0, 0, newW, newH),
            pivot,
            source.pixelsPerUnit,
            0,
            SpriteMeshType.FullRect);

        sprite.name = source.name + "_swap";
        return sprite;
    }

    /// <summary>
    /// Pega el sombrero sobre la cabeza de un cuadro. La cabeza es lo más alto
    /// del cuerpo: el punto más alto es la coronilla y el centro de esas filas es
    /// donde va el sombrero. Si no entra por arriba, el lienzo crece hacia arriba.
    /// Devuelve los píxeles (posiblemente con más filas) y la altura final.
    /// </summary>
    const int MinBodyHeight = 24;

    static Color[] ComposeHat(
        Color[] px, int w, int h, Sprite hat, int sink, Sprite curls, Vector2Int curlsOffset,
        out int newW, out int newH, out int padL)
    {
        newW = w;
        newH = h;
        padL = 0;
        if (hat != null && !hat.texture.isReadable) hat = null;
        if (curls != null && !curls.texture.isReadable) curls = null;
        if (hat == null && curls == null) return px;

        // Los cuadros del predicador son recortes ajustados al dibujo, y algunos
        // cuadros traen piezas sueltas (manos, ondas del timbre) que pasan por el
        // mismo swapper: solo el cuerpo entero lleva sombrero.
        if (h < MinBodyHeight) return px;

        int top = -1;                                   // fila más alta con píxel
        for (int y = h - 1; y >= 0 && top < 0; y--)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 0.5f) { top = y; break; }
        if (top < 0) return px;

        int minX = int.MaxValue, maxX = int.MinValue;
        for (int y = top; y > top - 3 && y >= 0; y--)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 0.5f) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); }
        if (minX > maxX) return px;

        padL = 0;
        newW = w;
        newH = h;
        int hw = 0, hh = 0, x0 = 0, y0 = 0;
        Color[] hp = null;

        if (hat != null)
        {
            Rect hr = hat.textureRect;
            hw = Mathf.RoundToInt(hr.width);
            hh = Mathf.RoundToInt(hr.height);
            hp = hat.texture.GetPixels(Mathf.RoundToInt(hr.x), Mathf.RoundToInt(hr.y), hw, hh);

            x0 = Mathf.RoundToInt((minX + maxX) * 0.5f - (hw - 1) * 0.5f);
            y0 = top + 1 - Mathf.Clamp(sink, 0, hh);    // fila de abajo del sombrero

            // El lienzo crece donde haga falta (arriba y a los costados) para que el
            // sombrero no se recorte. Lo agregado a la izquierda mueve el pivot.
            padL = Mathf.Max(0, -x0);
            int padR = Mathf.Max(0, x0 + hw - w);
            newW = w + padL + padR;
            newH = Mathf.Max(h, y0 + hh);
        }

        var result = new Color[newW * newH];            // lo nuevo queda transparente
        for (int y = 0; y < h; y++)
            System.Array.Copy(px, y * w, result, y * newW + padL, w);

        for (int y = 0; y < hh; y++)
        {
            int ty = y0 + y;
            if (ty < 0 || ty >= newH) continue;
            for (int x = 0; x < hw; x++)
            {
                int tx = x0 + padL + x;
                Color c = hp[y * hw + x];
                if (c.a > 0.5f) result[ty * newW + tx] = c;
            }
        }

        // Patillas que cuelgan: se ubican respecto de la coronilla y del centro de
        // la cabeza, así acompañan el balanceo igual que el sombrero. Van sobre el
        // lienzo existente, después del sombrero.
        if (curls != null)
        {
            Rect lr = curls.textureRect;
            int lw = Mathf.RoundToInt(lr.width), lh = Mathf.RoundToInt(lr.height);
            Color[] lp = curls.texture.GetPixels(Mathf.RoundToInt(lr.x), Mathf.RoundToInt(lr.y), lw, lh);

            int cx = Mathf.RoundToInt((minX + maxX) * 0.5f) + curlsOffset.x;
            int topY = top - curlsOffset.y;
            for (int y = 0; y < lh; y++)
            {
                int ty = topY - (lh - 1) + y;
                if (ty < 0 || ty >= newH) continue;
                for (int x = 0; x < lw; x++)
                {
                    int tx = cx + padL + x;
                    if (tx < 0 || tx >= newW) continue;
                    Color c = lp[y * lw + x];
                    if (c.a > 0.5f) result[ty * newW + tx] = c;
                }
            }
        }

        return result;
    }

    /// <summary>Limpia el cache. Llamalo al cambiar de escena si generaste muchas variantes.</summary>
    /// <summary>
    /// Devuelve una COPIA del sprite con los colores cambiados, sin pasar por el
    /// caché ni por un componente. Para vistas que necesitan el predicador en la
    /// paleta de una religión sin tener un SpriteRenderer animado (ej: la foto del
    /// DNI del highscore). Quien lo llama es dueño del sprite y de su textura.
    /// </summary>
    public static Sprite Recolor(
        Sprite source, IList<ColorSwap> colorSwaps, float tolerance = 0.02f,
        Sprite hat = null, int hatSink = 2,
        Sprite sidelock = null, Vector2Int sidelockOffset = default)
    {
        if (source == null) return source;
        if ((colorSwaps == null || colorSwaps.Count == 0) && hat == null && sidelock == null) return source;
        int swapCount = colorSwaps != null ? colorSwaps.Count : 0;

        Texture2D src = source.texture;
        if (!src.isReadable) return source;

        Rect r = source.textureRect;
        int x = Mathf.RoundToInt(r.x), y = Mathf.RoundToInt(r.y);
        int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);

        Color[] px = src.GetPixels(x, y, w, h);

        for (int i = 0; i < px.Length; i++)
        {
            if (px[i].a < 0.01f) continue;

            for (int s = 0; s < swapCount; s++)
            {
                Color from = colorSwaps[s].from;
                if (Mathf.Abs(px[i].r - from.r) > tolerance ||
                    Mathf.Abs(px[i].g - from.g) > tolerance ||
                    Mathf.Abs(px[i].b - from.b) > tolerance) continue;

                Color to = colorSwaps[s].to;
                px[i] = new Color(to.r, to.g, to.b, px[i].a);
                break;
            }
        }

        px = ComposeHat(
            px, w, h, hat, hatSink, sidelock, sidelockOffset, out int newW, out int newH, out int padL);

        var tex = new Texture2D(newW, newH, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = src.name + "_recolor"
        };
        tex.SetPixels(px);
        tex.Apply();

        var sprite = Sprite.Create(
            tex, new Rect(0, 0, newW, newH),
            new Vector2((source.pivot.x + padL) / newW, source.pivot.y / newH),
            source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = source.name + "_recolor";
        return sprite;
    }

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
