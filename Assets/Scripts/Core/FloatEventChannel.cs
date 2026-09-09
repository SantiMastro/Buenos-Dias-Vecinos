using UnityEngine;

namespace BuenosDias.Core
{
    /// <summary>Canal que lleva un número con coma. Tiempo restante, precisión.</summary>
    /// <remarks>
    /// Vive en su propio archivo porque Unity solo encuentra el script de un
    /// ScriptableObject si el archivo se llama igual que la clase. Agrupar
    /// varios canales en un mismo .cs deja los assets con el script roto.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "FloatEventChannel", menuName = "Buenos Días/Evento/Float", order = 40)]
    public sealed class FloatEventChannel : EventChannel<float> { }
}
