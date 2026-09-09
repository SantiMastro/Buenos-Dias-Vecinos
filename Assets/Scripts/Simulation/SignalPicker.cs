using System.Collections.Generic;
using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.Simulation
{
    /// <summary>Cómo se elige una señal concreta dentro de las de un mismo signo.</summary>
    public enum SignalPickMode
    {
        /// <summary>Todas las elegibles con la misma probabilidad.</summary>
        Uniforme,

        /// <summary>Proporcional al valor absoluto del peso.</summary>
        PorPesoRelativo
    }

    /// <summary>
    /// Elige qué señal concreta va en un slot, respetando el signo pedido y las
    /// restricciones del terreno.
    ///
    /// La separación entre elegir SIGNO y elegir SEÑAL es lo que evita el sesgo:
    /// si el auto no entra en un terreno angosto, el slot no queda vacío sino que
    /// se sortea otra positiva. Dejarlo vacío volvería más negativas de lo
    /// diseñado a todas las casas angostas.
    /// </summary>
    public sealed class SignalPicker
    {
        private readonly IReadOnlyList<HouseSignalDefinition> catalog;
        private readonly SignalPickMode mode;
        private readonly List<HouseSignalDefinition> candidates = new List<HouseSignalDefinition>();

        /// <summary>Prepara el sorteador sobre un catálogo de señales.</summary>
        public SignalPicker(IReadOnlyList<HouseSignalDefinition> catalog, SignalPickMode mode)
        {
            this.catalog = catalog;
            this.mode = mode;
        }

        /// <summary>
        /// Devuelve una señal del signo pedido que entre en el terreno y todavía
        /// tenga cupo, o <c>null</c> si no queda ninguna.
        /// </summary>
        public HouseSignalDefinition Pick(
            System.Random random, bool positive, float lotWidthPixels,
            IReadOnlyDictionary<SignalMountMode, int> usedByMode,
            HouseGenConfig config, ICollection<HouseSignalDefinition> alreadyUsed)
        {
            candidates.Clear();

            for (int i = 0; i < catalog.Count; i++)
            {
                HouseSignalDefinition signal = catalog[i];
                if (signal == null) continue;
                if (signal.IsPositive != positive) continue;
                if (alreadyUsed.Contains(signal)) continue;
                if (!signal.FitsLot(lotWidthPixels)) continue;

                usedByMode.TryGetValue(signal.MountMode, out int used);
                if (used >= config.CapacityFor(signal.MountMode)) continue;

                candidates.Add(signal);
            }

            if (candidates.Count == 0) return null;

            return mode == SignalPickMode.Uniforme
                ? candidates[random.Next(candidates.Count)]
                : PickWeighted(random);
        }

        private HouseSignalDefinition PickWeighted(System.Random random)
        {
            float total = 0f;
            for (int i = 0; i < candidates.Count; i++)
                total += Mathf.Abs(candidates[i].Weight);

            if (total <= 0f) return candidates[random.Next(candidates.Count)];

            float roll = (float)random.NextDouble() * total;
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= Mathf.Abs(candidates[i].Weight);
                if (roll <= 0f) return candidates[i];
            }

            return candidates[candidates.Count - 1];
        }
    }
}
