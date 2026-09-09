using System.Collections.Generic;
using UnityEngine;

namespace BuenosDias.Config
{
    /// <summary>Los tres finales posibles.</summary>
    public enum EndingKind
    {
        /// <summary>Cero conversiones. Silueta contra el atardecer.</summary>
        Crucifixion,

        /// <summary>Pocas conversiones. Pantalla de resultados normal.</summary>
        SeHizoDeNoche,

        /// <summary>Muchas conversiones. Bloqueado para quien no pueda ascender.</summary>
        Ascension
    }

    /// <summary>
    /// Qué final forzar para poder mirarlo, sin tocar los umbrales de verdad.
    ///
    /// Es un enum aparte de <see cref="EndingKind"/> y no un <c>EndingKind?</c>
    /// porque necesita un valor "apagado" que sea el PRIMERO: así el default de
    /// un campo serializado nuevo, que es 0, ya viene apagado, y un asset viejo
    /// que no tenga el campo tampoco se despierta forzando nada.
    /// </summary>
    public enum EndingOverride
    {
        /// <summary>Sin forzar: el final sale de la comitiva, como corresponde.</summary>
        Ninguno,

        /// <summary>Fuerza la crucifixión.</summary>
        Crucifixion,

        /// <summary>Fuerza el anochecer.</summary>
        SeHizoDeNoche,

        /// <summary>Fuerza la ascensión, aun con una religión que no puede ascender.</summary>
        Ascension
    }

    /// <summary>
    /// Asset raíz del balance. Desde acá se llega a todo lo demás.
    ///
    /// Es el que hay que seleccionar para cambiar la duración del día sin abrir
    /// un <c>.cs</c>, y el que sostiene los catálogos de religiones, señales y
    /// vecinos: ninguna clase del juego enumera esas cosas en código.
    /// </summary>
    [CreateAssetMenu(
        fileName = "GameConfig", menuName = "Buenos Días/Config/GAME CONFIG (raíz)", order = 0)]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Duración de la partida")]
        [Tooltip("Cuánto dura el día, en segundos. 72 es el valor tuneado.")]
        [SerializeField, Min(5f)] private float dayDurationSeconds = 72f;

        [Tooltip("Modo debug: el día no avanza. Sirve para probar la dificultad alta " +
                 "sin tener que ser bueno. NUNCA dejarlo activo en una build.")]
        [SerializeField] private bool infiniteTimeDebug;

        [Header("Semillas")]
        [Tooltip("Modo verificación: usa las semillas del Inspector tal cual, así " +
                 "todas las partidas salen IDÉNTICAS y se puede medir dos veces lo " +
                 "mismo. Apagado —el default— cada partida trae otra cuadra.")]
        [SerializeField] private bool fixedSeedDebug;

        [Header("Diagnóstico de finales")]
        [Tooltip("Fuerza un final para poder mirarlo, SIN tocar los umbrales. " +
                 "Ninguno es el default y es lo que corresponde jugando.\n\n" +
                 "Existe porque la alternativa era bajar 'ascensionAtLeast' a mano " +
                 "para ver la ascensión y acordarse de devolverlo. Eso es " +
                 "exactamente la bandera que hay que acordarse de apagar, o sea la " +
                 "que termina en la build, y encima con el peor castigo posible: " +
                 "una build donde CUALQUIERA asciende y nadie se entera hasta " +
                 "jugarla. Olvidarse de este, en cambio, muestra un final de más y " +
                 "deja el balance intacto.\n\n" +
                 "Avisa por consola cada vez que fuerza uno.")]
        [SerializeField] private EndingOverride forceEnding = EndingOverride.Ninguno;

        [Header("Progresión de dificultad")]
        [Tooltip("Conversiones a las que el skillcheck llega a su dificultad máxima. " +
                 "De ahí en más se estabiliza.")]
        [SerializeField, Min(1)] private int convertsForMaxDifficulty = 14;

        [Header("Finales")]
        [Tooltip("Con MENOS conversiones que esto, final de crucifixión. " +
                 "Con 1 significa que solo el cero lo dispara.")]
        [SerializeField, Min(0)] private int crucifixionBelow = 1;

