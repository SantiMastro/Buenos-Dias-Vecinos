using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuenosDias.Simulation
{
    /// <summary>Un puesto de la tabla: iniciales y puntaje.</summary>
    /// <remarks>
    /// Es un struct serializable para que <c>JsonUtility</c> lo lea y lo escriba
    /// tal cual; los campos son privados como en el resto del proyecto.
    /// </remarks>
    [Serializable]
    public struct HighscoreEntry
    {
        [SerializeField] private string initials;
        [SerializeField] private int score;

        /// <summary>Arma un puesto.</summary>
        public HighscoreEntry(string initials, int score)
        {
            this.initials = initials;
            this.score = score;
        }

        /// <summary>Iniciales. Un puesto sin iniciales —un JSON editado a mano— se lee como "---".</summary>
        public string Initials => string.IsNullOrEmpty(initials) ? "---" : initials;

        /// <summary>Comitiva al caer la noche.</summary>
        public int Score => score;
    }

    /// <summary>
    /// La tabla de récords de una religión: de mayor a menor y con tope.
    ///
    /// Se ordena SOLA: nadie de afuera ordena ni recorta. Así un JSON viejo o
    /// editado a mano, con los puestos desordenados o de más, sale igual de
    /// prolijo.
    ///
    /// ⚠️ En un empate gana el que llegó primero, como en los fichines: el récord
    /// nuevo tiene que SUPERAR al viejo para pasarle por delante. Con puntajes
    /// chicos —la comitiva rara vez pasa de 15— la tabla se llena de empates, y si
    /// el último en empatar ganara, cada partida pareja borraría de la tabla a
    /// alguien que llegó a lo mismo antes.
    ///
    /// Es plana para poder testearla sin Play Mode.
    /// </summary>
    public sealed class HighscoreTable
    {
        private readonly List<HighscoreEntry> entries = new List<HighscoreEntry>();
        private readonly int minimumScore;

        /// <summary>
        /// <paramref name="saved"/> puede venir en cualquier orden y con más
        /// puestos que <paramref name="capacity"/>: se ordena y se recorta acá.
        /// <paramref name="minimumScore"/> solo filtra los récords NUEVOS; los
        /// guardados se respetan aunque el mínimo haya subido después.
        /// </summary>
        public HighscoreTable(int capacity, IEnumerable<HighscoreEntry> saved = null, int minimumScore = 1)
        {
            Capacity = Math.Max(1, capacity);
            this.minimumScore = minimumScore;

            if (saved == null) return;

            foreach (HighscoreEntry entry in saved)
                if (entry.Score >= 0) Place(entry);
        }

        /// <summary>Cuántos puestos entran.</summary>
        public int Capacity { get; }

        /// <summary>Puestos, del mejor al peor.</summary>
        public IReadOnlyList<HighscoreEntry> Entries => entries;

        /// <summary>
        /// Puesto, contado desde 0, que ocuparía ese puntaje, o −1 si no entra: por
        /// debajo del mínimo, o sin superar al último con la tabla llena.
        /// </summary>
        public int RankFor(int score)
        {
            if (score < minimumScore) return -1;

            int rank = IndexAfterTies(score);
            return rank < Capacity ? rank : -1;
        }

        /// <summary>Si ese puntaje entra en la tabla.</summary>
        public bool Qualifies(int score) => RankFor(score) >= 0;

        /// <summary>
        /// Anota un récord y devuelve su puesto, o −1 si no entraba. El que queda
        /// último se cae de la tabla.
        /// </summary>
        public int Insert(string initials, int score)
        {
            int rank = RankFor(score);
            if (rank < 0) return -1;

            entries.Insert(rank, new HighscoreEntry(initials?.ToUpperInvariant(), score));
            Trim();
            return rank;
        }

        private void Place(HighscoreEntry entry)
        {
            entries.Insert(IndexAfterTies(entry.Score), entry);
            Trim();
        }

        /// <summary>
        /// El primer puesto con MENOS puntaje. Los empatados quedan adelante: es lo
        /// que hace que gane el que llegó primero, y lo que mantiene el orden de
        /// un JSON cargado cuando trae empates.
        /// </summary>
        private int IndexAfterTies(int score)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Score < score) return i;

            return entries.Count;
        }

        private void Trim()
        {
            if (entries.Count > Capacity) entries.RemoveRange(Capacity, entries.Count - Capacity);
        }
    }
}
