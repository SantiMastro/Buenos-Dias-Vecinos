using UnityEngine.InputSystem.Controls;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// Modalidad de OBJETO: cada acción tiene su entrada física propia.
    ///
    /// Es la modalidad del control que se está fabricando. No necesita saber qué
    /// espera el juego, porque el jugador ya lo dice con el cuerpo: el pie sobre
    /// la plancha ES el felpudo, sin ambigüedad posible.
    ///
    /// El pulso se calcula con <c>wasPressedThisFrame</c> del propio control y no
    /// con <c>onAnyButtonPress</c>: acá se sabe exactamente qué control mirar, así
    /// que escuchar todo el hardware sería aceptar apretones que no son de este
    /// juego.
    /// </summary>
    public sealed class DeviceInputSource : IInputSource
    {
        private readonly PhysicalBinding timbre;
        private readonly PhysicalBinding libro;
        private readonly PhysicalBinding felpudo;

        /// <summary>No se usa: en esta modalidad no hay nada que desambiguar.</summary>
        public InputContext Context { get; set; }

        /// <summary>Siempre 0: el gesto mantenido es cosa de la modalidad de un botón.</summary>
        public float HoldProgress => 0f;

        /// <summary>Toma los tres bindings ya configurados.</summary>
        public DeviceInputSource(
            PhysicalBinding timbre, PhysicalBinding libro, PhysicalBinding felpudo)
        {
            this.timbre = timbre;
            this.libro = libro;
            this.felpudo = felpudo;
        }

        /// <summary>No hace falta: el Input System ya mantiene el estado.</summary>
        public void Tick() { }

        /// <summary>Si esa acción se disparó en este cuadro.</summary>
        public bool Pressed(GameAction action)
        {
            ButtonControl control = ControlFor(action);
            return control != null && control.wasPressedThisFrame;
        }

        /// <summary>Si esa acción está sostenida ahora mismo.</summary>
        public bool Held(GameAction action)
        {
            ButtonControl control = ControlFor(action);
            return control != null && control.isPressed;
        }

        private ButtonControl ControlFor(GameAction action)
        {
            switch (action)
            {
                case GameAction.Timbre: return timbre?.Control();
                case GameAction.Libro: return libro?.Control();
                default: return felpudo?.Control();
            }
        }
    }
}
