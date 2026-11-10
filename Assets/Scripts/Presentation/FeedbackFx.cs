using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Feedback visual de las acciones que antes pasaban "en silencio":
    ///
    /// - <b>Timbrazo</b>: ondas que salen del timbre (ding... dong), dos notitas y
    ///   chispas. Sin esto, tocar el timbre solo se notaba por el brazo del
    ///   predicador y el sonido; en la muestra con ruido de fondo no se veía nada.
    /// - <b>No pasó nada</b>: gota de sudor de animé, rayitas de bajón sobre la
    ///   cabeza y viento cruzando la vereda. Si había alguien y te ignoró, además
    ///   aparece la venita de enojo (💢) sobre la puerta.
    /// - <b>Tiempo</b>: un "+4s" verde o "-3s" rojo que sube desde el predicador,
    ///   para que se entienda qué hizo con el reloj cada puerta.
    ///
    /// Mismo enfoque que <see cref="DoorBurst"/>: sprites de pocos píxeles armados
    /// por código, pegados a la grilla, pool fijo y sin assets nuevos. Lo crea y lo
    /// avanza <see cref="DoorOpeningView"/>, así no hay nada que cablear en la escena.
    /// </summary>
    public sealed class FeedbackFx
    {
        private const int MaxParticles = 72;
        private const int AlphaSteps = 4;
        private static float Px(float pixels) => ProjectConstants.ToUnits(pixels);

        private static readonly Color32 Edge = new Color32(0x1D, 0x16, 0x38, 0xFF);
        private static readonly Color BellYellow = new Color32(0xF7, 0xD5, 0x1D, 0xFF);
        private static readonly Color Cream = new Color32(0xF3, 0xEC, 0xE0, 0xFF);
        private static readonly Color Sweat = new Color32(0x8F, 0xDA, 0xF7, 0xFF);
        private static readonly Color Gloom = new Color32(0x6A, 0x5A, 0xB0, 0xFF);
        private static readonly Color Anger = new Color32(0xE8, 0x38, 0x3D, 0xFF);
        private static readonly Color Gain = new Color32(0x3D, 0xBE, 0x5B, 0xFF);

        private static readonly string[] NoteRows = { "..##.", "..#.#", "..#..", "###..", "###.." };
        private static readonly string[] DropRows = { "..#..", "..#..", ".###.", "#####", "#####", ".###." };
        private static readonly string[] VeinRows = { "##.##", "#...#", ".....", "#...#", "##.##" };
        private static readonly string[] GloomRows = { "#", "#", "#", "#", "#", "#" };
        private static readonly string[] WindRows = { "########" };
        private static readonly string[] SparkRows = { "##", "##" };

        /// <summary>Fuente de 3×5 para los números de tiempo: 0-9, '+', '-', 's'.</summary>
        private static readonly string[][] Glyphs =
        {
            new[] { "###", "#.#", "#.#", "#.#", "###" }, new[] { ".#.", "##.", ".#.", ".#.", "###" },
            new[] { "###", "..#", "###", "#..", "###" }, new[] { "###", "..#", ".##", "..#", "###" },
            new[] { "#.#", "#.#", "###", "..#", "..#" }, new[] { "###", "#..", "###", "..#", "###" },
            new[] { "###", "#..", "###", "#.#", "###" }, new[] { "###", "..#", ".#.", ".#.", ".#." },
            new[] { "###", "#.#", "###", "#.#", "###" }, new[] { "###", "#.#", "###", "..#", "###" },
            new[] { "...", ".#.", "###", ".#.", "..." }, new[] { "...", "...", "###", "...", "..." },
            new[] { ".##", "#..", ".#.", "..#", "##." }
        };

        private struct Particle
        {
            public SpriteRenderer Renderer;
            public bool Alive;
            public float Age, Delay, Life, FadeStart, Gravity;
            public Vector2 Position, Velocity;
            public Color Tint;
        }

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> Cache =
            new System.Collections.Generic.Dictionary<string, Sprite>();

        private readonly Particle[] particles = new Particle[MaxParticles];
        private readonly GameObject root;
        private readonly float z;

        private FeedbackFx(GameObject root, float z) { this.root = root; this.z = z; }

        /// <summary>Arma el pool sobre la capa y el material de <paramref name="reference"/>.</summary>
        public static FeedbackFx Create(SpriteRenderer reference)
        {
            if (reference == null) return null;

            var root = new GameObject("FeedbackFx");
            var fx = new FeedbackFx(root, reference.transform.position.z);

            for (int i = 0; i < MaxParticles; i++)
            {
                var go = new GameObject($"Fx_{i}");
                go.transform.SetParent(root.transform, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sortingLayerID = reference.sortingLayerID;
                renderer.sortingOrder = reference.sortingOrder + 12;
                renderer.sharedMaterial = reference.sharedMaterial;
                renderer.enabled = false;
                fx.particles[i].Renderer = renderer;
            }

            return fx;
        }

        /// <summary>Destruye el pool. Lo llama quien lo creó.</summary>
        public void Destroy()
        {
            if (root == null) return;
            if (Application.isPlaying) Object.Destroy(root);
            else Object.DestroyImmediate(root);
        }

        /// <summary>
        /// Ding-dong: dos tandas de tres ondas que crecen desde el timbre, dos
        /// notas que suben y chispas. <paramref name="door"/> es la puerta (centro,
        /// Y en el piso); el timbre está a +22 px en X y ~34 px de alto.
        /// </summary>
        public void PlayRing(Vector3 door)
        {
            var bell = new Vector2(door.x + Px(23f), door.y + Px(34f));
            int[] radii = { 4, 8, 12 };

            for (int beat = 0; beat < 2; beat++)
            for (int r = 0; r < radii.Length; r++)
                Spawn(Ring(radii[r]), bell, Vector2.zero, 0f, 0.14f, beat * 0.32f + r * 0.07f,
                    beat == 0 ? BellYellow : Cream, 0.4f);

            for (int i = 0; i < 2; i++)
                Spawn(Get("note", NoteRows), bell + new Vector2(Px(6f + i * 6f), Px(4f)),
                    new Vector2(0.5f + i * 0.3f, 1.3f), 0f, 0.8f, 0.05f + i * 0.3f, Cream, 0.55f);

            for (int i = 0; i < 6; i++)
            {
                float a = (i / 6f) * Mathf.PI * 2f + 0.3f;
                Spawn(Get("spark", SparkRows), bell, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.2f,
                    0f, 0.22f, 0f, BellYellow, 0.3f);
            }
        }

        /// <summary>
        /// "No pasó nada", estilo animé. Siempre: gota de sudor, rayitas de bajón y
        /// viento. Si <paramref name="empty"/> es falso (había alguien y no abrió),
        /// suma la venita de enojo sobre la puerta: te ignoraron a propósito.
        /// </summary>
        public void PlayIgnored(Vector3 preacher, Vector3 door, bool empty)
        {
            Spawn(Get("drop", DropRows), new Vector2(preacher.x + Px(10f), preacher.y + Px(32f)),
                new Vector2(0f, -0.18f), 0f, 1.4f, 0.05f, Sweat, 0.7f);

            for (int i = 0; i < 3; i++)
                Spawn(Get("gloom", GloomRows),
                    new Vector2(preacher.x + Px(-5f + i * 5f), preacher.y + Px(46f + (i == 1 ? 2f : 0f))),
                    new Vector2(0f, -0.25f), 0f, 1.1f, 0.15f + i * 0.08f, Gloom, 0.5f);

            for (int i = 0; i < 3; i++)
                Spawn(Get("wind", WindRows),
                    new Vector2(preacher.x + 3.2f + i * 0.6f, preacher.y + Px(8f + i * 9f)),
                    new Vector2(-4.5f, 0f), 0f, 1.3f, 0.2f + i * 0.18f, Cream, 0.6f);

            if (empty) return;

            var vein = new Vector2(door.x + Px(10f), door.y + Px(64f));
            Spawn(Get("vein", VeinRows), vein, Vector2.zero, 0f, 0.35f, 0f, Anger, 0.8f);
            Spawn(Get("vein", VeinRows), vein, Vector2.zero, 0f, 0.7f, 0.45f, Anger, 0.6f);
        }

        /// <summary>
        /// El cartelito de tiempo: "+4s" verde al convertir, "-3s" rojo al fallar.
        /// Sube y se frena, como un número de daño de RPG.
        /// </summary>
        public void PlayTime(Vector3 preacher, float seconds)
        {
            int value = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(seconds)));
            string text = (seconds >= 0f ? "+" : "-") + value + "s";
            Color tint = seconds >= 0f ? Gain : Anger;

            float width = text.Length * Px(4f);
            float left = preacher.x - width * 0.5f + Px(2f);

            for (int i = 0; i < text.Length; i++)
                Spawn(Glyph(text[i]), new Vector2(left + i * Px(4f), preacher.y + Px(48f)),
                    new Vector2(0f, 1.3f), 1.1f, 1.3f, 0.1f, tint, 0.7f);
        }

        /// <summary>Avanza todo el pool con el tiempo del juego.</summary>
        public void Tick(float deltaTime)
        {
            for (int i = 0; i < MaxParticles; i++)
            {
                if (!particles[i].Alive) continue;

                particles[i].Age += deltaTime;
                float age = particles[i].Age - particles[i].Delay;
                if (age < 0f) continue;
                if (age >= particles[i].Life) { Kill(i); continue; }

                // La "gravedad" frena hacia cero y no tira para abajo: sirve igual
                // para los números que suben y se paran.
                Vector2 v = particles[i].Velocity;
                particles[i].Velocity = Vector2.MoveTowards(v, Vector2.zero, particles[i].Gravity * deltaTime);
                particles[i].Position += particles[i].Velocity * deltaTime;
                Place(i, age / particles[i].Life);
            }
        }

        /// <summary>Apaga todo. Para cuando la partida corta.</summary>
        public void Clear()
        {
            for (int i = 0; i < MaxParticles; i++) Kill(i);
        }

        private void Spawn(Sprite sprite, Vector2 position, Vector2 velocity, float gravity,
            float life, float delay, Color tint, float fadeStart)
        {
            for (int i = 0; i < MaxParticles; i++)
            {
                if (particles[i].Alive) continue;

                particles[i] = new Particle
                {
                    Renderer = particles[i].Renderer, Alive = true, Delay = delay,
                    Life = Mathf.Max(0.05f, life), FadeStart = Mathf.Clamp01(fadeStart),
                    Gravity = gravity, Position = position, Velocity = velocity, Tint = tint
                };
                particles[i].Renderer.sprite = sprite;
                particles[i].Renderer.enabled = false;
                return;
            }
        }

        private void Place(int index, float progress)
        {
            float ppu = ProjectConstants.PixelsPerUnit;
            Particle p = particles[index];

            p.Renderer.transform.position = new Vector3(
                ParallaxLayer.Snap(p.Position.x, ppu), ParallaxLayer.Snap(p.Position.y, ppu), z);

            float fade = progress < p.FadeStart
                ? 1f
                : 1f - (progress - p.FadeStart) / Mathf.Max(0.0001f, 1f - p.FadeStart);
            float alpha = Mathf.Ceil(Mathf.Clamp01(fade) * AlphaSteps) / AlphaSteps;

            p.Renderer.color = new Color(p.Tint.r, p.Tint.g, p.Tint.b, p.Tint.a * alpha);
            p.Renderer.enabled = true;
        }

        private void Kill(int index)
        {
            particles[index].Alive = false;
            if (particles[index].Renderer != null) particles[index].Renderer.enabled = false;
        }

        // ---- Sprites: se arman una vez, con contorno oscuro de 1 px ----

        private static Sprite Glyph(char c)
        {
            int index = c >= '0' && c <= '9' ? c - '0' : c == '+' ? 10 : c == '-' ? 11 : 12;
            return Get("glyph_" + c, Glyphs[index]);
        }

        private static Sprite Ring(int radius)
        {
            string key = "ring_" + radius;
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            int size = radius * 2 + 1;
            var rows = new string[size];
            for (int y = 0; y < size; y++)
            {
                var chars = new char[size];
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - radius) * (x - radius) + (y - radius) * (y - radius));
                    chars[x] = Mathf.Abs(d - radius) < 0.55f ? '#' : '.';
                }
                rows[y] = new string(chars);
            }
            return Get(key, rows);
        }

        private static Sprite Get(string key, string[] rows)
        {
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            Sprite sprite = PixelSprite.FromRows(Outline(rows), new Vector2(0.5f, 0.5f), Paint);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Agrega un borde 'o' alrededor de la tinta '#', para que se lea sobre cualquier fondo.</summary>
        private static string[] Outline(string[] rows)
        {
            int h = rows.Length + 2, w = rows[0].Length + 2;
            var result = new string[h];
            for (int y = 0; y < h; y++)
            {
                var chars = new char[w];
                for (int x = 0; x < w; x++)
                {
                    if (Ink(rows, x - 1, y - 1)) { chars[x] = '#'; continue; }
                    bool near = false;
                    for (int dy = -1; dy <= 1 && !near; dy++)
                    for (int dx = -1; dx <= 1 && !near; dx++)
                        near = (dx == 0 || dy == 0) && Ink(rows, x - 1 + dx, y - 1 + dy);
                    chars[x] = near ? 'o' : '.';
                }
                result[y] = new string(chars);
            }
            return result;
        }

        private static bool Ink(string[] rows, int x, int y) =>
            y >= 0 && y < rows.Length && x >= 0 && x < rows[y].Length && rows[y][x] == '#';

        private static Color32 Paint(char cell)
        {
            switch (cell)
            {
                case '#': return new Color32(255, 255, 255, 255);
                case 'o': return Edge;
                default: return default;
            }
        }
    }
}
