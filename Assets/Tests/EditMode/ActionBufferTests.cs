using BuenosDias.Gameplay;
using NUnit.Framework;

namespace BuenosDias.Tests
{
    /// <summary>
    /// El buffer del timbrazo. Lo que se verifica es que un apretón adelantado
    /// valga un rato y después no, y que se gaste una sola vez.
    /// </summary>
    public sealed class ActionBufferTests
    {
        [Test]
        public void Sin_apretones_no_hay_nada_para_gastar()
        {
            Assert.IsFalse(new ActionBuffer(0.15f).TryConsume(0f));
        }

        [Test]
        public void Un_apreton_adelantado_vale_dentro_de_la_ventana()
        {
            var buffer = new ActionBuffer(0.15f);
            buffer.Record(1f);

            Assert.IsTrue(buffer.TryConsume(1.1f));
        }

        [Test]
        public void Pasada_la_ventana_el_apreton_se_pierde()
        {
            var buffer = new ActionBuffer(0.15f);
            buffer.Record(1f);

            Assert.IsFalse(buffer.TryConsume(1.3f));
        }

        /// <summary>Un apretón no puede tocar el timbre y además insistir.</summary>
        [Test]
        public void Se_gasta_una_sola_vez()
        {
            var buffer = new ActionBuffer(0.15f);
            buffer.Record(1f);

            Assert.IsTrue(buffer.TryConsume(1.05f));
            Assert.IsFalse(buffer.TryConsume(1.06f));
        }

        [Test]
        public void Con_ventana_cero_solo_vale_en_el_mismo_instante()
        {
            var buffer = new ActionBuffer(0f);
            buffer.Record(1f);

            Assert.IsTrue(buffer.TryConsume(1f));

            buffer.Record(2f);
            Assert.IsFalse(buffer.TryConsume(2.01f));
        }

        [Test]
        public void Descartar_borra_lo_guardado()
        {
            var buffer = new ActionBuffer(0.15f);
            buffer.Record(1f);
            buffer.Clear();

            Assert.IsFalse(buffer.TryConsume(1f));
        }
    }
}
