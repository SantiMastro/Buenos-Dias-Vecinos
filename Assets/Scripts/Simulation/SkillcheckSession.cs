using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.Simulation
{
    /// <summary>Cómo va la cadena de objeciones de una puerta.</summary>
    public enum SkillcheckSessionState
    {
        /// <summary>Todavía quedan eslabones por ganar.</summary>
        EnCurso,

        /// <summary>Se ganaron todos: el vecino se convierte.</summary>
        Convertido,

        /// <summary>Se falló un eslabón: la puerta se cierra.</summary>
        Rechazado
    }

    /// <summary>Qué pasó en este cuadro de la cadena.</summary>
    public readonly struct SkillcheckSessionTick
    {
        /// <summary>Un eslabón se resolvió en este cuadro.</summary>
        public bool LinkResolved { get; }

        /// <summary>Cómo salió, si se resolvió alguno.</summary>
        public SkillcheckOutcome LinkOutcome { get; }

        /// <summary>Arrancó un eslabón nuevo en este cuadro.</summary>
        public bool LinkStarted { get; }

        /// <summary>La cadena entera terminó en este cuadro.</summary>
        public bool Finished { get; }

        /// <summary>Arma el resultado de avanzar la cadena.</summary>
        public SkillcheckSessionTick(
            bool linkResolved, SkillcheckOutcome linkOutcome, bool linkStarted, bool finished)
        {
            LinkResolved = linkResolved;
            LinkOutcome = linkOutcome;
            LinkStarted = linkStarted;
            Finished = finished;
        }
    }

    /// <summary>
    /// La cadena de objeciones de una puerta: varios <see cref="SkillcheckAttempt"/>
    /// seguidos, separados por la pausa en la que el vecino contesta.
    ///
    /// Hay UN solo punto de entrada, <see cref="Advance"/>, que recibe el botón
    /// junto con el tiempo. Con un <c>Press()</c> aparte, el resultado dependería
    /// de en qué orden lo llamara quien la usa, que es justo el tipo de bug que
    /// aparece recién cuando alguien reordena un <c>Update</c>.
    ///
    /// Fallar un eslabón termina la cadena entera: la objeción que no se contestó
    /// es la que cierra la puerta.
    /// </summary>
    public sealed class SkillcheckSession
    {
        private readonly SkillcheckConfig config;
        private readonly SkillcheckSetup setup;
        private readonly System.Random random;

        private float pauseLeft;

        /// <summary>Estado de la cadena.</summary>
        public SkillcheckSessionState State { get; private set; } = SkillcheckSessionState.EnCurso;

        /// <summary>Cuántos eslabones hay que ganar para convertir.</summary>
        public int LinkCount { get; }

        /// <summary>Eslabón en curso, contando desde cero.</summary>
        public int LinkIndex { get; private set; }

        /// <summary>Aciertos perfectos acumulados. Los lee el bono de tiempo.</summary>
        public int PerfectHits { get; private set; }

        /// <summary>Aciertos buenos acumulados.</summary>
        public int GoodHits { get; private set; }

        /// <summary>
        /// Tirada en curso. Es <c>null</c> durante la pausa entre eslabones, que es
        /// lo que le dice a quien dibuja que esconda el aro mientras el vecino
        /// contesta.
        /// </summary>
        public SkillcheckAttempt Current { get; private set; }

        /// <summary>Arranca la cadena. El primer eslabón empieza ya.</summary>
        public SkillcheckSession(
            SkillcheckConfig config, SkillcheckSetup setup, System.Random random)
        {
            this.config = config;
            this.setup = setup;
            this.random = random;

            LinkCount = Mathf.Max(1, setup.ChainLinks);
            Current = new SkillcheckAttempt(config, setup, random);
        }

        /// <summary>
        /// Avanza la cadena un cuadro.
        ///
        /// El botón se resuelve ANTES de mover la aguja, contra el ángulo que se
        /// dibujó el cuadro pasado. El jugador reacciona a lo que vio, no a lo que
        /// todavía no se pintó, y moverla primero le cobraría un cuadro de latencia
        /// que a 60 fps son 0.03 rad de castigo que nadie puede ver ni corregir.
        /// </summary>
        public SkillcheckSessionTick Advance(float deltaTime, bool pressed)
        {
            if (State != SkillcheckSessionState.EnCurso) return default;

            if (Current == null) return Pause(deltaTime);

            if (pressed) Current.Press();
            else Current.Advance(deltaTime);

            return Current.Outcome == SkillcheckOutcome.EnCurso
                ? default
                : ResolveLink(Current.Outcome);
        }

        /// <summary>
        /// Corre la pausa en la que el vecino tira la réplica. El botón no se mira:
        /// el aro no está en pantalla, y tragarse el rebote del botón de arcade acá
        /// evita que un contacto sobrante se coma el eslabón siguiente.
        /// </summary>
        private SkillcheckSessionTick Pause(float deltaTime)
        {
            pauseLeft -= deltaTime;
            if (pauseLeft > 0f) return default;

            Current = new SkillcheckAttempt(config, setup, random);
            return new SkillcheckSessionTick(false, SkillcheckOutcome.EnCurso, true, false);
        }

        private SkillcheckSessionTick ResolveLink(SkillcheckOutcome outcome)
        {
            if (outcome == SkillcheckOutcome.Perfecto) PerfectHits++;
            else if (outcome == SkillcheckOutcome.Bueno) GoodHits++;

            Current = null;

            if (outcome == SkillcheckOutcome.Fallado)
                return Finish(outcome, SkillcheckSessionState.Rechazado);

            if (LinkIndex + 1 >= LinkCount)
                return Finish(outcome, SkillcheckSessionState.Convertido);

            LinkIndex++;
            pauseLeft = config.ChainPauseSeconds;
            return new SkillcheckSessionTick(true, outcome, false, false);
        }

        private SkillcheckSessionTick Finish(
            SkillcheckOutcome outcome, SkillcheckSessionState state)
        {
            State = state;
            return new SkillcheckSessionTick(true, outcome, false, true);
        }
    }
}
