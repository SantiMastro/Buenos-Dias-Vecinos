using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// El velo de atrás del skillcheck: una mancha oscura radial, densa en el
    /// centro y desvaneciéndose hacia afuera.
    ///
    /// Está para separar la lectura del mundo. Cuando el aro aparece, abajo hay
    /// una fachada con ventanas, un tendal y un skyline, y todo eso compite con lo
    /// único que hay que mirar durante 1.4 segundos.
    ///
    /// Es un disco DESVANECIDO y no uno opaco a propósito: un disco de borde duro
    /// se lee como una moneda pegada sobre la escena, y encima recorta el mundo con
    /// una circunferencia que no es parte de él. El desvanecido hace que la escena
    /// se apague, que es otra cosa.
    ///
    /// Se pinta UNA vez, en Awake: no cambia con la tirada.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SkillcheckVeil : MonoBehaviour
    {
        [Header("Tamaño")]
        [Tooltip("Lado de la textura del velo, en píxeles. Tiene que ser MAYOR que " +
                 "el del aro (128): el desvanecido necesita lugar por fuera del aro " +
                 "para llegar a cero, y si no entra se corta contra el borde.")]
        [SerializeField, Min(8)] private int textureSize = 192;

        [Tooltip("Radio hasta el que el velo va parejo, en píxeles. Por debajo del " +
                 "círculo interno del aro (45) para que la parte densa quede adentro " +
                 "del cuadrante y no dibuje un borde propio sobre el trazo.")]
        [SerializeField, Min(0f)] private float fullRadius = 34f;

        [Tooltip("Radio donde el velo llega a cero. No puede pasar la mitad del " +
                 "lado de la textura o el desvanecido queda cortado.")]
        [SerializeField, Min(1f)] private float fadeRadius = 92f;

        [Header("Densidad")]
        [Tooltip("Opacidad máxima, en el centro. Es la perilla para tunear cuánto " +
                 "se apaga el mundo detrás del skillcheck.\n\n" +
                 "0.75 no es un gusto: es donde la aguja cruza el 3:1 de contraste " +
                 "contra el peor fondo posible. Con 0.55 se queda en 2.05:1. Ver la " +
                 "tabla medida en §11.7 del plan antes de bajarlo.")]
        [SerializeField, Range(0f, 1f)] private float opacity = 0.75f;

        [Tooltip("Color del velo. #1D1638, el tono más oscuro del proyecto: la " +
                 "mezcla apunta a un color que ya existe en la paleta.")]
        [SerializeField] private Color veilColor = new Color32(0x1D, 0x16, 0x38, 0xFF);

        [Tooltip("Escalones del desvanecido. Pocos lo dejan como pixel art; muchos " +
                 "lo vuelven un degradado continuo.")]
        [SerializeField, Range(1, 64)] private int steps = 8;

        private ArcPainter painter;

        private void Awake()
        {
            if (fadeRadius * 2f > textureSize)
            {
                Debug.LogWarning(
                    $"[SkillcheckVeil] '{name}': el desvanecido llega a {fadeRadius} px " +
                    $"pero la textura mide {textureSize}. Se va a ver cortado en las " +
                    "cuatro esquinas.", this);
            }

            painter = new ArcPainter(textureSize, ProjectConstants.PixelsPerUnit);
            painter.PaintRadialVeil(
                fullRadius, fadeRadius, (Color32)veilColor, opacity, steps);
            painter.Apply();

            GetComponent<SpriteRenderer>().sprite = painter.Painted;
        }

        private void OnDestroy()
        {
            painter?.Dispose();
        }
    }
}
