using BuenosDias.Simulation;
using NUnit.Framework;

namespace BuenosDias.Tests
{
    /// <summary>
    /// La tabla de récords. Lo que se verifica es que se ordene y se recorte sola,
    /// y que en un empate gane el que llegó primero.
    /// </summary>
    public sealed class HighscoreTableTests
    {
        [Test]
        public void Lo_guardado_desordenado_sale_de_mayor_a_menor()
        {
            var table = new HighscoreTable(10, new[] { E("AAA", 3), E("BBB", 9), E("CCC", 5) });

            CollectionAssert.AreEqual(new[] { 9, 5, 3 }, Scores(table));
        }

        [Test]
        public void Lo_guardado_de_mas_se_recorta_al_tope()
        {
            var table = new HighscoreTable(2, new[] { E("AAA", 3), E("BBB", 9), E("CCC", 5) });

            CollectionAssert.AreEqual(new[] { 9, 5 }, Scores(table));
        }

        [Test]
        public void Un_record_nuevo_devuelve_su_puesto_y_empuja_a_los_de_abajo()
        {
            var table = new HighscoreTable(3, new[] { E("AAA", 9), E("BBB", 5), E("CCC", 3) });

            Assert.AreEqual(1, table.Insert("zzz", 7));
            CollectionAssert.AreEqual(new[] { 9, 7, 5 }, Scores(table));
            Assert.AreEqual("ZZZ", table.Entries[1].Initials);
        }

        [Test]
        public void En_un_empate_gana_el_que_llego_primero()
        {
            var table = new HighscoreTable(3, new[] { E("AAA", 5) });

            Assert.AreEqual(1, table.Insert("BBB", 5));
            Assert.AreEqual("AAA", table.Entries[0].Initials);
        }

        [Test]
        public void Con_la_tabla_llena_empatar_al_ultimo_no_alcanza()
        {
            var table = new HighscoreTable(2, new[] { E("AAA", 9), E("BBB", 5) });

            Assert.IsFalse(table.Qualifies(5));
            Assert.AreEqual(-1, table.Insert("CCC", 5));
            Assert.AreEqual(2, table.Entries.Count);
        }

        [Test]
        public void Con_lugar_libre_entra_cualquiera_desde_el_minimo()
        {
            var table = new HighscoreTable(10, null, minimumScore: 1);

            Assert.IsTrue(table.Qualifies(1));
            Assert.IsFalse(table.Qualifies(0));
        }

        [Test]
        public void Los_empates_guardados_conservan_su_orden()
        {
            var table = new HighscoreTable(10, new[] { E("AAA", 5), E("BBB", 5), E("CCC", 5) });

            Assert.AreEqual("AAA", table.Entries[0].Initials);
            Assert.AreEqual("BBB", table.Entries[1].Initials);
            Assert.AreEqual("CCC", table.Entries[2].Initials);
        }

        private static HighscoreEntry E(string initials, int score) => new HighscoreEntry(initials, score);

        private static int[] Scores(HighscoreTable table)
        {
            var scores = new int[table.Entries.Count];
            for (int i = 0; i < scores.Length; i++) scores[i] = table.Entries[i].Score;
            return scores;
        }
    }
}
