namespace BuenosDias.Tests
{
    /// <summary>
    /// Un <see cref="System.Random"/> con el resultado fijado.
    ///
    /// Existe porque elegir una semilla no alcanza para que un test sea
    /// determinista: apenas el pity sube, la probabilidad contra la que se compara
    /// cambia, y una semilla que "nunca abría" pasa a abrir. Fijar el valor hace
    /// que el test mida la mecánica y no la suerte de la semilla.
    /// </summary>
    public sealed class FixedRandom : System.Random
    {
        private readonly double value;

        /// <summary>Nunca abre: cualquier probabilidad queda por debajo.</summary>
        public static FixedRandom NeverOpens => new FixedRandom(0.999999d);

        /// <summary>Abre siempre que la probabilidad sea mayor que cero.</summary>
        public static FixedRandom AlwaysOpens => new FixedRandom(0d);

        /// <summary>Fija el valor que devuelve toda tirada.</summary>
        public FixedRandom(double value) => this.value = value;

        /// <inheritdoc />
        public override double NextDouble() => value;

        /// <inheritdoc />
        protected override double Sample() => value;

        /// <inheritdoc />
        public override int Next(int maxValue) => 0;
    }
}
