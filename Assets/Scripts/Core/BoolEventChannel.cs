using UnityEngine;

namespace BuenosDias.Core
{
    /// <summary>Canal que lleva un booleano. Casa ocupada o vacía.</summary>
    /// <remarks>
    /// Vive en su propio archivo porque Unity solo encuentra el script de un
    /// ScriptableObject si el archivo se llama igual que la clase. Agrupar
    /// varios canales en un mismo .cs deja los assets con el script roto.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "BoolEventChannel", menuName = "Buenos Días/Evento/Bool", order = 44)]
    public sealed class BoolEventChannel : EventChannel<bool> { }
}
