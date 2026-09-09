using BuenosDias.Config;
using BuenosDias.Simulation;
using NUnit.Framework;

namespace BuenosDias.Tests
{
    /// <summary>
    /// Un eslabón del skillcheck. Los bordes se verifican con números porque son
    /// justo lo que no se puede probar a mano: apretar en el límite exacto de la
    /// zona, o dejarla pasar por un pelo.
    /// </summary>
    public sealed class SkillcheckAttemptTests
    {
        private static SkillcheckAttempt Build(
            out SkillcheckConfig config, float zoneWidth = 0.5f, float speed = 2f)
        {
            config = ConfigFactory.Skillcheck();
            return new SkillcheckAttempt(
                config, new SkillcheckSetup(zoneWidth, speed, 1), FixedRandom.AlwaysOpens);
        }

        /// <summary>Con el random fijado en 0, la zona cae en zoneAheadMin.</summary>
        [Test]
        public void La_zona_aparece_por_delante_de_la_aguja()
        {
            SkillcheckAttempt attempt = Build(out SkillcheckConfig config);

            Assert.AreEqual(config.ZoneAheadMin, attempt.ZoneStart, 1e-5f);
            Assert.Greater(attempt.ZoneStart, 0f, "nunca puede aparecer encima de la aguja");
        }

        [Test]
        public void Apretar_en_la_zona_perfecta_da_perfecto()
        {
            SkillcheckAttempt attempt = Build(out _);
            AdvanceTo(attempt, (attempt.PerfectStart + attempt.PerfectEnd) * 0.5f);

            Assert.AreEqual(SkillcheckOutcome.Perfecto, attempt.Press());
        }

        [Test]
        public void Apretar_en_la_zona_buena_pero_fuera_de_la_perfecta_da_bueno()
        {
            SkillcheckAttempt attempt = Build(out _);
            AdvanceTo(attempt, (attempt.ZoneStart + attempt.PerfectStart) * 0.5f);

            Assert.AreEqual(SkillcheckOutcome.Bueno, attempt.Press());
        }

        [Test]
        public void Apretar_antes_de_la_zona_falla()
        {
            SkillcheckAttempt attempt = Build(out _);
            AdvanceTo(attempt, attempt.ZoneStart * 0.5f);

            Assert.AreEqual(SkillcheckOutcome.Fallado, attempt.Press());
        }

        [Test]
        public void Apretar_pasada_la_zona_falla()
        {
            SkillcheckAttempt attempt = Build(out _);
            AdvanceTo(attempt, attempt.ZoneEnd + 0.05f);

            Assert.AreEqual(SkillcheckOutcome.Fallado, attempt.Press());
        }

        /// <summary>Dejar pasar la zona es fallar, no esperar a la vuelta siguiente.</summary>
        [Test]
        public void Dejar_pasar_el_margen_pierde_la_tirada_sola()
        {
            SkillcheckAttempt attempt = Build(out _);

            AdvanceTo(attempt, attempt.ZoneEnd + 0.01f);
            Assert.AreEqual(SkillcheckOutcome.EnCurso, attempt.Outcome,
                "dentro del margen todavía se puede apretar");

            AdvanceTo(attempt, attempt.DeadlineAngle + 0.01f);
            Assert.AreEqual(SkillcheckOutcome.Fallado, attempt.Outcome);
        }

        /// <summary>
        /// Un botón de arcade rebota. Si el segundo contacto pudiera reevaluar la
        /// tirada, un perfecto se volvería falla sin que nadie tocara nada.
        /// </summary>
        [Test]
        public void Apretar_de_nuevo_no_cambia_una_tirada_resuelta()
        {
            SkillcheckAttempt attempt = Build(out _);
            AdvanceTo(attempt, (attempt.PerfectStart + attempt.PerfectEnd) * 0.5f);
            attempt.Press();

            AdvanceTo(attempt, attempt.DeadlineAngle + 1f);

            Assert.AreEqual(SkillcheckOutcome.Perfecto, attempt.Press());
            Assert.AreEqual(SkillcheckOutcome.Perfecto, attempt.Outcome);
        }

        [Test]
        public void La_aguja_no_avanza_una_vez_resuelta()
        {
            SkillcheckAttempt attempt = Build(out _);
            AdvanceTo(attempt, attempt.ZoneStart * 0.5f);
            attempt.Press();

            float frozen = attempt.NeedleAngle;
            attempt.Advance(1f);

            Assert.AreEqual(frozen, attempt.NeedleAngle, 1e-5f);
        }

        /// <summary>Avanza la aguja de a cuadros de 60 fps hasta pasar el ángulo pedido.</summary>
        private static void AdvanceTo(SkillcheckAttempt attempt, float angle)
        {
            const float frame = 1f / 60f;
            for (int i = 0; i < 10000 && attempt.NeedleAngle < angle; i++)
                attempt.Advance(frame);
        }
    }
}
