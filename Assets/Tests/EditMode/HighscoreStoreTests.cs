using System.IO;
using BuenosDias.Gameplay;
using BuenosDias.Simulation;
using NUnit.Framework;

namespace BuenosDias.Tests
{
    /// <summary>
    /// El archivo de récords en disco, en una carpeta temporal. Lo que se verifica
    /// es que lo guardado vuelva, que la escritura en dos pasos no deje basura y
    /// que un archivo roto se aparte en vez de pisarse.
    /// </summary>
    public sealed class HighscoreStoreTests
    {
        private const string FileName = "highscores.json";

        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(
                Path.GetTempPath(), "bdv-highscores-" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void Sin_archivo_carga_una_tabla_vacia()
        {
            HighscoreSave loaded = new HighscoreStore(directory, FileName).Load();

            Assert.IsFalse(loaded.WasUnreadable);
            Assert.AreEqual(0, loaded.EntriesFor("Aspiradoras").Count);
        }

        [Test]
        public void Lo_guardado_vuelve_al_abrir_de_nuevo()
        {
            var save = new HighscoreSave();
            save.Set("Aspiradoras", new[] { new HighscoreEntry("ABC", 12) });

            Assert.IsTrue(new HighscoreStore(directory, FileName).Save(save));

            HighscoreSave loaded = new HighscoreStore(directory, FileName).Load();
            Assert.AreEqual("ABC", loaded.EntriesFor("Aspiradoras")[0].Initials);
            Assert.AreEqual(12, loaded.EntriesFor("Aspiradoras")[0].Score);
        }

        [Test]
        public void Guardar_encima_reemplaza_y_no_deja_el_temporal()
        {
            var store = new HighscoreStore(directory, FileName);
            var save = new HighscoreSave();

            save.Set("Aspiradoras", new[] { new HighscoreEntry("AAA", 1) });
            store.Save(save);
            save.Set("Aspiradoras", new[] { new HighscoreEntry("BBB", 2) });
            store.Save(save);

            Assert.AreEqual("BBB", store.Load().EntriesFor("Aspiradoras")[0].Initials);
            Assert.IsFalse(File.Exists(store.FilePath + ".tmp"));
        }

        [Test]
        public void Un_archivo_ilegible_se_aparta_y_no_se_pisa()
        {
            var store = new HighscoreStore(directory, FileName);
            Directory.CreateDirectory(directory);
            File.WriteAllText(store.FilePath, "basura");

            HighscoreSave loaded = store.Load();

            Assert.IsTrue(loaded.WasUnreadable);
            Assert.IsFalse(File.Exists(store.FilePath));
            Assert.AreEqual(1, Directory.GetFiles(directory, FileName + ".ilegible-*").Length);
        }
    }
}
