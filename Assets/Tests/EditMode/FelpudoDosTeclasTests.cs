using BuenosDias.Gameplay;
using NUnit.Framework;

namespace BuenosDias.Tests
{
    /// <summary>
    /// El felpudo con dos teclas (S al subirse, B al bajarse). Lo que se verifica
    /// es por qué existe: cada tecla dice el estado, así que una repetida no lo
    /// invierte como pasaba con el toggle de Espacio, y tampoco confirma ni traba
    /// el timbre de más.
    /// </summary>
    public sealed class FelpudoDosTeclasTests
    {
        [Test]
        public void Subirse_pone_en_el_felpudo()
        {
            Assert.IsTrue(TwoButtonInputSource.SiguienteFelpudo(false, true, false, true));
        }

        [Test]
        public void Bajarse_saca_del_felpudo()
        {
            Assert.IsFalse(TwoButtonInputSource.SiguienteFelpudo(true, false, true, false));
        }

        /// <summary>
        /// La placa reintenta y vuelve a mandar la tecla al salir de la pausa: con
        /// el toggle eso invertía el felpudo, con dos teclas no cambia nada.
        /// </summary>
        [Test]
        public void Una_tecla_repetida_no_invierte_el_felpudo()
        {
            Assert.IsTrue(TwoButtonInputSource.SiguienteFelpudo(true, true, false, true));
            Assert.IsFalse(TwoButtonInputSource.SiguienteFelpudo(false, false, true, false));
        }

        [Test]
        public void Sin_teclas_queda_como_estaba()
        {
            Assert.IsTrue(TwoButtonInputSource.SiguienteFelpudo(true, false, false, false));
            Assert.IsFalse(TwoButtonInputSource.SiguienteFelpudo(false, false, false, false));
        }

        [Test]
        public void Las_dos_en_el_mismo_cuadro_manda_la_que_sigue_apretada()
        {
            Assert.IsTrue(TwoButtonInputSource.SiguienteFelpudo(false, true, true, true));
            Assert.IsFalse(TwoButtonInputSource.SiguienteFelpudo(true, true, true, false));
        }

        [Test]
        public void Un_cambio_cuenta_como_B()
        {
            Assert.IsTrue(TwoButtonInputSource.CuentaComoB(false, true, InputContext.Caminando));
            Assert.IsTrue(TwoButtonInputSource.CuentaComoB(true, false, InputContext.EnElFelpudo));
        }

        /// <summary>Una tecla repetida no confirma la religión ni traba el timbre.</summary>
        [Test]
        public void Sin_cambio_no_cuenta_como_B()
        {
            Assert.IsFalse(TwoButtonInputSource.CuentaComoB(true, true, InputContext.EnElFelpudo));
            Assert.IsFalse(TwoButtonInputSource.CuentaComoB(false, false, InputContext.Seleccion));
        }

        /// <summary>
        /// Se confirma subiéndose a la plancha; bajarse después no tiene que
        /// confirmar otra vez.
        /// </summary>
        [Test]
        public void En_la_seleccion_solo_subirse_confirma()
        {
            Assert.IsTrue(TwoButtonInputSource.CuentaComoB(false, true, InputContext.Seleccion));
            Assert.IsFalse(TwoButtonInputSource.CuentaComoB(true, false, InputContext.Seleccion));
        }

        // ── SLatido: el felpudo como alquiler ─────────────────────────────────

        private const float Cuadro = 1f / 60f;

        /// <summary>Pasan <paramref name="cuadros"/> cuadros sin ninguna tecla.</summary>
        private static bool SinTeclas(int cuadros, ref float sinLatido, float dt = Cuadro)
        {
            bool arriba = false;
            for (int i = 0; i < cuadros; i++)
                arriba = TwoButtonInputSource.SiguienteFelpudoLatido(false, false, false, dt, 0.4f, ref sinLatido);
            return arriba;
        }

        private static bool Teclas(bool sube, bool baja, bool subeApretada, ref float sinLatido)
        {
            return TwoButtonInputSource.SiguienteFelpudoLatido(sube, baja, subeApretada, Cuadro, 0.4f, ref sinLatido);
        }

        /// <summary>El valor por defecto es ABAJO: sin ninguna tecla se camina.</summary>
        [Test]
        public void Latido_sin_teclas_esta_abajo()
        {
            float sin = float.PositiveInfinity;
            Assert.IsFalse(SinTeclas(120, ref sin));
        }

