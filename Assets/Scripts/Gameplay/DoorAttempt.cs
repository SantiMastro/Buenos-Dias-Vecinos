using BuenosDias.Config;
using BuenosDias.Simulation;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// Una puerta: quién vive ahí, con cuánta puntería se tocó, cuántas veces se
    /// insistió y qué va pasando durante la espera.
    ///
    /// ⚠️ **La ocupación se tira UNA SOLA VEZ**, al primer timbrazo, y no se vuelve
    /// a tocar nunca. Insistir no vuelve a preguntar *si hay alguien*: pregunta si
    /// el que está adentro se decide a abrir.
    ///
    /// El motivo es que, si cada insistencia volviera a tirar la ocupación, la
    /// probabilidad acumulada tendería a 1 y insistir SIEMPRE terminaría
    /// funcionando. Las señales dejarían de informar un riesgo para informar un
    /// costo de tiempo, y leer la casa pagaría menos en vez de más.
    ///
    /// Con una sola tirada aparecen tres desenlaces en vez de dos, y el del medio
    /// es el interesante:
    ///
    /// <list type="bullet">
    /// <item>Abren → skillcheck.</item>
    /// <item>La cortina se movió y no abren → hay alguien y te está ignorando.</item>
    /// <item>No pasó nada → la casa está vacía, insistir es tiempo tirado.</item>
    /// </list>
    ///
    /// El asomo de cortina es lo que los separa, porque solo dispara si hay
    /// alguien. Insistir deja de ser una apuesta y pasa a ser una lectura.
    /// </summary>
    public sealed class DoorAttempt
    {
        private readonly WaitConfig waitConfig;
        private readonly DoorbellConfig doorbellConfig;
        private readonly ReligionDefinition religion;
        private readonly System.Random random;

        private WaitTimeline timeline;

        /// <summary>Casa cuyo timbre se tocó.</summary>
        public HouseInstance House { get; }

        /// <summary>Puntería del timbrazo, 0..1. Es la del primero y no cambia al insistir.</summary>
        public float Precision { get; }

        /// <summary>Quién vive ahí. Se resolvió una sola vez.</summary>
        public HouseOccupancy Occupancy { get; }

        /// <summary>Si hay alguien en la casa. No cambia por insistir.</summary>
        public bool SomeoneHome => Occupancy.IsOccupied;

        /// <summary>El vecino que atiende. <c>null</c> si la casa está vacía.</summary>
        public NeighborDefinition Neighbor => Occupancy.Neighbor;

        /// <summary>Cuántas veces se insistió. El primer timbrazo es 0.</summary>
        public int Insists { get; private set; }

        /// <summary>Timbrazos dados, contando el primero.</summary>
        public int Attempts => Insists + 1;

        /// <summary>Si ESTE timbrazo va a terminar con la puerta abierta.</summary>
        public bool WillOpen { get; private set; }

        /// <summary>
        /// Si el jugador ya vio moverse la cortina. Es lo que le dice que hay
        /// alguien adentro, y por lo tanto si insistir tiene sentido.
        /// </summary>
        public bool TellShown => timeline != null && timeline.TellShown;

        /// <summary>Si todavía se puede tocar de nuevo.</summary>
        public bool CanInsist => doorbellConfig.CanRingAgain(Attempts);

        private DoorAttempt(
            HouseInstance house, float precision, HouseOccupancy occupancy,
            WaitConfig waitConfig, DoorbellConfig doorbellConfig,
            ReligionDefinition religion, System.Random random)
        {
            House = house;
            Precision = precision;
            Occupancy = occupancy;
            this.waitConfig = waitConfig;
            this.doorbellConfig = doorbellConfig;
            this.religion = religion;
            this.random = random;
        }

        /// <summary>
        /// Toca el timbre por primera vez y arma la puerta.
        ///
        /// ⚠️ El orden de las tiradas es parte del contrato y todas salen del MISMO
        /// <c>System.Random</c>: primero la ocupación, después si abre, después la
        /// duración. Cambiar el orden cambia todas las partidas de una semilla dada.
        ///
        /// ⚠️ La tirada de "¿abre?" es NUEVA respecto de la versión de un botón, así
        /// que las mediciones viejas con semilla fija no se reproducen más.
        /// </summary>
        public static DoorAttempt Ring(
            HouseInstance house, float precision, HouseOccupancyResolver resolver,
            System.Random random, WaitConfig waitConfig, DoorbellConfig doorbellConfig,
            ReligionDefinition religion)
        {
            HouseOccupancy occupancy = resolver.Resolve(house.Layout, random);

            var attempt = new DoorAttempt(
                house, precision, occupancy, waitConfig, doorbellConfig, religion, random);

            attempt.StartWait();
            return attempt;
        }

        /// <summary>
        /// Vuelve a tocar la misma puerta. La ocupación NO se vuelve a tirar: lo
        /// único que se sortea de nuevo es si el que está adentro abre esta vez.
        /// </summary>
        public void Insist()
        {
            Insists++;
            StartWait();
        }

        /// <summary>
        /// Sortea si abren en este timbrazo y arranca el reloj de la espera.
        ///
        /// La cortina asoma si HAY ALGUIEN, no si va a abrir. Esa es toda la
        /// mecánica: el asomo delata la ocupación, que es un hecho fijo, y deja al
        /// jugador decidir si gasta otro timbrazo en alguien que ya sabe que está.
        /// </summary>
        private void StartWait()
        {
            WillOpen = SomeoneHome
                       && random.NextDouble() < doorbellConfig.OpenChanceAt(Insists);

            float duration = waitConfig.RollDuration(random, religion)
                             * doorbellConfig.WaitScaleAt(Insists);

            timeline = new WaitTimeline(waitConfig, duration, SomeoneHome);
        }

        /// <summary>
        /// Avanza la espera un cuadro.
        ///
        /// El asomo lo dispara acá y no quien llama: la cortina es de la casa, y
        /// quien maneja los estados del predicador no tiene por qué saber que una
        /// casa tiene un Animator.
        /// </summary>
        public WaitTick Advance(float deltaTime)
        {
            WaitTick tick = timeline.Advance(deltaTime);
            if (tick.TellFired) House.PlayTell();

            return tick;
        }
    }
}
