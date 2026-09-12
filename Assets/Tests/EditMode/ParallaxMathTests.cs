using BuenosDias.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace BuenosDias.Tests
{
    /// <summary>
    /// La cuenta del parallax. Lo que se verifica es lo que hacía temblar las
    /// capas: que la X caiga siempre en la grilla de píxeles y que, con la cámara
    /// avanzando, la capa nunca retroceda en pantalla.
    /// </summary>
    public sealed class ParallaxMathTests
    {
        private const float Ppu = 32f;

        [Test]
        public void Con_factor_uno_la_capa_no_se_mueve()
        {
            Assert.AreEqual(0f, ParallaxLayer.LayerX(0f, 10.37f, 0f, 1f, Ppu), 1e-5f);
        }

        [Test]
        public void Con_factor_cero_queda_clavada_a_la_camara()
        {
            Assert.AreEqual(10f, ParallaxLayer.LayerX(0f, 10f, 0f, 0f, Ppu), 1e-5f);
        }

        [TestCase(0.18f)]
        [TestCase(0.55f)]
        [TestCase(1.25f)]
        public void Siempre_cae_en_la_grilla(float factor)
        {
            for (float camera = 0f; camera < 6f; camera += 0.0123f)
            {
                float pixels = ParallaxLayer.LayerX(0.4f, camera, 0.013f, factor, Ppu) * Ppu;
                Assert.AreEqual(Mathf.Round(pixels), pixels, 1e-3f, $"cámara en {camera}");
            }
        }

        /// <summary>
        /// La cámara avanza a la derecha, así que en pantalla la capa solo puede
        /// quedarse o irse a la izquierda. Con la X de cámara sin redondear, el
        /// corrimiento oscilaba medio píxel y la capa volvía uno para atrás.
        /// </summary>
        [TestCase(0.18f)]
        [TestCase(0.55f)]
        [TestCase(1.25f)]
        public void En_pantalla_nunca_retrocede(float factor)
        {
            float previous = float.PositiveInfinity;

            for (float camera = 0f; camera < 6f; camera += 0.0071f)
            {
                float onScreen = ParallaxLayer.LayerX(0f, camera, 0f, factor, Ppu)
                                 - ParallaxLayer.Snap(camera, Ppu);

                Assert.LessOrEqual(onScreen, previous + 1e-4f, $"cámara en {camera}");
                previous = onScreen;
            }
        }
    }
}
