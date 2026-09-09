using System.Collections.Generic;
using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.Simulation
{
    /// <summary>
    /// Genera recetas de casa. Clase plana y determinista: la misma semilla da
    /// la misma cuadra, y por eso se puede simular miles de partidas para
    /// verificar el balance sin entrar en Play Mode.
    /// </summary>
    public sealed class HouseLayoutGenerator
    {
        private readonly HouseGenConfig config;
        private readonly PityConfig pityConfig;
        private readonly SignalPicker picker;
        private readonly System.Random random;

        private readonly Queue<float> recentChances = new Queue<float>();
        private readonly Dictionary<SignalMountMode, int> usedByMode =
            new Dictionary<SignalMountMode, int>();
        private readonly HashSet<HouseSignalDefinition> usedSignals =
            new HashSet<HouseSignalDefinition>();
        private readonly List<PlacedSignal> placed = new List<PlacedSignal>();

        /// <summary>Arma el generador con una semilla explícita.</summary>
        public HouseLayoutGenerator(
            HouseGenConfig config, PityConfig pityConfig, int seed,
            SignalPickMode pickMode = SignalPickMode.Uniforme)
        {
            this.config = config;
            this.pityConfig = pityConfig;
            random = new System.Random(seed);
            picker = new SignalPicker(config.AvailableSignals, pickMode);
        }

        /// <summary>
        /// Produce la próxima casa. <paramref name="emptyRun"/> es la racha de
        /// casas vacías: cuanto más alta, más se sesga la GENERACIÓN hacia señales
        /// positivas. Sesgar lo que se ve, y no la tirada escondida, es lo que
        /// mantiene honestas a las señales.
        /// </summary>
        public HouseLayout Next(int emptyRun)
        {
            float lotWidth = config.RollLotWidth(random);
            float gap = config.RollGap(random);
            RoofStyle roof = config.RoofStyleFor(lotWidth, random);
            Sprite wall = PickWall();
            float doorOffset = RollDoorOffset(lotWidth);

            float positiveBias = ComputePositiveBias(emptyRun);
            BuildSignals(lotWidth, positiveBias, doorOffset);

            float chance = pityConfig.ClampSignalChance(pityConfig.BaseChance + SumWeights());
            RememberChance(chance);

            return new HouseLayout(
                lotWidth, gap, wall, roof, doorOffset, placed.ToArray(), chance);
        }

        /// <summary>
        /// Sesgo hacia señales positivas. Suma el empuje por racha de vacías y el
        /// de barrio muerto, que mira las últimas casas generadas: si el vecindario
        /// se está VIENDO muerto, se lo compensa aunque no haya racha de timbrazos.
        /// </summary>
        private float ComputePositiveBias(int emptyRun)
        {
            float bias = Mathf.Clamp01(emptyRun * pityConfig.PositiveBiasPerEmpty);

            if (recentChances.Count >= pityConfig.DeadBlockWindow && LooksDead())
                bias = Mathf.Max(bias, pityConfig.PositiveBiasPerEmpty);

            return bias;
        }

        private bool LooksDead()
        {
            foreach (float chance in recentChances)
                if (chance >= pityConfig.ReadableChanceThreshold) return false;
            return true;
        }

        private void BuildSignals(float lotWidth, float positiveBias, float doorOffset)
        {
            placed.Clear();
            usedByMode.Clear();
            usedSignals.Clear();

            int count = random.Next(0, config.MaximumSignals + 1);
            bool doorAnchorTaken = false;
            int wallIndex = 0;
            int groundIndex = 0;

            for (int i = 0; i < count; i++)
            {
                // PRIMERO el signo, después la señal concreta. Si la elegida no
                // entra en el terreno se sortea otra del mismo signo, para que la
                // distribución de chance no dependa del ancho.
                bool positive = (float)random.NextDouble() < 0.5f + positiveBias * 0.5f;

                HouseSignalDefinition signal = picker.Pick(
                    random, positive, lotWidth, usedByMode, config, usedSignals);

                if (signal == null) continue;

                usedSignals.Add(signal);
                usedByMode.TryGetValue(signal.MountMode, out int used);
                usedByMode[signal.MountMode] = used + 1;

                placed.Add(Place(signal, ref doorAnchorTaken, ref wallIndex, ref groundIndex, doorOffset));
            }
        }

        private PlacedSignal Place(
            HouseSignalDefinition signal, ref bool doorAnchorTaken,
            ref int wallIndex, ref int groundIndex, float doorOffset)
        {
            switch (signal.MountMode)
            {
                case SignalMountMode.PropEnAnclaDePared:
                    // El farol reclama el ancla pegada al marco; los demás se corren
                    // a los laterales. Es un aplique, no puede ir en cualquier lado.
                    if (signal.ClaimsDoorSideAnchor && !doorAnchorTaken)
                    {
                        doorAnchorTaken = true;
                        return new PlacedSignal(signal, 0, true, doorOffset < 0f);
                    }
                    return new PlacedSignal(signal, wallIndex++, false, wallIndex % 2 == 0);

                case SignalMountMode.PropEnAnclaDeSuelo:
                    // Siempre del lado más ancho: con la puerta corrida, el lado
                    // angosto puede no dar el espacio que el prop necesita.
                    return new PlacedSignal(signal, groundIndex++, false, doorOffset < 0f);

                default:
                    return new PlacedSignal(signal, 0, false, false);
            }
        }

        private float SumWeights()
        {
            float sum = 0f;
            for (int i = 0; i < placed.Count; i++) sum += placed[i].Definition.Weight;
            return sum;
        }

        private Sprite PickWall()
        {
            IReadOnlyList<Sprite> walls = config.WallSprites;
            return walls.Count == 0 ? null : walls[random.Next(walls.Count)];
        }

        /// <summary>
        /// Corrimiento de la puerta respecto del centro. Se acota para que el lado
        /// angosto nunca quede por debajo de media anchura de camino.
        /// </summary>
        private float RollDoorOffset(float lotWidthPixels)
        {
            float limit = Mathf.Max(0f, lotWidthPixels * 0.5f - config.DoorPathWidthPixels);
            float jitter = limit * 0.35f;
            return (float)(random.NextDouble() * 2.0 - 1.0) * jitter;
        }

        private void RememberChance(float chance)
        {
            recentChances.Enqueue(chance);
            while (recentChances.Count > pityConfig.DeadBlockWindow) recentChances.Dequeue();
        }
    }
}
