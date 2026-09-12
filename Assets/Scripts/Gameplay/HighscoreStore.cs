using System;
using System.IO;
using System.Text;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// Lee y escribe el JSON de récords en disco.
    ///
    /// ⚠️ Escribe en DOS pasos: primero un <c>.tmp</c> al lado y, recién con ese
    /// completo, lo cambia por el de verdad. Un gabinete se apaga desenchufándolo;
    /// si el corte cae a mitad de una escritura directa, queda un JSON cortado y se
    /// pierde la tabla entera. En dos pasos, lo peor que puede pasar es perder el
    /// último récord.
    ///
    /// Un archivo ilegible no se pisa: se aparta con otro nombre antes de escribir
    /// encima, para que quien lo rompió editándolo a mano lo pueda recuperar.
    ///
    /// Ningún error de disco corta la partida: se avisa por consola y se sigue
    /// jugando con la tabla que haya en memoria.
    /// </summary>
    public sealed class HighscoreStore
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>
        /// El archivo va en <paramref name="directory"/>. En el juego es
        /// <c>Application.persistentDataPath</c>; los tests le pasan una carpeta
        /// temporal.
        /// </summary>
        public HighscoreStore(string directory, string fileName)
        {
            FilePath = Path.Combine(directory, fileName);
        }

        /// <summary>Ruta completa del JSON.</summary>
        public string FilePath { get; }

        /// <summary>Lee lo guardado. Sin archivo, o sin poder leerlo, devuelve una tabla vacía.</summary>
        public HighscoreSave Load()
        {
            if (!File.Exists(FilePath)) return new HighscoreSave();

            try
            {
                HighscoreSave save = HighscoreSave.FromJson(File.ReadAllText(FilePath, Utf8));
                if (save.WasUnreadable) SetAside();
                return save;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning(
                    $"[HighscoreStore] No se pudo leer '{FilePath}': {e.Message}. " +
                    "Se juega con la tabla vacía.");
                return new HighscoreSave();
            }
        }

        /// <summary>Guarda. Devuelve si pudo.</summary>
        public bool Save(HighscoreSave save)
        {
            string temporary = FilePath + ".tmp";

            try
            {
                string directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                File.WriteAllText(temporary, save.ToJson(), Utf8);
                Swap(temporary);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning(
                    $"[HighscoreStore] No se pudo guardar '{FilePath}': {e.Message}. " +
                    "El récord queda solo hasta que se cierre el juego.");
                return false;
            }
        }

        /// <summary>
        /// Cambia el temporal por el de verdad. <c>File.Replace</c> es el cambio de
        /// una sola vez que da el sistema de archivos; donde no existe, se copia.
        /// </summary>
        private void Swap(string temporary)
        {
            if (!File.Exists(FilePath))
            {
                File.Move(temporary, FilePath);
                return;
            }

            try
            {
                File.Replace(temporary, FilePath, null);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(temporary, FilePath, true);
                File.Delete(temporary);
            }
        }

        private void SetAside()
        {
            string aside = $"{FilePath}.ilegible-{DateTime.Now:yyyyMMdd-HHmmss}";

            try
            {
                File.Move(FilePath, aside);
                Debug.LogWarning(
                    $"[HighscoreStore] '{FilePath}' no se pudo leer y se apartó como " +
                    $"'{aside}'. Se arranca una tabla nueva.");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning(
                    $"[HighscoreStore] '{FilePath}' no se pudo leer ni apartar: {e.Message}.");
            }
        }
    }
}
