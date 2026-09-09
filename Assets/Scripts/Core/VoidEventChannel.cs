using UnityEngine;

namespace BuenosDias.Core
{
    /// <summary>Canal sin carga útil. Timbre tocado, puerta abierta, día terminado.</summary>
    /// <remarks>
    /// Vive en su propio archivo porque Unity solo encuentra el script de un
    /// ScriptableObject si el archivo se llama igual que la clase. Agrupar
    /// varios canales en un mismo .cs deja los assets con el script roto.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "VoidEventChannel", menuName = "Buenos Días/Evento/Void", order = 42)]
    public sealed class VoidEventChannel : EventChannel { }
}
