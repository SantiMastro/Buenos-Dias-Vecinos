using System.Collections.Generic;
using UnityEngine;

namespace BuenosDias.Config
{
    /// <summary>
    /// Cómo se reproduce un clip generado.
    /// Se llama ClipPlayMode y no AnimationPlayMode porque ese nombre ya existe
    /// en UnityEngine y la ambigüedad rompe la compilación.
    /// </summary>
    public enum ClipPlayMode
    {
        /// <summary>Cicla para siempre. Caminata, espera, ondas de timbre.</summary>
        Loop,

        /// <summary>Corre una vez y se queda en el último frame. Timbre, éxito, sí, no.</summary>
        Once,

        /// <summary>Un solo frame, sin movimiento. Poses neutras.</summary>
        Static
    }

    /// <summary>
    /// Diccionario de fps y modo de reproducción por prefijo de sprite.
    ///
    /// Existe para que cambiar la velocidad de una animación sea editar un asset
    /// y no abrir un <c>.cs</c>. La cantidad de frames NO se configura acá: la
    /// detecta el escáner mirando los archivos, porque los ciclos reales tienen
    /// largos distintos (walk 6, espera 4, timbre 3, vecinos 2).
    /// </summary>
    [CreateAssetMenu(
        fileName = "AnimationBuildConfig",
        menuName = "Buenos Días/Configuración de animaciones",
        order = 0)]
    public sealed class AnimationBuildConfig : ScriptableObject
    {
        /// <summary>Una regla del diccionario.</summary>
        [System.Serializable]
        public struct Rule
        {
            [Tooltip("Texto que tiene que aparecer en el nombre del grupo de sprites.\n" +
                     "Ej: '_walk' matchea chr_predicador_walk y chr_seguidor_01_walk.")]
            public string match;

            [Tooltip("Cuadros por segundo del clip generado.")]
            [Range(1, 60)] public int framesPerSecond;

            [Tooltip("Loop cicla, Once corre una vez y frena, Static es un frame quieto.")]
            public ClipPlayMode playMode;
        }

        [Header("Reglas")]
        [Tooltip("Se evalúan EN ORDEN y gana la primera que matchea. Por eso " +
                 "'ondas_timbre' tiene que ir antes que '_timbre': si no, las ondas " +
                 "heredarían el modo Once del timbre del predicador.\n" +
                 "Arrastrá para reordenar prioridades.")]
        [SerializeField]
        private List<Rule> rules = new List<Rule>
        {
            new Rule { match = "_walk",        framesPerSecond = 12, playMode = ClipPlayMode.Loop },
            new Rule { match = "_espera",      framesPerSecond = 8,  playMode = ClipPlayMode.Loop },
            new Rule { match = "_idle",        framesPerSecond = 1,  playMode = ClipPlayMode.Static },
            new Rule { match = "ondas_timbre", framesPerSecond = 12, playMode = ClipPlayMode.Loop },
            new Rule { match = "_timbre",      framesPerSecond = 12, playMode = ClipPlayMode.Once },
            new Rule { match = "_exito",       framesPerSecond = 12, playMode = ClipPlayMode.Once },
            new Rule { match = "_si",          framesPerSecond = 8,  playMode = ClipPlayMode.Once },
            new Rule { match = "_no",          framesPerSecond = 8,  playMode = ClipPlayMode.Once },
        };

        /// <summary>Reglas configuradas, en orden de prioridad.</summary>
        public IReadOnlyList<Rule> Rules => rules;

        /// <summary>
        /// Resuelve fps y modo para un grupo de sprites.
        ///
        /// Devuelve <c>false</c> si ninguna regla matchea, y en ese caso el grupo
        /// NO se anima. Esta lista es la whitelist, no un default: la carpeta de
        /// FX está llena de sprites de un solo frame que son decoración
        /// (halos de farol, sombra, luz de ventana) y generarles un clip sería
        /// basura. Si querés animar algo nuevo, agregale una regla.
        /// </summary>
        public bool TryResolve(string groupName, out Rule rule)
        {
            foreach (Rule candidate in rules)
            {
                if (string.IsNullOrEmpty(candidate.match)) continue;
                if (!groupName.Contains(candidate.match)) continue;

                rule = candidate;
                return true;
            }

            rule = default;
            return false;
        }
    }
}
