using BuenosDias.Config;
using BuenosDias.Simulation;
using NUnit.Framework;

namespace BuenosDias.Tests
{
    /// <summary>
    /// La dificultad del skillcheck, que es donde cuatro multiplicadores se apilan
    /// y pueden dejar la zona más fina que la aguja sin que nada se rompa.
    /// </summary>
    public sealed class SkillcheckDifficultyTests
    {
        private const int MaxConverts = 14;
        private const float OneThird = 2f * UnityEngine.Mathf.PI / 3f;
        private const float OneEighth = UnityEngine.Mathf.PI / 4f;

        private static SkillcheckDifficulty Build(float minimumWidth = 0.12f)
        {
            return new SkillcheckDifficulty(
                ConfigFactory.Skillcheck(minimumWidth), ConfigFactory.Followers(), MaxConverts);
        }

        [Test]
        public void El_progreso_se_clampea_entre_cero_y_uno()
        {
            SkillcheckDifficulty difficulty = Build();

            Assert.AreEqual(0f, difficulty.ProgressAt(0));
            Assert.AreEqual(0.5f, difficulty.ProgressAt(7), 1e-5f);
            Assert.AreEqual(1f, difficulty.ProgressAt(MaxConverts));
            Assert.AreEqual(1f, difficulty.ProgressAt(999), "pasado el techo no sigue subiendo");
        }

        [Test]
        public void La_zona_se_achica_y_la_aguja_acelera_con_el_progreso()
        {
            SkillcheckDifficulty difficulty = Build();

            SkillcheckSetup start = difficulty.Resolve(0.5f, 0, 0, null);
            SkillcheckSetup end = difficulty.Resolve(0.5f, MaxConverts, 0, null);

            Assert.Less(end.ZoneWidth, start.ZoneWidth);
            Assert.Greater(end.NeedleSpeed, start.NeedleSpeed);
        }

        [Test]
        public void La_zona_se_achica_y_la_aguja_acelera_con_la_comitiva()
        {
            SkillcheckDifficulty difficulty = Build();

            SkillcheckSetup alone = difficulty.Resolve(0.5f, 0, 0, null);
            SkillcheckSetup crowded = difficulty.Resolve(0.5f, 0, 10, null);

            Assert.Less(crowded.ZoneWidth, alone.ZoneWidth);
            Assert.Greater(crowded.NeedleSpeed, alone.NeedleSpeed);
        }

        /// <summary>
        /// Tocar pegado a la puerta da la zona más ancha. Es la mecánica que premia
        /// acercarse en vez de tocar desde donde se llegue, y es toda la razón de
        /// ser del rango del timbre.
        ///
        /// Este test falló al escribirlo y destapó que los dos valores del asset
        /// estaban invertidos: venían del prototipo, donde la variable era la
        /// distancia normalizada (0 = pegado a la puerta) en vez de la precisión.
        /// Tal como estaba, la jugada óptima era tocar desde el borde del alcance.
        /// </summary>
        [Test]
        public void Mejor_punteria_da_zona_mas_ancha()
        {
            SkillcheckDifficulty difficulty = Build();

            SkillcheckSetup sloppy = difficulty.Resolve(0f, 0, 0, null);
            SkillcheckSetup precise = difficulty.Resolve(1f, 0, 0, null);

            Assert.Greater(precise.ZoneWidth, sloppy.ZoneWidth);
        }

        /// <summary>
        /// El piso absoluto es la única garantía de que la peor combinación posible
        /// siga siendo apuntable: timbrazo al borde del alcance, final del día,
        /// comitiva llena y religión difícil.
        ///
        /// Ojo con el primer argumento: el peor caso es precisión CERO. Este test
        /// pasaba con 1 mientras los valores del asset estaban invertidos, y volvió
        /// a fallar al arreglarlos — que es exactamente para lo que sirve.
        /// </summary>
        [Test]
        public void La_zona_nunca_baja_del_piso_absoluto()
        {
            SkillcheckDifficulty difficulty = Build(minimumWidth: OneEighth);
            ReligionDefinition hard = ConfigFactory.Religion(zoneWidth: 0.75f);

            SkillcheckSetup worst = difficulty.Resolve(0f, MaxConverts, 20, hard, zoneScale: 0.55f);

            Assert.AreEqual(OneEighth, worst.ZoneWidth, 1e-5f);
        }

