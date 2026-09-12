using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuenosDias.Simulation
{
    /// <summary>
    /// Lo que se guarda en disco: una tabla de récords por religión, cada una bajo
    /// su clave.
    ///
    /// Es JSON de <c>JsonUtility</c> y no un formato propio: se abre con el Bloc de
    /// notas para borrar un puesto o corregir unas iniciales. Y un archivo que no
    /// se entiende —cortado por un corte de luz, editado mal— se detecta con
    /// <see cref="WasUnreadable"/> en vez de reventar la partida.
    ///
    /// Acá no se ordena ni se recorta nada: eso es de <see cref="HighscoreTable"/>.
    /// </summary>
    [Serializable]
    public sealed class HighscoreSave
    {
        /// <summary>Versión del formato. Sube el día que cambie la forma del JSON.</summary>
        public const int CurrentVersion = 1;

        [SerializeField] private int version = CurrentVersion;
        [SerializeField] private List<HighscoreBoard> boards = new List<HighscoreBoard>();

        /// <summary>Versión con la que se escribió.</summary>
        public int Version => version;

        /// <summary>
        /// Si el texto de origen no se pudo leer. Quien guarda lo usa para apartar
        /// el archivo roto antes de escribir encima.
        /// </summary>
        public bool WasUnreadable { get; private set; }

        /// <summary>Puestos guardados de esa clave. Vacío si no hay tabla todavía.</summary>
        public IReadOnlyList<HighscoreEntry> EntriesFor(string key)
        {
            HighscoreBoard board = Find(key);
            return board != null ? board.Entries : (IReadOnlyList<HighscoreEntry>)Array.Empty<HighscoreEntry>();
        }

        /// <summary>Reemplaza los puestos de esa clave. Las demás tablas no se tocan.</summary>
        public void Set(string key, IReadOnlyList<HighscoreEntry> entries)
        {
            HighscoreBoard board = Find(key);
            if (board == null)
            {
                board = new HighscoreBoard(key);
                boards.Add(board);
            }

            board.Replace(entries);
        }

        /// <summary>El JSON, con sangría para que se pueda leer a mano.</summary>
        public string ToJson() => JsonUtility.ToJson(this, true);

        /// <summary>
        /// Lee el JSON. Nunca tira: texto vacío es una tabla vacía, y texto roto es
        /// una tabla vacía marcada como <see cref="WasUnreadable"/>.
        /// </summary>
        public static HighscoreSave FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new HighscoreSave();

            try
            {
                HighscoreSave loaded = JsonUtility.FromJson<HighscoreSave>(json);
                if (loaded == null) return Unreadable();

                if (loaded.boards == null) loaded.boards = new List<HighscoreBoard>();
                return loaded;
            }
            catch (ArgumentException)
            {
                return Unreadable();
            }
        }

        private static HighscoreSave Unreadable() => new HighscoreSave { WasUnreadable = true };

        private HighscoreBoard Find(string key)
        {
            key ??= string.Empty;

            foreach (HighscoreBoard board in boards)
                if (board != null && board.Key == key) return board;

            return null;
        }
    }

    /// <summary>Una tabla guardada: la clave de la religión y sus puestos.</summary>
    [Serializable]
    public sealed class HighscoreBoard
    {
        [SerializeField] private string key = string.Empty;
        [SerializeField] private List<HighscoreEntry> entries = new List<HighscoreEntry>();

        /// <summary>Vacía. La usa <c>JsonUtility</c> al leer.</summary>
        public HighscoreBoard() { }

        /// <summary>Una tabla nueva, sin puestos.</summary>
        public HighscoreBoard(string key)
        {
            this.key = key ?? string.Empty;
        }

        /// <summary>De qué religión es.</summary>
        public string Key => key ?? string.Empty;

        /// <summary>Puestos, en el orden en que se guardaron.</summary>
        public IReadOnlyList<HighscoreEntry> Entries =>
            entries ?? (IReadOnlyList<HighscoreEntry>)Array.Empty<HighscoreEntry>();

        /// <summary>Reemplaza los puestos por una copia de los que llegan.</summary>
        public void Replace(IReadOnlyList<HighscoreEntry> source)
        {
            entries = new List<HighscoreEntry>(source ?? Array.Empty<HighscoreEntry>());
        }
    }
}
