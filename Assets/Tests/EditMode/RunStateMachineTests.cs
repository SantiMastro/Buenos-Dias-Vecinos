using BuenosDias.Simulation;
using NUnit.Framework;

namespace BuenosDias.Tests
{
    /// <summary>
    /// La máquina de estado de la partida. Lo que se verifica no es que el camino
    /// feliz funcione —eso es una línea— sino que los avisos repetidos y los
    /// fuera de tiempo NO hagan nada: son los que en un juego de un botón llegan
    /// solos, sin que nadie los pida.
    /// </summary>
    public sealed class RunStateMachineTests
    {
        [Test]
        public void Arranca_eligiendo_religion()
        {
            var run = new RunStateMachine();

            Assert.AreEqual(RunPhase.Seleccion, run.Phase);
        }

        [Test]
        public void El_camino_completo_pasa_por_las_tres_etapas()
        {
            var run = new RunStateMachine();

            Assert.IsTrue(run.Confirm());
            Assert.AreEqual(RunPhase.Jugando, run.Phase);

            Assert.IsTrue(run.Finish());
            Assert.AreEqual(RunPhase.Final, run.Phase);

            Assert.IsTrue(run.Restart());
            Assert.AreEqual(RunPhase.Seleccion, run.Phase);
        }

        /// <summary>
        /// El reloj puede avisar el final dos veces —termina por tiempo en el
        /// mismo cuadro en que termina una puerta— y el segundo aviso no puede
        /// volver a disparar el final.
        /// </summary>
        [Test]
        public void Terminar_dos_veces_no_hace_nada_la_segunda()
        {
            var run = new RunStateMachine();
            run.Confirm();

            Assert.IsTrue(run.Finish());
            Assert.IsFalse(run.Finish());
            Assert.AreEqual(RunPhase.Final, run.Phase);
        }

        [Test]
        public void Confirmar_dos_veces_no_hace_nada_la_segunda()
        {
            var run = new RunStateMachine();

            Assert.IsTrue(run.Confirm());
            Assert.IsFalse(run.Confirm());
            Assert.AreEqual(RunPhase.Jugando, run.Phase);
        }

        /// <summary>
        /// El botón que reinicia es el MISMO que toca timbres. Si reiniciara desde
        /// cualquier etapa, tocar un timbre volvería al menú.
        /// </summary>
        [Test]
        public void No_se_reinicia_mientras_se_juega()
        {
            var run = new RunStateMachine();
            run.Confirm();

            Assert.IsFalse(run.Restart());
            Assert.AreEqual(RunPhase.Jugando, run.Phase);
        }

        [Test]
        public void No_se_reinicia_mientras_se_elige()
        {
            var run = new RunStateMachine();

            Assert.IsFalse(run.Restart());
            Assert.AreEqual(RunPhase.Seleccion, run.Phase);
        }

        [Test]
        public void No_se_termina_un_dia_que_no_empezo()
        {
            var run = new RunStateMachine();

            Assert.IsFalse(run.Finish());
            Assert.AreEqual(RunPhase.Seleccion, run.Phase);
        }

        [Test]
        public void El_aviso_llega_con_la_etapa_nueva_ya_puesta()
        {
            var run = new RunStateMachine();
            RunPhase seen = RunPhase.Final;
            int calls = 0;

            run.Changed += phase => { seen = phase; calls++; };
            run.Confirm();

            Assert.AreEqual(1, calls);
            Assert.AreEqual(RunPhase.Jugando, seen);
        }

        /// <summary>Una transición rechazada no puede avisar un cambio que no pasó.</summary>
        [Test]
        public void Una_transicion_rechazada_no_avisa()
        {
            var run = new RunStateMachine();
            int calls = 0;

            run.Changed += _ => calls++;
            run.Finish();
            run.Restart();

            Assert.AreEqual(0, calls);
        }

        [Test]
        public void Despues_de_reiniciar_se_puede_volver_a_jugar()
        {
            var run = new RunStateMachine();
            run.Confirm();
            run.Finish();
            run.Restart();

            Assert.IsTrue(run.Confirm());
            Assert.AreEqual(RunPhase.Jugando, run.Phase);
        }
    }
}
