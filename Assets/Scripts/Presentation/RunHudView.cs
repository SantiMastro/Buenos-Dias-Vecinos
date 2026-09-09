using BuenosDias.Config;
using BuenosDias.Gameplay;
using BuenosDias.Simulation;
using UnityEngine;
using UnityEngine.Serialization;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// El HUD del día: cuántas almas se llevan, por cuánto multiplican las
    /// objeciones y cuántos segundos quedan.
    ///
    /// Van juntos en una clase porque comparten lo único que tiene enjundia acá,
    /// que es anclarse al borde de la cámara leyendo el <c>orthographicSize</c>
    /// cada cuadro (§3.8). Partirlos en tres vistas duplicaría ese cálculo, que es
    /// justo el que no hay que copiar.
    ///
    /// **El contador de ALMAS es el score y va siempre visible, arriba a la
    /// izquierda.** En su momento se decidió no ponerlo porque "la comitiva ya es
    /// el contador": la fila que camina atrás. Jugando se vio que no alcanza —la
    /// fila se sale del encuadre, se mezcla con las casas, y no se puede contar de
    /// un vistazo justo cuando hace falta, que es mientras se decide si el tiempo
    /// alcanza para una casa más—.
    ///
    /// Muestra los SEGUIDORES que quedan, no las conversiones acumuladas. Es el
    /// mismo número que resuelve los finales.
    ///
    /// **El multiplicador va al lado del contador y no en un cartel propio.**
    /// Hubo un cartel centrado arriba que decía COMITIVA 7 - ×2 OBJECIONES, y era
    /// tres datos donde hacía falta uno: ocupaba media pantalla, repetía el número
    /// de almas que ya está a la izquierda, y ponía en palabras algo que se mira de
    /// reojo. Lo único que agregaba de verdad —el tramo en el que estás, que cambia
    /// la dificultad sin anunciarse— cabe en un "×2" chiquito pegado al contador.
    ///
    /// **El número de segundos no es redundante con la barra.** La barra dice la
    /// FORMA —cuánto queda de la franja— y el número dice si alcanza para una casa
    /// más, que es la pregunta que el jugador se hace de verdad. Por eso va pegado
    /// a la punta de la barra: son el mismo dato mirado de dos maneras.
    ///
    /// Solo DIBUJA. Los números los llevan otros.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunHudView : MonoBehaviour
    {
        [Header("De quién escucha")]
        [Tooltip("Dueño del reloj del día.")]
        [SerializeField] private DayDirector director;

        [Tooltip("Quien lleva la comitiva y sabe cuántos eslabones toca.")]
        [SerializeField] private SkillcheckRunner skillcheck;

        [Tooltip("De acá sale la religión activa, que puede sumar eslabones.")]
        [SerializeField] private RunDirector runDirector;

        [Header("Referencias")]
        [Tooltip("Cámara desde la que se ancla. NO se usa una constante: la Pixel " +
                 "Perfect Camera cambia el tamaño ortográfico en runtime.")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Cartel de los segundos que quedan. Va pegado a la barra.")]
        [SerializeField] private TextLabel timeLabel;

        [Tooltip("Contador de almas. Es el score: va grande y siempre visible.")]
        [SerializeField] private TextLabel soulsLabel;

        [Tooltip("El '×2' chiquito al lado del contador: en qué tramo de comitiva " +
                 "está el jugador, o sea cuántas objeciones tiene cada puerta.")]
        [FormerlySerializedAs("partyLabel")]
        [SerializeField] private TextLabel linksLabel;

        [Tooltip("Ícono que va ANTES del número de almas. Trae su propio contorno " +
                 "de un píxel, que es lo que sostiene la legibilidad de esta " +
                 "esquina sin que el número tenga que pagar un contorno propio.")]
        [SerializeField] private SpriteRenderer soulsIcon;

        [Header("Colocación, en píxeles")]
        [Tooltip("Margen del contador de almas contra el borde IZQUIERDO.")]
        [SerializeField, Min(0f)] private float soulsMarginLeftPixels = 10f;

        [Tooltip("Distancia del borde de arriba a la LÍNEA BASE del contador. " +
                 "Con 19 la tinta del número ocupa de 4 a 19 y queda centrada contra " +
                 "el hueco de la barra, que va a 7 px del borde y mide 8 de alto.")]
        [SerializeField, Min(0f)] private float soulsMarginTopPixels = 19f;

        [Tooltip("Hueco entre el contador de almas y el multiplicador. Se mide desde " +
                 "el ANCHO real del contador, así que no hay que retocarlo al pasar " +
                 "de 9 a 10 almas.")]
        [SerializeField, Min(0f)] private float linksGapPixels = 4f;

        [Tooltip("Hueco entre el ícono y el número.")]
        [SerializeField, Min(0f)] private float soulsIconGapPixels = 4f;

        [Tooltip("Multiplicador de tamaño del ícono. ENTERO siempre.\n\n" +
                 "Está en 1 porque el ícono es un ESCUDO de 16×16 con la tinta " +
                 "llena de borde a borde, no un glifo chiquito: a escala 1 mide 16 " +
                 "px contra los 15 que mide un dígito a escala 3, o sea que ya pesa " +
                 "lo mismo. A escala 3 mediría 48 y sería tres veces el número.")]
        [SerializeField, Range(1, 4)] private int soulsIconScale = 1;

        [Tooltip("Distancia del borde de arriba a la línea base del número de " +
                 "segundos. Con 14 queda centrado contra la barra, igual que el " +
                 "contador de almas.")]
        [SerializeField, Min(0f)] private float timeMarginTopPixels = 14f;

        [Tooltip("Cuánto a la derecha del centro va el número de segundos, alineado " +
                 "por su IZQUIERDA. El marco mide 128 px, o sea que termina en 64, y " +
                 "la luna de la punta llega hasta 82: con 88 el número queda pegado a " +
                 "la luna sin pisarla.")]
        [SerializeField] private float timeOffsetXPixels = 88f;

        /// <summary>
        /// El sprite reescalado, si hizo falta. Se guarda para poder destruirlo:
        /// lo crea esta clase, así que es suyo.
        /// </summary>
        private Sprite scaledIcon;

        /// <summary>
        /// Grita si falta algo.
        ///
        /// El HUD estuvo sin dibujarse durante dos vueltas enteras y la consola no
        /// decía una palabra: sin referencias, <c>LateUpdate</c> hacía un
        /// <c>return</c> mudo y los <c>SetText</c> caían en un <c>null</c> que nadie
        /// miraba. Un sistema que se apaga solo tiene que decirlo.
        /// </summary>
        private void Awake()
        {
            Require(targetCamera, "targetCamera");
            Require(timeLabel, "timeLabel");
            Require(soulsLabel, "soulsLabel");
            Require(linksLabel, "linksLabel");
            Require(director, "director");
            Require(skillcheck, "skillcheck");

            ScaleIcon();

            if (targetCamera != null && !transform.IsChildOf(targetCamera.transform))
                Debug.LogError(
                    $"[RunHudView] '{name}' NO cuelga del transform de la cámara. " +
                    "Los carteles se colocan en coordenadas locales, así que van a " +
                    "quedarse clavados en el mundo mientras la cámara se aleja.", this);
        }

        private void OnDestroy()
        {
            if (scaledIcon != null) Destroy(scaledIcon);
        }

        /// <summary>
        /// Agranda el ícono bajándole los <c>pixelsPerUnit</c> al sprite, NUNCA
        /// tocando <c>localScale</c>. Es el mismo truco que usa
        /// <see cref="TextLabel"/> y por el mismo motivo: escalando el transform,
        /// cualquier redondeo de la cámara pixel-perfect corre medio píxel y el
        /// dibujo queda con los bordes rotos.
        /// </summary>
        private void ScaleIcon()
        {
            if (soulsIconScale == 1 || soulsIcon == null || soulsIcon.sprite == null) return;

            Sprite source = soulsIcon.sprite;
            scaledIcon = Sprite.Create(
                source.texture, source.rect, new Vector2(0.5f, 0.5f),
                ProjectConstants.PixelsPerUnit / soulsIconScale, 0, SpriteMeshType.FullRect);
            scaledIcon.name = $"{source.name}_x{soulsIconScale}";

            soulsIcon.sprite = scaledIcon;
        }

        private void Require(Object reference, string field)
        {
            if (reference != null) return;

            Debug.LogError(
                $"[RunHudView] '{name}': el campo '{field}' está sin asignar, así que " +
                "esa parte del HUD no se va a ver.", this);
        }

        private void OnEnable()
        {
            if (director != null) director.RemainingChanged += OnRemainingChanged;
            if (skillcheck != null) skillcheck.FollowersChanged += OnFollowersChanged;

            // Los carteles arrancan con el estado de AHORA y no vacíos: los dos
            // eventos avisan de cambios, así que sin esto el HUD quedaría en blanco
            // hasta que algo se moviera.
            OnRemainingChanged(director != null ? director.Remaining : 0f);
            OnFollowersChanged(skillcheck != null ? skillcheck.Followers : 0);
        }

        private void OnDisable()
        {
            if (director != null) director.RemainingChanged -= OnRemainingChanged;
            if (skillcheck != null) skillcheck.FollowersChanged -= OnFollowersChanged;
        }

        /// <summary>
        /// Los segundos van REDONDEADOS PARA ARRIBA. Con el redondeo normal, el
        /// cartel muestra 0 durante medio segundo en el que todavía se puede tocar
        /// un timbre, y eso se lee como que el juego siguió andando después de
        /// terminarse.
        /// </summary>
        private void OnRemainingChanged(float remaining)
        {
            if (timeLabel == null) return;

            timeLabel.SetText(Mathf.CeilToInt(Mathf.Max(0f, remaining)).ToString());
        }

        private void OnFollowersChanged(int followers)
        {
            if (soulsLabel != null) soulsLabel.SetText(followers.ToString());
            if (linksLabel == null) return;

            ReligionDefinition religion = runDirector != null ? runDirector.Religion : null;
            int links = skillcheck != null ? skillcheck.ChainLinksWith(religion) : 1;

            // El × es el signo de multiplicación de la fuente, no una equis.
            linksLabel.SetText("×" + links);
        }

        /// <summary>
        /// Coloca los tres carteles contra el borde de la cámara, cada cuadro.
        ///
        /// ⚠️ Se recalcula SIEMPRE en vez de hornear la posición: la Pixel Perfect
        /// Camera reescribe el tamaño ortográfico según la ventana (§3.8), así que
        /// una posición fija se despega del borde en cuanto cambia la resolución.
        ///
        /// Los dos ejes NO se anclan igual, y es a propósito. El alto sale de la
        /// cámara cruda porque el HUD tiene que quedar alineado con la barra de
        /// tiempo, que se ancla así. El ancho se recorta contra la caja de diseño
        /// porque si no, en una ventana más ancha que 384 el contador se va a
        /// buscar el borde físico y aparece despegado de todo lo demás.
        /// </summary>
        private void LateUpdate()
        {
            if (targetCamera == null) return;

            // En la selección de religión se amontonan encima del nombre y del
            // trueque, y en el final serían ruido sobre la cinemática.
            bool playing = runDirector == null || runDirector.Phase == RunPhase.Jugando;
            Show(timeLabel, playing);
            Show(soulsLabel, playing);
            Show(linksLabel, playing);
            if (soulsIcon != null) soulsIcon.enabled = playing;
            if (!playing) return;

            float top = targetCamera.orthographicSize;
            float left = -Mathf.Min(
                top * targetCamera.aspect,
                ProjectConstants.ToUnits(ProjectConstants.ReferenceWidth * 0.5f));

            Place(timeLabel, ProjectConstants.ToUnits(timeOffsetXPixels),
                  top - ProjectConstants.ToUnits(timeMarginTopPixels));

            // El contador se ancla por su IZQUIERDA, no por el centro: es el único
            // pegado a una esquina, y centrarlo lo movería al pasar de 9 a 10.
            float soulsX = left + ProjectConstants.ToUnits(soulsMarginLeftPixels);
            float baseline = top - ProjectConstants.ToUnits(soulsMarginTopPixels);

            soulsX = PlaceIcon(soulsX, baseline);
            Place(soulsLabel, soulsX, baseline);

            // El multiplicador va en la MISMA línea base que el contador aunque sea
            // tres veces más chico: los dos glifos apoyan en el borde de abajo de su
            // celda, así que compartir la Y ya los deja alineados como un renglón.
            float soulsWidth = soulsLabel != null ? soulsLabel.WidthPixels : 0;
            Place(linksLabel,
                  soulsX + ProjectConstants.ToUnits(soulsWidth + linksGapPixels),
                  baseline);
        }

        /// <summary>
        /// Pone el ícono a la izquierda del número y devuelve dónde sigue el
        /// renglón. Si no hay ícono, el renglón arranca donde arrancaba.
        ///
        /// La medida sale del <c>bounds</c> del sprite y no de un campo: el ícono
        /// puede estar reescalado, y dos números que tienen que coincidir y se
        /// escriben en dos lugares terminan no coincidiendo.
        ///
        /// Apoya en la MISMA línea base que el número, no en su centro: el dígito
        /// ocupa cinco de las ocho filas de su celda, así que centrar contra la
        /// celda lo dejaría flotando por encima del renglón.
        /// </summary>
        private float PlaceIcon(float x, float baseline)
        {
            if (soulsIcon == null || soulsIcon.sprite == null) return x;

            Vector3 half = soulsIcon.sprite.bounds.extents;
            Vector3 local = soulsIcon.transform.localPosition;
            soulsIcon.transform.localPosition = new Vector3(
                x + half.x, baseline + half.y, local.z);

            return x + half.x * 2f + ProjectConstants.ToUnits(soulsIconGapPixels);
        }

        /// <summary>
        /// Apaga el objeto entero y no cada renderer: el <see cref="TextLabel"/>
        /// usa un pool de renderers cuya cantidad cambia con el texto, así que
        /// apagarlos de a uno obligaría a esta clase a saber cómo dibuja la otra.
        /// </summary>
        private static void Show(TextLabel label, bool visible)
        {
            if (label != null && label.gameObject.activeSelf != visible)
                label.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Coloca un cartel en coordenadas LOCALES a la cámara.
        ///
        /// ⚠️ Tiene que ser <c>localPosition</c> y el objeto tiene que colgar del
        /// transform de la cámara. Con posición de mundo, los carteles se quedaban
        /// clavados donde arrancó la partida mientras la cámara seguía al
        /// predicador: el cálculo estaba bien —la altura salía del
        /// <c>orthographicSize</c> de cada cuadro— pero se aplicaba en un sistema de
        /// coordenadas que se movía debajo. Es el mismo patrón que ya usaba la barra
        /// de tiempo, que por eso nunca tuvo el problema.
        ///
        /// ⚠️ Y la Z se deja como está. Estuvo en 0, que es el plano exacto de la
        /// cámara, y el near clip de 0,1 se comía el HUD entero: activo, con letras,
        /// en la capa de más arriba, y sin dibujarse. Vive en el prefab, en 5, igual
        /// que la barra de tiempo.
        /// </summary>
        private void Place(TextLabel label, float x, float y)
        {
            if (label == null) return;

            Vector3 local = label.transform.localPosition;
            label.transform.localPosition = new Vector3(x, y, local.z);
        }
    }
}