        [Test]
        public void Latido_una_S_sube_y_sigue_arriba_mientras_no_caduque()
        {
            float sin = float.PositiveInfinity;
            Assert.IsTrue(Teclas(true, false, true, ref sin));
            Assert.IsTrue(SinTeclas(20, ref sin));   // 0,33 s sin latido
        }

        /// <summary>Si la placa deja de mandar S (se bajó, se colgó, se cayó el USB), camina sola.</summary>
        [Test]
        public void Latido_sin_S_vuelve_a_caminar_solo()
        {
            float sin = float.PositiveInfinity;
            Teclas(true, false, true, ref sin);
            Assert.IsFalse(SinTeclas(30, ref sin));  // 0,5 s
        }

        /// <summary>Un latido perdido (la S de un cuadro largo) no suelta al predicador.</summary>
        [Test]
        public void Latido_un_latido_perdido_no_baja_el_felpudo()
        {
            float sin = float.PositiveInfinity;
            Teclas(true, false, true, ref sin);
            Assert.IsTrue(SinTeclas(18, ref sin));   // 0,3 s: falta un latido
            Assert.IsTrue(Teclas(true, false, true, ref sin));
            Assert.IsTrue(SinTeclas(18, ref sin));
        }

        /// <summary>Bajarse tiene que ser inmediato, sin esperar a que caduque.</summary>
        [Test]
        public void Latido_B_corta_de_inmediato_y_queda_abajo()
        {
            float sin = float.PositiveInfinity;
            Teclas(true, false, true, ref sin);
            Assert.IsFalse(Teclas(false, true, false, ref sin));
            Assert.IsFalse(SinTeclas(3, ref sin));
        }

        [Test]
        public void Latido_una_S_despues_de_una_B_vuelve_a_subir()
        {
            float sin = float.PositiveInfinity;
            Teclas(true, false, true, ref sin);
            Teclas(false, true, false, ref sin);
            Assert.IsTrue(Teclas(true, false, true, ref sin));
        }

        [Test]
        public void Latido_B_y_S_en_el_mismo_cuadro_manda_la_que_sigue_apretada()
        {
            float sin = float.PositiveInfinity;
            Assert.IsTrue(Teclas(true, true, true, ref sin));
            Assert.IsFalse(Teclas(true, true, false, ref sin));
        }

        /// <summary>
        /// Un tirón del juego de un segundo no puede hacer caducar el felpudo: se
        /// perdería la puerta que se estaba atendiendo.
        /// </summary>
        [Test]
        public void Latido_un_cuadro_largo_no_hace_caducar_de_golpe()
        {
            float sin = float.PositiveInfinity;
            Teclas(true, false, true, ref sin);
            Assert.IsTrue(SinTeclas(1, ref sin, dt: 2f));
        }

        // ── Revocación: el seguro contra el felpudo trabado ───────────────────

        /// <summary>Sin revocar, el felpudo es lo que dice la placa.</summary>
        [Test]
        public void Revocado_sin_revocar_manda_la_placa()
        {
            bool revocado = false;
            Assert.IsTrue(TwoButtonInputSource.ResolverRevocado(true, ref revocado));
            Assert.IsFalse(TwoButtonInputSource.ResolverRevocado(false, ref revocado));
            Assert.IsFalse(revocado);
        }

        /// <summary>Mientras la placa siga diciendo "arriba", el juego lo ignora.</summary>
        [Test]
        public void Revocado_ignora_la_placa_mientras_siga_diciendo_arriba()
        {
            bool revocado = true;
            for (int i = 0; i < 10; i++)
                Assert.IsFalse(TwoButtonInputSource.ResolverRevocado(true, ref revocado));
            Assert.IsTrue(revocado);
        }

        /// <summary>La primera vez que la placa dice "abajo" se levanta la revocación.</summary>
        [Test]
        public void Revocado_se_levanta_cuando_la_placa_dice_abajo()
        {
            bool revocado = true;
            Assert.IsFalse(TwoButtonInputSource.ResolverRevocado(false, ref revocado));
            Assert.IsFalse(revocado);
            Assert.IsTrue(TwoButtonInputSource.ResolverRevocado(true, ref revocado));
        }
    }
}
