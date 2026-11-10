using BuenosDias.Gameplay;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Abre la hoja de la puerta mientras el vecino atiende, y la cierra al salir.
    /// También dibuja las dos respuestas visibles de una puerta que terminó: la
    /// fuente de colores al convertir y el polvo con puntitos cuando no hay nadie
    /// (ver <see cref="DoorBurst"/>).
    ///
    /// El sprite <c>env_puerta_abierta</c> estaba en el proyecto desde la primera
    /// tanda de arte y nunca lo enganchó nadie: atender era un cambio de estado que
    /// no se veía en ninguna parte. Se notaba sobre todo al INSISTIR, donde el
    /// jugador paga segundos por una puerta que después se veía igual que la que no
    /// le abrió, y la conclusión razonable era que insistir estaba roto.
    ///
    /// La puerta está abierta exactamente mientras dura
    /// <see cref="PreacherState.Atendido"/>. No hace falta nada más: el vecino no
    /// tiene sprite, así que la hoja abierta ES la señal de que hay alguien
    /// escuchando, y que se cierre al terminar el skillcheck se lee solo como que
    /// la charla terminó.
    ///
    /// ⚠️ Se guarda la casa que se abrió en vez de volver a pedírsela a la FSM.
    /// <c>PreacherController.Abandon</c> limpia el intento ANTES de avisar el cambio
    /// de estado, así que en el cuadro en que hay que cerrar, <c>Target</c> ya es
    /// null y la puerta se quedaría abierta para siempre.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorOpeningView : MonoBehaviour
    {
        [Tooltip("La FSM del predicador. Se escucha; nunca se le pide nada.")]
        [SerializeField] private PreacherController preacher;

        [Tooltip("Quien resuelve las puertas. De acá sale el aviso de que se convirtió " +
                 "al vecino, que dispara la fuente. Si queda vacío se busca en el " +
                 "mismo objeto que el predicador.")]
        [SerializeField] private SkillcheckRunner skillcheck;

        private HouseInstance opened;
        private DoorBurst burst;
        private FeedbackFx fx;

        private void Awake()
        {
            if (preacher == null)
            {
                Debug.LogError(
                    $"[DoorOpeningView] '{name}' no tiene predicador asignado, así que " +
                    "las puertas no se van a abrir nunca.", this);
                enabled = false;
                return;
            }

            if (skillcheck == null) skillcheck = preacher.GetComponent<SkillcheckRunner>();

            // Sin renderer de referencia no hay capa ni material para dibujar los
            // chispazos: las puertas funcionan igual, solo sin ellos.
            burst = DoorBurst.Create(preacher.GetComponent<SpriteRenderer>());
            fx = FeedbackFx.Create(preacher.GetComponent<SpriteRenderer>());
        }

        private void OnEnable()
        {
            preacher.StateChanged += OnStateChanged;
            preacher.DoorbellRung += OnDoorbellRung;
            if (skillcheck != null) skillcheck.Finished += OnDoorFinished;
        }

        private void OnDisable()
        {
            preacher.StateChanged -= OnStateChanged;
            preacher.DoorbellRung -= OnDoorbellRung;
            if (skillcheck != null) skillcheck.Finished -= OnDoorFinished;

            // Cerrar al apagarse no es una cortesía: la casa vive en un pool y se
            // recicla, así que una hoja abierta olvidada acá reaparece en otra
            // cuadra como una casa que abre sola.
            Close();
            burst?.Clear();
            fx?.Clear();
        }

        private void OnDestroy()
        {
            burst?.Destroy();
            fx?.Destroy();
        }

        private void Update()
        {
            burst?.Tick(Time.deltaTime);
            fx?.Tick(Time.deltaTime);
        }

        /// <summary>
        /// Timbrazo (primero o insistencia): ondas desde el timbre de ESA casa. Sin
        /// esto, tocar solo se notaba por el brazo y el sonido.
        /// </summary>
        private void OnDoorbellRung(float precision)
        {
            HouseInstance house = preacher.Target;
            if (house != null) fx?.PlayRing(house.DoorPosition);
        }

        private void OnStateChanged(PreacherState state)
        {
            if (state == PreacherState.SinRespuesta) ShowNoAnswer();

            if (state != PreacherState.Atendido) { Close(); return; }

            opened = preacher.Target;
            if (opened != null) opened.SetDoorOpen(true);
        }

        /// <summary>
        /// Terminó la espera y nadie abrió. Si ADEMÁS no hay nadie en casa, se
        /// muestra: es lo que separa "no hay nadie" de "hay alguien y te ignora", que
        /// hasta ahora solo se leía por la ausencia de la cortina.
        /// </summary>
        private void ShowNoAnswer()
        {
            HouseInstance house = preacher.Target;
            if (house == null) return;

            // Siempre: la reacción de animé del predicador ("no pasó nada"). Si
            // había alguien, además la venita de enojo sobre la puerta.
            fx?.PlayIgnored(preacher.transform.position, house.DoorPosition, preacher.TargetIsEmpty);

            if (preacher.TargetIsEmpty) burst?.PlayEmpty(house.DoorPosition);
        }

        /// <summary>
        /// La fuente sale al convertir. Va acá y no al cambiar de estado porque el
        /// resultado llega ANTES de que la FSM salga de la puerta: en ese cuadro
        /// la casa abierta todavía está guardada.
        /// </summary>
        private void OnDoorFinished(SkillcheckResult result)
        {
            // El "+4s" / "-3s": que se vea qué le hizo la puerta al reloj.
            if (Mathf.Abs(result.TimeDelta) > 0.01f)
                fx?.PlayTime(preacher.transform.position, result.TimeDelta);

            if (result.State != SkillcheckSessionState.Convertido || opened == null) return;

            burst?.PlayFountain(opened.DoorPosition);
        }

        private void Close()
        {
            if (opened == null) return;

            opened.SetDoorOpen(false);
            opened = null;
        }
    }
}
