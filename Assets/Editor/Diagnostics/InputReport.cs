using System.Collections.Generic;
using System.Text;
using BuenosDias.DebugTools;
using BuenosDias.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace BuenosDias.EditorTools.Diagnostics
{
    /// <summary>
    /// Arma el informe de input en texto plano, para pegar en un mensaje.
    ///
    /// Existe para que reportar una falla del gabinete no sea transcribir a mano lo
    /// que dice la ventana: se copia el estado exacto y se manda. Un layout mal
    /// tipeado manda a buscar el problema al lugar equivocado.
    ///
    /// Va aparte de la ventana porque son dos cosas distintas: una dibuja y la otra
    /// escribe. La ventana no tiene por qué saber formatear texto.
    /// </summary>
    public static class InputReport
    {
        /// <summary>
        /// Arma el informe. <paramref name="log"/> son las líneas del registro de
        /// apretones, de la más nueva a la más vieja; <paramref name="timing"/> son
        /// los tiempos de cada apretón y cada suelta.
        /// </summary>
        public static string Build(IReadOnlyList<string> log, InputTimingLog timing)
        {
            var sb = new StringBuilder(4096);

            Header(sb);
            Devices(sb);
            Log(sb, log);
            Held(sb);
            Timing(sb, timing);

            return sb.ToString();
        }

        /// <summary>Segundos a milisegundos enteros, o una raya si no hay dato.</summary>
        public static string Ms(double seconds)
        {
            return double.IsNaN(seconds) ? "—" : $"{seconds * 1000.0:0}";
        }

        /// <summary>
        /// Una línea de tiempos: cuándo pasó desde el cero, qué fue, y los tres
        /// números que importan según sea apretón o suelta.
        /// </summary>
        public static string TimingLine(InputTimingLog timing, TimedInput entry)
        {
            string at = Ms(entry.Time - timing.Origin).PadLeft(7);
            string kind = entry.Pressed ? "APRIETA" : "suelta ";
            string any = $"Δ {Ms(entry.SincePreviousAny)}";

            if (!entry.Pressed)
                return $"+{at}  {kind}  {entry.Control}   {any} · sostenido {Ms(entry.HeldFor)}";

            string bounce = timing.IsBounce(entry) ? "   ⚠ REBOTE" : string.Empty;
            return $"+{at}  {kind}  {entry.Control}   {any} · desde su apretón anterior " +
                   $"{Ms(entry.SincePreviousPress)} · desde su suelta {Ms(entry.SinceRelease)}{bounce}";
        }

        /// <summary>El resumen de un control en una línea.</summary>
        public static string SummaryLine(ControlTiming summary)
        {
            return $"{summary.Control}: {summary.Presses} apretones, {summary.Bounces} rebotes · " +
                   $"entre apretones mín {Ms(summary.MinInterval)} / prom {Ms(summary.AverageInterval)} / " +
                   $"máx {Ms(summary.MaxInterval)} · sostenido mín {Ms(summary.MinHeld)} / " +
                   $"prom {Ms(summary.AverageHeld)} / máx {Ms(summary.MaxHeld)}";
        }

        /// <summary>
        /// Los tiempos van del más VIEJO al más nuevo, al revés que el registro de
        /// apretones: acá lo que se lee es una secuencia, y una secuencia se lee
        /// en el orden en que pasó.
        /// </summary>
        private static void Timing(StringBuilder sb, InputTimingLog timing)
        {
            sb.AppendLine();

            int count = timing != null ? timing.Entries.Count : 0;
            sb.AppendLine($"4 · TIEMPOS — {count} eventos, del más viejo al más nuevo, en ms");
            sb.AppendLine($"     (+ = desde el cronómetro a cero · rebote = reapretón a menos de " +
                          $"{Ms(timing?.BounceSeconds ?? double.NaN)} ms de soltar)");

            if (count == 0)
            {
                sb.AppendLine("  (ninguno: no se registró ni un apretón ni una suelta)");
                return;
            }

            foreach (TimedInput entry in timing.Entries)
                sb.AppendLine($"  {TimingLine(timing, entry)}");

            sb.AppendLine();
            sb.AppendLine("  RESUMEN POR CONTROL");
            foreach (ControlTiming summary in timing.Summarize())
                sb.AppendLine($"  · {SummaryLine(summary)}");
        }

        private static void Header(StringBuilder sb)
        {
            sb.AppendLine("INFORME DE INPUT — BUENOS DÍAS, VECINO");
            sb.AppendLine($"Unity {Application.unityVersion}   ·   {SystemInfo.operatingSystem}");
            sb.AppendLine($"Modo: {(EditorApplication.isPlaying ? "Play Mode" : "Edit Mode")}");
            sb.AppendLine(
                $"Aceptar controles ruidosos: {(OneButtonInput.AcceptNoisyControls ? "SÍ" : "no")}");
            sb.AppendLine();
        }

        private static void Devices(StringBuilder sb)
        {
            var devices = InputSystem.devices;
            sb.AppendLine($"1 · DETECCIÓN — {devices.Count} dispositivos");

            if (devices.Count == 0)
            {
                sb.AppendLine("  (ninguno: si el gabinete está enchufado, el problema es de " +
                              "detección — driver, cable o puerto)");
                sb.AppendLine();
                return;
            }

            foreach (InputDevice device in devices)
            {
                sb.AppendLine($"  · {device.displayName}");
                sb.AppendLine($"      layout      {device.layout}");
                sb.AppendLine($"      interfaz    {Interface(device)}");
                sb.AppendLine($"      fabricante  {Text(device.description.manufacturer)}");
                sb.AppendLine($"      producto    {Text(device.description.product)}");
                sb.AppendLine($"      estado      {(device.enabled ? "activo" : "DESHABILITADO")}");
            }

            sb.AppendLine();
        }

        /// <summary>
        /// El registro va tal cual se vio en pantalla. El veredicto de cada línea es
        /// el que valía EN EL MOMENTO del apretón: si después se aflojó el filtro, las
        /// líneas viejas siguen diciendo lo que dijeron, que es lo que hay que saber
        /// para entender qué pasó.
        /// </summary>
        private static void Log(StringBuilder sb, IReadOnlyList<string> log)
        {
            sb.AppendLine($"2 · APRETONES — {log.Count} registrados, del más nuevo al más viejo");
            sb.AppendLine("     (el veredicto es el que valía al momento de cada apretón)");

            if (log.Count == 0)
                sb.AppendLine("  (ninguno: no llegó un solo apretón)");
            else
                foreach (string line in log) sb.AppendLine($"  {line}");

            sb.AppendLine();
        }

        private static void Held(StringBuilder sb)
        {
            sb.AppendLine("3 · APRETADO AL COPIAR");

            int found = 0;
            foreach (InputDevice device in InputSystem.devices)
            foreach (InputControl control in device.allControls)
            {
                if (!(control is ButtonControl button) || !button.isPressed) continue;
                if (control.synthetic) continue;

                sb.AppendLine($"  · {control.path}" +
                              $"{(control.noisy ? "   [ruidoso]" : string.Empty)}");
                found++;
            }

            if (found == 0) sb.AppendLine("  (nada)");
        }

        private static string Interface(InputDevice device)
        {
            return Text(device.description.interfaceName);
        }

        private static string Text(string value)
        {
            return string.IsNullOrEmpty(value) ? "(vacío)" : value;
        }
    }
}
