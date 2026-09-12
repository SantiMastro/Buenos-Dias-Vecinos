using BuenosDias.Config;
using BuenosDias.Presentation;
using UnityEngine;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// La señal de silueta: una sombra que cruza detrás de la cortina de la
    /// ventana de tell cada tanto.
    ///
    /// Es una PISTA, como el resto de las señales: sube la probabilidad de que
    /// haya alguien, no la garantiza. Que sea una sombra y no una cara es a
    /// propósito: se lee de reojo, que es como se mira una casa caminando.
    ///
    /// Se dibuja encima de la cortina y translúcida, que es lo que la hace leerse
    /// como alguien pasando del otro lado y no como un muñeco pegado al vidrio.
    /// El sprite sale de un dibujo en texto y lo comparten todas las casas. Los
    /// tiempos y la opacidad salen de <see cref="SignalVisualsConfig"/> y se leen
    /// cada cuadro, así un ajuste en Play se ve en el acto.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WindowSilhouette : MonoBehaviour
    {
        /// <summary>Cabeza, cuello y hombros. 11 × 14 px.</summary>
        private static readonly string[] Rows =
        {
            "....###....",
            "...#####...",
            "...#####...",
            "...#####...",
            "....###....",
            ".....#.....",
            "..#######..",
            ".#########.",
            "###########",
            "###########",
            "###########",
            "###########",
            "###########",
            "###########"
        };

        /// <summary>Violeta oscuro #1D1638 de la paleta.</summary>
        private static readonly Color32 Ink = new Color32(0x1D, 0x16, 0x38, 0xFF);

        private static Sprite shared;

        private SignalVisualsConfig visuals;
        private SpriteRenderer body;
        private float windowWidth;
        private float spriteWidth;
        private float waitLeft;
        private float crossElapsed = -1f;
        private bool leftToRight;

        /// <summary>
        /// Arma la silueta colgada de la ventana. La llama la casa UNA vez, al
        /// entrar al pool: después solo se prende y se apaga.
        /// </summary>
        public static WindowSilhouette Create(
            Transform window, float windowWidthUnits, SpriteRenderer reference, int sortingOrder,
            SignalVisualsConfig visuals)
        {
            var go = new GameObject("Silhouette");
            go.transform.SetParent(window, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SharedSprite();
            renderer.sortingLayerID = reference.sortingLayerID;
            renderer.sortingOrder = sortingOrder;
            renderer.sharedMaterial = reference.sharedMaterial;

            var silhouette = go.AddComponent<WindowSilhouette>();
            silhouette.visuals = SignalVisualsConfig.OrDefault(visuals);
            silhouette.body = renderer;
            silhouette.windowWidth = windowWidthUnits;
            silhouette.spriteWidth = renderer.sprite.rect.width / renderer.sprite.pixelsPerUnit;
            silhouette.Hide();
            return silhouette;
        }

        /// <summary>
        /// Prende la señal. Arranca con una pausa sorteada para que dos casas
        /// vecinas no crucen la sombra en el mismo instante.
        /// </summary>
        public void Show()
        {
            enabled = true;
            body.enabled = false;
            crossElapsed = -1f;
            waitLeft = Random.Range(0.2f, Mathf.Max(0.2f, visuals.MaxPauseSeconds));
        }

        /// <summary>Apaga la señal.</summary>
        public void Hide()
        {
            enabled = false;
            if (body != null) body.enabled = false;
        }

        private void Update()
        {
            if (crossElapsed < 0f)
            {
                waitLeft -= Time.deltaTime;
                if (waitLeft > 0f) return;

                crossElapsed = 0f;
                leftToRight = Random.value < 0.5f;
                body.enabled = true;
            }

            crossElapsed += Time.deltaTime;
            float t = crossElapsed / visuals.CrossSeconds;

            if (t >= 1f)
            {
                body.enabled = false;
                crossElapsed = -1f;
                waitLeft = Random.Range(visuals.MinPauseSeconds, visuals.MaxPauseSeconds);
                return;
            }

            Place(t);
        }

        /// <summary>
        /// Coloca la sombra a lo ancho de la ventana, sin salirse del marco, y la
        /// funde en las puntas del cruce.
        /// </summary>
        private void Place(float t)
        {
            float margin = ProjectConstants.ToUnits(visuals.FrameMarginPixels);
            float near = margin;
            float far = Mathf.Max(margin, windowWidth - margin - spriteWidth);
            float x = leftToRight ? Mathf.Lerp(near, far, t) : Mathf.Lerp(far, near, t);

            transform.localPosition = new Vector3(
                ParallaxLayer.Snap(x, ProjectConstants.PixelsPerUnit),
                ProjectConstants.ToUnits(visuals.SillPixels), 0f);

            // Aparece y se va en las puntas del cruce, de a escalones: un fundido
            // continuo inventa tonos que no están en la paleta.
            float fade = Mathf.Min(t, 1f - t) / visuals.FadeFraction;
            body.color = new Color(1f, 1f, 1f, visuals.SilhouetteMaxAlpha * visuals.Step(fade));
        }

        private static Sprite SharedSprite()
        {
            if (shared == null)
                shared = PixelSprite.FromRows(Rows, Vector2.zero, cell => cell == '#' ? Ink : default);

            return shared;
        }
    }
}
