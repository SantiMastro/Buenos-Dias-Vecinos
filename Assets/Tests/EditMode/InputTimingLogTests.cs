using BuenosDias.DebugTools;
using NUnit.Framework;

namespace BuenosDias.Tests
{
    /// <summary>
    /// El registro de tiempos de la ventana de diagnóstico. Lo que se verifica es
    /// que los números que se van a usar para ajustar el input salgan bien: entre
    /// apretones, cuánto se sostuvo, y cuándo algo es un rebote.
    /// </summary>
    public sealed class InputTimingLogTests
    {
        private const string Space = "/Keyboard/space";
        private const string Click = "/Mouse/leftButton";

        [Test]
        public void El_primer_evento_no_tiene_anterior()
        {
            var log = new InputTimingLog();

            TimedInput first = log.Record(Space, true, 10.0);

            Assert.IsTrue(double.IsNaN(first.SincePreviousAny));
            Assert.IsTrue(double.IsNaN(first.SincePreviousPress));
            Assert.AreEqual(10.0, log.Origin, 1e-9);
        }

        [Test]
        public void Mide_cuanto_estuvo_apretado()
        {
            var log = new InputTimingLog();
            log.Record(Space, true, 1.0);

            TimedInput release = log.Record(Space, false, 1.25);

            Assert.AreEqual(0.25, release.HeldFor, 1e-9);
        }

        [Test]
        public void Mide_el_intervalo_entre_apretones_del_mismo_control()
        {
            var log = new InputTimingLog();
            log.Record(Space, true, 1.0);
            log.Record(Click, true, 1.1);
            log.Record(Space, false, 1.2);

            TimedInput second = log.Record(Space, true, 1.5);

            Assert.AreEqual(0.5, second.SincePreviousPress, 1e-9, "el click del medio no cuenta");
            Assert.AreEqual(0.3, second.SinceRelease, 1e-9);
            Assert.AreEqual(0.3, second.SincePreviousAny, 1e-9);
        }

        /// <summary>
        /// Un reapretón a pocos milisegundos de soltar no es una persona: es el
        /// contacto rebotando o el traductor soltando y volviendo a apretar.
        /// </summary>
        [Test]
        public void Un_reapreton_pegado_a_la_suelta_es_rebote()
        {
            var log = new InputTimingLog(bounceSeconds: 0.03);
            log.Record(Space, true, 1.0);
            log.Record(Space, false, 1.5);

            TimedInput bounce = log.Record(Space, true, 1.51);

            Assert.IsTrue(log.IsBounce(bounce));
            Assert.AreEqual(1, log.Summarize()[0].Bounces);
        }

        [Test]
        public void El_resumen_da_minimo_promedio_y_maximo()
        {
            var log = new InputTimingLog();
            log.Record(Space, true, 0.0);
            log.Record(Space, false, 0.1);
            log.Record(Space, true, 1.0);
            log.Record(Space, false, 1.3);
            log.Record(Space, true, 3.0);

            ControlTiming summary = log.Summarize()[0];

            Assert.AreEqual(3, summary.Presses);
            Assert.AreEqual(1.0, summary.MinInterval, 1e-9);
            Assert.AreEqual(1.5, summary.AverageInterval, 1e-9);
            Assert.AreEqual(2.0, summary.MaxInterval, 1e-9);
            Assert.AreEqual(0.1, summary.MinHeld, 1e-9);
            Assert.AreEqual(0.3, summary.MaxHeld, 1e-9);
        }

        [Test]
        public void Pasada_la_capacidad_se_descartan_los_mas_viejos()
        {
            var log = new InputTimingLog(capacity: 2);
            log.Record(Space, true, 1.0);
            log.Record(Space, false, 2.0);
            log.Record(Space, true, 3.0);

            Assert.AreEqual(2, log.Entries.Count);
            Assert.AreEqual(2.0, log.Entries[0].Time, 1e-9);
        }

        [Test]
        public void A_cero_borra_todo()
        {
            var log = new InputTimingLog();
            log.Record(Space, true, 1.0);
            log.Clear();

            TimedInput fresh = log.Record(Space, true, 5.0);

            Assert.AreEqual(1, log.Entries.Count);
            Assert.IsTrue(double.IsNaN(fresh.SincePreviousPress));
            Assert.AreEqual(5.0, log.Origin, 1e-9);
        }
    }
}
