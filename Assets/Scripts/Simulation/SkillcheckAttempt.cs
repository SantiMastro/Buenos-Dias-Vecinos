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
    /// El ángulo es RECORRIDO, no posición en el aro: el sentido de giro
    /// (<see cref="Direction"/>) solo lo usa quien dibuja, que espeja aguja y zona.
    /// Así girar al revés no toca ni una comparación.
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

        /// <summary>Recorrido de la aguja, en radianes desde el arranque.</summary>
        public float NeedleAngle { get; private set; }

        /// <summary>Velocidad de la aguja en esta tirada, en rad/s.</summary>
        public float Speed => speed;

        /// <summary>
        /// Sentido de giro: 1 horario, −1 antihorario. Es cosa de quien dibuja; la
        /// lógica mide recorrido y no lo necesita.
        /// </summary>
        public int Direction { get; }

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
        ///
        /// <paramref name="speedScale"/> es el acelerón de los eslabones
        /// consecutivos: lo decide la cadena, que sabe en cuál va.
        /// </summary>
        public SkillcheckAttempt(
            SkillcheckConfig config, SkillcheckSetup setup, System.Random random,
            float speedScale = 1f)
        {
            this.config = config;
            speed = setup.NeedleSpeed * Mathf.Max(0f, speedScale);
            ZoneWidth = setup.ZoneWidth;
            ZoneStart = Mathf.Lerp(
                config.ZoneAheadMin, config.ZoneAheadMax, (float)random.NextDouble());

            // El sentido se sortea DESPUÉS de la zona y del mismo random: así una
            // semilla dada sigue poniendo la zona donde la ponía antes.
            Direction = config.RandomDirection && random.NextDouble() < 0.5 ? -1 : 1;
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

        /// <summary>Resuelve el apretón contra el ángulo dibujado, sin barrido.</summary>
        public SkillcheckOutcome Press() => PressWithin(0f);

        /// <summary>
        /// Resuelve el apretón contra el TRAMO que recorrió la aguja desde el último
        /// cuadro dibujado, y se queda con el mejor resultado de ese tramo.
        ///
        /// El apretón pasó en algún momento entre ese cuadro y este. Juzgarlo solo
        /// contra el ángulo dibujado hacía que el QTE dependiera del FPS: a 30 fps,
        /// con la aguja rápida, podía saltarse la zona entera entre dos cuadros y
        /// no había forma de pegarle. El tramo se topa en
        /// <c>pressSweepMaxSeconds</c> para que un tirón no regale aciertos.
        ///
        /// Devuelve el resultado, que ya no cambia: apretar de nuevo sobre una
        /// tirada resuelta no hace nada, porque el rebote de un botón de arcade no
        /// puede convertir un perfecto en una falla.
        /// </summary>
        public SkillcheckOutcome PressWithin(float deltaTime)
        {
            if (Outcome != SkillcheckOutcome.EnCurso) return Outcome;

            float seconds = Mathf.Clamp(deltaTime, 0f, config.PressSweepMaxSeconds);
            float from = NeedleAngle;
            float to = NeedleAngle + speed * seconds;

            if (Overlaps(from, to, PerfectStart, PerfectEnd))
                Outcome = SkillcheckOutcome.Perfecto;
            else if (Overlaps(from, to, ZoneStart, ZoneEnd))
                Outcome = SkillcheckOutcome.Bueno;
            else
                Outcome = SkillcheckOutcome.Fallado;

            return Outcome;
        }

        private static bool Overlaps(float from, float to, float start, float end)
        {
            return from <= end && to >= start;
        }
    }
}
