using System.Collections.Generic;
using BuenosDias.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
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
    /// </summary>
    public sealed class InputDiagnosticsWindow : EditorWindow
    {
        private const int LogCapacity = 14;

        /// <summary>
        /// La palanca de ruidosos vive en <c>EditorPrefs</c> y NO en un asset: es una
        /// decisión de la máquina que está probando el gabinete, no del proyecto. En
        /// un asset se colaría en un commit y viajaría a todas las máquinas.
        /// </summary>
        private const string NoisyPrefKey = "BuenosDias.Input.AcceptNoisyControls";

        private readonly List<string> log = new List<string>();
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
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;
            EditorApplication.update -= Repaint;
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

            EditorGUILayout.EndScrollView();
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
                EditorGUIUtility.systemCopyBuffer = InputReport.Build(log);
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
