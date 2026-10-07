using NUnit.Framework;
using UnityEngine;

namespace BuenosDias.Tests
{
    /// <summary>
    /// El power up vive en el SkillcheckRunner (un MonoBehaviour), así que acá se
    /// verifica la config que lo gobierna. La mecánica completa se prueba en Play
    /// Mode: tres perfects seguidos, "+1 YA!" en el HUD, y la puerta siguiente
    /// convierte sola.
    /// </summary>
    public sealed class PerfectStreakPowerUpTests
    {
        [Test]
        public void Por_defecto_el_power_up_esta_prendido_y_pide_tres_perfects()
        {
            var config = ConfigFactory.Skillcheck();

            Assert.IsTrue(config.InstantConvertPowerUp);
            Assert.AreEqual(3, config.PerfectsForPowerUp);
        }
    }
}
