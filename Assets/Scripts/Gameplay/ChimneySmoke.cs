using BuenosDias.Config;
using BuenosDias.Presentation;
using UnityEngine;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// La señal de chimenea: una chimenea de ladrillo sobre la losa, con
    /// bocanadas de humo que suben y se desvanecen.
    ///
    /// Es una PISTA, como el resto de las señales: sube la probabilidad de que
    /// haya alguien, no la garantiza. El humo se ve desde lejos, antes que
    /// cualquier cosa de la fachada, y eso le da al jugador tiempo de decidir si
    /// frena.
    ///
    /// Todo se dibuja por código a partir de dibujos en texto, y los sprites los
    /// comparten todas las casas. El humo es un pool fijo de bocanadas: la casa
    /// entra y sale del pool todo el tiempo y crear objetos por bocanada metería
    /// basura en cada scroll. Los tiempos salen de <see cref="SignalVisualsConfig"/>
    /// y se leen cada cuadro.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChimneySmoke : MonoBehaviour
    {
        /// <summary>
        /// Chimenea de 10 × 14 px: sombrero arriba y ladrillos con juntas
        /// corridas. 'o' es borde, '#' ladrillo, '=' junta.
        /// </summary>
        private static readonly string[] ChimneyRows =
        {
            "oooooooooo",
            "o########o",
            "oooooooooo",
            ".o######o.",
            ".o#=##=#o.",
            ".o######o.",
            ".o=##=##o.",
            ".o######o.",
            ".o#=##=#o.",
            ".o######o.",
            ".o=##=##o.",
            ".o######o.",
            ".o######o.",
            ".o######o."
        };

        private static readonly string[] SmallPuffRows =
        {
            ".#.",
            "###",
            ".#."
        };

        private static readonly string[] BigPuffRows =
        {
            ".###.",
            "#####",
            "#####",
            "#####",
            ".###."
        };

        private static readonly Color32 Edge = new Color32(0x1D, 0x16, 0x38, 0xFF);
        private static readonly Color32 Brick = new Color32(0xA8, 0x45, 0x5A, 0xFF);
        private static readonly Color32 Mortar = new Color32(0x7A, 0x2F, 0x3D, 0xFF);
        private static readonly Color32 Smoke = new Color32(0xC9, 0xC2, 0xB4, 0xFF);

        /// <summary>
        /// Tope del pool. Con los valores por defecto viven cuatro bocanadas a la
        /// vez; el techo deja margen para tunear el ritmo en Play sin quedarse sin.
        /// </summary>
        private const int MaxPuffs = 12;

        /// <summary>
        /// Píxeles que la base se hunde en la losa. La chimenea va DETRÁS de la
        /// losa, así que ese tramo lo tapa el techo y parece que sale de adentro.
        /// </summary>
        private const float EmbedPixels = 3f;

        private struct Puff
        {
            public SpriteRenderer Renderer;
            public float Age;
            public float Drift;
            public bool Alive;
        }

        private static Sprite chimneySprite;
        private static Sprite smallPuff;
        private static Sprite bigPuff;

        private readonly Puff[] puffs = new Puff[MaxPuffs];
        private SignalVisualsConfig visuals;
        private SpriteRenderer chimney;
        private float topY;
        private float spawnLeft;

        /// <summary>
        /// Arma la chimenea y su pool de humo colgados de la casa. La llama la
        /// casa UNA vez, al entrar al pool: después solo se prende y se apaga.
        /// </summary>
        public static ChimneySmoke Create(Transform house, SpriteRenderer roof, SignalVisualsConfig visuals)
        {
            var root = new GameObject("Chimney");
            root.transform.SetParent(house, false);

            var smoke = root.AddComponent<ChimneySmoke>();
            smoke.visuals = SignalVisualsConfig.OrDefault(visuals);
            smoke.chimney = NewRenderer(root.transform, "Body", ChimneySprite(), roof, roof.sortingOrder - 1);
            smoke.topY = smoke.chimney.sprite.rect.height / smoke.chimney.sprite.pixelsPerUnit;

            for (int i = 0; i < MaxPuffs; i++)
            {
                smoke.puffs[i].Renderer = NewRenderer(
                    root.transform, $"Puff_{i}", SmallPuff(), roof, roof.sortingOrder + 2);
            }

            smoke.Hide();
            return smoke;
        }

        /// <summary>
        /// Prende la señal con la BASE de la chimenea en esa posición local de la
        /// casa, que es el tope de la losa.
        /// </summary>
        public void Show(Vector3 roofTopLocalPosition)
        {
            transform.localPosition =
                roofTopLocalPosition - new Vector3(0f, ProjectConstants.ToUnits(EmbedPixels), 0f);

            enabled = true;
            chimney.enabled = true;
            spawnLeft = Random.Range(0f, visuals.PuffEverySeconds);
            ClearPuffs();
        }

        /// <summary>Apaga la señal.</summary>
        public void Hide()
        {
            enabled = false;
            if (chimney != null) chimney.enabled = false;
            ClearPuffs();
        }

        private void Update()
        {
            spawnLeft -= Time.deltaTime;
            if (spawnLeft <= 0f)
            {
                Spawn();
                spawnLeft += visuals.PuffEverySeconds;
            }

            for (int i = 0; i < MaxPuffs; i++)
            {
                if (!puffs[i].Alive) continue;

                puffs[i].Age += Time.deltaTime;
                if (puffs[i].Age >= visuals.PuffLifeSeconds)
                {
                    puffs[i].Alive = false;
                    puffs[i].Renderer.enabled = false;
                    continue;
                }

                Place(ref puffs[i]);
            }
        }

        private void Spawn()
        {
            for (int i = 0; i < MaxPuffs; i++)
            {
                if (puffs[i].Alive) continue;

                // El corrimiento tira más para un lado que para el otro: es viento,
                // y un humo que se abre parejo a los dos lados se lee como vapor.
                float drift = visuals.PuffDriftPixels;
                puffs[i].Age = 0f;
                puffs[i].Drift = Random.Range(-drift * 0.4f, drift);
                puffs[i].Alive = true;
                puffs[i].Renderer.enabled = true;
                Place(ref puffs[i]);
                return;
            }
        }

        /// <summary>
        /// Sube la bocanada, la corre con el viento, la agranda a mitad de camino
        /// y la desvanece de a escalones.
        /// </summary>
        private void Place(ref Puff puff)
        {
            float t = puff.Age / visuals.PuffLifeSeconds;
            float ppu = ProjectConstants.PixelsPerUnit;

            float x = ParallaxLayer.Snap(ProjectConstants.ToUnits(puff.Drift * t), ppu);
            float y = ParallaxLayer.Snap(topY + ProjectConstants.ToUnits(visuals.PuffRisePixels * t), ppu);
            puff.Renderer.transform.localPosition = new Vector3(x, y, 0f);

            puff.Renderer.sprite = t < 0.4f ? SmallPuff() : BigPuff();
            puff.Renderer.color = new Color(1f, 1f, 1f, visuals.SmokeMaxAlpha * visuals.Step(1f - t));
        }

        private void ClearPuffs()
        {
            for (int i = 0; i < MaxPuffs; i++)
            {
                puffs[i].Alive = false;
                if (puffs[i].Renderer != null) puffs[i].Renderer.enabled = false;
            }
        }

        private static SpriteRenderer NewRenderer(
            Transform parent, string name, Sprite sprite, SpriteRenderer reference, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerID = reference.sortingLayerID;
            renderer.sortingOrder = sortingOrder;
            renderer.sharedMaterial = reference.sharedMaterial;
            renderer.enabled = false;
            return renderer;
        }

        private static Sprite ChimneySprite()
        {
            if (chimneySprite == null)
                chimneySprite = PixelSprite.FromRows(ChimneyRows, new Vector2(0.5f, 0f), ChimneyColor);

            return chimneySprite;
        }

        private static Sprite SmallPuff()
        {
            if (smallPuff == null)
                smallPuff = PixelSprite.FromRows(SmallPuffRows, new Vector2(0.5f, 0.5f), SmokeColor);

            return smallPuff;
        }

        private static Sprite BigPuff()
        {
            if (bigPuff == null)
                bigPuff = PixelSprite.FromRows(BigPuffRows, new Vector2(0.5f, 0.5f), SmokeColor);

            return bigPuff;
        }

        private static Color32 ChimneyColor(char cell)
        {
            switch (cell)
            {
                case 'o': return Edge;
                case '#': return Brick;
                case '=': return Mortar;
                default: return default;
            }
        }

        private static Color32 SmokeColor(char cell) => cell == '#' ? Smoke : default;
    }
}
