namespace BuenosDias.Gameplay
{
    /// <summary>Qué hicieron A y B en este cuadro, ya arbitrado.</summary>
    public readonly struct TwoButtonFrame
    {
        /// <summary>A dispara en este cuadro. Su significado lo pone el contexto.</summary>
        public bool APulse { get; }

        /// <summary>B confirmó la religión.</summary>
        public bool Confirmed { get; }

        /// <summary>B sube o baja del felpudo.</summary>
        public bool ToggleMat { get; }

        /// <summary>Arma el resultado de un cuadro.</summary>
        public TwoButtonFrame(bool aPulse, bool confirmed, bool toggleMat)
        {
            APulse = aPulse;
            Confirmed = confirmed;
            ToggleMat = toggleMat;
        }
    }

    /// <summary>
    /// Decide qué vale cuando A y B llegan juntos en la modalidad de dos botones.
    ///
    /// ⚠️ Sin esto, un solo apretón podía FRENAR y TOCAR EL TIMBRE a la vez: B
    /// sube al felpudo y el predicador queda en la puerta, y un A que llega en ese
    /// mismo cuadro —o unos milisegundos después— se lee como timbrazo. Pasa cuando
    /// dos controles mandan la misma señal, cuando comparten masa y un contacto
    /// arrastra al otro, o cuando A y B quedaron atados a la misma tecla.
    ///
    /// La regla: **mientras B cambia el estado, gana B**, y durante una ventana
    /// corta A no dispara. Frenar es la intención: nadie quiere tocar un timbre en
    /// el mismo instante en que decide subirse.
    ///
    /// Hay dos contextos donde B NO manda, a propósito:
    /// <list type="bullet">
    /// <item><b>Atendido</b>: con la puerta abierta B no hace nada mecánico hasta
    /// que termina la cadena, y comerse el golpe del skillcheck por eso sería
    /// castigar un acierto.</item>
    /// <item><b>Final</b>: ahí B no significa nada y A reinicia.</item>
    /// </list>
    ///
    /// Es una clase plana para poder testear la ventana sin dispositivos.
    /// </summary>
    public sealed class TwoButtonArbiter
    {
        private readonly float coincidenceSeconds;
        private float lastBTime = float.NegativeInfinity;

        /// <summary>
        /// <paramref name="coincidenceSeconds"/> es cuánto después de B se sigue
        /// descartando A. En 0 solo se descarta el A del mismo cuadro.
        /// </summary>
        public TwoButtonArbiter(float coincidenceSeconds)
        {
            this.coincidenceSeconds = coincidenceSeconds < 0f ? 0f : coincidenceSeconds;
        }

        /// <summary>
        /// Resuelve un cuadro. <paramref name="now"/> tiene que ser tiempo NO
        /// escalado, para que una pausa no le cambie el largo a la ventana.
        /// </summary>
        public TwoButtonFrame Resolve(bool aPressed, bool bPressed, InputContext context, float now)
        {
            if (bPressed) lastBTime = now;

            bool choosing = context == InputContext.Seleccion;
            bool bRecent = now - lastBTime <= coincidenceSeconds;
            bool aPulse = aPressed && !(bRecent && BDominates(context));

            return new TwoButtonFrame(aPulse, bPressed && choosing, bPressed && !choosing);
        }

        private static bool BDominates(InputContext context)
        {
            return context != InputContext.Atendido && context != InputContext.Final;
        }
    }
}
