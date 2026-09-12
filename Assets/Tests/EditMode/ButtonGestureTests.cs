using BuenosDias.Gameplay;
using NUnit.Framework;

namespace BuenosDias.Tests
{
    /// <summary>
    /// El gesto del botón único cuando hay varios botones. Lo que se verifica es
    /// que apretar y soltar un segundo botón con el primero sostenido no corte el
    /// mantenido ni invente un toque.
    /// </summary>
    public sealed class ButtonGestureTests
    {
        private sealed class FakeButton
        {
            public bool Down;
        }

        private static ButtonGesture<FakeButton> NewGesture()
        {
            return new ButtonGesture<FakeButton>(button => button.Down);
        }

        [Test]
        public void Sin_apretones_no_esta_sostenido()
        {
            Assert.IsFalse(NewGesture().IsHeld);
        }

        [Test]
        public void Un_boton_sostenido_y_soltado()
        {
            ButtonGesture<FakeButton> gesture = NewGesture();
            var a = new FakeButton { Down = true };

            gesture.Press(a, 1f);
            Assert.IsTrue(gesture.IsHeld);
            Assert.AreEqual(1f, gesture.StartTime);

            a.Down = false;
            Assert.IsFalse(gesture.IsHeld);
        }

        [Test]
        public void Un_segundo_boton_no_reinicia_el_mantenido()
        {
            ButtonGesture<FakeButton> gesture = NewGesture();
            var a = new FakeButton { Down = true };
            var b = new FakeButton { Down = true };

            gesture.Press(a, 1f);
            gesture.Press(b, 1.3f);

            Assert.AreEqual(1f, gesture.StartTime);
        }

        /// <summary>
        /// El bug: soltar el segundo dejaba al gesto sin botón sostenido, y el
        /// botón único lo leía como un toque.
        /// </summary>
        [Test]
        public void Soltar_el_segundo_con_el_primero_apretado_no_corta_el_gesto()
        {
            ButtonGesture<FakeButton> gesture = NewGesture();
            var a = new FakeButton { Down = true };
            var b = new FakeButton { Down = true };
            gesture.Press(a, 1f);
            gesture.Press(b, 1.3f);

            b.Down = false;
            Assert.IsTrue(gesture.IsHeld);

            a.Down = false;
            Assert.IsFalse(gesture.IsHeld);
        }

        [Test]
        public void Un_apreton_despues_de_soltar_todo_es_un_gesto_nuevo()
        {
            ButtonGesture<FakeButton> gesture = NewGesture();
            var a = new FakeButton { Down = true };
            var b = new FakeButton { Down = true };

            gesture.Press(a, 1f);
            a.Down = false;
            gesture.Press(b, 5f);

            Assert.AreEqual(5f, gesture.StartTime);
            Assert.IsTrue(gesture.IsHeld);
        }

        [Test]
        public void Recuerda_el_ultimo_apretado()
        {
            ButtonGesture<FakeButton> gesture = NewGesture();
            var a = new FakeButton { Down = true };
            var b = new FakeButton { Down = true };

            gesture.Press(a, 1f);
            gesture.Press(b, 1.3f);

            Assert.AreSame(b, gesture.Last);
        }
    }
}
