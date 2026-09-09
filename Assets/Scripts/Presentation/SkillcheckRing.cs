using BuenosDias.Config;
using BuenosDias.Gameplay;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Dibuja el skillcheck: el aro, la zona pintada en la pista entre los dos
    /// círculos del sprite, y la aguja girando.
    ///
    /// No decide nada. Se suscribe al <see cref="SkillcheckRunner"/> y sigue el
    /// ángulo de la tirada; si esta clase no existiera, el juego se resolvería
    /// igual, solo que a ciegas.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillcheckRing : MonoBehaviour
    {
        private const float TwoPi = Mathf.PI * 2f;

        [Header("Referencias")]
        [Tooltip("Quien corre la cadena. Se escucha; nunca se le pide nada.")]
        [SerializeField] private SkillcheckRunner runner;

        [Tooltip("El velo que apaga el mundo detrás. Se prende y se apaga con el " +
                 "resto; sus perillas viven en su propio SkillcheckVeil.")]
        [SerializeField] private SpriteRenderer veilRenderer;

        [Tooltip("El contorno claro que hace que el aro se lea contra cualquier " +
                 "fondo. Sus perillas viven en su propio SkillcheckOutline.")]
        [SerializeField] private SpriteRenderer outlineRenderer;

        [Tooltip("El aro de fondo, ui_skillcheck_aro.")]
        [SerializeField] private SpriteRenderer ringRenderer;

        [Tooltip("Dónde se pinta la zona. Su sprite lo genera esta clase.")]
        [SerializeField] private SpriteRenderer zoneRenderer;

        [Tooltip("La aguja, ui_skillcheck_aguja. Gira sobre el centro del aro.")]
        [SerializeField] private SpriteRenderer needleRenderer;

        [Header("Pista de la zona, en píxeles del aro")]
        [Tooltip("Lado de la textura donde se pinta. Tiene que ser el mismo que el " +
                 "del sprite del aro (128) o la zona no queda concéntrica.")]
        [SerializeField, Min(8)] private int textureSize = 128;

        [Tooltip("Radio interno de la pista. El aro trae dos círculos y el canal " +
                 "libre entre ellos va de 46 a 55 px medidos sobre el PNG.")]
        [SerializeField, Min(0f)] private float trackInnerRadius = 46f;

        [Tooltip("Radio externo de la pista.")]
        [SerializeField, Min(0f)] private float trackOuterRadius = 55f;

        [Header("Aguja")]
        [Tooltip("Hasta dónde llega la punta, en píxeles del aro. La zona termina " +
                 "en trackOuterRadius, así que para que la CRUCE y la pase este " +
                 "número tiene que ser mayor. El sprite dibujado a mano llegaba a " +
                 "27,5 px y la zona arranca en 46: no la tocaba nunca.")]
        [SerializeField, Min(1f)] private float needleOuterRadius = 60f;

        [Tooltip("Desde dónde arranca. En 0 sale del centro del aro.")]
        [SerializeField, Min(0f)] private float needleInnerRadius = 0f;

        [Tooltip("Ancho de la aguja en píxeles. El sprite original tenía 4.")]
        [SerializeField, Min(1f)] private float needleThickness = 4f;

        [Tooltip("Color de la aguja. Hueso #F3ECE0, el mismo del sprite original.")]
        [SerializeField] private Color needleColor = new Color32(0xF3, 0xEC, 0xE0, 0xFF);

        [Header("Colores")]
        [Tooltip("Zona buena. Verde salvia de la paleta, #6F8A5E.")]
        [SerializeField] private Color goodColor = new Color32(0x6F, 0x8A, 0x5E, 0xFF);

        [Tooltip("Zona perfecta, dentro de la buena. Naranja #FF9A5B de la paleta.")]
        [SerializeField] private Color perfectColor = new Color32(0xFF, 0x9A, 0x5B, 0xFF);

        [Header("Devolución del acierto")]
        [Tooltip("Segundos que la zona queda teñida después de apretar. Es lo único " +
                 "que le dice al jugador si le pegó, así que por debajo de un par de " +
                 "cuadros no se ve.")]
        [SerializeField, Min(0f)] private float flashSeconds = 0.25f;

        [Tooltip("Tinte al acertar.")]
        [SerializeField] private Color hitTint = new Color32(0xF3, 0xEC, 0xE0, 0xFF);

        [Tooltip("Tinte al errar. Rojo #A8455A de la paleta.")]
        [SerializeField] private Color missTint = new Color32(0xA8, 0x45, 0x5A, 0xFF);

        private ArcPainter painter;
        private ArcPainter needlePainter;
        private SkillcheckAttempt attempt;
        private float flashLeft;

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }

            painter = new ArcPainter(textureSize, ProjectConstants.PixelsPerUnit);
            zoneRenderer.sprite = painter.Painted;

            PaintNeedle();
            Show(false);
        }

        private void OnEnable()
        {
            if (runner == null) return;

            runner.AttemptStarted += OnAttemptStarted;
            runner.AttemptResolved += OnAttemptResolved;
        }

        private void OnDisable()
        {
            if (runner == null) return;

            runner.AttemptStarted -= OnAttemptStarted;
            runner.AttemptResolved -= OnAttemptResolved;
        }

        private void OnDestroy()
        {
            painter?.Dispose();
            needlePainter?.Dispose();
        }

        /// <summary>
        /// La aguja se mueve en LateUpdate para leer el ángulo que dejó el
        /// <c>Update</c> de la FSM. Al revés, la aguja dibujada iría un cuadro
        /// atrasada respecto de la que resuelve el apretón.
        /// </summary>
        private void LateUpdate()
        {
            if (attempt == null) return;

            PointNeedle();
            if (attempt.Outcome == SkillcheckOutcome.EnCurso) return;

            flashLeft -= Time.deltaTime;
            if (flashLeft > 0f) return;

            attempt = null;
            Show(false);
        }

        private void OnAttemptStarted(SkillcheckAttempt started)
        {
            attempt = started;
            flashLeft = 0f;
            zoneRenderer.color = Color.white;

            Repaint(started);
            PointNeedle();
            Show(true);
        }

        private void OnAttemptResolved(SkillcheckOutcome outcome)
        {
            flashLeft = flashSeconds;
            zoneRenderer.color =
                outcome == SkillcheckOutcome.Fallado ? missTint : hitTint;
        }

        /// <summary>
        /// Repinta la zona. La perfecta va encima de la buena y no al lado: son la
        /// misma banda, y pintarlas en dos pasadas evita tener que partir el arco
        /// en tres tramos cuando la perfecta toca un borde.
        /// </summary>
        private void Repaint(SkillcheckAttempt target)
        {
            // Los colores se exponen como Color para tener el picker completo en el
            // Inspector, pero el buffer es Color32: el casteo va explícito para que
            // no dependa de una conversión implícita que se lee como un descuido.
            painter.Clear();
            painter.Paint(
                target.ZoneStart, target.ZoneWidth,
                trackInnerRadius, trackOuterRadius, (Color32)goodColor);
            painter.Paint(
                target.PerfectStart, target.PerfectEnd - target.PerfectStart,
                trackInnerRadius, trackOuterRadius, (Color32)perfectColor);
            painter.Apply();
        }

        /// <summary>
        /// Genera la aguja por código, apuntando a cero, y la cuelga del renderer.
        ///
        /// Se pinta UNA sola vez: girarla es cosa del transform, así que el costo
        /// no vuelve a aparecer por cuadro. Se genera en vez de usar un PNG porque
        /// el largo tiene que poder cambiarse desde el Inspector sin volver a
        /// dibujar un sprite, y porque estirar un sprite con escala rompe la
        /// grilla de píxeles.
        /// </summary>
        private void PaintNeedle()
        {
            needlePainter = new ArcPainter(textureSize, ProjectConstants.PixelsPerUnit);
            needlePainter.Clear();
            needlePainter.PaintRay(
                0f, needleInnerRadius, needleOuterRadius, needleThickness,
                (Color32)needleColor);
            needlePainter.Apply();

            needleRenderer.sprite = needlePainter.Painted;
        }

        /// <summary>
        /// El ángulo de la tirada crece y no vuelve nunca: el módulo es cosa de
        /// quien dibuja. La rotación va en Z negativa porque Unity gira antihorario
        /// y el convenio del skillcheck es horario.
        /// </summary>
        private void PointNeedle()
        {
            float degrees = Mathf.Repeat(attempt.NeedleAngle, TwoPi) * Mathf.Rad2Deg;
            needleRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, -degrees);
        }

        private void Show(bool visible)
        {
            veilRenderer.enabled = visible;
            outlineRenderer.enabled = visible;
            ringRenderer.enabled = visible;
            zoneRenderer.enabled = visible;
            needleRenderer.enabled = visible;
        }

        private bool ValidateSetup()
        {
            if (runner != null && veilRenderer != null && outlineRenderer != null
                && ringRenderer != null && zoneRenderer != null
                && needleRenderer != null) return true;

            Debug.LogError(
                $"[SkillcheckRing] '{name}' tiene referencias sin asignar " +
                "(runner, velo, contorno, aro, zona o aguja).", this);
            return false;
        }
    }
}
