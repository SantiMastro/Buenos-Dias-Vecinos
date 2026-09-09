using System.Collections.Generic;
using BuenosDias.Config;

namespace BuenosDias.Simulation
{
    /// <summary>Qué pasó al tocar el timbre de una casa.</summary>
    public readonly struct HouseOccupancy
    {
        /// <summary>Si había alguien.</summary>
        public bool IsOccupied { get; }

        /// <summary>Qué vecino atiende. Es <c>null</c> si no había nadie.</summary>
        public NeighborDefinition Neighbor { get; }

        /// <summary>
        /// Probabilidad con la que se resolvió, ya con el pity aplicado. No la usa
        /// el juego: existe para que las ventanas de verificación puedan medir si
        /// lo que sale coincide con lo que la casa mostraba.
        /// </summary>
        public float Probability { get; }

        /// <summary>Si la casa era legible, o sea si el pity podía alcanzarla.</summary>
        public bool WasReadable { get; }

        /// <summary>Arma el resultado de un timbrazo.</summary>
        public HouseOccupancy(
            bool isOccupied, NeighborDefinition neighbor, float probability, bool wasReadable)
        {
            IsOccupied = isOccupied;
            Neighbor = neighbor;
            Probability = probability;
            WasReadable = wasReadable;
        }
    }

    /// <summary>
    /// Decide si hay alguien detrás de la puerta y quién es.
    ///
    /// Va aparte de <see cref="PityTracker"/> porque son dos responsabilidades
    /// distintas: el tracker es la MEMORIA del anti-racha y no sabe nada de
    /// vecinos; esto une esa memoria con la casa concreta y con el catálogo.
    ///
    /// Es una clase plana y determinista a propósito: con esto se pueden simular
    /// miles de partidas y medir la peor racha de puertas cerradas sin entrar en
    /// Play Mode, que es la verificación pendiente de §3.3 del plan.
    /// </summary>
    public sealed class HouseOccupancyResolver
    {
        private readonly PityTracker pity;
        private readonly PityConfig config;
        private readonly IReadOnlyList<NeighborDefinition> neighbors;

        /// <summary>Memoria del anti-racha que alimenta y consulta.</summary>
        public PityTracker Pity => pity;

        /// <summary>Toma la memoria del anti-racha y el catálogo de vecinos.</summary>
        public HouseOccupancyResolver(
            PityTracker pity, PityConfig config, IReadOnlyList<NeighborDefinition> neighbors)
        {
            this.pity = pity;
            this.config = config;
            this.neighbors = neighbors;
        }

        /// <summary>
        /// Resuelve el timbrazo y actualiza la memoria del anti-racha.
        ///
        /// El vecino se sortea DESPUÉS de saber que hay alguien, y con la misma
        /// secuencia aleatoria. Sortearlo siempre gastaría un número por casa
        /// vacía y haría que la cuadra dejara de ser reproducible al cambiar
        /// cualquier cosa del catálogo de vecinos.
        /// </summary>
        public HouseOccupancy Resolve(HouseLayout layout, System.Random random)
        {
            float chance = layout.Chance;
            float probability = pity.ProbabilityFor(chance);
            bool readable = config.IsReadable(chance);

            bool occupied = pity.Resolve(chance, random);
            if (!occupied) return new HouseOccupancy(false, null, probability, readable);

            return new HouseOccupancy(true, PickNeighbor(random), probability, readable);
        }

        private NeighborDefinition PickNeighbor(System.Random random)
        {
            if (neighbors == null || neighbors.Count == 0) return null;
            return neighbors[random.Next(neighbors.Count)];
        }
    }
}
