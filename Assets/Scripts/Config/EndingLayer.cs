using System.Collections.Generic;
using UnityEngine;

namespace BuenosDias.Config
{
    /// <summary>
    /// Una pieza de una cinemática de final: qué sprite, dónde, cuándo aparece y
    /// si se anima.
    ///
    /// Las posiciones van en píxeles **desde el centro de la cámara** y no desde un
    /// borde: la Pixel Perfect Camera cambia el <c>orthographicSize</c> en runtime
    /// (§3.8), así que lo que se mide desde un borde se despega al cambiar la
    /// ventana. El centro no se mueve.
    /// </summary>
    [System.Serializable]
    public sealed class EndingLayer
    {
        [Tooltip("Nombre para encontrarla en la jerarquía. No lo lee nadie.")]
        [SerializeField] private string label = "capa";

        [Tooltip("Cuadros. Uno solo = quieta. Varios = animación.")]
        [SerializeField] private List<Sprite> frames = new List<Sprite>();

        [Tooltip("Cuadros por segundo si tiene más de uno.")]
        [SerializeField, Min(0.1f)] private float framesPerSecond = 8f;

        [Tooltip("Si la animación vuelve a empezar. Apagado se queda en el último " +
                 "cuadro, que es lo que quiere una nube que se abre y no se cierra.")]
        [SerializeField] private bool loop = true;

        [Tooltip("Cuadro por el que arranca. Sirve para desfasar tres cuervos que " +
                 "usan la misma animación y no tienen que aletear al unísono.")]
        [SerializeField, Min(0)] private int startFrame;

        [Tooltip("Posición en píxeles desde el CENTRO de la cámara.")]
        [SerializeField] private Vector2Int positionPixels;

        [Tooltip("Píxeles por segundo que se corre desde donde apareció. En 0 se " +
                 "queda quieta, que es lo que quieren la cruz o la loma. Sirve " +
                 "para que una figura ASCIENDA y para que un folleto se vaya " +
                 "volando sin que ninguna de las dos cosas sea código.")]
        [SerializeField] private Vector2 driftPixelsPerSecond;

        [Tooltip("Grados por segundo. ⚠ Solo para piezas RADIALES y con pivot al " +
                 "centro, como los rayos: rotar pixel art rompe la grilla y en un " +
                 "dibujo con líneas rectas se ve enseguida. En un abanico de rayos " +
                 "que gira no hay grilla que romper, y es el único caso.")]
        [SerializeField] private float degreesPerSecond;

        [Tooltip("Orden de dibujo dentro de la cinemática. Mayor = más adelante.")]
        [SerializeField] private int sortingOrder;

        [Tooltip("Segundos desde que arranca el final hasta que esta pieza aparece.")]
        [SerializeField, Min(0f)] private float appearAtSeconds;

        [Tooltip("Tinte. Blanco deja el sprite como está.")]
        [SerializeField] private Color tint = Color.white;

        /// <summary>Nombre en la jerarquía.</summary>
        public string Label => label;

        /// <summary>Cuadros de la pieza.</summary>
        public IReadOnlyList<Sprite> Frames => frames;

        /// <summary>Cuadros por segundo.</summary>
        public float FramesPerSecond => framesPerSecond;

        /// <summary>Si la animación cicla.</summary>
        public bool Loop => loop;

        /// <summary>Cuadro inicial.</summary>
        public int StartFrame => startFrame;

        /// <summary>Posición en píxeles desde el centro de la cámara.</summary>
        public Vector2Int PositionPixels => positionPixels;

        /// <summary>Cuánto se corre por segundo, en píxeles.</summary>
        public Vector2 DriftPixelsPerSecond => driftPixelsPerSecond;

        /// <summary>Cuánto gira por segundo, en grados.</summary>
        public float DegreesPerSecond => degreesPerSecond;

        /// <summary>Orden de dibujo.</summary>
        public int SortingOrder => sortingOrder;

        /// <summary>Segundos hasta que aparece.</summary>
        public float AppearAtSeconds => appearAtSeconds;

        /// <summary>Tinte del sprite.</summary>
        public Color Tint => tint;

        /// <summary>
        /// Qué cuadro toca a esa altura del final. Devuelve <c>null</c> si la pieza
        /// no tiene ninguno cargado, para que un asset a medio llenar no reviente la
        /// cinemática entera.
        /// </summary>
        public Sprite FrameAt(float seconds)
        {
            if (frames.Count == 0) return null;
            if (frames.Count == 1) return frames[0];

            int step = startFrame + Mathf.FloorToInt(seconds * framesPerSecond);
            if (loop) return frames[((step % frames.Count) + frames.Count) % frames.Count];

            return frames[Mathf.Clamp(step, 0, frames.Count - 1)];
        }
    }
}
