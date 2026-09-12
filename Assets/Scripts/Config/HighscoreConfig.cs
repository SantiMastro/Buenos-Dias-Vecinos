using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Config
{
    /// <summary>
    /// Perillas de la tabla de récords: cuántos puestos, cómo se cargan las
    /// iniciales, cuándo aparece y dónde se guarda.
    ///
    /// Existe porque las aspiradoras no pueden ascender: sin un final al que
    /// apuntar, lo que queda por perseguir es superar al de antes.
    /// </summary>
    [CreateAssetMenu(fileName = "HighscoreConfig", menuName = "Buenos Días/Config/Highscore", order = 21)]
    public sealed class HighscoreConfig : ScriptableObject
    {
        private const string DefaultFileName = "highscores.json";

        [Header("Tabla")]
        [Tooltip("Puestos que guarda y muestra. Hasta 10: es lo que entra en pantalla " +
                 "entre el borde de arriba y el titular del final.")]
        [SerializeField, Range(1, 10)] private int capacity = 10;

        [Tooltip("Puntaje mínimo para entrar. Con 1, terminar sin nadie atrás no deja récord.")]
        [SerializeField, Min(0)] private int minimumScore = 1;

        [Tooltip("PRENDIDO (el default): la tabla aparece solo con las religiones que " +
                 "no pueden ascender —las aspiradoras—, que no tienen otro final que " +
                 "perseguir.\nAPAGADO: cada religión tiene su propia tabla.")]
        [SerializeField] private bool onlyWithoutAscension = true;

        [Header("Iniciales")]
        [Tooltip("Cuántas letras. 3, como en los fichines.")]
        [SerializeField, Range(1, 5)] private int initialsLength = 3;

        [Tooltip("Letras por las que cicla el toque, en orden. Tienen que existir en la " +
                 "fuente 6x8: A-Z, 0-9, ÁÉÍÓÚÑÜ y algunos signos.")]
        [SerializeField] private string alphabet = InitialsEntry.DefaultAlphabet;

        [Tooltip("Segundos que hay que mantener el click para fijar la letra. Por " +
                 "debajo de ~0,4 un toque lento se lee como confirmación.")]
        [SerializeField, Range(0.2f, 1.5f)] private float holdSeconds = 0.6f;

        [Tooltip("Segundos SIN tocar nada después de los cuales las iniciales se " +
                 "guardan como estén. 0 = esperar para siempre. Es para el gabinete: " +
                 "si alguien se va a mitad de la carga, el juego no puede quedar " +
                 "clavado en esta pantalla.")]
        [SerializeField, Min(0f)] private float idleTimeoutSeconds = 20f;

        [Header("Tiempos")]
        [Tooltip("Segundos desde que cae la noche hasta que aparece la tabla. Tiene " +
                 "que dejar leer el titular (entra a 1,2 s) y la segunda línea (1,7 s).")]
        [SerializeField, Min(0f)] private float showAfterSeconds = 2.5f;

        [Tooltip("Segundos, después de guardar, en los que el click no reinicia. Sin " +
                 "esto, un click apurado justo después de fijar la última letra se " +
                 "come la tabla sin que nadie la vea.")]
        [SerializeField, Min(0f)] private float restartLockoutSeconds = 1.5f;

        [Header("Archivo")]
        [Tooltip("Nombre del JSON dentro de Application.persistentDataPath. En Windows " +
                 "es %USERPROFILE%\\AppData\\LocalLow\\<compañía>\\<producto>. El " +
                 "menú Tools/Buenos Días/Highscore abre la carpeta.")]
        [SerializeField] private string fileName = DefaultFileName;

        /// <summary>Puestos de la tabla.</summary>
        public int Capacity => capacity;

        /// <summary>Puntaje mínimo para entrar.</summary>
        public int MinimumScore => minimumScore;

        /// <summary>Cuántas letras llevan las iniciales.</summary>
        public int InitialsLength => initialsLength;

        /// <summary>Letras por las que cicla el toque.</summary>
        public string Alphabet => string.IsNullOrEmpty(alphabet) ? InitialsEntry.DefaultAlphabet : alphabet;

        /// <summary>Segundos de mantenido para fijar una letra.</summary>
        public float HoldSeconds => holdSeconds;

        /// <summary>Segundos quieto para dar la carga por terminada. 0 = nunca.</summary>
        public float IdleTimeoutSeconds => idleTimeoutSeconds;

        /// <summary>Segundos desde que cae la noche hasta la tabla.</summary>
        public float ShowAfterSeconds => showAfterSeconds;

        /// <summary>Segundos después de guardar en los que no se reinicia.</summary>
        public float RestartLockoutSeconds => restartLockoutSeconds;

        /// <summary>Nombre del JSON.</summary>
        public string FileName => string.IsNullOrWhiteSpace(fileName) ? DefaultFileName : fileName;

        /// <summary>Si esa religión juega por récord.</summary>
        public bool AppliesTo(ReligionDefinition religion)
        {
            return !onlyWithoutAscension || (religion != null && !religion.CanAscend);
        }

        /// <summary>
        /// Bajo qué clave se guarda la tabla de una religión: el nombre del asset.
        /// Renombrar el asset arranca una tabla nueva.
        /// </summary>
        public static string KeyFor(ReligionDefinition religion)
        {
            return religion != null ? religion.name : "sin-religion";
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(alphabet)) alphabet = InitialsEntry.DefaultAlphabet;
            if (string.IsNullOrWhiteSpace(fileName)) fileName = DefaultFileName;
        }
    }
}
