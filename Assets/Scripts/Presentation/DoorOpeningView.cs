using BuenosDias.Gameplay;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Abre la hoja de la puerta mientras el vecino atiende, y la cierra al salir.
    ///
    /// El sprite <c>env_puerta_abierta</c> estaba en el proyecto desde la primera
    /// tanda de arte y nunca lo enganchó nadie: atender era un cambio de estado que
    /// no se veía en ninguna parte. Se notaba sobre todo al INSISTIR, donde el
    /// jugador paga segundos por una puerta que después se veía igual que la que no
    /// le abrió, y la conclusión razonable era que insistir estaba roto.
    ///
    /// La puerta está abierta exactamente mientras dura
    /// <see cref="PreacherState.Atendido"/>. No hace falta nada más: el vecino no
    /// tiene sprite, así que la hoja abierta ES la señal de que hay alguien
    /// escuchando, y que se cierre al terminar el skillcheck se lee solo como que
    /// la charla terminó.
    ///
    /// ⚠️ Se guarda la casa que se abrió en vez de volver a pedírsela a la FSM.
    /// <c>PreacherController.Abandon</c> limpia el intento ANTES de avisar el cambio
    /// de estado, así que en el cuadro en que hay que cerrar, <c>Target</c> ya es
    /// null y la puerta se quedaría abierta para siempre.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorOpeningView : MonoBehaviour
    {
        [Tooltip("La FSM del predicador. Se escucha; nunca se le pide nada.")]
        [SerializeField] private PreacherController preacher;

        private HouseInstance opened;

        private void Awake()
        {
            if (preacher != null) return;

            Debug.LogError(
                $"[DoorOpeningView] '{name}' no tiene predicador asignado, así que " +
                "las puertas no se van a abrir nunca.", this);
            enabled = false;
        }

        private void OnEnable() => preacher.StateChanged += OnStateChanged;

        private void OnDisable()
        {
            preacher.StateChanged -= OnStateChanged;

            // Cerrar al apagarse no es una cortesía: la casa vive en un pool y se
            // recicla, así que una hoja abierta olvidada acá reaparece en otra
            // cuadra como una casa que abre sola.
            Close();
        }

        private void OnStateChanged(PreacherState state)
        {
            if (state != PreacherState.Atendido) { Close(); return; }

            opened = preacher.Target;
            if (opened != null) opened.SetDoorOpen(true);
        }

        private void Close()
        {
            if (opened == null) return;

            opened.SetDoorOpen(false);
            opened = null;
        }
    }
}
