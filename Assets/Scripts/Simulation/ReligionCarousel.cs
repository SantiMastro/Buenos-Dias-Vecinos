namespace BuenosDias.Simulation
{
    /// <summary>
    /// El índice que cicla la lista de religiones con un solo botón.
    ///
    /// Trabaja sobre una CANTIDAD, no sobre la lista: así el criterio 10 —agregar
    /// una religión es crear el asset y sumarlo a <c>GameConfig</c>— se cumple sin
    /// que esta clase sepa qué es una religión, y se puede testear sin assets.
    ///
    /// Da la vuelta al llegar al final. Con un solo botón no hay "atrás": la única
    /// forma de volver a la primera es seguir avanzando, así que ciclar es la
    /// diferencia entre poder cambiar de opinión y quedarse trabado en la última.
    /// </summary>
    public sealed class ReligionCarousel
    {
        private readonly int count;

        /// <summary>Cuántas opciones cicla.</summary>
        public int Count => count;

        /// <summary>
        /// Opción actual. Vale <c>-1</c> si no hay ninguna, para que un catálogo
        /// vacío no devuelva el índice 0 y termine en un IndexOutOfRange lejos de
        /// acá.
        /// </summary>
        public int Index { get; private set; }

        /// <summary>Si no hay ninguna opción para elegir.</summary>
        public bool IsEmpty => count <= 0;

        /// <summary>Arma el ciclo sobre esa cantidad de opciones.</summary>
        public ReligionCarousel(int count)
        {
            this.count = count > 0 ? count : 0;
            Index = IsEmpty ? -1 : 0;
        }

        /// <summary>Pasa a la siguiente y la devuelve. Da la vuelta al final.</summary>
        public int Next()
        {
            if (IsEmpty) return -1;

            Index = (Index + 1) % count;
            return Index;
        }
    }
}
