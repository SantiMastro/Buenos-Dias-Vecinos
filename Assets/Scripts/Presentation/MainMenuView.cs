using BuenosDias.Gameplay;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Pantalla de título: el nombre del juego y un "apretá para jugar" que
    /// parpadea. Solo se ve en la etapa de menú, que es antes de elegir religión
    /// y solo la primera vez que se abre el juego.
    ///
    /// El componente vive en un objeto que NUNCA se apaga y el contenido cuelga de
    /// otro: si se apagara este mismo objeto, también se apagaría la suscripción
    /// al cambio de etapa y el menú no volvería a aparecer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuView : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Dueño de la partida. De acá sale si todavía estamos en el menú.")]
        [SerializeField] private RunDirector runDirector;

        [Tooltip("Todo lo que se dibuja en el menú. Se prende y se apaga entero.")]
        [SerializeField] private GameObject content;

        [Tooltip("El cartel de 'apretá para jugar'. Parpadea.")]
        [SerializeField] private GameObject prompt;

        [Header("Parpadeo")]
        [Tooltip("Veces por segundo que se prende y se apaga el cartel.")]
        [SerializeField, Range(0.2f, 4f)] private float blinkHertz = 1.4f;

        private void OnEnable()
        {
            if (runDirector != null) runDirector.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            if (runDirector != null) runDirector.PhaseChanged -= OnPhaseChanged;
        }

        private void Start()
        {
            if (runDirector == null || content == null)
            {
                Debug.LogError(
                    $"[MainMenuView] '{name}' tiene referencias sin asignar " +
                    "(partida o contenido).", this);
                enabled = false;
                return;
            }

            OnPhaseChanged(runDirector.Phase);
        }

        private void Update()
        {
            if (prompt == null || !content.activeSelf) return;

            // onda cuadrada, con tiempo sin escalar: el menú no depende del timeScale
            bool visible = Mathf.Repeat(Time.unscaledTime * blinkHertz, 1f) < 0.65f;
            if (prompt.activeSelf != visible) prompt.SetActive(visible);
        }

        private void OnPhaseChanged(RunPhase phase)
        {
            content.SetActive(phase == RunPhase.Menu);
        }
    }
}