        [Tooltip("Desde esta cantidad de conversiones, final de ascensión.")]
        [SerializeField, Min(1)] private int ascensionAtLeast = 10;

        [Header("Catálogos")]
        [Tooltip("Religiones jugables, en el orden en que las cicla el botón. " +
                 "Agregar una es crear el asset y sumarlo acá.")]
        [SerializeField] private List<ReligionDefinition> religions = new List<ReligionDefinition>();

        [Tooltip("Tipos de vecino que pueden abrir la puerta.")]
        [SerializeField] private List<NeighborDefinition> neighbors = new List<NeighborDefinition>();

        [Header("Configs de sistema")]
        [SerializeField] private WalkConfig walk;
        [SerializeField] private DoorbellConfig doorbell;
        [SerializeField] private WaitConfig wait;
        [SerializeField] private SkillcheckConfig skillcheck;
        [SerializeField] private PityConfig pity;
        [SerializeField] private HouseGenConfig houseGeneration;
        [SerializeField] private DayCycleConfig dayCycle;
        [SerializeField] private FollowerConfig followers;

        [Header("Bono de tiempo")]
        [Tooltip("Segundos base por conversión, por cada eslabón de la cadena.")]
        [SerializeField, Min(0f)] private float bonusBaseSeconds = 3f;

        [Tooltip("Segundos extra por acierto PERFECTO, por eslabón.")]
        [SerializeField, Min(0f)] private float bonusPerfectSeconds = 4f;

        [Tooltip("Segundos extra por acierto BUENO, por eslabón.")]
        [SerializeField, Min(0f)] private float bonusGoodSeconds = 1.5f;

        [Tooltip("Techo del bono POR ESLABÓN, antes del multiplicador de la religión. " +
                 "Con 7 y una cadena de 3, el techo de esa puerta es 21. TIENE QUE QUEDAR EN base + perfecto (hoy 3+4=7) O POR ENCIMA: por debajo satura las dos calidades y vuelve el bug de abajo.\n\n" +
                 "Era un techo absoluto de 16 s y estaba mal: con 4 eslabones, pegarle " +
                 "todo al naranja y todo al verde pagaban EXACTAMENTE lo mismo, porque " +
                 "los dos saturaban. El premio a la precisión desaparecía justo donde la " +
                 "cadena es más larga y el check más difícil.")]
        [SerializeField, Min(0f)] private float bonusCapPerLinkSeconds = 7f;

        /// <summary>Duración del día en segundos.</summary>
        public float DayDurationSeconds => dayDurationSeconds;

        /// <summary>Si el reloj está congelado por debug.</summary>
        public bool InfiniteTimeDebug => infiniteTimeDebug;

        /// <summary>Conversiones necesarias para la dificultad máxima.</summary>
        public int ConvertsForMaxDifficulty => convertsForMaxDifficulty;

        /// <summary>Religiones disponibles.</summary>
        public IReadOnlyList<ReligionDefinition> Religions => religions;

        /// <summary>Tipos de vecino disponibles.</summary>
        public IReadOnlyList<NeighborDefinition> Neighbors => neighbors;

        /// <summary>Config de caminata.</summary>
        public WalkConfig Walk => walk;

        /// <summary>Config del timbre.</summary>
        public DoorbellConfig Doorbell => doorbell;

        /// <summary>Config de la espera.</summary>
        public WaitConfig Wait => wait;

        /// <summary>Config del skillcheck.</summary>
        public SkillcheckConfig Skillcheck => skillcheck;

        /// <summary>Config del anti-racha.</summary>
        public PityConfig Pity => pity;

        /// <summary>Config de generación de casas.</summary>
        public HouseGenConfig HouseGeneration => houseGeneration;

        /// <summary>Config del ciclo de día.</summary>
        public DayCycleConfig DayCycle => dayCycle;

        /// <summary>Config de la comitiva.</summary>
        public FollowerConfig Followers => followers;

