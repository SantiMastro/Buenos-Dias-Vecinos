using System.Collections.Generic;

namespace BuenosDias.DebugTools
{
    /// <summary>Un cambio de estado de un botón, con sus tiempos ya calculados.</summary>
    public readonly struct TimedInput
    {
        /// <summary>Qué control fue, por su path. Ej: <c>/Keyboard/space</c>.</summary>
        public string Control { get; }

        /// <summary>True si se apretó, false si se soltó.</summary>
        public bool Pressed { get; }

        /// <summary>Momento del evento, en segundos, en el reloj del Input System.</summary>
        public double Time { get; }

        /// <summary>Segundos desde el evento anterior de CUALQUIER control. NaN en el primero.</summary>
        public double SincePreviousAny { get; }

        /// <summary>Solo en apretones: segundos desde el apretón anterior de este mismo control.</summary>
        public double SincePreviousPress { get; }

        /// <summary>Solo en apretones: segundos desde que se soltó este mismo control. Los rebotes se ven acá.</summary>
        public double SinceRelease { get; }

        /// <summary>Solo en sueltas: segundos que estuvo apretado.</summary>
        public double HeldFor { get; }

        /// <summary>Arma un registro ya calculado.</summary>
        public TimedInput(
            string control, bool pressed, double time, double sincePreviousAny,
            double sincePreviousPress, double sinceRelease, double heldFor)
        {
            Control = control;
            Pressed = pressed;
            Time = time;
            SincePreviousAny = sincePreviousAny;
            SincePreviousPress = sincePreviousPress;
            SinceRelease = sinceRelease;
            HeldFor = heldFor;
        }
    }

    /// <summary>El resumen de un control: cuántas veces, cada cuánto y cuánto dura.</summary>
    public readonly struct ControlTiming
    {
        /// <summary>Qué control es.</summary>
        public string Control { get; }

        /// <summary>Apretones registrados.</summary>
        public int Presses { get; }

        /// <summary>Apretones que llegaron menos de <see cref="InputTimingLog.BounceSeconds"/> después de soltar.</summary>
        public int Bounces { get; }

        /// <summary>Intervalo entre apretones: mínimo, promedio y máximo, en segundos. NaN si no hubo dos.</summary>
        public double MinInterval { get; }

        /// <summary>Promedio del intervalo entre apretones.</summary>
        public double AverageInterval { get; }

        /// <summary>Máximo del intervalo entre apretones.</summary>
        public double MaxInterval { get; }

        /// <summary>Tiempo apretado: mínimo, promedio y máximo, en segundos. NaN si no se soltó nunca.</summary>
        public double MinHeld { get; }

        /// <summary>Promedio del tiempo apretado.</summary>
        public double AverageHeld { get; }

        /// <summary>Máximo del tiempo apretado.</summary>
        public double MaxHeld { get; }

        /// <summary>Arma el resumen.</summary>
        public ControlTiming(
            string control, int presses, int bounces,
            double minInterval, double averageInterval, double maxInterval,
            double minHeld, double averageHeld, double maxHeld)
        {
            Control = control;
            Presses = presses;
            Bounces = bounces;
            MinInterval = minInterval;
            AverageInterval = averageInterval;
            MaxInterval = maxInterval;
            MinHeld = minHeld;
            AverageHeld = averageHeld;
            MaxHeld = maxHeld;
        }
    }

    /// <summary>
    /// Registro de TIEMPOS de input: cada apretón y cada suelta, con cuánto pasó
    /// desde el anterior y cuánto estuvo apretado.
    ///
    /// Existe para ajustar los tiempos del input con números y no a ojo. El
    /// control físico —la plancha, el timbre, la Raspberry Pi que los traduce a
    /// teclas— tiene sus propios tiempos: cuánto tarda en mandar, si rebota, si
    /// suelta y vuelve a apretar solo. La ventana de coincidencia y el buffer del
    /// timbre tienen que salir de esos números.
    ///
    /// Es una clase plana, sin Unity, para poder testearla y para poder usarla
    /// algún día desde un overlay en la build, que es donde corre el gabinete.
    /// </summary>
    public sealed class InputTimingLog
    {
        private readonly int capacity;
        private readonly List<TimedInput> entries;
        private readonly Dictionary<string, double> lastPress = new Dictionary<string, double>();
        private readonly Dictionary<string, double> lastRelease = new Dictionary<string, double>();
        private double lastAny = double.NaN;

