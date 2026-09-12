using System.Collections.Generic;
using BuenosDias.DebugTools;
using BuenosDias.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace BuenosDias.EditorTools.Diagnostics
{
    /// <summary>
    /// Ventana de diagnóstico de input, para el día que se conecte el gabinete.
    ///
    /// Responde UNA pregunta en dos segundos: **¿el problema es detección o es
    /// binding?** Son dos fallas distintas que se sienten igual —el botón no hace
    /// nada— y se arreglan en lugares opuestos:
    ///
    /// - El dispositivo NO aparece en la lista → detección. Es driver, cable o USB;
    ///   el juego no tiene nada que ver.
    /// - Aparece, pero apretar no registra nada → el encoder no manda ese botón, o
    ///   lo manda como eje en vez de como botón.
    /// - Registra pero sale DESCARTADO → binding. El control llega y el juego lo
    ///   filtra; es problema del juego.
    ///
    /// Anda en Edit Mode y en Play Mode: el Input System detecta dispositivos sin
    /// necesidad de darle Play, así que se puede enchufar el gabinete y probarlo
    /// sin correr el juego.
    ///
    /// Y trae con qué RESOLVER ahí mismo, sin editar código:
    ///
    /// - **Aceptar controles ruidosos** afloja ese filtro en caliente. Es la salida
    ///   para el único riesgo que quedó anotado del gabinete: si el encoder marca sus
    ///   botones como ruidosos, el juego no responde y sin esto habría que tocar un
    ///   <c>.cs</c> con el botón en la mano.
    /// - **Copiar informe** deja en el portapapeles los dispositivos con sus layouts y
    ///   los últimos apretones con veredicto y motivo, para mandarlo sin transcribir.
    ///
    /// Y mide TIEMPOS: cada apretón y cada suelta con el timestamp del evento, cuánto
    /// pasó desde el anterior, cuánto estuvo apretado y si fue un rebote. Es con lo
    /// que se ajustan la ventana de coincidencia y el buffer del timbre del
    /// <c>InputConfig</c> según cómo manda las teclas el control físico.
    /// </summary>
    public sealed class InputDiagnosticsWindow : EditorWindow
    {
        private const int LogCapacity = 14;
        private const int TimingLinesOnScreen = 25;

        /// <summary>
        /// La palanca de ruidosos vive en <c>EditorPrefs</c> y NO en un asset: es una
        /// decisión de la máquina que está probando el gabinete, no del proyecto. En
        /// un asset se colaría en un commit y viajaría a todas las máquinas.
        /// </summary>
        private const string NoisyPrefKey = "BuenosDias.Input.AcceptNoisyControls";

        private readonly List<string> log = new List<string>();
        private readonly InputTimingLog timing = new InputTimingLog();
        private Vector2 scroll;
        private System.IDisposable subscription;

        [MenuItem("Tools/Buenos Días/Diagnóstico de input", priority = 200)]
        private static void Open()
        {
            GetWindow<InputDiagnosticsWindow>("Input").minSize = new Vector2(460f, 480f);
        }

        /// <summary>
        /// Devuelve la palanca a su valor guardado después de CADA recarga de dominio.
        /// Sin esto, darle Play la apagaría sola y el gabinete volvería a quedar mudo
        /// justo al probarlo. Corre aunque la ventana nunca se haya abierto.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void RestoreNoisyOverride()
        {
            OneButtonInput.AcceptNoisyControls = EditorPrefs.GetBool(NoisyPrefKey, false);
        }

        private void OnEnable()
        {
            subscription = InputSystem.onAnyButtonPress.Call(OnPressed);
            InputSystem.onEvent += OnInputEvent;
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;
            InputSystem.onEvent -= OnInputEvent;
            EditorApplication.update -= Repaint;
        }

        /// <summary>
        /// Mira cada evento de estado ANTES de que se aplique y anota qué botones
        /// cambiaron, con el tiempo del EVENTO y no el del cuadro: dos apretones
        /// que caen en el mismo cuadro tienen que salir con sus milisegundos de
        /// diferencia, que es justamente lo que se viene a medir.
        ///
        /// A diferencia del registro de arriba, acá se ven también las SUELTAS,
        /// que son las que dicen cuánto se sostuvo y si hubo rebote.
        /// </summary>
        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;

            foreach (InputControl control in eventPtr.EnumerateChangedControls(device))
            {
                if (!(control is ButtonControl button) || control.synthetic) continue;

                // El estado actual todavía es el de ANTES del evento: onEvent corre
                // antes de que el Input System lo aplique.
                bool before = button.isPressed;
                bool after = button.IsValueConsideredPressed(button.ReadValueFromEvent(eventPtr));
                if (before == after) continue;

                timing.Record(control.path, after, eventPtr.time);
            }
        }

        private void OnPressed(InputControl control)
        {
            // Se registra TODO, aceptado o no. Un log que solo mostrara lo aceptado
            // no podría distinguir "no llega" de "llega y se descarta", que es
            // justamente la pregunta que esta ventana viene a contestar.
            bool ok = OneButtonInput.Accepts(control);
            string mark = ok ? "ACEPTADO " : "DESCARTADO";
            string why = ok ? "" : Reason(control);

            log.Insert(0, $"{mark}  {control.device.displayName} → {control.path}{why}");
            if (log.Count > LogCapacity) log.RemoveAt(log.Count - 1);
        }

        private static string Reason(InputControl control)
        {
            if (!(control is ButtonControl))
                return "   (no es un botón: es " + control.GetType().Name + ")";
            if (control.synthetic)
                return "   (sintético: lo arma el layout, no es un botón real)";
            if (control.noisy)
                return "   (RUIDOSO: el layout dice que cambia solo. Si esto sale en un " +
                       "botón del gabinete, ES el motivo de que no responda → tildá " +
                       "'Aceptar controles ruidosos' arriba y volvé a apretar)";
            if (!control.device.enabled)
                return "   (el dispositivo está deshabilitado)";
            return "";
        }

        private void OnGUI()
        {
            DrawToolbar();

            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawDevices();
            EditorGUILayout.Space();
            DrawLog();
            EditorGUILayout.Space();
            DrawHeld();
            EditorGUILayout.Space();
            DrawTiming();

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Los tiempos: los últimos eventos, del más nuevo al más viejo, y un
        /// resumen por control. Todo en milisegundos, que es la escala en la que se
        /// ajustan la ventana de coincidencia y el buffer.
        /// </summary>
        private void DrawTiming()
        {
            EditorGUILayout.LabelField("4 · TIEMPOS ENTRE INPUTS (ms)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Cronómetro a cero", GUILayout.Width(140f))) timing.Clear();

            string since = double.IsNaN(timing.LastTime)
                ? "sin eventos todavía"
                : $"desde el último evento: {InputReport.Ms(InputState.currentTime - timing.LastTime)} ms";
            EditorGUILayout.LabelField(since);
            EditorGUILayout.EndHorizontal();

            IReadOnlyList<TimedInput> entries = timing.Entries;
            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Pisá y soltá el felpudo varias veces, tocá el timbre, cerrá el libro. " +
                    "Cada apretón y cada suelta aparecen acá con su tiempo.\n\n" +
                    "Para medir el control físico: poné el cronómetro a cero, hacé la " +
                    "prueba y tocá 'Copiar informe'.", MessageType.Info);
                return;
            }

            int shown = 0;
            for (int i = entries.Count - 1; i >= 0 && shown < TimingLinesOnScreen; i--, shown++)
                EditorGUILayout.LabelField(InputReport.TimingLine(timing, entries[i]));

            if (entries.Count > TimingLinesOnScreen)
                EditorGUILayout.LabelField(
                    $"… {entries.Count - TimingLinesOnScreen} más en el informe copiado");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Resumen por control", EditorStyles.miniBoldLabel);
            foreach (ControlTiming summary in timing.Summarize())
                EditorGUILayout.LabelField(InputReport.SummaryLine(summary));
        }

        /// <summary>
        /// Las dos herramientas para resolver con el gabinete en la mano: aflojar el
        /// filtro y llevarse el estado exacto.
        /// </summary>
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Copiar informe", EditorStyles.toolbarButton, GUILayout.Width(110f)))
            {
                EditorGUIUtility.systemCopyBuffer = InputReport.Build(log, timing);
                ShowNotification(new GUIContent("Informe copiado"));
            }

            GUILayout.FlexibleSpace();

            bool wanted = GUILayout.Toggle(
                OneButtonInput.AcceptNoisyControls, "Aceptar controles ruidosos",
                EditorStyles.toolbarButton, GUILayout.Width(180f));

            EditorGUILayout.EndHorizontal();

            if (wanted != OneButtonInput.AcceptNoisyControls) SetNoisyOverride(wanted);

            if (!OneButtonInput.AcceptNoisyControls) return;

            EditorGUILayout.HelpBox(
                "Filtro aflojado: los controles RUIDOSOS pasan como botones buenos. " +
                "Sirve para rescatar un encoder que marca así sus botones. Dejarlo " +
                "prendido con un mando que de verdad tiene ruido —un acelerómetro, un " +
                "gatillo analógico— hace que se toquen timbres solos.\n\n" +
                "Vive en EditorPrefs de esta máquina: no se commitea, y para una BUILD " +
                "hay que prender el campo del componente OneButtonInput.",
                MessageType.Warning);
        }

        private void SetNoisyOverride(bool value)
        {
            OneButtonInput.AcceptNoisyControls = value;
            EditorPrefs.SetBool(NoisyPrefKey, value);
        }

        private void DrawDevices()
        {
            var devices = InputSystem.devices;
            EditorGUILayout.LabelField(
                $"1 · DETECCIÓN — {devices.Count} dispositivos", EditorStyles.boldLabel);

            if (devices.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No hay NINGÚN dispositivo. Si el gabinete está enchufado, el " +
                    "problema es de detección: driver, cable o puerto. El juego no " +
                    "tiene nada que ver.", MessageType.Error);
                return;
            }

            foreach (InputDevice device in devices)
            {
                EditorGUILayout.LabelField(
                    $"· {device.displayName}",
                    $"{device.layout}   [{Interface(device)}]   " +
                    $"{(device.enabled ? "activo" : "DESHABILITADO")}");
            }

            EditorGUILayout.HelpBox(
                "Un encoder de arcade puede aparecer como Joystick o como HID " +
                "genérico, NO como Gamepad. Eso es normal y el juego lo acepta " +
                "igual: no se ata a bindings por tipo de dispositivo.",
                MessageType.Info);
        }

        private static string Interface(InputDevice device)
        {
            string name = device.description.interfaceName;
            return string.IsNullOrEmpty(name) ? "sin interfaz" : name;
        }

        private void DrawLog()
        {
            EditorGUILayout.LabelField("2 · APRETONES, del más nuevo al más viejo",
                EditorStyles.boldLabel);

            if (log.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Todavía no llegó ningún apretón. Apretá el botón del gabinete.\n\n" +
                    "Si el dispositivo SÍ figura arriba y acá no aparece nada, el " +
                    "encoder no está mandando ese botón, o lo manda como eje en vez " +
                    "de como botón.", MessageType.Warning);
                return;
            }

            foreach (string line in log) EditorGUILayout.LabelField(line);

            if (GUILayout.Button("Limpiar")) log.Clear();
        }

        private void DrawHeld()
        {
            EditorGUILayout.LabelField("3 · APRETADO AHORA MISMO", EditorStyles.boldLabel);

            int found = 0;
            foreach (InputDevice device in InputSystem.devices)
            {
                foreach (InputControl control in device.allControls)
                {
                    if (!(control is ButtonControl button) || !button.isPressed) continue;
                    if (control.synthetic) continue;

                    EditorGUILayout.LabelField($"· {control.path}");
                    found++;
                }
            }

            if (found == 0) EditorGUILayout.LabelField("· nada");
        }
    }
}
