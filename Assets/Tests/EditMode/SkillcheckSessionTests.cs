using BuenosDias.Config;
using BuenosDias.Simulation;
using NUnit.Framework;

namespace BuenosDias.Tests
{
    /// <summary>
    /// La cadena de objeciones. Lo que se verifica acá es el pegamento entre
    /// eslabones: que fallar corte, que la pausa exista y que los aciertos se
    /// cuenten separados, porque de esa cuenta sale el bono de tiempo.
    /// </summary>
    public sealed class SkillcheckSessionTests
    {
        private const float Frame = 1f / 60f;

        private static SkillcheckSession Build(out SkillcheckConfig config, int links = 1)
        {
            config = ConfigFactory.Skillcheck();
            return new SkillcheckSession(
                config, new SkillcheckSetup(0.5f, 2f, links), FixedRandom.AlwaysOpens);
        }

        [Test]
        public void Una_cadena_de_un_eslabon_se_gana_con_un_acierto()
        {
            SkillcheckSession session = Build(out _);
            PressPerfect(session);

            Assert.AreEqual(SkillcheckSessionState.Convertido, session.State);
            Assert.AreEqual(1, session.PerfectHits);
        }

        /// <summary>La objeción que no se contestó es la que cierra la puerta.</summary>
        [Test]
        public void Fallar_un_eslabon_corta_la_cadena_aunque_queden_eslabones()
        {
            SkillcheckSession session = Build(out _, links: 3);
            session.Advance(0f, true);   // apretar antes de la zona es fallar

            Assert.AreEqual(SkillcheckSessionState.Rechazado, session.State);
            Assert.AreEqual(0, session.LinkIndex, "no se pasa al eslabón siguiente");
        }

        [Test]
        public void Entre_eslabones_no_hay_tirada_en_curso()
        {
            SkillcheckSession session = Build(out _, links: 2);
            PressPerfect(session);

            Assert.AreEqual(SkillcheckSessionState.EnCurso, session.State);
            Assert.IsNull(session.Current, "durante la pausa el aro no se dibuja");
            Assert.AreEqual(1, session.LinkIndex);
        }

        [Test]
        public void Pasada_la_pausa_arranca_el_eslabon_siguiente()
        {
            SkillcheckSession session = Build(out SkillcheckConfig config, links: 2);
            PressPerfect(session);

            bool started = RunPause(session, config.ChainPauseSeconds + Frame);

            Assert.IsTrue(started, "el tick avisa que arrancó un eslabón");
            Assert.IsNotNull(session.Current);
            Assert.AreEqual(0f, session.Current.NeedleAngle, 1e-5f, "la aguja arranca de cero");
        }

        /// <summary>
        /// El botón durante la pausa se traga: el aro no está en pantalla, y así el
        /// rebote del botón de arcade no se come el eslabón siguiente.
        /// </summary>
        [Test]
        public void El_boton_durante_la_pausa_no_hace_nada()
        {
            SkillcheckSession session = Build(out SkillcheckConfig config, links: 2);
            PressPerfect(session);

            session.Advance(Frame, true);

            Assert.AreEqual(SkillcheckSessionState.EnCurso, session.State);
            Assert.IsNull(session.Current);

            RunPause(session, config.ChainPauseSeconds + Frame);
            Assert.IsNotNull(session.Current, "la pausa siguió corriendo igual");
        }

        [Test]
        public void Ganar_todos_los_eslabones_convierte()
        {
            SkillcheckSession session = Build(out SkillcheckConfig config, links: 3);

            for (int link = 0; link < 3; link++)
            {
                if (link > 0) RunPause(session, config.ChainPauseSeconds + Frame);
                PressPerfect(session);
            }

            Assert.AreEqual(SkillcheckSessionState.Convertido, session.State);
            Assert.AreEqual(3, session.PerfectHits);
        }

        /// <summary>La cadena es la misma objeción poniéndose más insistente.</summary>
        [Test]
        public void Cada_eslabon_consecutivo_va_mas_rapido()
        {
            SkillcheckSession session = Build(out SkillcheckConfig config, links: 3);
            float first = session.Current.Speed;

            PressPerfect(session);
            RunPause(session, config.ChainPauseSeconds + Frame);
            float second = session.Current.Speed;

            PressPerfect(session);
            RunPause(session, config.ChainPauseSeconds + Frame);
            float third = session.Current.Speed;

            Assert.AreEqual(first * config.SpeedMultiplierForLink(1), second, 1e-4f);
            Assert.Greater(second, first);
            Assert.Greater(third, second);
        }

        /// <summary>El bono de tiempo paga distinto perfecto que bueno, así que se cuentan aparte.</summary>
        [Test]
        public void Los_aciertos_se_cuentan_separados_por_calidad()
        {
            SkillcheckSession session = Build(out SkillcheckConfig config, links: 2);

            PressGood(session);
            RunPause(session, config.ChainPauseSeconds + Frame);
            PressPerfect(session);

            Assert.AreEqual(1, session.GoodHits);
            Assert.AreEqual(1, session.PerfectHits);
        }

        [Test]
        public void Una_cadena_resuelta_no_avanza_mas()
        {
            SkillcheckSession session = Build(out _);
            PressPerfect(session);

            SkillcheckSessionTick tick = session.Advance(1f, true);

            Assert.IsFalse(tick.LinkResolved);
            Assert.IsFalse(tick.Finished);
            Assert.AreEqual(SkillcheckSessionState.Convertido, session.State);
        }

        /// <summary>Lleva la aguja hasta el medio de la zona perfecta y aprieta.</summary>
        private static void PressPerfect(SkillcheckSession session)
        {
            SkillcheckAttempt attempt = session.Current;
            AdvanceTo(session, (attempt.PerfectStart + attempt.PerfectEnd) * 0.5f);
            session.Advance(0f, true);
        }

        /// <summary>Aprieta dentro de la zona buena pero antes de la perfecta.</summary>
        private static void PressGood(SkillcheckSession session)
        {
            SkillcheckAttempt attempt = session.Current;
            AdvanceTo(session, (attempt.ZoneStart + attempt.PerfectStart) * 0.5f);
            session.Advance(0f, true);
        }

        private static void AdvanceTo(SkillcheckSession session, float angle)
        {
            for (int i = 0; i < 10000; i++)
            {
                if (session.Current == null || session.Current.NeedleAngle >= angle) return;
                session.Advance(Frame, false);
            }
        }

        /// <summary>
        /// Corre la pausa y corta apenas arranca el eslabón, para que el test que
        /// mira la aguja recién nacida no la encuentre ya movida.
        /// </summary>
        private static bool RunPause(SkillcheckSession session, float seconds)
        {
            for (float t = 0f; t < seconds; t += Frame)
                if (session.Advance(Frame, false).LinkStarted) return true;

            return false;
        }
    }
}
