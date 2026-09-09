using BuenosDias.Config;
using BuenosDias.Gameplay;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Los carteles de la pantalla de selección: el nombre de la religión y la
    /// línea que dice en qué te cambia la partida.
    ///
    /// Sin esto la pantalla contestaba solo con el color de la camisa y la
    /// corbata, y **tres de las cinco religiones comparten camisa blanca**: se
    /// distinguían por unos pocos píxeles de corbata, y nada decía que los mormones
    /// caminan a 1.30 ni que los budistas tienen la aguja a 0.75. Se elegía a
    /// ciegas.
    ///
    /// Se esconde sola fuera de la selección: escucha la etapa, nadie la prende.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReligionScreenView : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Quien elige. Se lee y se escucha; nunca se le pide nada.")]
        [SerializeField] private ReligionSelector selector;

        [Tooltip("Dueño de la partida. De acá sale si todavía se está eligiendo.")]
        [SerializeField] private RunDirector runDirector;

        [Tooltip("Cartel del nombre. Va en grande.")]
        [SerializeField] private TextLabel nameLabel;

        [Tooltip("Cartel del trueque. Va chico, debajo del nombre.")]
        [SerializeField] private TextLabel taglineLabel;

        private void OnEnable()
        {
            if (selector != null) selector.Changed += Show;
            if (runDirector != null) runDirector.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            if (selector != null) selector.Changed -= Show;
            if (runDirector != null) runDirector.PhaseChanged -= OnPhaseChanged;
        }

        /// <summary>
        /// La primera escritura va en Start: el selector arma su catálogo en Awake
        /// y el orden de Awake entre dos GameObjects no está definido. Unity sí
        /// garantiza que todos los Awake corren antes que cualquier Start.
        /// </summary>
        private void Start()
        {
            if (!ValidateSetup()) { enabled = false; return; }

            Show(selector.Selected);
            OnPhaseChanged(runDirector.Phase);
        }

        private void OnPhaseChanged(RunPhase phase)
        {
            bool choosing = phase == RunPhase.Seleccion;
            nameLabel.gameObject.SetActive(choosing);
            taglineLabel.gameObject.SetActive(choosing);
        }

        private void Show(ReligionDefinition religion)
        {
            if (religion == null) return;

            nameLabel.SetText(religion.DisplayName);
            taglineLabel.SetText(religion.Tagline);
        }

        private bool ValidateSetup()
        {
            if (selector != null && runDirector != null
                && nameLabel != null && taglineLabel != null) return true;

            Debug.LogError(
                $"[ReligionScreenView] '{name}' tiene referencias sin asignar " +
                "(selector, partida, cartel de nombre o cartel de trueque).", this);
            return false;
        }
    }
}
