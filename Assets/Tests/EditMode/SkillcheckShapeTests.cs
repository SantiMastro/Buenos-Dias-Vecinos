using BuenosDias.Config;
using BuenosDias.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace BuenosDias.Tests
{
    /// <summary>Formas del skillcheck: geometría de la pista y sorteo por eslabón.</summary>
    public sealed class SkillcheckShapeTests
    {
        [Test]
        public void El_cuadrado_llega_al_lado_arriba_y_a_la_esquina_en_diagonal()
        {
            Assert.AreEqual(
                SkillcheckShapeGeometry.SquareHalfSide,
                SkillcheckShapeGeometry.BoundaryRadius(SkillcheckShape.Cuadrado, 0f), 1e-3f);

            Assert.AreEqual(
                SkillcheckShapeGeometry.SquareHalfSide * Mathf.Sqrt(2f),
                SkillcheckShapeGeometry.BoundaryRadius(SkillcheckShape.Cuadrado, Mathf.PI * 0.25f),
                1e-3f);
        }

        [Test]
        public void El_triangulo_llega_al_vertice_arriba_y_al_lado_abajo()
        {
            Assert.AreEqual(
                SkillcheckShapeGeometry.TriangleCircumradius,
                SkillcheckShapeGeometry.BoundaryRadius(SkillcheckShape.Triangulo, 0f), 1e-3f);

            Assert.AreEqual(
                SkillcheckShapeGeometry.TriangleCircumradius * 0.5f,
                SkillcheckShapeGeometry.BoundaryRadius(SkillcheckShape.Triangulo, Mathf.PI), 1e-3f);
        }

        [Test]
        public void El_centro_esta_adentro_de_todas_las_formas()
        {
            foreach (SkillcheckShape shape in System.Enum.GetValues(typeof(SkillcheckShape)))
                Assert.Greater(SkillcheckShapeGeometry.InwardDistance(shape, 0f, 0f), 0f, shape.ToString());
        }

        [Test]
        public void El_borde_calculado_por_el_rayo_cae_sobre_el_borde_de_la_forma()
        {
            foreach (SkillcheckShape shape in System.Enum.GetValues(typeof(SkillcheckShape)))
            {
                for (float angle = 0f; angle < Mathf.PI * 2f; angle += 0.3f)
                {
                    float r = SkillcheckShapeGeometry.BoundaryRadius(shape, angle);
                    float inward = SkillcheckShapeGeometry.InwardDistance(
                        shape, Mathf.Sin(angle) * r, Mathf.Cos(angle) * r);

                    Assert.AreEqual(0f, inward, 1e-2f, $"{shape} a {angle} rad");
                }
            }
        }

        [Test]
        public void Sin_sorteo_de_forma_siempre_sale_circulo()
        {
            SkillcheckConfig config = ConfigFactory.Skillcheck();
            var so = new UnityEditor.SerializedObject(config);
            so.FindProperty("randomShape").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(SkillcheckShape.Circulo, config.RollShape(new FixedRandom(0.99d)));
        }

        [Test]
        public void El_sorteo_recorre_toda_la_lista_de_formas()
        {
            SkillcheckConfig config = ConfigFactory.Skillcheck();

            Assert.AreEqual(SkillcheckShape.Circulo, config.RollShape(new FixedRandom(0d)));
            Assert.AreEqual(SkillcheckShape.Estrella, config.RollShape(new FixedRandom(0.999999d)));
        }

        [Test]
        public void La_posicion_sorteada_queda_entre_menos_uno_y_uno()
        {
            SkillcheckConfig config = ConfigFactory.Skillcheck();
            var low = new SkillcheckAttempt(config, new SkillcheckSetup(0.5f, 2f, 1), new FixedRandom(0d));
            var high = new SkillcheckAttempt(config, new SkillcheckSetup(0.5f, 2f, 1), new FixedRandom(0.999999d));

            Assert.AreEqual(-1f, low.OffsetX, 1e-3f);
            Assert.AreEqual(1f, high.OffsetY, 1e-3f);
        }
    }
}
