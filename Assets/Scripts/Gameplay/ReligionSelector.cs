using BuenosDias.Config;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// La elección de religión con un solo botón: toque corto pasa a la siguiente,
    /// mantenido confirma.
    ///
    /// ⚠️ El cambio se hace **al soltar**, no al apretar. Un mantenido EMPIEZA con
    /// un apretón: si la siguiente saliera al apretar, cada confirmación cambiaría
    /// de religión antes de confirmar, y el jugador terminaría siempre con la de
    /// después de la que quería.
    ///
    /// La lista sale de <see cref="GameConfig"/> y esta clase no enumera ninguna
    /// religión (criterio 10). El ciclado vive en <see cref="ReligionCarousel"/>,
    /// que es plano y testeable.
    ///
    /// Solo corre en la etapa de selección: quien maneja la partida lo apaga
    /// después. No dibuja; avisa y la pantalla se suscribe.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReligionSelector : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Asset raíz de balance. De acá sale el catálogo de religiones.")]
        [SerializeField] private GameConfig gameConfig;

        [Tooltip("Input de un botón: el toque corto y el mantenido salen de acá.")]
        [SerializeField] private GameInput input;

        private ReligionCarousel carousel;

        /// <summary>Religión apuntada ahora. <c>null</c> si el catálogo está vacío.</summary>
        public ReligionDefinition Selected =>
            carousel == null || carousel.IsEmpty ? null : gameConfig.Religions[carousel.Index];

        /// <summary>Posición dentro del catálogo. <c>-1</c> si está vacío.</summary>
        public int Index => carousel?.Index ?? -1;

        /// <summary>Cuántas opciones hay. Lo dibuja la pantalla como fichas.</summary>
        public int Count => carousel?.Count ?? 0;

        /// <summary>
        /// Cuánto le falta al mantenido, de 0 a 1. Da 0 cuando no es el turno de
        /// elegir, así que la pantalla no necesita saber en qué etapa está la
        /// partida para decidir si dibuja el medidor.
        /// </summary>
        public float HoldProgress => enabled && input != null ? input.HoldProgress : 0f;

        /// <summary>
        /// Declara qué se está esperando. Va en <c>OnEnable</c> y no en el
        /// <c>RunDirector</c> porque el que sabe que esta pantalla está viva es
        /// esta pantalla.
        /// </summary>
        private void OnEnable() => input.Context = InputContext.Seleccion;

        /// <summary>Cambió la religión apuntada.</summary>
        public event System.Action<ReligionDefinition> Changed;

        /// <summary>Se confirmó. Entrega la religión con la que se va a jugar.</summary>
        public event System.Action<ReligionDefinition> Confirmed;

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }

            carousel = new ReligionCarousel(gameConfig.Religions.Count);
        }

        /// <summary>
        /// Pasar de religión y confirmar son dos ACCIONES, y de traducirlas se
        /// encarga la entrada.
        ///
        /// Con el control físico son dos cosas distintas: el timbre pasa, el libro
        /// confirma. Con un botón único son el toque y el mantenido, que es el
        /// esquema de siempre. Acá no se nota la diferencia, y esa es la idea: la
        /// traba contra confirmar una vez por cuadro y la memoria del apretón en
        /// curso ahora viven en la fuente de un botón, que es la única que las
        /// necesita.
        /// </summary>
        private void Update()
        {
            if (carousel == null || carousel.IsEmpty) return;

            if (input.Pressed(GameAction.Libro))
            {
                Confirmed?.Invoke(Selected);
                return;
            }

            if (!input.Pressed(GameAction.Timbre)) return;

            carousel.Next();
            Changed?.Invoke(Selected);
        }

        private bool ValidateSetup()
        {
            if (gameConfig == null || input == null)
            {
                Debug.LogError(
                    $"[ReligionSelector] '{name}' tiene referencias sin asignar " +
                    "(GameConfig o input).", this);
                return false;
            }

            if (gameConfig.Religions.Count == 0)
            {
                Debug.LogError(
                    $"[ReligionSelector] '{name}': el GameConfig no tiene ninguna " +
                    "religión cargada. No hay nada para elegir.", this);
                return false;
            }

            return true;
        }
    }
}
