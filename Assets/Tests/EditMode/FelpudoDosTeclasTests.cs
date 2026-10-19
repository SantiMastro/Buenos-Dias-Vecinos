using BuenosDias.Gameplay;
using NUnit.Framework;

namespace BuenosDias.Tests
{
    /// <summary>
    /// El felpudo con dos teclas (S al subirse, B al bajarse). Lo que se verifica
    /// es por qué existe: cada tecla dice el estado, así que una repetida no lo
    /// invierte como pasaba con el toggle de Espacio, y tampoco confirma ni traba
    /// el timbre de más.
    /// </summary>
    public sealed class FelpudoDosTeclasTests
    {
        [Test]
        public void Subirse_pone_en_el_felpudo()
        {
            Assert.IsTrue(TwoButtonInputSource.SiguienteFelpudo(false, true, false, true));
        }

        [Test]
        public void Bajarse_saca_del_felpudo()
        {
            Assert.IsFalse(TwoButtonInputSource.SiguienteFelpudo(true, false, true, false));
        }

        /// <summary>
        /// La placa reintenta y vuelve a mandar la tecla al salir de la pausa: con
        /// el toggle eso invertía el felpudo, con dos teclas no cambia nada.
        /// </summary>
        [Test]
        public void Una_tecla_repetida_no_invierte_el_felpudo()
        {
            Assert.IsTrue(TwoButtonInputSource.SiguienteFelpudo(true, true, false, true));
            Assert.IsFalse(TwoButtonInputSource.SiguienteFelpudo(false, false, true, false));
        }

        [Test]
        public void Sin_teclas_queda_como_estaba()
        {
            Assert.IsTrue(TwoButtonInputSource.SiguienteFelpudo(true, false, false, false));
            Assert.IsFalse(TwoButtonInputSource.SiguienteFelpudo(false, false, false, false));
        }

        [Test]
        public void Las_dos_en_el_mismo_cuadro_manda_la_que_sigue_apretada()
        {
            Assert.IsTrue(TwoButtonInputSource.SiguienteFelpudo(false, true, true, true));
            Assert.IsFalse(TwoButtonInputSource.SiguienteFelpudo(true, true, true, false));
        }

        [Test]
        public void Un_cambio_cuenta_como_B()
        {
            Assert.IsTrue(TwoButtonInputSource.CuentaComoB(false, true, InputContext.Caminando));
            Assert.IsTrue(TwoButtonInputSource.CuentaComoB(true, false, InputContext.EnElFelpudo));
        }

        /// <summary>Una tecla repetida no confirma la religión ni traba el timbre.</summary>
        [Test]
        public void Sin_cambio_no_cuenta_como_B()
        {
            Assert.IsFalse(TwoButtonInputSource.CuentaComoB(true, true, InputContext.EnElFelpudo));
            Assert.IsFalse(TwoButtonInputSource.CuentaComoB(false, false, InputContext.Seleccion));
        }

        /// <summary>
        /// Se confirma subiéndose a la plancha; bajarse después no tiene que
        /// confirmar otra vez.
        /// </summary>
        [Test]
        public void En_la_seleccion_solo_subirse_confirma()
        {
            Assert.IsTrue(TwoButtonInputSource.CuentaComoB(false, true, InputContext.Seleccion));
            Assert.IsFalse(TwoButtonInputSource.CuentaComoB(true, false, InputContext.Seleccion));
        }
    }
}
