using BuenosDias.Config;
using NUnit.Framework;
using UnityEngine;

namespace BuenosDias.Tests
{
    /// <summary>
    /// El encendido de ventanas según lo habitada que se ve la casa. Lo que se
    /// verifica es que la luz sea otra lectura de las MISMAS señales: más chance,
    /// más luz y antes; poca chance, a oscuras.
    /// </summary>
    public sealed class WindowLightTests
    {
        private static DayCycleConfig Cycle() => ScriptableObject.CreateInstance<DayCycleConfig>();

        [Test]
        public void Una_casa_que_se_ve_vacia_no_prende_nunca()
        {
            DayCycleConfig cycle = Cycle();

            Assert.AreEqual(0f, cycle.WindowLightAt(1f, 0.1f));
        }

        [Test]
        public void Una_casa_que_se_ve_habitada_prende_antes_que_una_neutra()
        {
            DayCycleConfig cycle = Cycle();
            float early = cycle.LightsOnFraction - 0.05f;

            Assert.Greater(cycle.WindowLightAt(early, 0.95f), 0f);
            Assert.AreEqual(0f, cycle.WindowLightAt(early, 0.5f));
        }

        /// <summary>Una casa neutra prende igual que antes de este cambio.</summary>
        [Test]
        public void Una_casa_neutra_prende_como_siempre()
        {
            DayCycleConfig cycle = Cycle();

            for (float progress = 0f; progress <= 1f; progress += 0.05f)
                Assert.AreEqual(cycle.WindowLightAt(progress), cycle.WindowLightAt(progress, 0.5f), 1e-5f);
        }

        [Test]
        public void La_segunda_ventana_solo_se_prende_en_las_bien_habitadas()
        {
            DayCycleConfig cycle = Cycle();

            Assert.AreEqual(0f, cycle.SecondWindowLightAt(1f, 0.5f));
            Assert.Greater(cycle.SecondWindowLightAt(1f, 0.9f), 0f);
        }

        [Test]
        public void Las_senales_dibujadas_por_codigo_tienen_cupo()
        {
            var config = ScriptableObject.CreateInstance<HouseGenConfig>();

            Assert.AreEqual(1, config.CapacityFor(SignalMountMode.SiluetaEnVentana));
            Assert.AreEqual(1, config.CapacityFor(SignalMountMode.ChimeneaEnTecho));
        }
    }
}
