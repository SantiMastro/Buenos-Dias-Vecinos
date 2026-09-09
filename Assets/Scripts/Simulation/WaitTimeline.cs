using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.Simulation
{
    /// <summary>Qué hay que disparar en este cuadro de la espera.</summary>
    public readonly struct WaitTick
    {
        /// <summary>Toca asomar la cortina.</summary>
        public bool TellFired { get; }

        /// <summary>Toca sonar un paso.</summary>
        public bool StepFired { get; }

        /// <summary>La espera terminó en este cuadro.</summary>
        public bool Finished { get; }

        /// <summary>Arma el resultado de avanzar el reloj.</summary>
        public WaitTick(bool tellFired, bool stepFired, bool finished)
        {
            TellFired = tellFired;
            StepFired = stepFired;
            Finished = finished;
        }
    }

    /// <summary>
    /// El reloj de una espera en la puerta: cuándo asoma la cortina y cuándo suena
    /// cada paso.
    ///
    /// Es una clase plana y no un MonoBehaviour porque el timing de la espera es
    /// EL momento del juego. Tiene que poder verificarse con números y simularse,
    /// no ajustarse a ojo dentro de un Update.
    ///
    /// Los dos tells solo existen si hay alguien: eso es lo que los hace tells. Y
    /// arrancan juntos, en el mismo instante, porque los pasos son el respaldo
    /// sonoro del asomo para quien no estaba mirando la ventana. Si arrancaran
    /// desde el segundo cero delatarían la ocupación de entrada y la cortina
    /// sobraría.
    /// </summary>
    public sealed class WaitTimeline
    {
        private readonly WaitConfig config;
        private readonly bool occupied;
        private readonly float firstTellAt;

        private float elapsed;
        private float nextTellAt;
        private float nextStepAt;

        /// <summary>Cuánto dura esta espera, en segundos.</summary>
        public float Duration { get; }

        /// <summary>Segundos transcurridos.</summary>
        public float Elapsed => elapsed;

        /// <summary>Si ya se mostró al menos un asomo.</summary>
        public bool TellShown { get; private set; }

        /// <summary>Segundo en el que aparece el primer asomo.</summary>
        public float FirstTellAt => firstTellAt;

        /// <summary>Arranca el reloj de una espera ya sorteada.</summary>
        public WaitTimeline(WaitConfig config, float duration, bool occupied)
        {
            this.config = config;
            this.occupied = occupied;
            Duration = duration;

            firstTellAt = duration * config.TellFraction;
            nextTellAt = firstTellAt;
            nextStepAt = firstTellAt;
        }

        /// <summary>
        /// Avanza el reloj y devuelve qué disparar. Los pasos aceleran a medida que
        /// se acerca la apertura: el progreso se mide desde el primer asomo, no
        /// desde el arranque, porque antes de eso no hay nadie caminando.
        /// </summary>
        public WaitTick Advance(float deltaTime)
        {
            if (elapsed >= Duration) return new WaitTick(false, false, false);

            elapsed += deltaTime;
            bool finished = elapsed >= Duration;

            if (!occupied) return new WaitTick(false, false, finished);

            bool tell = false;
            if (elapsed >= nextTellAt)
            {
                tell = true;
                TellShown = true;
                nextTellAt += config.TellRepeatSeconds;
            }

            bool step = false;
            if (elapsed >= nextStepAt)
            {
                step = true;
                nextStepAt = elapsed + config.StepIntervalAt(ApproachProgress);
            }

            return new WaitTick(tell, step, finished);
        }

        /// <summary>
        /// Cuánto falta para que abran, de 0 (recién arrancaron a caminar) a 1
        /// (abren ya). Es lo que acelera los pasos.
        /// </summary>
        private float ApproachProgress
        {
            get
            {
                float window = Duration - firstTellAt;
                if (window <= 0f) return 1f;
                return Mathf.Clamp01((elapsed - firstTellAt) / window);
            }
        }
    }
}
