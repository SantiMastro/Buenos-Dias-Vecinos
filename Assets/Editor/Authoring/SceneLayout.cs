namespace BuenosDias.EditorTools.Authoring
{
    /// <summary>
    /// Medidas con las que se construye la escena. Son constantes de autoría, no
    /// de balance: definen cómo se apilan las bandas verticales del cuadro, algo
    /// que viene dado por el tamaño de los sprites y no se tunea jugando.
    ///
    /// El origen del mundo (Y = 0) es la LÍNEA DE CAMINATA, o sea el tope de la
    /// vereda, que es donde apoyan los personajes y donde arranca la pared.
    ///
    /// Reparto vertical del cuadro de 216 px:
    ///   calle     40 px   →  Y  -2.00 .. -1.25
    ///   vereda    24 px   →  Y  -1.25 .. -0.75   (tope de vereda en 0 tras el corrimiento)
    ///   pared     64 px   →  Y   0.00 ..  2.00
    ///   techo     48 px   →  Y   2.00 ..  3.50
    ///   cielo     40 px   →  Y   3.50 ..  4.75
    /// </summary>
    public static class SceneLayout
    {
        /// <summary>Píxeles por unidad del proyecto. Lo fuerza SpriteImportSettings.</summary>
        public const float PixelsPerUnit = 32f;

        /// <summary>Ancho de la resolución de referencia, en píxeles.</summary>
        public const int ReferenceWidth = 384;

        /// <summary>Alto de la resolución de referencia, en píxeles.</summary>
        public const int ReferenceHeight = 216;

        /// <summary>Convierte píxeles a unidades de mundo.</summary>
        public static float Px(float pixels) => pixels / PixelsPerUnit;

        // --- Bandas verticales -------------------------------------------------

        /// <summary>Alto de la vereda: 24 px.</summary>
        public static float SidewalkHeight => Px(24f);

        /// <summary>Alto de la calle: 40 px.</summary>
        public static float StreetHeight => Px(40f);

        /// <summary>Alto de la pared de una casa: 64 px.</summary>
        public static float WallHeight => Px(64f);

        /// <summary>Borde inferior del cuadro. La calle apoya justo acá.</summary>
        public static float ScreenBottomY => -(SidewalkHeight + StreetHeight);

        /// <summary>Altura del centro de la cámara para que el borde inferior calce.</summary>
        public static float CameraCenterY => ScreenBottomY + Px(ReferenceHeight) * 0.5f;

        /// <summary>Media altura ortográfica: 216 / 2 / 32 = 3.375.</summary>
        public static float OrthographicSize => Px(ReferenceHeight) * 0.5f;

        /// <summary>Base del skyline lejano: asoma por encima de los techos.</summary>
        public static float SkylineBottomY => WallHeight;

        // --- Profundidades en Z ------------------------------------------------
        // No definen el orden de dibujo (eso lo hacen los sorting layers), pero
        // mantienen las capas separadas y hacen legible la escena en vista 3D.

        /// <summary>Z de la cámara.</summary>
        public const float CameraZ = -10f;

        /// <summary>Z del skyline lejano.</summary>
        public const float BackgroundZ = 10f;

        /// <summary>Z de árboles y postes.</summary>
        public const float FarZ = 5f;

        /// <summary>Z del suelo (vereda y calle).</summary>
        public const float GroundZ = 0.5f;

        /// <summary>Z de las casas.</summary>
        public const float HousesZ = 0f;

        /// <summary>Z de los postes de luz y demás mobiliario de vereda.</summary>
        public const float StreetFurnitureZ = -0.5f;

        /// <summary>Z de los personajes.</summary>
        public const float CharactersZ = -1f;

        /// <summary>Z de los arbustos de frente.</summary>
        public const float ForegroundZ = -3f;

        // --- Factores de parallax ---------------------------------------------

        /// <summary>Parallax del skyline lejano.</summary>
        public const float SkylineParallax = 0.18f;

        /// <summary>Parallax de árboles y postes.</summary>
        public const float FarParallax = 0.55f;

        /// <summary>Parallax del plano de juego: sin desplazamiento.</summary>
        public const float WorldParallax = 1f;

        /// <summary>Parallax de los arbustos de frente.</summary>
        public const float ForegroundParallax = 1.25f;
    }
}
