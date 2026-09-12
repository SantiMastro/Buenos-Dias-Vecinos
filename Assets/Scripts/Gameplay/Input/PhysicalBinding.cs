using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;

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
    ///
    /// ⚠️ Se leen TODOS los teclados y TODOS los mouse, no <c>Keyboard.current</c>
    /// ni <c>Mouse.current</c>. El control físico se presenta como un teclado y un
    /// mouse más, y <c>.current</c> salta al último dispositivo que mandó CUALQUIER
    /// evento —alcanza con mover el mouse de la PC—. Con dos controles enchufados,
    /// leer solo el "actual" perdía apretones y soltaba el felpudo solo.
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

        /// <summary>Si se apretó en este cuadro en cualquier dispositivo de ese tipo.</summary>
        public bool WasPressedThisFrame()
        {
            ReadOnlyArray<InputDevice> devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                ButtonControl control = ControlOn(devices[i]);
                if (control != null && control.wasPressedThisFrame) return true;
            }

            return false;
        }

        /// <summary>Si está apretado ahora en cualquier dispositivo de ese tipo.</summary>
        public bool IsPressed()
        {
            ReadOnlyArray<InputDevice> devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                ButtonControl control = ControlOn(devices[i]);
                if (control != null && control.isPressed) return true;
            }

            return false;
        }

        /// <summary>
        /// Si los dos bindings leen la MISMA entrada física. Dos acciones atadas a
        /// lo mismo se disparan juntas con un solo apretón, que es el bug de frenar
        /// y tocar el timbre a la vez.
        /// </summary>
        public bool SameInputAs(PhysicalBinding other)
        {
            if (other == null || device != other.device) return false;
            return device != BindingDevice.Teclado || key == other.key;
        }

        /// <summary>Nombre legible, para los mensajes de error.</summary>
        public string Describe()
        {
            return device == BindingDevice.Teclado ? key.ToString() : device.ToString();
        }

        /// <summary>El control de este binding en ese dispositivo, o <c>null</c> si no lo tiene.</summary>
        private ButtonControl ControlOn(InputDevice candidate)
        {
            switch (device)
            {
                case BindingDevice.MouseIzquierdo:
                    return candidate is Mouse left ? left.leftButton : null;
                case BindingDevice.MouseDerecho:
                    return candidate is Mouse right ? right.rightButton : null;
                default:
                    return key != Key.None && candidate is Keyboard keyboard ? keyboard[key] : null;
            }
        }
    }
}
