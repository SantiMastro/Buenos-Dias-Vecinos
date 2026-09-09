using UnityEngine;

namespace BuenosDias.Config
{
    /// <summary>
    /// Una religión jugable: sus multiplicadores y su paleta.
    ///
    /// Es un asset justamente para cumplir el criterio 10: agregar una religión
    /// tiene que ser crear un asset y sumarlo a la lista de <see cref="GameConfig"/>,
    /// sin que ninguna clase enumere religiones.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Religion", menuName = "Buenos Días/Religión", order = 10)]
    public sealed class ReligionDefinition : ScriptableObject
    {
        [Header("Identidad")]
        [Tooltip("Nombre en pantalla, en mayúsculas. Ej: TESTIGOS.")]
        [SerializeField] private string displayName = "TESTIGOS";

        [Tooltip("Identificador estable para guardar partidas y estadísticas. " +
                 "No lo cambies una vez publicado.")]
        [SerializeField] private string id = "testigos";

        [Tooltip("Una línea corta que diga en qué te cambia la partida. Es lo único " +
                 "que el jugador lee antes de elegir, así que va el trueque y no el " +
                 "chiste. En mayúsculas y con lo que tenga la fuente de bitmap.")]
        [SerializeField] private string tagline = string.Empty;

        [Header("Multiplicadores")]
        [Tooltip("Velocidad de caminata. 1 = velocidad base. Los mormones van a 1.30.")]
        [SerializeField, Range(0.5f, 2f)] private float walkSpeed = 1f;

        [Tooltip("Duración de la espera en la puerta. Mayor = espera más larga. " +
                 "Los evangelistas esperan 1.40; las aspiradoras 0.82.")]
        [SerializeField, Range(0.5f, 2f)] private float waitDuration = 1f;

        [Tooltip("Ancho de la zona del skillcheck. Mayor = más fácil. " +
                 "Los mormones tienen 0.80, los evangelistas 1.35.")]
        [SerializeField, Range(0.5f, 2f)] private float zoneWidth = 1f;

        [Tooltip("Velocidad de la aguja. Menor = más fácil. Los budistas tienen 0.75.")]
        [SerializeField, Range(0.5f, 2f)] private float needleSpeed = 1f;

        [Tooltip("Eslabones EXTRA de objeción encadenada, sumados a los que da la " +
                 "comitiva. Los budistas suman 1.")]
        [SerializeField, Range(0, 3)] private int extraChainLinks;

        [Tooltip("Multiplicador del bono de tiempo por conversión. " +
                 "Las aspiradoras tienen 1.60.")]
        [SerializeField, Range(0.5f, 2f)] private float timeBonus = 1f;

        [Header("Final")]
        [Tooltip("Si está apagado, esta opción no puede llegar al final de ascensión " +
                 "por más conversiones que junte. Es el caso de las aspiradoras: no es " +
                 "una religión, y que no pueda ascender es parte del chiste.")]
        [SerializeField] private bool canAscend = true;

        [Header("Paleta")]
        [Tooltip("Color de la camisa. Reemplaza al #F3ECE0 del sprite base.")]
        [SerializeField] private Color shirtColor = new Color32(0xF3, 0xEC, 0xE0, 0xFF);

        [Tooltip("Color de la corbata. Reemplaza al #7A2F3D del sprite base.")]
        [SerializeField] private Color tieColor = new Color32(0x7A, 0x2F, 0x3D, 0xFF);

        /// <summary>Nombre en pantalla.</summary>
        public string DisplayName => displayName;

        /// <summary>Identificador estable.</summary>
        public string Id => id;

        /// <summary>Línea corta con el trueque de esta religión.</summary>
        public string Tagline => tagline;

        /// <summary>Multiplicador de velocidad de caminata.</summary>
        public float WalkSpeed => walkSpeed;

        /// <summary>Multiplicador de duración de la espera.</summary>
        public float WaitDuration => waitDuration;

        /// <summary>Multiplicador del ancho de zona del skillcheck.</summary>
        public float ZoneWidth => zoneWidth;

        /// <summary>Multiplicador de velocidad de la aguja.</summary>
        public float NeedleSpeed => needleSpeed;

        /// <summary>Eslabones extra de cadena que suma esta religión.</summary>
        public int ExtraChainLinks => extraChainLinks;

        /// <summary>Multiplicador del bono de tiempo.</summary>
        public float TimeBonus => timeBonus;

        /// <summary>Si puede alcanzar el final de ascensión.</summary>
        public bool CanAscend => canAscend;

        /// <summary>Color de camisa de esta religión.</summary>
        public Color ShirtColor => shirtColor;

        /// <summary>Color de corbata de esta religión.</summary>
        public Color TieColor => tieColor;
    }
}
