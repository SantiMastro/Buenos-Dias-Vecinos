using BuenosDias.Simulation;
using NUnit.Framework;

namespace BuenosDias.Tests
{
    /// <summary>
    /// El reloj del día. Lo que se verifica es la economía: que el bono no rompa
    /// el techo, que el final se avise una sola vez, y que el atardecer no vuelva
    /// para atrás cuando se gana tiempo.
    /// </summary>
    public sealed class DayClockTests
    {
        private const float Duration = 72f;

        [Test]
        public void Arranca_con_el_dia_entero()
        {
            var clock = new DayClock(Duration);

            Assert.AreEqual(Duration, clock.Remaining, 1e-4f);
            Assert.AreEqual(0f, clock.Progress, 1e-4f);
            Assert.IsFalse(clock.IsOver);
        }

        [Test]
        public void El_progreso_sigue_al_tiempo_consumido()
        {
            var clock = new DayClock(Duration);
            clock.Advance(Duration * 0.25f);

            Assert.AreEqual(0.25f, clock.Progress, 1e-4f);
        }

        /// <summary>
        /// El final se avisa en UN solo cuadro. Si avisara en todos, quien escucha
        /// dispararía el final una vez por cuadro hasta que alguien lo frene.
        /// </summary>
        [Test]
        public void El_final_se_avisa_una_sola_vez()
        {
            var clock = new DayClock(Duration);

            Assert.IsFalse(clock.Advance(Duration - 1f), "todavía queda día");
            Assert.IsTrue(clock.Advance(2f), "acá termina");
            Assert.IsFalse(clock.Advance(1f), "y no vuelve a avisar");
            Assert.IsTrue(clock.IsOver);
        }

        [Test]
        public void El_tiempo_que_queda_nunca_baja_de_cero()
        {
            var clock = new DayClock(Duration);
            clock.Advance(Duration * 10f);

            Assert.AreEqual(0f, clock.Remaining, 1e-4f);
            Assert.AreEqual(1f, clock.Progress, 1e-4f);
        }

        [Test]
        public void Convertir_devuelve_tiempo()
        {
            var clock = new DayClock(Duration);
            clock.Advance(30f);
            clock.Add(7f);

            Assert.AreEqual(49f, clock.Remaining, 1e-4f);
        }

        [Test]
        public void Fallar_descuenta_tiempo()
        {
            var clock = new DayClock(Duration);
            clock.Advance(30f);
            clock.Add(-3f);

            Assert.AreEqual(39f, clock.Remaining, 1e-4f);
        }

        /// <summary>
        /// Sin techo, encadenar conversiones dejaría un colchón imposible de
        /// gastar y la presión del reloj desaparecería para el buen jugador.
        /// </summary>
        [Test]
        public void El_bono_no_pasa_la_duracion_inicial()
        {
            var clock = new DayClock(Duration);
            clock.Advance(2f);
            clock.Add(50f);

            Assert.AreEqual(Duration, clock.Remaining, 1e-4f);
        }

        [Test]
        public void Un_castigo_grande_puede_terminar_el_dia()
        {
            var clock = new DayClock(Duration);
            clock.Advance(Duration - 2f);
            clock.Add(-5f);

            Assert.AreEqual(0f, clock.Remaining, 1e-4f);
            Assert.IsTrue(clock.IsOver);
        }

        /// <summary>
        /// El sol no vuelve para atrás. Un cielo que se aclara al convertir se lee
        /// como un bug y no como un premio.
        /// </summary>
        [Test]
        public void El_atardecer_no_retrocede_al_ganar_tiempo()
        {
            var clock = new DayClock(Duration);
            clock.Advance(Duration * 0.5f);

            float antes = clock.SunsetProgress;
            clock.Add(20f);

            Assert.Less(clock.Progress, antes, "el progreso instantáneo sí baja");
            Assert.AreEqual(antes, clock.SunsetProgress, 1e-4f, "el atardecer no");
        }

        [Test]
        public void El_atardecer_sigue_subiendo_despues_de_un_bono()
        {
            var clock = new DayClock(Duration);
            clock.Advance(Duration * 0.5f);
            clock.Add(20f);
            clock.Advance(Duration * 0.4f);

            Assert.Greater(clock.SunsetProgress, 0.5f);
        }

        /// <summary>El modo debug de GameConfig: el reloj no corre.</summary>
        [Test]
        public void Congelado_el_reloj_no_avanza()
        {
            var clock = new DayClock(Duration, frozen: true);

            Assert.IsFalse(clock.Advance(1000f));
            Assert.AreEqual(Duration, clock.Remaining, 1e-4f);
            Assert.IsFalse(clock.IsOver);
        }
    }
}
