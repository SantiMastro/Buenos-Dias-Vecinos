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
    /// izquierdo) y el felpudo (Espacio, que manda la Raspberry Pi). Hubo otros
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

        [Tooltip("Cómo se lee el felpudo.\n\n" +
                 "APAGADO (toggle, el que va): cada toque sube o baja del felpudo. Es " +
                 "lo que corresponde a la plancha de la Raspberry Pi, que manda un " +
                 "toque de ~50 ms al pisar y otro al bajarse, y nunca sostiene la " +
                 "tecla.\n\n" +
                 "PRENDIDO (sostenido): apretado es estar arriba, suelto es caminar. " +
                 "Solo sirve si la Pi pasa a mandar la tecla MIENTRAS está pisada; " +
                 "con la Pi de hoy dejaría al predicador caminando siempre.")]
        [SerializeField] private bool felpudoSostenido;

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

        /// <summary>Si el felpudo se sostiene en vez de alternar.</summary>
        public bool FelpudoSostenido => felpudoSostenido;

        /// <summary>Segundos después del felpudo durante los que el timbre no dispara.</summary>
        public float CoincidenceSeconds => coincidenceSeconds;

        /// <summary>Segundos que se guarda un timbrazo adelantado.</summary>
        public float RingBufferSeconds => ringBufferSeconds;
    }
}
