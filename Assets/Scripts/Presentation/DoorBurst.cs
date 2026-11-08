using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Los chispazos con que termina una puerta: una fuente de confeti de colores
    /// cuando se convierte al vecino, y una nubecita de polvo con tres puntitos
    /// ("...") cuando no había nadie en casa.
    ///
    /// Existe porque el juego casi no decía nada del interior de la casa: el
    /// jugador tocaba, esperaba y recién por la ausencia de cortina se enteraba de
    /// que no había nadie. Acá las dos puntas —el acierto y la casa vacía— tienen
    /// su propia respuesta visible, además del sonido.
    ///
    /// Está hecho igual que el humo de la chimenea (<c>ChimneySmoke</c>) y no con
    /// un sistema de partículas de Unity: son sprites de pocos píxeles con
    /// posiciones pegadas a la grilla, en el mismo material y la misma capa que el
    /// predicador, así que se ven del mismo mundo y no hace falta ningún asset.
    ///
    /// Es un pool fijo: crear objetos por chispazo metería basura justo en el
    /// instante en que se corta la acción. No es un <c>MonoBehaviour</c>: lo crea y
    /// lo avanza quien lo usa (<see cref="DoorOpeningView"/>), así no hay nada
    /// nuevo que enganchar en la escena.
    /// </summary>
    public sealed class DoorBurst
    {
        private const int MaxParticles = 48;

        /// <summary>
        /// La opacidad baja de a escalones y no de corrido, como el humo de la
        /// chimenea: el arte es de píxeles y un fundido continuo desentonaba.
        /// </summary>
        private const int AlphaSteps = 4;

        private const float GravityUnitsPerSecond = 10f;

        /// <summary>Alto de la puerta en unidades (56 px). Ubica los puntitos.</summary>
        private const float DoorHeightUnits = 1.75f;

        private static readonly string[] SquareRows = { "##", "##" };
        private static readonly string[] SparkRows = { "#" };

        private static readonly string[] DustRows =
        {
            ".###.",
            "#####",
            "#####",
            "#####",
            ".###."
        };

        private static readonly string[] DotRows =
        {
            "oooo",
            "o##o",
            "o##o",
            "oooo"
        };

        private static readonly Color32 Edge = new Color32(0x1D, 0x16, 0x38, 0xFF);
        private static readonly Color32 Cream = new Color32(0xF3, 0xEC, 0xE0, 0xFF);
        private static readonly Color32 Dust = new Color32(0xC9, 0xC2, 0xB4, 0xFF);

        /// <summary>Los colores del confeti: un arcoíris sin grises ni marrones.</summary>
        private static readonly Color[] Confetti =
        {
            new Color32(0xE8, 0x38, 0x3D, 0xFF),
            new Color32(0xF2, 0x8C, 0x28, 0xFF),
            new Color32(0xF7, 0xD5, 0x1D, 0xFF),
            new Color32(0x3D, 0xBE, 0x5B, 0xFF),
            new Color32(0x2C, 0xC4, 0xE0, 0xFF),
            new Color32(0x4B, 0x6B, 0xE8, 0xFF),
            new Color32(0xD1, 0x4B, 0xD6, 0xFF),
            new Color32(0xFF, 0xFF, 0xFF, 0xFF)
        };

        private struct Particle
        {
            public SpriteRenderer Renderer;
            public bool Alive;
            public float Age;
            public float Delay;
            public float Life;
            public float FadeStart;
            public float Gravity;
            public Vector2 Position;
            public Vector2 Velocity;
            public Color Tint;
        }

        private static Sprite squareSprite;
        private static Sprite sparkSprite;
        private static Sprite dustSprite;
        private static Sprite dotSprite;

        private readonly Particle[] particles = new Particle[MaxParticles];
        private readonly GameObject root;
        private readonly float z;

        private DoorBurst(GameObject root, float z)
        {
            this.root = root;
            this.z = z;
        }

        /// <summary>
        /// Arma el pool. <paramref name="reference"/> pone la capa y el material: se
        /// dibuja en la del predicador, por encima de él.
        /// </summary>
        public static DoorBurst Create(SpriteRenderer reference)
        {
            if (reference == null) return null;

            var root = new GameObject("DoorBurst");
            var burst = new DoorBurst(root, reference.transform.position.z);

            for (int i = 0; i < MaxParticles; i++)
            {
                var go = new GameObject($"Particle_{i}");
                go.transform.SetParent(root.transform, false);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sortingLayerID = reference.sortingLayerID;
                renderer.sortingOrder = reference.sortingOrder + 10;
                renderer.sharedMaterial = reference.sharedMaterial;
                renderer.enabled = false;
                burst.particles[i].Renderer = renderer;
            }

            return burst;
        }

        /// <summary>
        /// Destruye los objetos del pool. Lo llama quien lo creó al apagarse.
        /// <c>Destroy</c> solo vale en Play; fuera de Play, Unity pide
        /// <c>DestroyImmediate</c> y si no deja el objeto vivo en la escena.
        /// </summary>
        public void Destroy()
        {
            if (root == null) return;

            if (Application.isPlaying) Object.Destroy(root);
            else Object.DestroyImmediate(root);
        }

        /// <summary>
        /// La fuente: un chorro de confeti multicolor que sale sobre el felpudo, sube
        /// en abanico y cae. <paramref name="door"/> es la posición mundial de la
        /// puerta, con la Y en el piso.
        /// </summary>
        public void PlayFountain(Vector3 door)
        {
            const int count = 32;
            float baseY = ProjectConstants.ToUnits(10f);

            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(-22f, 22f) * Mathf.Deg2Rad;
                float speed = Random.Range(4.2f, 6.2f);
                bool spark = Random.value < 0.25f;

                Spawn(
                    spark ? SparkSprite() : SquareSprite(),
                    new Vector2(door.x + Random.Range(-0.1f, 0.1f), door.y + baseY),
                    new Vector2(Mathf.Sin(angle) * speed, Mathf.Cos(angle) * speed),
                    GravityUnitsPerSecond,
                    life: Random.Range(0.9f, 1.3f),
                    delay: i * 0.012f,
                    tint: Confetti[Random.Range(0, Confetti.Length)],
                    fadeStart: 0.65f);
            }
        }

        /// <summary>
        /// La casa vacía: unas nubecitas de polvo que se levantan del felpudo y tres
        /// puntitos que van apareciendo de a uno sobre la puerta, como un "..." de
        /// nadie contesta. <paramref name="door"/> es la posición mundial de la puerta.
        /// </summary>
        public void PlayEmpty(Vector3 door)
        {
            var dust = new Color(1f, 1f, 1f, 0.85f);

            for (int i = 0; i < 6; i++)
            {
                Spawn(
                    DustSprite(),
                    new Vector2(door.x + Random.Range(-0.35f, 0.35f), door.y + ProjectConstants.ToUnits(6f)),
                    new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(0.5f, 0.9f)),
                    0f,
                    life: Random.Range(0.8f, 1.1f),
                    delay: Random.Range(0f, 0.25f),
                    tint: dust,
                    fadeStart: 0.2f);
            }

            // Los puntitos tienen la misma hora de salida y distinta de entrada: así
            // aparecen de a uno pero se van juntos.
            const float together = 1.4f;
            float dotY = door.y + DoorHeightUnits * 0.62f;

            for (int i = 0; i < 3; i++)
            {
                float delay = i * 0.2f;
                Spawn(
                    DotSprite(),
                    new Vector2(door.x + (i - 1) * 0.22f, dotY),
                    Vector2.zero,
                    0f,
                    life: together - delay,
                    delay: delay,
                    tint: Color.white,
                    fadeStart: 0.7f);
            }
        }

        /// <summary>Avanza todo el pool. Va con el tiempo del juego.</summary>
        public void Tick(float deltaTime)
        {
            for (int i = 0; i < MaxParticles; i++)
            {
                if (!particles[i].Alive) continue;

                particles[i].Age += deltaTime;
                float age = particles[i].Age - particles[i].Delay;
                if (age < 0f) continue;

                if (age >= particles[i].Life)
                {
                    Kill(i);
                    continue;
                }

                particles[i].Velocity.y -= particles[i].Gravity * deltaTime;
                particles[i].Position += particles[i].Velocity * deltaTime;
                Place(i, age / particles[i].Life);
            }
        }

        /// <summary>Apaga todo lo que esté en el aire. Para cuando la partida corta.</summary>
        public void Clear()
        {
            for (int i = 0; i < MaxParticles; i++) Kill(i);
        }

        private void Spawn(
            Sprite sprite, Vector2 position, Vector2 velocity, float gravity,
            float life, float delay, Color tint, float fadeStart)
        {
            for (int i = 0; i < MaxParticles; i++)
            {
                if (particles[i].Alive) continue;

                particles[i].Alive = true;
                particles[i].Age = 0f;
                particles[i].Delay = delay;
                particles[i].Life = Mathf.Max(0.05f, life);
                particles[i].FadeStart = Mathf.Clamp01(fadeStart);
                particles[i].Gravity = gravity;
                particles[i].Position = position;
                particles[i].Velocity = velocity;
                particles[i].Tint = tint;
                particles[i].Renderer.sprite = sprite;
                particles[i].Renderer.enabled = false;
                return;
            }

            // Pool lleno: se pierde esta partícula. Con 48 de tope y un chorro de 32
            // no pasa salvo que dos chispazos se pisen, y ahí no se nota.
        }

        /// <summary>Pega la partícula a la grilla de píxeles y le pone su opacidad.</summary>
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

        // Los sprites se arman una vez y los comparten todos. Son objetos de runtime
        // con HideAndDontSave, así que sobreviven a Play; el chequeo de null es el de
        // Unity, que también cubre el caso de que alguno haya sido destruido.

        private static Sprite SquareSprite()
        {
            if (squareSprite == null)
                squareSprite = PixelSprite.FromRows(SquareRows, new Vector2(0.5f, 0.5f), White);
            return squareSprite;
        }

        private static Sprite SparkSprite()
        {
            if (sparkSprite == null)
                sparkSprite = PixelSprite.FromRows(SparkRows, new Vector2(0.5f, 0.5f), White);
            return sparkSprite;
        }

        private static Sprite DustSprite()
        {
            if (dustSprite == null)
                dustSprite = PixelSprite.FromRows(DustRows, new Vector2(0.5f, 0.5f), DustColor);
            return dustSprite;
        }

        private static Sprite DotSprite()
        {
            if (dotSprite == null)
                dotSprite = PixelSprite.FromRows(DotRows, new Vector2(0.5f, 0.5f), DotColor);
            return dotSprite;
        }

        private static Color32 White(char cell) => cell == '#' ? new Color32(255, 255, 255, 255) : default;

        private static Color32 DustColor(char cell) => cell == '#' ? Dust : default;

        private static Color32 DotColor(char cell)
        {
            switch (cell)
            {
                case 'o': return Edge;
                case '#': return Cream;
                default: return default;
            }
        }
    }
}
