using BuenosDias.Config;
using NUnit.Framework;

namespace BuenosDias.Tests
{
    /// <summary>
    /// Cuánta gente se va al fallar una puerta.
    ///
    /// Se testea porque el castigo estaba definido en la spec pero nadie había
    /// verificado que fuera <see cref="FollowerPenaltyMode.BajarDeTramo"/> lo que
    /// corría de verdad, ni qué forma tenía la curva.
    /// </summary>
    public sealed class FollowerPenaltyTests
    {
        [Test]
        public void Una_comitiva_chica_no_pierde_a_nadie()
        {
            FollowerConfig config = ConfigFactory.Followers(abandonThreshold: 5);

            Assert.AreEqual(4, config.ApplyPenalty(4));
            Assert.AreEqual(0, config.ApplyPenalty(0));
        }

        [Test]
        public void Bajar_de_tramo_cae_al_tope_del_tramo_de_abajo()
        {
            FollowerConfig config = ConfigFactory.Followers(
                penaltyMode: FollowerPenaltyMode.BajarDeTramo);

            Assert.AreEqual(7, config.ApplyPenalty(10), "Con tramos de 4, abajo de 8 está el 7.");
            Assert.AreEqual(3, config.ApplyPenalty(6), "Abajo de 4 está el 3.");
        }

        [Test]
        public void Quitar_uno_saca_uno_solo()
        {
            FollowerConfig config = ConfigFactory.Followers(
                penaltyMode: FollowerPenaltyMode.QuitarUno);

            Assert.AreEqual(9, config.ApplyPenalty(10));
            Assert.AreEqual(5, config.ApplyPenalty(6));
        }

        [Test]
        public void La_comitiva_nunca_queda_negativa()
        {
            FollowerConfig config = ConfigFactory.Followers(abandonThreshold: 0);

            Assert.GreaterOrEqual(config.ApplyPenalty(0), 0);
            Assert.GreaterOrEqual(config.ApplyPenalty(1), 0);
        }

        /// <summary>
        /// ⚠️ Esto NO es un bug, es la forma que tiene "caer al tramo de abajo", y
        /// queda escrito acá porque sorprende: fallar con 8 cuesta UNA persona y
        /// fallar con 7 cuesta CUATRO. El costo de equivocarse depende de en qué
        /// parte del tramo estés, y el jugador no ve los tramos.
        /// </summary>
        [Test]
        public void El_castigo_es_desparejo_dentro_del_tramo()
        {
            FollowerConfig config = ConfigFactory.Followers(
                penaltyMode: FollowerPenaltyMode.BajarDeTramo);

            Assert.AreEqual(1, 8 - config.ApplyPenalty(8), "Al borde de abajo se pierde uno.");
            Assert.AreEqual(4, 7 - config.ApplyPenalty(7), "Al borde de arriba se pierden cuatro.");
        }
    }
}
