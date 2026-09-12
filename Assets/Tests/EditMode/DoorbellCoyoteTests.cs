using BuenosDias.Config;
using NUnit.Framework;
using UnityEngine;

namespace BuenosDias.Tests
{
    /// <summary>
    /// El coyote del felpudo. Lo que se verifica es que pasarse por poco todavía
    /// agarre la puerta, que pasarse de más no, y que el perdón no premie: la
    /// puntería en la franja de coyote es cero.
    /// </summary>
    public sealed class DoorbellCoyoteTests
    {
        private static DoorbellConfig Doorbell() => ScriptableObject.CreateInstance<DoorbellConfig>();

        [Test]
        public void Pasarse_por_poco_todavia_agarra_la_puerta()
        {
            DoorbellConfig config = Doorbell();
            float justPast = config.ReachAfter + config.Coyote * 0.5f;

            Assert.IsFalse(config.IsInReach(justPast));
            Assert.IsTrue(config.IsInCoyote(justPast));
            Assert.IsTrue(config.CanGrab(justPast));
        }

        [Test]
        public void En_el_coyote_la_punteria_es_cero()
        {
            DoorbellConfig config = Doorbell();
            float justPast = config.ReachAfter + config.Coyote * 0.5f;

            Assert.AreEqual(0f, config.PrecisionFor(justPast));
        }

        [Test]
        public void Pasarse_de_mas_no_agarra_nada()
        {
            DoorbellConfig config = Doorbell();
            float tooFar = config.ReachAfter + config.Coyote + 0.1f;

            Assert.IsFalse(config.CanGrab(tooFar));
        }

        /// <summary>El coyote es para el que se PASÓ: antes de la puerta no suma nada.</summary>
        [Test]
        public void Antes_de_la_puerta_no_hay_coyote()
        {
            DoorbellConfig config = Doorbell();
            float early = -(config.ReachBefore + config.Coyote * 0.5f);

            Assert.IsFalse(config.IsInCoyote(early));
            Assert.IsFalse(config.CanGrab(early));
        }

        [Test]
        public void Dentro_del_alcance_no_es_coyote()
        {
            DoorbellConfig config = Doorbell();

            Assert.IsFalse(config.IsInCoyote(config.ReachAfter * 0.5f));
            Assert.IsTrue(config.CanGrab(config.ReachAfter * 0.5f));
        }
    }
}
