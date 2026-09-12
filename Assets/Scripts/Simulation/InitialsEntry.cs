using System;

namespace BuenosDias.Simulation
{
    /// <summary>Qué pasó con la carga de iniciales en este cuadro.</summary>
    public enum InitialsStep
    {
        /// <summary>Nada que redibujar.</summary>
        Nada,

        /// <summary>La letra que se está eligiendo pasó a la siguiente.</summary>
        CambioLetra,

        /// <summary>Se fijó una letra y el cursor pasó a la próxima.</summary>
        LetraFijada,

        /// <summary>Se fijó la última letra: las iniciales están completas.</summary>
        Completo
    }

    /// <summary>
    /// Cargar iniciales con UN botón, como en los fichines.
    ///
    /// <list type="bullet">
    /// <item><b>Toque corto</b>: la letra que se está eligiendo pasa a la
    /// siguiente, A→B→…→Z→A.</item>
    /// <item><b>Mantener</b>: la letra queda fija y el cursor pasa a la próxima.
    /// Con la última fija, se terminó.</item>
    /// </list>
    ///
    /// ⚠️ La letra cambia al SOLTAR y no al apretar, y es a propósito: al apretar
    /// todavía no se sabe si es un toque o el principio de un mantenido. Si
    /// cambiara al apretar, cada confirmación correría primero la letra, y fijar
    /// la A obligaría a dar la vuelta entera al abecedario.
    ///
    /// Solo cuenta un apretón que EMPIEZA con la carga abierta: un botón que ya
    /// venía sostenido de la partida no fija nada solo.
    ///
    /// Es plana y recibe el tiempo de afuera para poder testearla sin Play Mode.
    /// </summary>
    public sealed class InitialsEntry
    {
        /// <summary>El abecedario por defecto: la fuente tiene las 26 mayúsculas.</summary>
        public const string DefaultAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        private readonly string alphabet;
        private readonly float holdSeconds;
        private readonly int[] letters;

        private bool tracking;
        private bool holdSpent;
        private float pressStart;

        /// <summary>
        /// <paramref name="length"/> letras, todas arrancando en la primera del
        /// <paramref name="alphabet"/>. Mantener <paramref name="holdSeconds"/>
        /// fija la letra.
        /// </summary>
        public InitialsEntry(int length, string alphabet, float holdSeconds)
        {
            this.alphabet = string.IsNullOrEmpty(alphabet) ? DefaultAlphabet : alphabet;
            this.holdSeconds = Math.Max(0f, holdSeconds);
            letters = new int[Math.Max(1, length)];
        }

        /// <summary>Cuántas letras lleva.</summary>
        public int Length => letters.Length;

        /// <summary>Qué letra se está eligiendo. Igual a <see cref="Length"/> al terminar.</summary>
        public int Cursor { get; private set; }

        /// <summary>Si ya se fijaron todas.</summary>
        public bool IsComplete => Cursor >= letters.Length;

        /// <summary>La letra de ese puesto. Las que faltan valen la primera del abecedario.</summary>
        public char LetterAt(int index) => alphabet[letters[index]];

        /// <summary>Las iniciales como quedaron.</summary>
        public string Initials
        {
            get
            {
                var chars = new char[letters.Length];
                for (int i = 0; i < chars.Length; i++) chars[i] = LetterAt(i);
                return new string(chars);
            }
        }

        /// <summary>
        /// Cuánto lleva el mantenido en curso, de 0 a 1. En 0 si no hay apretón,
        /// si ya fijó o si la carga terminó. Lo dibuja el medidor.
        /// </summary>
        public float HoldProgress(float now)
        {
            if (!tracking || holdSpent || IsComplete || holdSeconds <= 0f) return 0f;
            return Math.Min(1f, Math.Max(0f, (now - pressStart) / holdSeconds));
        }

        /// <summary>
        /// Avanza un cuadro. <paramref name="pressed"/> es el pulso del apretón y
        /// <paramref name="held"/> si sigue abajo; <paramref name="now"/> tiene que
        /// ser tiempo NO escalado.
        /// </summary>
        public InitialsStep Update(bool pressed, bool held, float now)
        {
            if (IsComplete) return InitialsStep.Nada;

            InitialsStep step = InitialsStep.Nada;

            if (pressed)
            {
                // Un apretón nuevo sin haber visto soltar el anterior: pasa cuando
                // soltar y volver a apretar caen entre dos cuadros. El anterior fue
                // un toque y cuenta.
                if (tracking && !holdSpent) step = Advance();

                tracking = true;
                holdSpent = false;
                pressStart = now;
            }

            if (!tracking) return step;

            if (held)
            {
                if (holdSpent || now - pressStart < holdSeconds) return step;

                holdSpent = true;
                Cursor++;
                return IsComplete ? InitialsStep.Completo : InitialsStep.LetraFijada;
            }

            // Soltó. Si el mantenido ya había fijado, soltar no hace nada: si no,
            // la letra NUEVA arrancaría corrida.
            tracking = false;
            return holdSpent ? step : Advance();
        }

        /// <summary>
        /// Da la carga por terminada con las letras como estén. Es la salida por
        /// tiempo: si alguien se va a mitad de la carga, el gabinete no puede
        /// quedar clavado en esta pantalla.
        /// </summary>
        public void Complete()
        {
            Cursor = letters.Length;
            tracking = false;
        }

        private InitialsStep Advance()
        {
            letters[Cursor] = (letters[Cursor] + 1) % alphabet.Length;
            return InitialsStep.CambioLetra;
        }
    }
}
