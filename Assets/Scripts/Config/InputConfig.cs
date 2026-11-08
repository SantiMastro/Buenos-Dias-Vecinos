using BuenosDias.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BuenosDias.Config
{
    /// <summary>
    /// Todo lo que se ajusta del input: qué tecla es cada botón y los tiempos que
    /// deciden si un apretón cuenta.
    ///
    /// Se juega con UN solo esquema, el del control alternativo: el timbre (click
    /// izquierdo) y el felpudo (Espacio, o S y B, según lo que tenga cargado la
    /// Raspberry Pi: ver <see cref="TeclasFelpudo"/>). Hubo otros
    /// —un botón único, el control por objeto— y se sacaron: nunca iban a
    /// usarse, y cada uno era una bandera más para acordarse de poner bien.
    ///
    /// Vive en un asset y no en el componente de la escena porque los tiempos se
    /// ajustan MIDIENDO —con la ventana de diagnóstico y el control enchufado— y
    /// un asset se puede tocar en Play sin que el ajuste se pierda al salir.
    /// </summary>
    [CreateAssetMenu(fileName = "InputConfig", menuName = "Buenos Días/Config/Input", order = 20)]
    public sealed class InputConfig : ScriptableObject
    {
        [Header("Botones")]
        [Tooltip("BOTÓN A: el timbre. Toca, insiste y le pega al QTE según el " +
                 "momento. Por defecto el click izquierdo, que es el timbre físico " +
                 "ya soldado. NUNCA frena.")]
        [SerializeField]
        private PhysicalBinding botonA = new PhysicalBinding(BindingDevice.MouseIzquierdo, Key.Space);

        [Tooltip("BOTÓN B: el felpudo. Frena y vuelve a caminar. Por defecto Espacio, " +
                 "que es lo que manda la plancha.")]
        [SerializeField]
        private PhysicalBinding botonB = new PhysicalBinding(BindingDevice.Teclado, Key.Space);

        [Tooltip("Cómo se lee el felpudo con 'Espacio' (con 'S y B' no se usa).\n\n" +
                 "APAGADO (toggle, el que va): cada toque sube o baja del felpudo. Es " +
                 "lo que corresponde a la plancha de la Raspberry Pi, que manda un " +
                 "toque de ~50 ms al pisar y otro al bajarse, y nunca sostiene la " +
                 "tecla.\n\n" +
                 "PRENDIDO (sostenido): apretado es estar arriba, suelto es caminar. " +
                 "Solo sirve si la Pi pasa a mandar la tecla MIENTRAS está pisada; " +
                 "con la Pi de hoy dejaría al predicador caminando siempre.")]
        [SerializeField] private bool felpudoSostenido;

        [Header("Felpudo")]
        [Tooltip("Qué teclas manda la placa del felpudo. TIENE QUE COINCIDIR con el " +
                 "firmware cargado en la Raspberry Pi: si no, el felpudo no hace nada.\n\n" +
                 "ESPACIO (firmware v16): un toque de Espacio al subirse y otro al " +
                 "bajarse, y cada toque alterna. Usa el Botón B y el sostenido. Si se " +
                 "pierde o sobra un toque, el felpudo queda invertido hasta corregirlo " +
                 "a mano.\n\n" +
                 "S Y B (firmware v17): una tecla al subirse y otra al bajarse. Cada " +
                 "tecla dice el estado, así que una perdida o repetida se corrige sola " +
                 "con la siguiente. Usa las dos teclas de abajo; el Botón B y el " +
                 "sostenido no se usan.\n\n" +
                 "S LATIDO (firmware v19): la placa REPITE S mientras hay alguien " +
                 "arriba y manda una B al bajarse. El juego solo frena mientras " +
                 "sigan llegando S y, sin teclas, camina: una tecla perdida o de más " +
                 "se corrige sola en 'Latido caduca'. Usa las dos teclas de abajo.")]
        [SerializeField] private TeclasFelpudo teclasFelpudo = TeclasFelpudo.Espacio;

        [Tooltip("Con 'S y B': la tecla que manda la placa cuando alguien SE SUBE " +
                 "(TECLA_SUBIO en el code.py de la placa).")]
        [SerializeField]
        private PhysicalBinding felpudoSube = new PhysicalBinding(BindingDevice.Teclado, Key.S);

        [Tooltip("Con 'S y B': la tecla que manda la placa cuando alguien SE BAJA " +
                 "(TECLA_BAJO en el code.py de la placa).")]
        [SerializeField]
        private PhysicalBinding felpudoBaja = new PhysicalBinding(BindingDevice.Teclado, Key.B);

        [Tooltip("Con 'S Latido': segundos sin recibir una S para dar el felpudo por " +
                 "bajado y volver a caminar. La placa manda una S cada ~150 ms, así que " +
                 "0.4 aguanta dos latidos perdidos. Más largo = el predicador tarda más " +
                 "en caminar si la placa se cuelga; más corto = un tropiezo del USB lo " +
                 "suelta de la puerta.")]
        [SerializeField, Range(0.15f, 1f)] private float latidoCaducaSeconds = 0.4f;

        [Tooltip("Seguro contra el felpudo TRABADO (la placa cree que hay alguien y no lo " +
                 "hay). Segundos que el predicador puede estar parado en el felpudo sin " +
                 "hacer nada —sin tocar el timbre, o con la puerta ya resuelta y sin " +
                 "bajarse— antes de darlo por trabado: camina, y el juego ignora la " +
                 "plancha hasta que la placa diga 'abajo'. Solo con 'S y B' o 'S Latido'. " +
                 "0 lo apaga.")]
        [SerializeField, Range(0f, 30f)] private float felpudoOciosoSeconds = 8f;

        [Header("Tiempos")]
        [Tooltip("Segundos después del felpudo durante los que el timbre NO dispara.\n\n" +
                 "Es la traba contra frenar y tocar con un solo apretón. En 0 solo se " +
                 "descarta el timbre del mismo cuadro.\n\n" +
                 "0.05 sale de medir el control: con Espacio y mouse separados no hay " +
                 "contacto que arrastre al otro (cero rebotes en 30 pisadas), así que " +
                 "una traba más larga solo se comía clicks hechos a propósito justo " +
                 "después de pisar.")]
        [SerializeField, Range(0f, 0.3f)] private float coincidenceSeconds = 0.05f;

        [Tooltip("Segundos que se GUARDA un timbrazo que llegó un poco antes de poder " +
                 "usarse: justo antes de que llegue la señal del felpudo, mientras " +
                 "camina hasta la puerta después de frenar, o justo antes de que " +
                 "termine la espera (ahí se lee como insistir). 0 lo apaga.\n\n" +
                 "0.25 sale de medir la plancha con la Raspberry Pi: revisa el sensor " +
                 "cada ~189 ms, así que la señal del felpudo llega hasta ~190 ms " +
                 "después de pisar, mientras que el click del mouse llega al instante. " +
                 "En 'pisar + click' el click llega PRIMERO; la ventana cubre una vuelta " +
                 "de la Pi más un margen.")]
        [SerializeField, Range(0f, 0.5f)] private float ringBufferSeconds = 0.25f;

        /// <summary>El timbre.</summary>
        public PhysicalBinding BotonA => botonA;

        /// <summary>El felpudo.</summary>
        public PhysicalBinding BotonB => botonB;

        /// <summary>Si el felpudo se sostiene en vez de alternar. Solo con <see cref="TeclasFelpudo.Espacio"/>.</summary>
        public bool FelpudoSostenido => felpudoSostenido;

        /// <summary>Qué teclas manda la placa del felpudo.</summary>
        public TeclasFelpudo TeclasFelpudo => teclasFelpudo;

        /// <summary>Con <see cref="TeclasFelpudo.SyB"/>: la tecla de subirse.</summary>
        public PhysicalBinding FelpudoSube => felpudoSube;

        /// <summary>Con <see cref="TeclasFelpudo.SyB"/>: la tecla de bajarse.</summary>
        public PhysicalBinding FelpudoBaja => felpudoBaja;

        /// <summary>Con <see cref="TeclasFelpudo.SLatido"/>: segundos sin S para volver a caminar.</summary>
        public float LatidoCaducaSeconds => latidoCaducaSeconds;

        /// <summary>Segundos parado en el felpudo sin hacer nada para darlo por trabado. 0 = apagado.</summary>
        public float FelpudoIdleSeconds => felpudoOciosoSeconds;

        /// <summary>Segundos después del felpudo durante los que el timbre no dispara.</summary>
        public float CoincidenceSeconds => coincidenceSeconds;

        /// <summary>Segundos que se guarda un timbrazo adelantado.</summary>
        public float RingBufferSeconds => ringBufferSeconds;
    }
}
