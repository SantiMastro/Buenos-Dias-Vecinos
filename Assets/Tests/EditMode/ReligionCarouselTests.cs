using BuenosDias.Simulation;
using NUnit.Framework;

namespace BuenosDias.Tests
{
    /// <summary>
    /// El ciclo de religiones. Lo que se verifica es que dar la vuelta funcione y
    /// que un catálogo vacío no devuelva un índice que después indexa una lista.
    /// </summary>
    public sealed class ReligionCarouselTests
    {
        [Test]
        public void Arranca_en_la_primera()
        {
            var carousel = new ReligionCarousel(5);

            Assert.AreEqual(0, carousel.Index);
            Assert.IsFalse(carousel.IsEmpty);
        }

        [Test]
        public void Avanza_de_a_una()
        {
            var carousel = new ReligionCarousel(5);

            Assert.AreEqual(1, carousel.Next());
            Assert.AreEqual(2, carousel.Next());
            Assert.AreEqual(2, carousel.Index);
        }

        /// <summary>
        /// Con un solo botón no hay "atrás": la única forma de volver a la primera
        /// es seguir avanzando. Sin la vuelta, el jugador queda trabado en la
        /// última opción que tocó.
        /// </summary>
        [Test]
        public void Da_la_vuelta_al_llegar_al_final()
        {
            var carousel = new ReligionCarousel(3);
            carousel.Next();
            carousel.Next();

            Assert.AreEqual(2, carousel.Index);
            Assert.AreEqual(0, carousel.Next());
        }

        [Test]
        public void Con_una_sola_opcion_se_queda_en_ella()
        {
            var carousel = new ReligionCarousel(1);

            Assert.AreEqual(0, carousel.Next());
            Assert.AreEqual(0, carousel.Next());
        }

        /// <summary>
        /// Un catálogo vacío devuelve −1, no 0. Con 0 el error aparecería recién al
        /// indexar la lista, lejos de la causa.
        /// </summary>
        [Test]
        public void Sin_religiones_el_indice_es_menos_uno()
        {
            var carousel = new ReligionCarousel(0);

            Assert.IsTrue(carousel.IsEmpty);
            Assert.AreEqual(-1, carousel.Index);
            Assert.AreEqual(-1, carousel.Next());
        }

        [Test]
        public void Una_cantidad_negativa_se_trata_como_vacia()
        {
            var carousel = new ReligionCarousel(-3);

            Assert.IsTrue(carousel.IsEmpty);
            Assert.AreEqual(0, carousel.Count);
            Assert.AreEqual(-1, carousel.Index);
        }
    }
}
