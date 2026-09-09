using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// El contorno claro del aro: dos anillos de 1 px pegados por fuera de cada
    /// trazo oscuro de <c>ui_skillcheck_aro</c>.
    ///
    /// Es la GARANTÍA de legibilidad, y por eso el color es fijo y claro. Un trazo
    /// bicolor se lee contra cualquier fondo: es lo mismo que hace el outline de
    /// los sprites, y por eso los personajes no desaparecen contra una pared.
    ///
    /// La alternativa era teñir el aro según la hora del día, y eso obliga a
    /// mantener sincronizados dos gradientes que viven en assets distintos: el
    /// cielo de <c>DayCycleConfig</c> termina en <c>#1D1638</c>, que es EXACTAMENTE
    /// el color del aro. Cualquiera que toque uno de los dos sin acordarse del otro
    /// vuelve a hacer desaparecer el aro. Con el contorno el problema no existe,
    /// porque no depende del fondo.
    ///
    /// El velo hace otra cosa: separa la UI del mundo. Este garantiza que se vea.
    ///
    /// ⚠️ No se pinta en la textura de la zona a propósito: ese renderer se tiñe al
    /// acertar y al fallar, y el contorno dejaría de ser un color fijo — se pondría
    /// rojo justo en el cuadro en que hay que leer el resultado.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SkillcheckOutline : MonoBehaviour
    {
        [Header("Tamaño")]
        [Tooltip("Lado de la textura. El mismo que el del aro (128) para que los " +
                 "radios se midan sobre la misma grilla.")]
        [SerializeField, Min(8)] private int textureSize = 128;

        [Header("Radios, en píxeles del aro")]
        // Medido sobre el PNG: los trazos oscuros caen en 44.5–45.5 (círculo
        // interno) y 55.5–56.5 (externo). El contorno va pegado por fuera de cada
        // uno, o sea hacia adentro del interno y hacia afuera del externo, que es
        // donde termina la figura.
        [Tooltip("Dónde arranca el contorno de adentro. Va pegado por dentro del " +
                 "círculo interno del aro, que cae en 44.5 px.")]
        [SerializeField, Min(0f)] private float innerRadius = 43f;

        [Tooltip("Dónde arranca el contorno de afuera. Va pegado por fuera del " +
                 "círculo externo del aro, que cae en 56.5 px.")]
        [SerializeField, Min(0f)] private float outerRadius = 57f;

        [Tooltip("Grosor de cada contorno. Ojo: es un ancho de RADIO, no una " +
                 "cantidad garantizada de píxeles.\n\n" +
                 "Con 1 el anillo sale AGUJEREADO: no en todos los ángulos cae un " +
                 "centro de píxel dentro de la banda, y quedan 74 de 360 ángulos " +
                 "donde el aro vuelve a depender del fondo. Medido: 1.5 deja 7, " +
                 "2 deja 0. Por eso el default es 2 y no 1.")]
        [SerializeField, Min(0.5f)] private float thickness = 2f;

        [Header("Color")]
        [Tooltip("Color del contorno. FIJO y claro a propósito: si siguiera al " +
                 "ciclo de día volvería a depender del fondo, que es el problema " +
                 "que este contorno viene a resolver. #F3ECE0 de la paleta.")]
        [SerializeField] private Color outlineColor = new Color32(0xF3, 0xEC, 0xE0, 0xFF);

        private ArcPainter painter;

        private void Awake()
        {
            painter = new ArcPainter(textureSize, ProjectConstants.PixelsPerUnit);
            painter.PaintFullRing(innerRadius, innerRadius + thickness, (Color32)outlineColor);
            painter.PaintFullRing(outerRadius, outerRadius + thickness, (Color32)outlineColor);
            painter.Apply();

            GetComponent<SpriteRenderer>().sprite = painter.Painted;
        }

        private void OnDestroy()
        {
            painter?.Dispose();
        }
    }
}
