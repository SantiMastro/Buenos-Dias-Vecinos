namespace BuenosDias.Config
{
    /// <summary>
    /// Invariantes del proyecto. No son valores de balance: cambiarlos rompe la
    /// relación entre el arte y la cámara, así que no van a un ScriptableObject.
    /// </summary>
    public static class ProjectConstants
    {
        /// <summary>
        /// Píxeles por unidad de mundo.
        ///
        /// Es la única fuente de verdad del proyecto y tiene que coincidir con el
        /// PPU que fuerza <c>Assets/Editor/SpriteImportSettings.cs</c> al importar.
        /// Toda la spec del juego está escrita en píxeles; los ScriptableObjects
        /// exponen los valores en píxeles y convierten con esto.
        /// </summary>
        public const float PixelsPerUnit = 32f;

        /// <summary>Ancho de la resolución de referencia, en píxeles.</summary>
        public const int ReferenceWidth = 384;

        /// <summary>Alto de la resolución de referencia, en píxeles.</summary>
        public const int ReferenceHeight = 216;

        /// <summary>Convierte píxeles a unidades de mundo.</summary>
        public static float ToUnits(float pixels) => pixels / PixelsPerUnit;

        /// <summary>Convierte unidades de mundo a píxeles.</summary>
        public static float ToPixels(float units) => units * PixelsPerUnit;
    }
}