        /// <summary>
        /// <paramref name="capacity"/> es cuántos eventos se guardan; pasado eso se
        /// descartan los más viejos. <paramref name="bounceSeconds"/> es por debajo
        /// de cuánto un reapretón cuenta como rebote.
        /// </summary>
        public InputTimingLog(int capacity = 400, double bounceSeconds = 0.03)
        {
            this.capacity = capacity < 1 ? 1 : capacity;
            BounceSeconds = bounceSeconds;
            entries = new List<TimedInput>(this.capacity);
        }

        /// <summary>Por debajo de cuántos segundos después de soltar, un apretón es rebote.</summary>
        public double BounceSeconds { get; }

        /// <summary>Eventos guardados, del más viejo al más nuevo.</summary>
        public IReadOnlyList<TimedInput> Entries => entries;

        /// <summary>Momento del primer evento desde el último "a cero". NaN si no hubo ninguno.</summary>
        public double Origin { get; private set; } = double.NaN;

        /// <summary>Momento del último evento. NaN si no hubo ninguno.</summary>
        public double LastTime => lastAny;

        /// <summary>Registra un cambio de estado y devuelve el registro con los tiempos calculados.</summary>
        public TimedInput Record(string control, bool pressed, double time)
        {
            if (double.IsNaN(Origin)) Origin = time;

            double sinceAny = double.IsNaN(lastAny) ? double.NaN : time - lastAny;
            double sincePress = double.NaN;
            double sinceRelease = double.NaN;
            double held = double.NaN;

            if (pressed)
            {
                if (lastPress.TryGetValue(control, out double previous)) sincePress = time - previous;
                if (lastRelease.TryGetValue(control, out double released)) sinceRelease = time - released;
                lastPress[control] = time;
            }
            else
            {
                if (lastPress.TryGetValue(control, out double pressedAt)) held = time - pressedAt;
                lastRelease[control] = time;
            }

            var entry = new TimedInput(control, pressed, time, sinceAny, sincePress, sinceRelease, held);

            if (entries.Count >= capacity) entries.RemoveAt(0);
            entries.Add(entry);
            lastAny = time;

            return entry;
        }

        /// <summary>Si ese apretón llegó tan pegado a la suelta anterior que es un rebote.</summary>
        public bool IsBounce(TimedInput entry)
        {
            return entry.Pressed && !double.IsNaN(entry.SinceRelease) && entry.SinceRelease < BounceSeconds;
        }

        /// <summary>Pone el cronómetro a cero: borra todo lo registrado.</summary>
        public void Clear()
        {
            entries.Clear();
            lastPress.Clear();
            lastRelease.Clear();
            lastAny = double.NaN;
            Origin = double.NaN;
        }

        /// <summary>Resume lo guardado por control, en el orden en que apareció cada uno.</summary>
        public List<ControlTiming> Summarize()
        {
            var order = new List<string>();
            var intervals = new Dictionary<string, List<double>>();
            var holds = new Dictionary<string, List<double>>();
            var presses = new Dictionary<string, int>();
            var bounces = new Dictionary<string, int>();

            foreach (TimedInput entry in entries)
            {
                if (!presses.ContainsKey(entry.Control))
                {
                    order.Add(entry.Control);
                    presses[entry.Control] = 0;
                    bounces[entry.Control] = 0;
                    intervals[entry.Control] = new List<double>();
                    holds[entry.Control] = new List<double>();
                }

                if (entry.Pressed)
                {
                    presses[entry.Control]++;
                    if (!double.IsNaN(entry.SincePreviousPress)) intervals[entry.Control].Add(entry.SincePreviousPress);
                    if (IsBounce(entry)) bounces[entry.Control]++;
                }
                else if (!double.IsNaN(entry.HeldFor))
                {
                    holds[entry.Control].Add(entry.HeldFor);
                }
            }

            var result = new List<ControlTiming>(order.Count);
            foreach (string control in order)
            {
                Stats(intervals[control], out double minI, out double avgI, out double maxI);
                Stats(holds[control], out double minH, out double avgH, out double maxH);
                result.Add(new ControlTiming(
                    control, presses[control], bounces[control], minI, avgI, maxI, minH, avgH, maxH));
            }

            return result;
        }

        private static void Stats(List<double> values, out double min, out double average, out double max)
        {
            if (values.Count == 0)
            {
                min = average = max = double.NaN;
                return;
            }

            min = double.MaxValue;
            max = double.MinValue;
            double sum = 0d;

            foreach (double value in values)
            {
                if (value < min) min = value;
                if (value > max) max = value;
                sum += value;
            }

            average = sum / values.Count;
        }
    }
}