        /// <summary>
        /// El QTE arranca grande y fácil: al principio del día ni la peor puntería,
        /// ni una religión que achica la zona, ni haber insistido la dejan por
        /// debajo de un tercio del círculo.
        /// </summary>
        [Test]
        public void Al_arrancar_la_zona_es_al_menos_un_tercio_del_circulo()
        {
            SkillcheckDifficulty difficulty = Build(minimumWidth: OneEighth);
            ReligionDefinition hard = ConfigFactory.Religion(zoneWidth: 0.5f);

            SkillcheckSetup first = difficulty.Resolve(0f, 0, 0, hard, zoneScale: 0.55f);

            Assert.GreaterOrEqual(first.ZoneWidth, OneThird - 1e-3f);
        }

        /// <summary>El piso baja de a poco: a mitad de camino queda entre los dos.</summary>
        [Test]
        public void El_piso_baja_de_un_tercio_a_un_octavo_con_el_progreso()
        {
            SkillcheckDifficulty difficulty = Build(minimumWidth: OneEighth);
            ReligionDefinition hard = ConfigFactory.Religion(zoneWidth: 0.5f);

            SkillcheckSetup middle = difficulty.Resolve(0f, MaxConverts / 2, 20, hard, zoneScale: 0.55f);

            Assert.Less(middle.ZoneWidth, OneThird);
            Assert.Greater(middle.ZoneWidth, OneEighth);
        }

        /// <summary>
        /// La presión del reloj también se siente en la puerta: con poco tiempo por
        /// delante la zona es más chica, aunque no se haya convertido a nadie.
        /// </summary>
        [Test]
        public void La_zona_se_achica_cuando_queda_poco_tiempo()
        {
            SkillcheckDifficulty difficulty = Build(minimumWidth: OneEighth);

            SkillcheckSetup morning = difficulty.Resolve(1f, 0, 0, null, timeUsed: 0f);
            SkillcheckSetup dusk = difficulty.Resolve(1f, 0, 0, null, timeUsed: 0.9f);

            Assert.Less(dusk.ZoneWidth, morning.ZoneWidth);
        }

        /// <summary>
        /// El piso de un tercio es "al arrancar", no "sin conversiones": avanzado
        /// el día baja aunque el jugador no haya convertido a nadie.
        /// </summary>
        [Test]
        public void Al_final_del_dia_el_piso_baja_aunque_no_haya_conversiones()
        {
            SkillcheckDifficulty difficulty = Build(minimumWidth: OneEighth);
            ReligionDefinition hard = ConfigFactory.Religion(zoneWidth: 0.5f);

            SkillcheckSetup late = difficulty.Resolve(0f, 0, 0, hard, zoneScale: 0.55f, timeUsed: 1f);

            Assert.AreEqual(OneEighth, late.ZoneWidth, 1e-5f);
        }

        [Test]
        public void La_cadena_de_objeciones_va_por_tramos_de_cuatro()
        {
            SkillcheckDifficulty difficulty = Build();

            Assert.AreEqual(1, difficulty.ChainLinksFor(0, null));
            Assert.AreEqual(1, difficulty.ChainLinksFor(3, null));
            Assert.AreEqual(2, difficulty.ChainLinksFor(4, null));
            Assert.AreEqual(2, difficulty.ChainLinksFor(7, null));
            Assert.AreEqual(3, difficulty.ChainLinksFor(8, null));
            Assert.AreEqual(4, difficulty.ChainLinksFor(12, null));
        }

        [Test]
        public void El_techo_de_eslabones_no_se_pasa_por_mas_comitiva()
        {
            SkillcheckDifficulty difficulty = Build();

            Assert.AreEqual(4, difficulty.ChainLinksFor(50, null));
        }

        /// <summary>
        /// El extra de la religión se suma DESPUÉS del techo por tramo: una
        /// religión de +1 siempre cuesta un eslabón más, aunque la comitiva ya
        /// estuviera en el máximo.
        /// </summary>
        [Test]
        public void El_extra_de_la_religion_se_suma_despues_del_techo()
        {
            SkillcheckDifficulty difficulty = Build();
            ReligionDefinition budistas = ConfigFactory.Religion(extraChainLinks: 1);

            Assert.AreEqual(2, difficulty.ChainLinksFor(0, budistas));
            Assert.AreEqual(5, difficulty.ChainLinksFor(50, budistas));
        }

        [Test]
        public void Sin_religion_los_multiplicadores_valen_uno()
        {
            SkillcheckDifficulty difficulty = Build();
            ReligionDefinition neutral = ConfigFactory.Religion();

            SkillcheckSetup withoutReligion = difficulty.Resolve(0.5f, 3, 2, null);
            SkillcheckSetup withNeutral = difficulty.Resolve(0.5f, 3, 2, neutral);

            Assert.AreEqual(withNeutral.ZoneWidth, withoutReligion.ZoneWidth, 1e-5f);
            Assert.AreEqual(withNeutral.NeedleSpeed, withoutReligion.NeedleSpeed, 1e-5f);
        }
    }
}
