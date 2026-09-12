using BuenosDias.Simulation;
using NUnit.Framework;

namespace BuenosDias.Tests
{
    /// <summary>
    /// La carga de iniciales con un botón: toque = letra siguiente, mantener =
    /// fijar. Lo delicado es que soltar un mantenido no corra la letra nueva, y
    /// que un botón que ya venía apretado no fije nada solo.
    /// </summary>
    public sealed class InitialsEntryTests
    {
        private const float Hold = 0.6f;

        [Test]
        public void Arranca_todo_en_A()
        {
            InitialsEntry entry = New();

            Assert.AreEqual("AAA", entry.Initials);
            Assert.AreEqual(0, entry.Cursor);
        }

        [Test]
        public void Un_toque_pasa_a_la_letra_siguiente()
        {
            InitialsEntry entry = New();

            Assert.AreEqual(InitialsStep.CambioLetra, Tap(entry, 0f));
            Assert.AreEqual('B', entry.LetterAt(0));
        }

        /// <summary>Al apretar todavía no se sabe si es toque o mantenido.</summary>
        [Test]
        public void Apretar_no_cambia_la_letra_hasta_soltar()
        {
            InitialsEntry entry = New();

            Assert.AreEqual(InitialsStep.Nada, entry.Update(true, true, 0f));
            Assert.AreEqual('A', entry.LetterAt(0));
        }

        [Test]
        public void Despues_de_la_ultima_letra_vuelve_a_la_primera()
        {
            var entry = new InitialsEntry(1, "AB", Hold);

            Tap(entry, 0f);
            Tap(entry, 1f);

            Assert.AreEqual('A', entry.LetterAt(0));
        }

        [Test]
        public void Mantener_fija_la_letra_y_pasa_a_la_proxima()
        {
            InitialsEntry entry = New();
            Tap(entry, 0f);

            Assert.AreEqual(InitialsStep.LetraFijada, HoldDown(entry, 1f));
            Assert.AreEqual(1, entry.Cursor);
            Assert.AreEqual('B', entry.LetterAt(0));
        }

        [Test]
        public void Soltar_despues_de_fijar_no_corre_la_letra_nueva()
        {
            InitialsEntry entry = New();

            HoldDown(entry, 0f);

            Assert.AreEqual('A', entry.LetterAt(1));
        }

        [Test]
        public void Fijar_la_ultima_completa_las_iniciales()
        {
            InitialsEntry entry = New();

            Tap(entry, 0f);        // B
            HoldDown(entry, 1f);   // fija B
            HoldDown(entry, 3f);   // fija A
            Tap(entry, 5f);
            Tap(entry, 6f);        // C

            Assert.AreEqual(InitialsStep.Completo, HoldDown(entry, 7f));
            Assert.IsTrue(entry.IsComplete);
            Assert.AreEqual("BAC", entry.Initials);
        }

        /// <summary>Un click que venía de la partida no puede fijar la primera letra solo.</summary>
        [Test]
        public void Un_boton_que_ya_venia_apretado_no_fija_nada()
        {
            InitialsEntry entry = New();

            entry.Update(false, true, 0f);

            Assert.AreEqual(InitialsStep.Nada, entry.Update(false, true, 5f));
            Assert.AreEqual(0, entry.Cursor);
        }

        [Test]
        public void Un_toque_mas_corto_que_un_cuadro_cuenta()
        {
            InitialsEntry entry = New();

            Assert.AreEqual(InitialsStep.CambioLetra, entry.Update(true, false, 0f));
            Assert.AreEqual('B', entry.LetterAt(0));
        }

        /// <summary>Soltar y volver a apretar entre dos cuadros: son dos toques.</summary>
        [Test]
        public void Dos_toques_sin_ver_el_soltar_cuentan_los_dos()
        {
            InitialsEntry entry = New();

            entry.Update(true, true, 0f);
            Assert.AreEqual(InitialsStep.CambioLetra, entry.Update(true, true, 0.1f));
            Assert.AreEqual(InitialsStep.CambioLetra, entry.Update(false, false, 0.15f));

            Assert.AreEqual('C', entry.LetterAt(0));
        }

        [Test]
        public void Completar_por_tiempo_guarda_como_esta()
        {
            InitialsEntry entry = New();
            Tap(entry, 0f);

            entry.Complete();

            Assert.IsTrue(entry.IsComplete);
            Assert.AreEqual("BAA", entry.Initials);
            Assert.AreEqual(InitialsStep.Nada, entry.Update(true, true, 1f));
        }

        [Test]
        public void El_medidor_sube_con_el_mantenido_y_se_vacia_al_fijar()
        {
            InitialsEntry entry = New();
            entry.Update(true, true, 0f);

            Assert.AreEqual(0.5f, entry.HoldProgress(Hold * 0.5f), 1e-4f);

            entry.Update(false, true, Hold);
            Assert.AreEqual(0f, entry.HoldProgress(Hold));
        }

        private static InitialsEntry New() => new InitialsEntry(3, InitialsEntry.DefaultAlphabet, Hold);

        /// <summary>Apretar y soltar enseguida.</summary>
        private static InitialsStep Tap(InitialsEntry entry, float at)
        {
            entry.Update(true, true, at);
            return entry.Update(false, false, at + 0.05f);
        }

        /// <summary>
        /// Apretar, sostener hasta fijar y soltar. Devuelve lo que pasó al fijar.
        ///
        /// Sostiene un poco MÁS que el umbral y no justo el umbral: en float,
        /// 7,6 − 7 da 0,5999999 y el mantenido no llegaba. Jugando nunca se cae
        /// exacto en el borde; el test tampoco tiene que depender de eso.
        /// </summary>
        private static InitialsStep HoldDown(InitialsEntry entry, float at)
        {
            entry.Update(true, true, at);
            InitialsStep step = entry.Update(false, true, at + Hold + 0.01f);
            entry.Update(false, false, at + Hold + 0.06f);
            return step;
        }
    }
}
