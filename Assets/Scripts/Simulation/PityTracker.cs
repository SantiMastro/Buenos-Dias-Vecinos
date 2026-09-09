using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.Simulation
{
    /// <summary>
    /// Memoria del anti-racha. Clase plana y determinista, para poder simular
    /// miles de partidas y medir la peor racha sin entrar en Play Mode.
    ///
    /// La regla que gobierna todo: **el pity nunca hace que abra una casa que se
    /// ve muerta**. En casas por debajo del umbral de legibilidad ni el pity ni
    /// la garantía dura aplican. Insistir con casas que se ven vacías sigue
    /// saliendo vacío, y eso es lo que le enseña al jugador a leer.
    /// </summary>
    public sealed class PityTracker
    {
        private readonly PityConfig config;

        /// <summary>Pity acumulado por casas vacías.</summary>
        public float Pity { get; private set; }

        /// <summary>Casas vacías seguidas.</summary>
        public int EmptyRun { get; private set; }

        /// <summary>Arranca el contador en cero.</summary>
        public PityTracker(PityConfig config) => this.config = config;

        /// <summary>Vuelve al estado inicial. Se llama al empezar una partida.</summary>
        public void Reset()
        {
            Pity = 0f;
            EmptyRun = 0;
        }

        /// <summary>
        /// Probabilidad final de que la casa esté ocupada, en el momento de tocar
        /// el timbre. El pity solo suma en casas legibles.
        /// </summary>
        public float ProbabilityFor(float houseChance)
        {
            if (!config.IsReadable(houseChance)) return config.ClampRoll(houseChance);
            return config.ClampRoll(houseChance + Pity);
        }

        /// <summary>
        /// Si esta casa tiene ocupación garantizada por la racha. Solo alcanza a
        /// las casas legibles.
        /// </summary>
        public bool IsGuaranteed(float houseChance)
        {
            return config.IsReadable(houseChance) && EmptyRun >= config.GuaranteedAfterEmptyRun;
        }

        /// <summary>
        /// Resuelve si hay alguien y actualiza la memoria. Recibe el
        /// <see cref="System.Random"/> de la partida para seguir siendo reproducible.
        /// </summary>
        public bool Resolve(float houseChance, System.Random random)
        {
            bool occupied = IsGuaranteed(houseChance)
                            || random.NextDouble() < ProbabilityFor(houseChance);

            // El umbral de legibilidad es SIMÉTRICO: una casa que se veía muerta
            // queda fuera del sistema de compensación en las DOS direcciones. Que
            // no reciba pity ya lo resolvían ProbabilityFor e IsGuaranteed; esto
            // es la otra mitad, que tampoco lo alimente.
            //
            // El pity existe para compensar FRUSTRACIÓN, y en una casa que se veía
            // muerta no hay frustración: el jugador leyó bien y decidió tocar
            // igual. Que eso sumara compensación era un error conceptual, no un
            // desbalance — convertía a las casas muertas en fichas baratas para
            // cargar el pity de las buenas. Ver §10.4 del plan.
            if (!config.IsReadable(houseChance)) return occupied;

            if (occupied)
            {
                Pity = 0f;
                EmptyRun = 0;
            }
            else
            {
                Pity = Mathf.Min(Pity + config.Increment, config.Cap);
                EmptyRun++;
            }

            return occupied;
        }
    }
}
