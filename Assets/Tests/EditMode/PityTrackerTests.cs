using BuenosDias.Config;
using BuenosDias.Simulation;
using NUnit.Framework;

namespace BuenosDias.Tests
{
    /// <summary>
    /// El anti-racha, que es donde un cambio silencioso arruinaría el balance sin
    /// romper nada visible.
    ///
    /// El azar se fija con <see cref="FixedRandom"/> en vez de elegir una semilla:
    /// apenas el pity sube, la probabilidad contra la que se compara cambia, y una
    /// semilla que "nunca abría" pasa a abrir. El umbral de legibilidad se mueve
    /// para elegir si la casa participa del sistema o no.
    /// </summary>
    public sealed class PityTrackerTests
    {
        [Test]
        public void Arranca_en_cero()
        {
            var tracker = new PityTracker(ConfigFactory.Pity());

            Assert.AreEqual(0f, tracker.Pity);
            Assert.AreEqual(0, tracker.EmptyRun);
        }

        [Test]
        public void Una_casa_legible_vacia_sube_el_pity_y_la_racha()
        {
            // Umbral 0: hasta la casa de chance 0 cuenta como legible.
            var tracker = new PityTracker(ConfigFactory.Pity(readableThreshold: 0f));

            Assert.IsFalse(tracker.Resolve(0f, FixedRandom.NeverOpens));
            Assert.AreEqual(0.22f, tracker.Pity, 1e-5f);
            Assert.AreEqual(1, tracker.EmptyRun);
        }

        [Test]
        public void Una_casa_ocupada_resetea_el_pity_y_la_racha()
        {
            var tracker = new PityTracker(ConfigFactory.Pity(readableThreshold: 0f));
            tracker.Resolve(0f, FixedRandom.NeverOpens);
            tracker.Resolve(0f, FixedRandom.NeverOpens);

            Assert.IsTrue(tracker.Resolve(1f, FixedRandom.AlwaysOpens));
            Assert.AreEqual(0f, tracker.Pity);
            Assert.AreEqual(0, tracker.EmptyRun);
        }

        /// <summary>
        /// La mitad "no alimenta" del umbral simétrico. Es la regresión de §10.4:
        /// mientras las casas muertas sumaban pity, tocarlas era combustible barato
        /// para cargar la suerte de las buenas, y la estrategia óptima terminaba
        /// siendo ignorar las señales.
        /// </summary>
        [Test]
        public void Una_casa_por_debajo_del_umbral_no_alimenta_el_pity()
        {
            var tracker = new PityTracker(ConfigFactory.Pity(readableThreshold: 0.42f));

            for (int i = 0; i < 5; i++)
                Assert.IsFalse(tracker.Resolve(0f, FixedRandom.NeverOpens));

            Assert.AreEqual(0f, tracker.Pity, "una casa que se veía muerta no es una decepción");
            Assert.AreEqual(0, tracker.EmptyRun);
        }

        /// <summary>La mitad "no recibe" del mismo umbral.</summary>
        [Test]
        public void Una_casa_por_debajo_del_umbral_no_recibe_pity()
        {
            var tracker = new PityTracker(ConfigFactory.Pity(readableThreshold: 0.42f));

            Assert.AreEqual(0.30f, tracker.ProbabilityFor(0.30f), 1e-5f);
            Assert.IsFalse(tracker.IsGuaranteed(0.30f));
        }

        [Test]
        public void El_pity_acumulado_se_suma_a_las_casas_legibles()
        {
            var tracker = new PityTracker(ConfigFactory.Pity(readableThreshold: 0f));
            tracker.Resolve(0f, FixedRandom.NeverOpens);
            tracker.Resolve(0f, FixedRandom.NeverOpens);

            Assert.AreEqual(0.44f, tracker.ProbabilityFor(0f), 1e-5f);
        }

        [Test]
        public void La_garantia_dura_solo_alcanza_a_las_legibles()
        {
            var tracker = new PityTracker(
                ConfigFactory.Pity(readableThreshold: 0.42f, guaranteedAfter: 3));

            // Tres legibles seguidas que salen vacías. Con NeverOpens no importa
            // cuánto suba el pity: la tirada nunca alcanza.
            for (int i = 0; i < 3; i++) tracker.Resolve(0.42f, FixedRandom.NeverOpens);

            Assert.AreEqual(3, tracker.EmptyRun);
            Assert.IsTrue(tracker.IsGuaranteed(0.42f), "la legible sí entra en la garantía");
            Assert.IsFalse(tracker.IsGuaranteed(0.41f), "la muerta queda exenta");
        }

        [Test]
        public void El_pity_no_pasa_del_techo()
        {
            // La garantía dura se corre fuera de alcance a propósito: con el valor
            // real dispara en la cuarta vacía, abre la puerta y resetea el pity, y
            // el test terminaría midiendo la garantía en vez del techo.
            var tracker = new PityTracker(ConfigFactory.Pity(
                readableThreshold: 0f, increment: 0.22f, cap: 0.5f, guaranteedAfter: 999));

            for (int i = 0; i < 6; i++) tracker.Resolve(0f, FixedRandom.NeverOpens);

            Assert.AreEqual(0.5f, tracker.Pity, 1e-5f);
        }
    }
}
