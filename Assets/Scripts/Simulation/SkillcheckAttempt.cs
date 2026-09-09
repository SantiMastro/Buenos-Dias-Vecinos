using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.Simulation
{
    /// <summary>Cómo terminó un eslabón del skillcheck.</summary>
    public enum SkillcheckOutcome
    {
        /// <summary>La aguja sigue girando.</summary>
        EnCurso,

        /// <summary>Cayó en la zona perfecta.</summary>
        Perfecto,

        /// <summary>Cayó en la zona buena.</summary>
        Bueno,

        /// <summary>Erró, o dejó pasar la zona.</summary>
        Fallado
    }

    /// <summary>
    /// Un eslabón del skillcheck: la aguja gira y el jugador aprieta una vez.
    ///
    /// El ángulo de la aguja NO se envuelve en 2π: arranca en 0 y crece. La zona
    /// siempre aparece a menos de 3.1 rad por delante y el margen de falla corta
    /// la tirada poco después, así que envolver solo agregaría casos de borde en
    /// las comparaciones. Quien dibuja hace el módulo; la lógica no lo necesita.
    ///
    /// Es una clase plana para poder verificar los bordes —apretar justo en el
    /// límite de la zona, dejarla pasar por un pelo— sin depender de la mano de
    /// nadie.
    /// </summary>
    public sealed class SkillcheckAttempt
    {
        private readonly SkillcheckConfig config;
        private readonly float speed;

        /// <summary>Cómo va la tirada.</summary>
        public SkillcheckOutcome Outcome { get; private set; } = SkillcheckOutcome.EnCurso;

        /// <summary>Ángulo de la aguja, en radianes desde el arranque.</summary>
        public float NeedleAngle { get; private set; }

        /// <summary>Dónde empieza la zona buena.</summary>
        public float ZoneStart { get; }

        /// <summary>Ancho de la zona buena.</summary>
        public float ZoneWidth { get; }

        /// <summary>Dónde termina la zona buena.</summary>
        public float ZoneEnd => ZoneStart + ZoneWidth;

        /// <summary>Dónde empieza la zona perfecta, dentro de la buena.</summary>
        public float PerfectStart => ZoneStart + ZoneWidth * config.PerfectStart;

        /// <summary>Dónde termina la zona perfecta.</summary>
        public float PerfectEnd => ZoneStart + ZoneWidth * config.PerfectEnd;

        /// <summary>Ángulo pasado el cual la tirada se da por perdida sola.</summary>
        public float DeadlineAngle => ZoneEnd + config.FailMarginRadians;

        /// <summary>
        /// Arranca un eslabón. La zona aparece a una distancia sorteada por delante
        /// de la aguja: nunca menos de <c>zoneAheadMin</c>, porque más cerca no
        /// daría tiempo a reaccionar y la falla sería del juego y no del jugador.
        /// </summary>
        public SkillcheckAttempt(
            SkillcheckConfig config, SkillcheckSetup setup, System.Random random)
        {
            this.config = config;
            speed = setup.NeedleSpeed;
            ZoneWidth = setup.ZoneWidth;
            ZoneStart = Mathf.Lerp(
                config.ZoneAheadMin, config.ZoneAheadMax, (float)random.NextDouble());
        }

        /// <summary>
        /// Avanza la aguja. Si se pasó del margen sin que nadie apretara, la tirada
        /// se pierde sola: dejar pasar la zona es fallar, no esperar a la siguiente.
        /// </summary>
        public void Advance(float deltaTime)
        {
            if (Outcome != SkillcheckOutcome.EnCurso) return;

            NeedleAngle += speed * deltaTime;
            if (NeedleAngle > DeadlineAngle) Outcome = SkillcheckOutcome.Fallado;
        }

        /// <summary>
        /// Resuelve el apretón. Devuelve el resultado, que ya no cambia: apretar de
        /// nuevo sobre una tirada resuelta no hace nada, porque el rebote de un
        /// botón de arcade no puede convertir un perfecto en una falla.
        /// </summary>
        public SkillcheckOutcome Press()
        {
            if (Outcome != SkillcheckOutcome.EnCurso) return Outcome;

            if (NeedleAngle >= PerfectStart && NeedleAngle <= PerfectEnd)
                Outcome = SkillcheckOutcome.Perfecto;
            else if (NeedleAngle >= ZoneStart && NeedleAngle <= ZoneEnd)
                Outcome = SkillcheckOutcome.Bueno;
            else
                Outcome = SkillcheckOutcome.Fallado;

            return Outcome;
        }
    }
}
