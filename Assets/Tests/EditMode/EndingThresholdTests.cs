using BuenosDias.Config;
using NUnit.Framework;

namespace BuenosDias.Tests
{
    /// <summary>
    /// Qué final sale según la gente que queda atrás al caer la noche.
    ///
    /// Estos tests existen por un bug real: el juego resolvía el final con las
    /// CONVERSIONES ACUMULADAS en vez de con la comitiva que quedaba. Quien
    /// convertía 10 y perdía 3 en el camino veía la ascensión con 7 personas
    /// atrás, y el cartel decía 10.
    /// </summary>
    public sealed class EndingThresholdTests
    {
        [Test]
        public void Sin_nadie_atras_sale_la_crucifixion()
        {
            GameConfig config = ConfigFactory.Game(crucifixionBelow: 1, ascensionAtLeast: 10);

            Assert.AreEqual(EndingKind.Crucifixion, config.ResolveEnding(0, null));
        }

        [Test]
        public void Con_la_comitiva_completa_sale_la_ascension()
        {
            GameConfig config = ConfigFactory.Game(crucifixionBelow: 1, ascensionAtLeast: 10);

            Assert.AreEqual(EndingKind.Ascension, config.ResolveEnding(10, null));
        }

        [Test]
        public void En_el_medio_se_hizo_de_noche()
        {
            GameConfig config = ConfigFactory.Game(crucifixionBelow: 1, ascensionAtLeast: 10);

            Assert.AreEqual(EndingKind.SeHizoDeNoche, config.ResolveEnding(5, null));
        }

        /// <summary>
        /// El corazón del bug: el número que se le pasa CAMBIA el final. Diez
        /// conversiones daban ascensión donde siete seguidores dan la noche, así
        /// que pasar el número equivocado no era un detalle de presentación.
        /// </summary>
        [Test]
        public void Perder_seguidores_en_el_camino_cambia_el_final()
        {
            GameConfig config = ConfigFactory.Game(crucifixionBelow: 1, ascensionAtLeast: 10);

            Assert.AreEqual(EndingKind.Ascension, config.ResolveEnding(10, null),
                "Diez atrás es ascensión.");
            Assert.AreEqual(EndingKind.SeHizoDeNoche, config.ResolveEnding(7, null),
                "Siete atrás no lo es, aunque hayan sido diez conversiones.");
        }

        [Test]
        public void Una_religion_que_no_asciende_cae_al_final_del_medio()
        {
            GameConfig config = ConfigFactory.Game(crucifixionBelow: 1, ascensionAtLeast: 10);
            ReligionDefinition terrenal = ConfigFactory.Religion(canAscend: false);

            Assert.AreEqual(EndingKind.SeHizoDeNoche, config.ResolveEnding(20, terrenal));
        }

        [Test]
        public void Una_religion_que_asciende_si_llega_a_la_ascension()
        {
            GameConfig config = ConfigFactory.Game(crucifixionBelow: 1, ascensionAtLeast: 10);
            ReligionDefinition celestial = ConfigFactory.Religion(canAscend: true);

            Assert.AreEqual(EndingKind.Ascension, config.ResolveEnding(20, celestial));
        }

        [Test]
        public void El_umbral_de_crucifixion_se_respeta_hacia_arriba()
        {
            GameConfig config = ConfigFactory.Game(crucifixionBelow: 3, ascensionAtLeast: 10);

            Assert.AreEqual(EndingKind.Crucifixion, config.ResolveEnding(2, null));
            Assert.AreEqual(EndingKind.SeHizoDeNoche, config.ResolveEnding(3, null));
        }
    }
}
