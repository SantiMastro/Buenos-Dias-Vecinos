using UnityEngine;

namespace BuenosDias.Config
{
    /// <summary>Cómo se castiga a la comitiva cuando el jugador falla.</summary>
    public enum FollowerPenaltyMode
    {
        /// <summary>Se va uno solo. Casi no alivia la dificultad.</summary>
        QuitarUno,

        /// <summary>
        /// Se van los que hagan falta para caer al tope del tramo inferior:
        /// F=10 → 7, F=6 → 3. Es dramático a propósito, para que se sienta que
        /// perdiste algo.
        /// </summary>
        BajarDeTramo
    }

    /// <summary>
    /// La fila de seguidores: cómo escala la dificultad y cómo se castiga fallar.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FollowerConfig", menuName = "Buenos Días/Config/Comitiva", order = 25)]
    public sealed class FollowerConfig : ScriptableObject
    {
        [Header("Formación")]
        [Tooltip("Separación entre seguidores, en píxeles.")]
        [SerializeField, Min(1f)] private float spacingPixels = 22f;

        [Tooltip("Segundos de retardo por posición en la fila. Hace que la comitiva " +
                 "se mueva como una víbora en vez de como un bloque.")]
        [SerializeField, Range(0f, 0.5f)] private float delayPerPosition = 0.06f;

        [Header("Escala de dificultad")]
        [Tooltip("Si está activo, velocidad y ancho escalan de forma CONTINUA con " +
                 "las fórmulas de abajo (recomendado). Si lo apagás, escalan por " +
                 "tramos de 4 seguidores.\n" +
                 "La tabla de la spec era una muestra en los bordes de cada tramo; " +
                 "las fórmulas son la fuente de verdad.")]
        [SerializeField] private bool continuousScaling = true;

        [Tooltip("Cuánto sube la velocidad de aguja por seguidor. 0.035 en la spec.")]
        [SerializeField, Range(0f, 0.2f)] private float speedPerFollower = 0.035f;

        [Tooltip("Techo del multiplicador de velocidad.")]
        [SerializeField, Range(1f, 3f)] private float speedMax = 1.75f;

        [Tooltip("Cuánto baja el ancho de zona por seguidor. 0.030 en la spec.")]
        [SerializeField, Range(0f, 0.2f)] private float widthPerFollower = 0.030f;

        [Tooltip("Piso del multiplicador de ancho.")]
        [SerializeField, Range(0.1f, 1f)] private float widthMin = 0.45f;

        [Header("Cadena")]
        [Tooltip("Cuántos seguidores entran en cada tramo de objeciones. Con 4: " +
                 "0-3 → 1 eslabón, 4-7 → 2, 8-11 → 3, 12+ → 4.\n" +
                 "Los eslabones SIEMPRE van por tramo: son un entero, no pueden ser " +
                 "continuos.")]
        [SerializeField, Min(1)] private int followersPerChainLink = 4;

        [Tooltip("Techo de eslabones encadenados, antes del extra de la religión.")]
        [SerializeField, Min(1)] private int maximumChainLinks = 4;

        [Header("Castigo por fallar")]
        [Tooltip("A partir de cuántos seguidores fallar hace que alguno abandone.")]
        [SerializeField, Min(0)] private int abandonThreshold = 5;

        [Tooltip("Cómo se van. BajarDeTramo es el default: quitar uno solo casi no " +
                 "alivia la dificultad y la válvula de escape queda decorativa.")]
        [SerializeField] private FollowerPenaltyMode penaltyMode = FollowerPenaltyMode.BajarDeTramo;

        [Header("Castigo de tiempo")]
        [Tooltip("Segundos que se pierden al fallar.")]
        [SerializeField, Min(0f)] private float failTimePenalty = 3f;

        [Tooltip("Segundos que se pierden al fallar con muchos seguidores.")]
        [SerializeField, Min(0f)] private float failTimePenaltyCrowded = 4f;

        [Tooltip("A partir de cuántos seguidores se aplica el castigo agravado.")]
        [SerializeField, Min(0)] private int crowdedThreshold = 6;

        /// <summary>Separación entre seguidores, en unidades.</summary>
        public float Spacing => ProjectConstants.ToUnits(spacingPixels);

        /// <summary>Retardo por posición en la fila.</summary>
        public float DelayPerPosition => delayPerPosition;

        /// <summary>Modo de castigo configurado.</summary>
        public FollowerPenaltyMode PenaltyMode => penaltyMode;

        /// <summary>Multiplicador de velocidad de aguja para esa cantidad de seguidores.</summary>
        public float SpeedMultiplier(int followers)
        {
            int effective = continuousScaling ? followers : BracketFloor(followers);
            return Mathf.Clamp(1f + effective * speedPerFollower, 1f, speedMax);
        }

        /// <summary>Multiplicador de ancho de zona para esa cantidad de seguidores.</summary>
        public float WidthMultiplier(int followers)
        {
            int effective = continuousScaling ? followers : BracketFloor(followers);
            return Mathf.Clamp(1f - effective * widthPerFollower, widthMin, 1f);
        }

        /// <summary>Eslabones de objeción para esa cantidad de seguidores. Siempre por tramo.</summary>
        public int ChainLinks(int followers)
        {
            int links = 1 + followers / followersPerChainLink;
            return Mathf.Min(links, maximumChainLinks);
        }

        /// <summary>Segundos que se pierden al fallar con esa comitiva.</summary>
        public float TimePenalty(int followers)
        {
            return followers >= crowdedThreshold ? failTimePenaltyCrowded : failTimePenalty;
        }

        /// <summary>
        /// Cuántos seguidores quedan después de fallar. Devuelve el mismo número
        /// si la comitiva es demasiado chica como para que alguien abandone.
        /// </summary>
        public int ApplyPenalty(int followers)
        {
            if (followers < abandonThreshold) return followers;

            if (penaltyMode == FollowerPenaltyMode.QuitarUno)
                return Mathf.Max(0, followers - 1);

            // Caer al tope del tramo de abajo: con 10 y tramos de 4, el tramo
            // inferior termina en 7.
            int bracket = followers / followersPerChainLink;
            if (bracket <= 0) return Mathf.Max(0, followers - 1);
            return bracket * followersPerChainLink - 1;
        }

        /// <summary>Borde inferior del tramo al que pertenece esa cantidad.</summary>
        private int BracketFloor(int followers)
        {
            return followers / followersPerChainLink * followersPerChainLink;
        }
    }
}
