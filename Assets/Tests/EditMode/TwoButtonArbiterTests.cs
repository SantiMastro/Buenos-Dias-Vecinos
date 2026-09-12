using BuenosDias.Gameplay;
using NUnit.Framework;

namespace BuenosDias.Tests
{
    /// <summary>
    /// El arbitraje de la modalidad de dos botones. Lo que se verifica es el bug
    /// que lo motivó: un solo apretón no puede frenar y tocar el timbre a la vez,
    /// y la traba no puede comerse el golpe del skillcheck.
    /// </summary>
    public sealed class TwoButtonArbiterTests
    {
        private const float Window = 0.1f;

        [Test]
        public void Solo_A_dispara()
        {
            var arbiter = new TwoButtonArbiter(Window);

            TwoButtonFrame frame = arbiter.Resolve(true, false, InputContext.EnElFelpudo, 0f);

            Assert.IsTrue(frame.APulse);
            Assert.IsFalse(frame.ToggleMat);
            Assert.IsFalse(frame.Confirmed);
        }

        [Test]
        public void A_y_B_en_el_mismo_cuadro_frena_sin_tocar_el_timbre()
        {
            var arbiter = new TwoButtonArbiter(Window);

            TwoButtonFrame frame = arbiter.Resolve(true, true, InputContext.Caminando, 0f);

            Assert.IsFalse(frame.APulse);
            Assert.IsTrue(frame.ToggleMat);
        }

        /// <summary>
        /// B sube al felpudo y el predicador queda en la puerta: un A que llega
        /// unos milisegundos después ya se leería como timbrazo.
        /// </summary>
        [Test]
        public void Un_A_que_llega_justo_despues_de_B_no_toca()
        {
            var arbiter = new TwoButtonArbiter(Window);
            arbiter.Resolve(false, true, InputContext.Caminando, 1f);

            TwoButtonFrame frame = arbiter.Resolve(true, false, InputContext.EnElFelpudo, 1.05f);

            Assert.IsFalse(frame.APulse);
        }

        [Test]
        public void Pasada_la_ventana_A_vuelve_a_tocar()
        {
            var arbiter = new TwoButtonArbiter(Window);
            arbiter.Resolve(false, true, InputContext.Caminando, 1f);

            TwoButtonFrame frame = arbiter.Resolve(true, false, InputContext.EnElFelpudo, 1.2f);

            Assert.IsTrue(frame.APulse);
        }

        [Test]
        public void En_la_seleccion_B_confirma_y_no_mueve_el_felpudo()
        {
            var arbiter = new TwoButtonArbiter(Window);

            TwoButtonFrame frame = arbiter.Resolve(false, true, InputContext.Seleccion, 0f);

            Assert.IsTrue(frame.Confirmed);
            Assert.IsFalse(frame.ToggleMat);
        }

        [Test]
        public void En_la_seleccion_A_y_B_juntos_confirman_sin_pasar_de_religion()
        {
            var arbiter = new TwoButtonArbiter(Window);

            TwoButtonFrame frame = arbiter.Resolve(true, true, InputContext.Seleccion, 0f);

            Assert.IsTrue(frame.Confirmed);
            Assert.IsFalse(frame.APulse);
        }

        /// <summary>
        /// Con la puerta abierta B no hace nada hasta que termina la cadena, así
        /// que no tiene por qué comerse un acierto.
        /// </summary>
        [Test]
        public void Con_la_puerta_abierta_B_no_se_come_el_golpe_del_skillcheck()
        {
            var arbiter = new TwoButtonArbiter(Window);

            TwoButtonFrame frame = arbiter.Resolve(true, true, InputContext.Atendido, 0f);

            Assert.IsTrue(frame.APulse);
            Assert.IsTrue(frame.ToggleMat);
        }

        [Test]
        public void En_el_final_A_reinicia_aunque_llegue_con_B()
        {
            var arbiter = new TwoButtonArbiter(Window);

            TwoButtonFrame frame = arbiter.Resolve(true, true, InputContext.Final, 0f);

            Assert.IsTrue(frame.APulse);
        }

        [Test]
        public void Con_ventana_cero_solo_se_descarta_el_mismo_cuadro()
        {
            var arbiter = new TwoButtonArbiter(0f);

            Assert.IsFalse(arbiter.Resolve(true, true, InputContext.Caminando, 1f).APulse);
            Assert.IsTrue(arbiter.Resolve(true, false, InputContext.EnElFelpudo, 1.016f).APulse);
        }
    }
}
