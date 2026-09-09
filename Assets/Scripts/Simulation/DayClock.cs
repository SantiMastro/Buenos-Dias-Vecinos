using UnityEngine;

namespace BuenosDias.Simulation
{
    /// <summary>
    /// El reloj del día: cuánto queda, cuánto se hizo de noche, y si terminó.
    ///
    /// Es una clase plana porque el reloj es la economía entera del juego: cada
    /// puerta cuesta tiempo y cada conversión lo devuelve. Con eso adentro de un
    /// <c>MonoBehaviour</c> no se podría simular una partida sin entrar en Play.
    ///
    /// Hay DOS progresos y no uno, que es la decisión que más importa acá:
    /// <see cref="Progress"/> es el instantáneo y sube y baja con los bonos;
    /// <see cref="SunsetProgress"/> nunca baja. Los visuales usan el segundo
    /// porque un cielo que se aclara al convertir se lee como un bug, no como un
    /// premio: el sol no vuelve para atrás.
    /// </summary>
    public sealed class DayClock
    {
        private readonly float duration;
        private readonly bool frozen;

        /// <summary>Segundos que quedan de día.</summary>
        public float Remaining { get; private set; }

        /// <summary>
        /// Cuánto se consumió el día, de 0 a 1. Baja si se gana tiempo, así que
        /// sirve para la barra pero NO para el cielo.
        /// </summary>
        public float Progress => 1f - Mathf.Clamp01(Remaining / duration);

        /// <summary>
        /// Lo más oscuro que llegó a estar el día. Nunca baja. Es el que va a los
        /// gradientes de cielo y luz.
        /// </summary>
        public float SunsetProgress { get; private set; }

        /// <summary>Si el día ya terminó.</summary>
        public bool IsOver => Remaining <= 0f;

        /// <summary>
        /// Arranca el día. <paramref name="frozen"/> es el modo debug de
        /// <c>GameConfig</c>: el reloj no corre, para poder probar dificultad alta
        /// sin tener que ser bueno.
        /// </summary>
        public DayClock(float durationSeconds, bool frozen = false)
        {
            duration = Mathf.Max(1f, durationSeconds);
            this.frozen = frozen;
            Remaining = duration;
        }

        /// <summary>
        /// Corre el reloj. Devuelve <c>true</c> en el ÚNICO cuadro en que el día
        /// termina, para que quien escuche dispare el final una sola vez sin tener
        /// que llevar su propio flag.
        /// </summary>
        public bool Advance(float deltaTime)
        {
            if (frozen || IsOver) return false;

            Remaining = Mathf.Max(0f, Remaining - deltaTime);
            Track();

            return IsOver;
        }

        /// <summary>
        /// Suma o resta segundos: positivo por convertir, negativo por fallar.
        ///
        /// El techo es la duración inicial. Sin techo, encadenar conversiones
        /// dejaría un colchón que ya no se puede gastar y la presión del reloj
        /// —que es de lo que se trata el juego— desaparecería para el buen
        /// jugador. Con techo, el bono se cobra en el momento en que hace falta.
        /// </summary>
        public void Add(float seconds)
        {
            if (IsOver) return;

            Remaining = Mathf.Clamp(Remaining + seconds, 0f, duration);
            Track();
        }

        private void Track()
        {
            if (Progress > SunsetProgress) SunsetProgress = Progress;
        }
    }
}
