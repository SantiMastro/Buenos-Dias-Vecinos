namespace BuenosDias.Config
{
    /// <summary>
    /// Forma de la pista del skillcheck. La aguja y la zona siguen siendo ángulos
    /// (la simulación no cambia): la forma solo decide por dónde corre la aguja
    /// en pantalla.
    /// </summary>
    public enum SkillcheckShape
    {
        /// <summary>El aro de siempre.</summary>
        Circulo,

        /// <summary>Un cuadrado: la aguja barre desde el centro hasta el borde.</summary>
        Cuadrado,

        /// <summary>Un triángulo equilátero apuntando hacia arriba.</summary>
        Triangulo,

        /// <summary>Rombo: un cuadrado parado sobre la punta.</summary>
        Rombo,

        /// <summary>Pentágono regular con un vértice arriba.</summary>
        Pentagono,

        /// <summary>Hexágono regular con un vértice arriba.</summary>
        Hexagono,

        /// <summary>Octógono regular.</summary>
        Octagono,

        /// <summary>Estrella de cinco puntas: la pista se mete y sale del centro.</summary>
        Estrella
    }
}
