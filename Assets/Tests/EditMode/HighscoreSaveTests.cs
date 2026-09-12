using BuenosDias.Simulation;
using NUnit.Framework;

namespace BuenosDias.Tests
{
    /// <summary>
    /// El formato del JSON de récords: que vuelva igual de lo que se escribió, que
    /// las tablas de cada religión no se mezclen y que un archivo roto se detecte
    /// sin tirar.
    /// </summary>
    public sealed class HighscoreSaveTests
    {
        [Test]
        public void Ida_y_vuelta_conserva_las_tablas_de_cada_clave()
        {
            var save = new HighscoreSave();
            save.Set("Aspiradoras", new[] { new HighscoreEntry("ABC", 12), new HighscoreEntry("XYZ", 7) });
            save.Set("Mormones", new[] { new HighscoreEntry("MMM", 3) });

            HighscoreSave loaded = HighscoreSave.FromJson(save.ToJson());

            Assert.IsFalse(loaded.WasUnreadable);
            Assert.AreEqual(2, loaded.EntriesFor("Aspiradoras").Count);
            Assert.AreEqual("ABC", loaded.EntriesFor("Aspiradoras")[0].Initials);
            Assert.AreEqual(12, loaded.EntriesFor("Aspiradoras")[0].Score);
            Assert.AreEqual("MMM", loaded.EntriesFor("Mormones")[0].Initials);
        }

        [Test]
        public void Una_clave_sin_tabla_da_vacio()
        {
            Assert.AreEqual(0, new HighscoreSave().EntriesFor("Aspiradoras").Count);
        }

        [Test]
        public void Guardar_dos_veces_la_misma_clave_reemplaza()
        {
            var save = new HighscoreSave();
            save.Set("Aspiradoras", new[] { new HighscoreEntry("AAA", 1), new HighscoreEntry("BBB", 2) });
            save.Set("Aspiradoras", new[] { new HighscoreEntry("CCC", 3) });

            Assert.AreEqual(1, save.EntriesFor("Aspiradoras").Count);
            Assert.AreEqual("CCC", save.EntriesFor("Aspiradoras")[0].Initials);
        }

        [Test]
        public void Texto_vacio_es_una_tabla_vacia_y_no_un_archivo_roto()
        {
            HighscoreSave loaded = HighscoreSave.FromJson("   ");

            Assert.IsFalse(loaded.WasUnreadable);
            Assert.AreEqual(0, loaded.EntriesFor("Aspiradoras").Count);
        }

        [TestCase("esto no es json")]
        [TestCase("{ \"version\": 1, \"boards\": [ { \"key\": ")]
        public void Un_JSON_roto_se_marca_ilegible_sin_tirar(string json)
        {
            HighscoreSave loaded = HighscoreSave.FromJson(json);

            Assert.IsTrue(loaded.WasUnreadable);
            Assert.AreEqual(0, loaded.EntriesFor("Aspiradoras").Count);
        }
    }
}
