using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// El frenazo sobre el felpudo: a qué puerta se va y con cuánta puntería se
    /// frenó.
    ///
    /// ⚠️ La puntería se congela **en el instante en que el pie toca la plancha**,
    /// no cuando el predicador llega a la puerta. Esa es toda la mecánica: la
    /// precisión sigue siendo del jugador, pero el personaje ya no se queda parado
    /// a tres metros hablándole al aire. Si midiéramos al llegar, todas las puertas
    /// darían puntería perfecta y el sistema entero se caería.
    ///
    /// Existe como tipo propio y no como tres campos sueltos del controlador
    /// porque los tres solo tienen sentido juntos y solo durante un estado. Sueltos,
    /// nada impedía leer la puntería mientras el predicador caminaba por la vereda.
    /// </summary>
    public readonly struct DoorApproach
    {
        /// <summary>Casa a cuya puerta se va. <c>null</c> si se frenó lejos de todas.</summary>
        public HouseInstance House { get; }

        /// <summary>Puntería del frenazo, 0..1, medida al pisar.</summary>
        public float Precision { get; }

        /// <summary>Dónde está la puerta, en X del mundo.</summary>
        public float DoorX { get; }

        /// <summary>Si hay una puerta a la que ir.</summary>
        public bool HasDoor => House != null;

        private DoorApproach(HouseInstance house, float precision, float doorX)
        {
            House = house;
            Precision = precision;
            DoorX = doorX;
        }

        /// <summary>Frenar lejos de cualquier puerta: no hay adónde ir.</summary>
        public static DoorApproach Nowhere => new DoorApproach(null, 0f, 0f);

        /// <summary>
        /// Resuelve el frenazo. <paramref name="signedDistance"/> es negativa antes
        /// de la puerta y positiva después, así que pasarse queda contemplado sin
        /// ningún caso especial: el predicador simplemente camina para el otro lado.
        ///
        /// ⚠️ <paramref name="pace"/> DIVIDE la distancia antes de preguntar, y con
        /// eso escala la hitbox entera —el alcance y la curva de puntería— con un
        /// solo número. Es lo que hace que la ventana de frenado dure lo mismo EN
        /// SEGUNDOS cuando el día acelera: sin esto, correr más rápido la acortaría
        /// en tiempo y la dificultad subiría por dos vías a la vez sin que nadie lo
        /// haya decidido.
        ///
        /// Dicho de otra manera: dividida por el ritmo, la distancia deja de ser
        /// distancia y pasa a ser TIEMPO hasta la puerta, que es lo que el jugador
        /// está midiendo con el pie.
        /// </summary>
        public static DoorApproach At(
            HouseInstance house, float preacherX, float signedDistance,
            DoorbellConfig config, float pace)
        {
            if (house == null) return Nowhere;

            float measured = signedDistance / Mathf.Max(0.01f, pace);
            if (!config.IsInReach(measured)) return Nowhere;

            // ⚠️ La puerta se ubica con la distancia REAL, no con la medida: lo que
            // se escala es la ventana de decisión, no la geometría de la cuadra.
            return new DoorApproach(
                house, config.PrecisionFor(measured), preacherX - signedDistance);
        }

        /// <summary>Si a esa X ya se puede dar por llegado.</summary>
        public bool Arrived(float x, float tolerance)
        {
            return !HasDoor || Mathf.Abs(x - DoorX) <= tolerance;
        }

        /// <summary>La X del cuadro siguiente, caminando hacia la puerta.</summary>
        public float Step(float x, float speed, float deltaTime)
        {
            return HasDoor ? Mathf.MoveTowards(x, DoorX, speed * deltaTime) : x;
        }
    }
}
