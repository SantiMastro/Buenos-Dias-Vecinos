using System.Collections.Generic;
using BuenosDias.Config;
using BuenosDias.Simulation;
using NUnit.Framework;

namespace BuenosDias.Tests
{
    /// <summary>
    /// El reloj de la espera. Es EL momento del juego, así que su timing se
    /// verifica con números y no mirándolo.
    /// </summary>
    public sealed class WaitTimelineTests
    {
        private const float Frame = 1f / 60f;

        /// <summary>Corre el reloj entero y devuelve en qué segundo pasó cada cosa.</summary>
        private static void Run(
            WaitTimeline timeline, float duration,
            out List<float> tells, out List<float> steps)
        {
            tells = new List<float>();
            steps = new List<float>();

            for (float t = 0f; t <= duration + Frame; t += Frame)
            {
                WaitTick tick = timeline.Advance(Frame);
                if (tick.TellFired) tells.Add(t + Frame);
                if (tick.StepFired) steps.Add(t + Frame);
                if (tick.Finished) break;
            }
        }

        [Test]
        public void No_asoma_antes_de_la_fraccion_configurada()
        {
            var timeline = new WaitTimeline(ConfigFactory.Wait(tellFraction: 0.55f), 2f, true);

            // 1.0 s de 2 s es el 50%, todavía por debajo del 55%.
            for (int i = 0; i < 60; i++)
                Assert.IsFalse(timeline.Advance(Frame).TellFired);

            Assert.IsFalse(timeline.TellShown);
        }

        [Test]
        public void El_primer_asomo_cae_en_la_fraccion_configurada()
        {
            var timeline = new WaitTimeline(ConfigFactory.Wait(tellFraction: 0.55f), 2f, true);
            Run(timeline, 2f, out List<float> tells, out _);

            Assert.IsNotEmpty(tells);
            Assert.AreEqual(1.1f, tells[0], Frame, "0.55 de 2 s");
            Assert.AreEqual(1.1f, timeline.FirstTellAt, 1e-5f);
        }

        /// <summary>
        /// Lo que hace que el asomo sea un tell y no decoración: si la casa está
        /// vacía no hay nadie que asome ni que camine.
        /// </summary>
        [Test]
        public void Una_casa_vacia_no_asoma_ni_hace_pasos()
        {
            var timeline = new WaitTimeline(ConfigFactory.Wait(), 2.8f, false);
            Run(timeline, 2.8f, out List<float> tells, out List<float> steps);

            Assert.IsEmpty(tells);
            Assert.IsEmpty(steps);
            Assert.IsFalse(timeline.TellShown);
        }

        [Test]
        public void Los_pasos_arrancan_con_el_primer_asomo_y_no_antes()
        {
            var timeline = new WaitTimeline(ConfigFactory.Wait(tellFraction: 0.55f), 2.8f, true);
            Run(timeline, 2.8f, out List<float> tells, out List<float> steps);

            Assert.IsNotEmpty(steps);
            Assert.AreEqual(tells[0], steps[0], Frame,
                "los pasos son el respaldo sonoro del asomo, no un aviso anterior");
        }

        [Test]
        public void Los_pasos_aceleran_hacia_la_apertura()
        {
            var timeline = new WaitTimeline(ConfigFactory.Wait(), 2.8f, true);
            Run(timeline, 2.8f, out _, out List<float> steps);

            Assert.GreaterOrEqual(steps.Count, 3, "hacen falta al menos dos intervalos");

            float first = steps[1] - steps[0];
            float last = steps[steps.Count - 1] - steps[steps.Count - 2];
            Assert.Less(last, first, "el último intervalo tiene que ser más corto");
        }

        /// <summary>
        /// Documenta una consecuencia del diseño, no un bug: con el piso de espera
        /// de 1.8 s, el segundo asomo caería en 2.19 s, o sea después del final. La
        /// repetición como red de seguridad solo existe en las esperas largas.
        /// </summary>
        [Test]
        public void Con_la_espera_minima_el_asomo_no_se_repite()
        {
            var config = ConfigFactory.Wait(tellFraction: 0.55f, tellRepeat: 1.2f);

            var shortWait = new WaitTimeline(config, 1.8f, true);
            Run(shortWait, 1.8f, out List<float> shortTells, out _);

            var longWait = new WaitTimeline(config, 2.8f, true);
            Run(longWait, 2.8f, out List<float> longTells, out _);

            Assert.AreEqual(1, shortTells.Count);
            Assert.AreEqual(2, longTells.Count);
        }
    }
}
