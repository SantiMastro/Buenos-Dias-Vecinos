using BuenosDias.Config;
using BuenosDias.Gameplay;
using System.Collections.Generic;
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

        [Header("Timbre de fondo")]
        [Tooltip("Sprite del timbre que aparece DETRÁS del skillcheck (prop_timbre). \n" +
                 "Vacío = no se dibuja timbre.")]
        [SerializeField] private Sprite doorbellSprite;

        [Tooltip("Sprite del timbre apretado (prop_timbre_activo). Se muestra al \n" +
                 "acertar. Vacío = se usa el normal.")]
        [SerializeField] private Sprite doorbellPressedSprite;

        [Tooltip("Segundos que tarda el timbre en 'aparecer' creciendo de a escalas \n" +
                 "enteras (×1, ×2, ×3), para no romper la grilla de píxeles.")]
        [SerializeField, Min(0f)] private float doorbellPopSeconds = 0.12f;

        [Tooltip("Cuántos píxeles baja el timbre al apretarlo.")]
        [SerializeField, Range(0, 6)] private int doorbellPressDepthPixels = 2;

        [Tooltip("Segundos que dura el hundido del timbre antes de volver.")]
        [SerializeField, Min(0f)] private float doorbellPressSeconds = 0.09f;

        private SpriteRenderer bellRenderer;
        private float bellAge;
        private float bellPressAge = -1f;
        private bool bellMissed;

        private Vector3 homePosition;
        private SkillcheckShape shape = SkillcheckShape.Circulo;
        private Sprite circleRingSprite;
        private Sprite circleOutlineSprite;
        private ArcPainter shapeNeedlePainter;
        private readonly Dictionary<SkillcheckShape, ArcPainter> shapeRings =
            new Dictionary<SkillcheckShape, ArcPainter>();
        private readonly Dictionary<SkillcheckShape, ArcPainter> shapeOutlines =
            new Dictionary<SkillcheckShape, ArcPainter>();

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
            shapeNeedlePainter = new ArcPainter(textureSize, ProjectConstants.PixelsPerUnit);
            homePosition = transform.localPosition;
            BuildBell();
            Show(false);
        }

        private void OnEnable()
        {
            if (runner == null) return;

            runner.AttemptStarted += OnAttemptStarted;
            runner.AttemptResolved += OnAttemptResolved;
            runner.Cancelled += OnCancelled;
        }

        private void OnDisable()
        {
            if (runner == null) return;

            runner.AttemptStarted -= OnAttemptStarted;
            runner.AttemptResolved -= OnAttemptResolved;
            runner.Cancelled -= OnCancelled;
        }

        private void OnDestroy()
        {
            painter?.Dispose();
            needlePainter?.Dispose();
            shapeNeedlePainter?.Dispose();
            foreach (ArcPainter p in shapeRings.Values) p.Dispose();
            foreach (ArcPainter p in shapeOutlines.Values) p.Dispose();
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
            AnimateBell();
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

            PlaceAtRandom(started);
            ApplyShape(started.Shape);
            Repaint(started);
            PointNeedle();
            StartBell();
            Show(true);
        }

        /// <summary>
        /// La puerta se cortó sin resolverse —se hizo de noche con la puerta
        /// abierta—: el aro se va en el acto, sin destello, porque no hubo golpe
        /// que devolver.
        /// </summary>
        private void OnCancelled()
        {
            attempt = null;
            flashLeft = 0f;
            Show(false);
        }

        private void OnAttemptResolved(SkillcheckOutcome outcome)
        {
            flashLeft = flashSeconds;
            zoneRenderer.color =
                outcome == SkillcheckOutcome.Fallado ? missTint : hitTint;

            PressBell(outcome != SkillcheckOutcome.Fallado);
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

            if (target.Shape == SkillcheckShape.Circulo)
            {
                painter.Paint(
                    ArcStart(target.ZoneStart, target.ZoneEnd, target.Direction), target.ZoneWidth,
                    trackInnerRadius, trackOuterRadius, (Color32)goodColor);
                painter.Paint(
                    ArcStart(target.PerfectStart, target.PerfectEnd, target.Direction),
                    target.PerfectEnd - target.PerfectStart,
                    trackInnerRadius, trackOuterRadius, (Color32)perfectColor);
            }
            else
            {
                // Mismo canal que el aro: de −0.5 a (ancho − 0.5) hacia adentro del borde.
                float channel = SkillcheckShapeGeometry.ChannelWidth(target.Shape);
                painter.PaintShapeBand(
                    target.Shape, -0.5f, channel - 0.5f,
                    ArcStart(target.ZoneStart, target.ZoneEnd, target.Direction), target.ZoneWidth,
                    (Color32)goodColor);
                painter.PaintShapeBand(
                    target.Shape, -0.5f, channel - 0.5f,
                    ArcStart(target.PerfectStart, target.PerfectEnd, target.Direction),
                    target.PerfectEnd - target.PerfectStart, (Color32)perfectColor);
            }

            painter.Apply();
        }

        /// <summary>
        /// Dónde arranca, en el aro, un tramo del recorrido. En horario es el
        /// mismo número; en antihorario el tramo se espeja, así que el arco va de
        /// −fin a −inicio. El pintor siempre barre en horario.
        /// </summary>
        private static float ArcStart(float start, float end, int direction)
        {
            return direction >= 0 ? start : -end;
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
        ///
        /// El sentido de la tirada multiplica el recorrido: en antihorario el
        /// ángulo en el aro es el recorrido negado, igual que la zona espejada.
        /// </summary>
        private void PointNeedle()
        {
            float turn = Mathf.Repeat(attempt.Direction * attempt.NeedleAngle, TwoPi);

            if (shape == SkillcheckShape.Circulo)
            {
                needleRenderer.transform.localRotation =
                    Quaternion.Euler(0f, 0f, -turn * Mathf.Rad2Deg);
                return;
            }

            // Fuera del círculo la aguja cambia de largo con el ángulo, así que no
            // alcanza con rotar un sprite: se repinta apuntando al ángulo.
            needleRenderer.transform.localRotation = Quaternion.identity;
            shapeNeedlePainter.Clear();
            shapeNeedlePainter.PaintShapeNeedle(shape, turn, needleThickness, (Color32)needleColor);
            shapeNeedlePainter.Apply();
        }

        /// <summary>
        /// Cambia los sprites de pista, contorno y aguja según la forma del eslabón.
        /// El círculo usa los sprites originales de la escena; las otras formas se
        /// pintan por código la primera vez que salen y quedan en caché.
        /// </summary>
        private void ApplyShape(SkillcheckShape newShape)
        {
            // Los sprites originales se guardan recién acá: el contorno se pinta en
            // el Awake de otro componente, y recién en juego ya está listo.
            if (circleRingSprite == null) circleRingSprite = ringRenderer.sprite;
            if (circleOutlineSprite == null) circleOutlineSprite = outlineRenderer.sprite;

            shape = newShape;

            if (shape == SkillcheckShape.Circulo)
            {
                ringRenderer.sprite = circleRingSprite;
                outlineRenderer.sprite = circleOutlineSprite;
                needleRenderer.sprite = needlePainter.Painted;
                return;
            }

            ringRenderer.sprite = ShapeRing(shape).Painted;
            outlineRenderer.sprite = ShapeOutline(shape).Painted;
            needleRenderer.sprite = shapeNeedlePainter.Painted;
        }

        /// <summary>Los dos trazos oscuros que encierran el canal, como los del aro.</summary>
        private ArcPainter ShapeRing(SkillcheckShape target)
        {
            if (shapeRings.TryGetValue(target, out ArcPainter cached)) return cached;

            var built = new ArcPainter(textureSize, ProjectConstants.PixelsPerUnit);
            built.Clear();

            float channel = SkillcheckShapeGeometry.ChannelWidth(target);
            Color32 stroke = new Color32(0x1D, 0x16, 0x38, 0xFF);

            built.PaintShapeBand(
                target, -2f, -0.5f, 0f, Mathf.PI * 2f, stroke);

            built.PaintShapeBand(
                target, channel - 0.5f, channel + 1f, 0f, Mathf.PI * 2f, stroke);

            built.Apply();
            shapeRings[target] = built;
            return built;
        }

        /// <summary>El contorno claro que despega la pista de cualquier fondo.</summary>
        private ArcPainter ShapeOutline(SkillcheckShape target)
        {
            if (shapeOutlines.TryGetValue(target, out ArcPainter cached)) return cached;

            var built = new ArcPainter(textureSize, ProjectConstants.PixelsPerUnit);
            built.Clear();

            float channel = SkillcheckShapeGeometry.ChannelWidth(target);
            Color32 light = new Color32(0xF3, 0xEC, 0xE0, 0xFF);

            built.PaintShapeBand(target, -4f, -2f, 0f, Mathf.PI * 2f, light);

            built.PaintShapeBand(
                target, channel + 1f, channel + 3f, 0f, Mathf.PI * 2f, light);

            built.Apply();
            shapeOutlines[target] = built;
            return built;
        }

        /// <summary>
        /// Corre el skillcheck a un lugar sorteado de la pantalla. Se redondea a
        /// píxel entero: la cámara es pixel-perfect y una posición a medio píxel
        /// haría temblar los bordes de la pista.
        /// </summary>
        private void PlaceAtRandom(SkillcheckAttempt target)
        {
            SkillcheckConfig config = runner != null ? runner.Config : null;
            if (config == null || !config.RandomPosition)
            {
                transform.localPosition = homePosition;
                return;
            }

            float xPixels = Mathf.Round(target.OffsetX * config.PositionRangeXPixels);
            float yPixels = Mathf.Round(target.OffsetY * config.PositionRangeYPixels);

            transform.localPosition = homePosition + new Vector3(
                ProjectConstants.ToUnits(xPixels), ProjectConstants.ToUnits(yPixels), 0f);
        }

        // ── Timbre de fondo ────────────────────────────────────────────────────

        /// <summary>
        /// Crea el timbre por código, como hijo del skillcheck, detrás del aro.
        /// Va al mismo orden que el contorno (que no se superpone con él: el timbre
        /// vive en el centro) y por delante del velo.
        /// </summary>
        private void BuildBell()
        {
            if (doorbellSprite == null) return;

            var go = new GameObject("Timbre");
            go.transform.SetParent(transform, false);

            bellRenderer = go.AddComponent<SpriteRenderer>();
            bellRenderer.sprite = doorbellSprite;
            bellRenderer.sortingLayerID = ringRenderer.sortingLayerID;
            bellRenderer.sortingOrder = outlineRenderer.sortingOrder;
            bellRenderer.sharedMaterial = ringRenderer.sharedMaterial;
            bellRenderer.enabled = false;
        }

        /// <summary>Qué tan grande se dibuja el timbre, en escala ENTERA.</summary>
        private int BellScale()
        {
            // En las formas angostas por dentro el timbre grande no entra.
            switch (shape)
            {
                case SkillcheckShape.Triangulo:
                case SkillcheckShape.Rombo:
                case SkillcheckShape.Estrella:
                    return 2;
                default:
                    return 3;
            }
        }

        private void StartBell()
        {
            bellAge = 0f;
            bellPressAge = -1f;
            bellMissed = false;
            if (bellRenderer == null) return;

            bellRenderer.sprite = doorbellSprite;
            AnimateBell();
        }

        private void PressBell(bool hit)
        {
            if (bellRenderer == null) return;

            bellPressAge = 0f;
            bellMissed = !hit;
            if (hit && doorbellPressedSprite != null) bellRenderer.sprite = doorbellPressedSprite;
        }

        /// <summary>
        /// Aparece creciendo ×1 → ×2 → ×3 (solo escalas enteras), se hunde unos
        /// píxeles al apretarlo y, si se erró, tiembla de costado.
        /// </summary>
        private void AnimateBell()
        {
            if (bellRenderer == null) return;

            bellAge += Time.deltaTime;

            int target = BellScale();
            float step = target > 1 ? doorbellPopSeconds / (target - 1) : 0f;
            int scale = step <= 0f
                ? target
                : Mathf.Clamp(1 + Mathf.FloorToInt(bellAge / step), 1, target);
            bellRenderer.transform.localScale = new Vector3(scale, scale, 1f);

            float offsetX = 0f;
            float offsetY = 0f;

            if (bellPressAge >= 0f)
            {
                bellPressAge += Time.deltaTime;

                if (bellMissed)
                {
                    // Temblor de un píxel, alternando cada ~0.04 s, durante el destello.
                    if (bellPressAge < flashSeconds)
                        offsetX = (Mathf.FloorToInt(bellPressAge / 0.04f) % 2 == 0 ? 1f : -1f);
                }
                else if (bellPressAge < doorbellPressSeconds)
                {
                    offsetY = -doorbellPressDepthPixels;
                }
            }

            // El sprite del timbre importa con pivot abajo (prop_), así que sin esto
            // queda parado SOBRE el centro del aro. Se corre para que su CENTRO
            // caiga en el centro, en píxeles enteros para no romper la grilla.
            Sprite bell = bellRenderer.sprite;
            if (bell != null)
            {
                offsetX -= Mathf.Round((bell.rect.width * 0.5f - bell.pivot.x) * scale);
                offsetY -= Mathf.Round((bell.rect.height * 0.5f - bell.pivot.y) * scale);
            }

            bellRenderer.transform.localPosition = new Vector3(
                ProjectConstants.ToUnits(offsetX), ProjectConstants.ToUnits(offsetY), 0f);
        }

        private void Show(bool visible)
        {
            veilRenderer.enabled = visible;
            outlineRenderer.enabled = visible;
            ringRenderer.enabled = visible;
            zoneRenderer.enabled = visible;
            needleRenderer.enabled = visible;
            if (bellRenderer != null) bellRenderer.enabled = visible;
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
