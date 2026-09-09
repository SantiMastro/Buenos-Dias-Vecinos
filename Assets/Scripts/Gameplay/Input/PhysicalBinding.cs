using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace BuenosDias.Gameplay
{
    /// <summary>De qué dispositivo sale una acción en la modalidad de objeto.</summary>
    public enum BindingDevice
    {
        /// <summary>Una tecla del teclado.</summary>
        Teclado,

        /// <summary>Botón izquierdo del mouse.</summary>
        MouseIzquierdo,

        /// <summary>Botón derecho del mouse.</summary>
        MouseDerecho
    }

    /// <summary>
    /// A qué entrada física está atada una acción.
    ///
    /// ⚠️ El TIMBRE está soldado a click izquierdo y eso es hardware: el pulsador
    /// del timbre de pared va al switch del botón izquierdo de una placa de mouse.
    /// Cambiarlo acá no cambia el fierro.
    ///
    /// Para el felpudo hay que elegir una tecla que aguante estar apretada mucho
    /// tiempo. **Nunca Shift, Ctrl, Alt, Caps Lock, Tab, Esc ni la de Windows**:
    /// Shift sostenida dispara el diálogo de Sticky Keys de Windows ENCIMA del
    /// juego, Alt se roba el menú de la ventana, Caps Lock es un toggle y la de
    /// Windows abre el menú Inicio. Cualquiera de esas arruina una demo con el pie
    /// apoyado sobre la plancha.
    /// </summary>
    [System.Serializable]
    public sealed class PhysicalBinding
    {
        [Tooltip("De qué dispositivo sale.")]
        [SerializeField] private BindingDevice device = BindingDevice.Teclado;

        [Tooltip("Qué tecla, si sale del teclado. Se ignora con el mouse.")]
        [SerializeField] private Key key = Key.Space;

        /// <summary>Arma un binding. Lo usan los defaults del Inspector.</summary>
        public PhysicalBinding(BindingDevice device, Key key)
        {
            this.device = device;
            this.key = key;
        }

        /// <summary>El control concreto, o <c>null</c> si el dispositivo no está.</summary>
        public ButtonControl Control()
        {
            switch (device)
            {
                case BindingDevice.MouseIzquierdo:
                    return Mouse.current?.leftButton;
                case BindingDevice.MouseDerecho:
                    return Mouse.current?.rightButton;
                default:
                    return Keyboard.current?[key];
            }
        }

        /// <summary>Nombre legible, para los mensajes de error.</summary>
        public string Describe()
        {
            return device == BindingDevice.Teclado ? key.ToString() : device.ToString();
        }
    }
}
