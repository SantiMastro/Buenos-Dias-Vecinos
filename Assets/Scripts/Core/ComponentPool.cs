using System.Collections.Generic;
using UnityEngine;

namespace BuenosDias.Core
{
    /// <summary>
    /// Pool de componentes con tamaño fijo.
    ///
    /// El juego recicla casas y seguidores continuamente; hacer Instantiate y
    /// Destroy en cada scroll generaría basura constante y picos de GC a mitad
    /// de un skillcheck.
    /// </summary>
    /// <typeparam name="T">Componente que cuelga del prefab.</typeparam>
    public sealed class ComponentPool<T> where T : Component
    {
        private readonly Queue<T> available = new Queue<T>();
        private readonly List<T> all = new List<T>();

        /// <summary>Cuántas instancias creó en total.</summary>
        public int Capacity => all.Count;

        /// <summary>Cuántas están libres ahora mismo.</summary>
        public int AvailableCount => available.Count;

        /// <summary>
        /// Instancia el pool entero de una. Se llama en el arranque, nunca a
        /// mitad de partida.
        /// </summary>
        public ComponentPool(T prefab, int size, Transform parent)
        {
            for (int i = 0; i < size; i++)
            {
                T instance = Object.Instantiate(prefab, parent);
                instance.name = $"{prefab.name}_{i:00}";
                instance.gameObject.SetActive(false);
                available.Enqueue(instance);
                all.Add(instance);
            }
        }

        /// <summary>
        /// Saca una instancia libre, o <c>null</c> si se agotaron.
        /// Devolver null en vez de agrandar el pool es deliberado: quedarse corto
        /// es un error de dimensionamiento y conviene que se note.
        /// </summary>
        public T Get()
        {
            if (available.Count == 0) return null;

            T instance = available.Dequeue();
            instance.gameObject.SetActive(true);
            return instance;
        }

        /// <summary>Devuelve una instancia al pool.</summary>
        public void Release(T instance)
        {
            if (instance == null) return;
            instance.gameObject.SetActive(false);
            available.Enqueue(instance);
        }

        /// <summary>Devuelve todas las instancias al pool.</summary>
        public void ReleaseAll()
        {
            available.Clear();
            foreach (T instance in all)
            {
                instance.gameObject.SetActive(false);
                available.Enqueue(instance);
            }
        }
    }
}
