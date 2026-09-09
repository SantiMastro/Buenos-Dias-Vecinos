using System;
using UnityEngine;

namespace BuenosDias.Core
{
    /// <summary>
    /// Canal de eventos con carga útil, como asset.
    ///
    /// Resuelve el nudo entre tres reglas del proyecto: la UI escucha al juego,
    /// el juego no conoce la UI, y no hay singletons ni <c>FindObjectOfType</c>.
    /// Un <c>UnityEvent</c> en el Inspector obligaría al juego a tener una
    /// referencia a la UI. Acá el juego levanta el evento sobre un asset y la UI
    /// se suscribe a ese mismo asset: ninguno de los dos conoce al otro, y el
    /// cableado se ve arrastrando assets en el Inspector.
    /// </summary>
    /// <typeparam name="T">Tipo de la carga útil.</typeparam>
    public abstract class EventChannel<T> : ScriptableObject
    {
        private Action<T> listeners;

        /// <summary>Cantidad de suscriptores. Solo para diagnóstico.</summary>
        public int ListenerCount => listeners?.GetInvocationList().Length ?? 0;

        /// <summary>Notifica a todos los suscriptores.</summary>
        public void Raise(T payload) => listeners?.Invoke(payload);

        /// <summary>Se suscribe al canal.</summary>
        public void Subscribe(Action<T> listener) => listeners += listener;

        /// <summary>Se da de baja del canal.</summary>
        public void Unsubscribe(Action<T> listener) => listeners -= listener;

        /// <summary>
        /// Los ScriptableObject sobreviven a salir de Play Mode en el Editor, así
        /// que un suscriptor de la corrida anterior quedaría colgado apuntando a
        /// un objeto destruido. Limpiar al habilitar evita ese fantasma.
        /// </summary>
        protected virtual void OnEnable() => listeners = null;

        /// <summary>Limpia los suscriptores al descargarse.</summary>
        protected virtual void OnDisable() => listeners = null;
    }

    /// <summary>Canal sin carga útil: solo avisa que algo pasó.</summary>
    public abstract class EventChannel : ScriptableObject
    {
        private Action listeners;

        /// <summary>Cantidad de suscriptores. Solo para diagnóstico.</summary>
        public int ListenerCount => listeners?.GetInvocationList().Length ?? 0;

        /// <summary>Notifica a todos los suscriptores.</summary>
        public void Raise() => listeners?.Invoke();

        /// <summary>Se suscribe al canal.</summary>
        public void Subscribe(Action listener) => listeners += listener;

        /// <summary>Se da de baja del canal.</summary>
        public void Unsubscribe(Action listener) => listeners -= listener;

        /// <summary>Limpia suscriptores fantasma de la corrida anterior.</summary>
        protected virtual void OnEnable() => listeners = null;

        /// <summary>Limpia los suscriptores al descargarse.</summary>
        protected virtual void OnDisable() => listeners = null;
    }
}