        /// <summary>
        /// La semilla que le toca a un sistema esta partida.
        ///
        /// Cada sistema trae la suya del Inspector y son distintas entre sí a
        /// propósito (§5b): así se puede repetir una cuadra con otras tiradas. Lo
        /// que decide acá es si se usan tal cual —modo verificación— o si se
        /// mezclan con el reloj para que cada partida sea otra.
        ///
        /// ⚠️ El default es MEZCLAR. Una bandera que hay que acordarse de apagar
        /// antes de la build termina en la build; una que hay que acordarse de
        /// prender para medir, en el peor caso arruina una medición.
        ///
        /// Se mezcla con XOR y no se reemplaza: si a todos les tocara el mismo
        /// número, los tres sistemas quedarían sincronizados entre sí, que es
        /// justamente lo que las semillas separadas evitan.
        /// </summary>
        public int SeedFor(int designerSeed)
        {
            if (fixedSeedDebug) return designerSeed;
            return designerSeed ^ System.Environment.TickCount;
        }

        /// <summary>Progreso de dificultad 0..1 para esa cantidad de conversiones.</summary>
        public float DifficultyProgress(int converts)
        {
            return Mathf.Clamp01(converts / (float)convertsForMaxDifficulty);
        }

        /// <summary>
        /// Final que corresponde. La ascensión se le niega a quien no puede
        /// ascender por más gente que junte, y en ese caso cae en el final
        /// intermedio.
        ///
        /// ⚠️ <paramref name="followers"/> es la COMITIVA que queda al caer la
        /// noche, no las conversiones acumuladas. Quien pierde seguidores en el
        /// camino tiene que caer al final que le corresponde por lo que le quedó.
        /// </summary>
        public EndingKind ResolveEnding(int followers, ReligionDefinition religion)
        {
            if (forceEnding != EndingOverride.Ninguno) return Forced();

            if (followers < crucifixionBelow) return EndingKind.Crucifixion;

            bool canAscend = religion == null || religion.CanAscend;
            if (followers >= ascensionAtLeast && canAscend) return EndingKind.Ascension;

            return EndingKind.SeHizoDeNoche;
        }

        /// <summary>
        /// El final forzado, y un aviso por consola.
        ///
        /// El aviso no es cortesía: esta bandera vive en un asset que se commitea,
        /// así que la única defensa contra dejarla puesta es que grite cada vez que
        /// hace algo. Salta al terminar el día, o sea una vez por partida.
        ///
        /// ⚠️ Se saltea también el bloqueo de ascensión de la religión, a propósito:
        /// para MIRAR la cinemática hace falta poder verla con cualquiera. Por eso
        /// no sirve para verificar los umbrales, que es justamente lo que este campo
        /// no toca.
        /// </summary>
        private EndingKind Forced()
        {
            // El −1 salta el "Ninguno", que ocupa el 0 para que el default venga apagado.
            var kind = (EndingKind)((int)forceEnding - 1);

            Debug.LogWarning(
                $"[GameConfig] '{name}': final FORZADO a {kind} por el campo " +
                "'forceEnding'. Los umbrales no se tocaron. Ponelo en Ninguno para " +
                "que el final vuelva a salir de la comitiva.", this);

            return kind;
        }

        /// <summary>
        /// Bono de tiempo de una conversión, ya topeado y multiplicado por la
        /// religión. <paramref name="perfectHits"/> y <paramref name="goodHits"/>
        /// cuentan aciertos por eslabón de la cadena.
        /// </summary>
        public float TimeBonusFor(int perfectHits, int goodHits, ReligionDefinition religion)
        {
            int links = Mathf.Max(1, perfectHits + goodHits);
            float raw = bonusBaseSeconds * links
                        + bonusPerfectSeconds * perfectHits
                        + bonusGoodSeconds * goodHits;

            // El techo escala con la cadena. Existe para que UNA puerta no devuelva
            // medio día, y esa razón crece con el largo: un techo absoluto trata
            // igual a una puerta fácil que a cuatro objeciones seguidas.
            float capped = Mathf.Min(raw, bonusCapPerLinkSeconds * links);
            float multiplier = religion != null ? religion.TimeBonus : 1f;
            return capped * multiplier;
        }
    }
}
