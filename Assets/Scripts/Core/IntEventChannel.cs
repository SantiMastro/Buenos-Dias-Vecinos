using UnityEngine;

namespace BuenosDias.Core
{
    /// <summary>Canal que lleva un entero. Conversiones, seguidores.</summary>
    /// <remarks>
    /// Vive en su propio archivo porque Unity solo encuentra el script de un
    /// ScriptableObject si el archivo se llama igual que la clase. Agrupar
    /// varios canales en un mismo .cs deja los assets con el script roto.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "IntEventChannel", menuName = "Buenos Días/Evento/Int", order = 41)]
    public sealed class IntEventChannel : EventChannel<int> { }
}
