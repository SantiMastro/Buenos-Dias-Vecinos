using System.Collections.Generic;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// El gesto de "el botón" cuando el botón pueden ser varios: cualquier botón
    /// aceptado lo empieza y el gesto dura hasta que se sueltan TODOS.
    ///
    /// ⚠️ Antes se guardaba un solo control, el último. Con el control físico o con
    /// teclado y mouse a la vez, apretar un segundo botón mientras el primero
    /// seguía apretado reiniciaba el mantenido, y al soltar el segundo aparecía un
    /// toque que nadie dio: se cambiaba de religión o se tocaba un timbre solo.
    ///
    /// Es genérico y plano para poder testear el solapamiento sin dispositivos: el
    /// <c>MonoBehaviour</c> le pasa controles del Input System y los tests le
    /// pasan cualquier cosa que sepa decir si está apretada.
    /// </summary>
    public sealed class ButtonGesture<T> where T : class
    {
        private readonly List<T> held = new List<T>(4);
        private readonly System.Func<T, bool> isPressed;

        /// <summary>Cuándo empezó el gesto en curso, en el reloj que se le pase.</summary>
        public float StartTime { get; private set; }

        /// <summary>Último botón apretado. Lo lee la ventana de diagnóstico.</summary>
        public T Last { get; private set; }

        /// <summary>Toma cómo preguntar si un botón sigue apretado.</summary>
        public ButtonGesture(System.Func<T, bool> isPressed) => this.isPressed = isPressed;

        /// <summary>
        /// Si algún botón del gesto sigue apretado. Se calcula al consultarlo, así
        /// que no depende de en qué orden corra nadie.
        /// </summary>
        public bool IsHeld
        {
            get
            {
                for (int i = 0; i < held.Count; i++)
                    if (isPressed(held[i])) return true;

                return false;
            }
        }

        /// <summary>
        /// Registra un apretón. Si no había nada apretado es un gesto NUEVO y el
        /// reloj arranca de cero; si ya había uno, el botón se suma al gesto sin
        /// tocar el reloj.
        /// </summary>
        public void Press(T button, float now)
        {
            if (!IsHeld)
            {
                held.Clear();
                StartTime = now;
            }

            if (!held.Contains(button)) held.Add(button);
            Last = button;
        }
    }
}
