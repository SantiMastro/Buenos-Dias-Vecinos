using UnityEngine;

namespace BuenosDias.Core
{
    /// <summary>Canal que lleva un texto. Réplicas del vecino, carteles.</summary>
    /// <remarks>
    /// Vive en su propio archivo porque Unity solo encuentra el script de un
    /// ScriptableObject si el archivo se llama igual que la clase. Agrupar
    /// varios canales en un mismo .cs deja los assets con el script roto.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "StringEventChannel", menuName = "Buenos Días/Evento/String", order = 43)]
    public sealed class StringEventChannel : EventChannel<string> { }
}
