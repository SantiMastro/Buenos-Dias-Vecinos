using UnityEngine;

namespace BuenosDias.DebugTools
{
    /// <summary>
    /// Andamio de la fase 1: mueve el transform a velocidad constante para poder
    /// verificar el scroll, el parallax y la nitidez sin tener todavía al jugador.
    ///
    /// Lo reemplaza <c>PreacherMotor</c> en la fase 5.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ConstantMover : MonoBehaviour
    {
        [Header("Movimiento")]
        [Tooltip("Velocidad horizontal en píxeles por segundo. 152 px/s es la " +
                 "velocidad base del predicador según la spec.")]
        [SerializeField] private float pixelsPerSecond = 152f;

        [Tooltip("Píxeles por unidad del proyecto. Tiene que coincidir con el PPU de " +
                 "los sprites (32) o la velocidad no va a ser la real.")]
        [SerializeField] private float pixelsPerUnit = 32f;

        /// <summary>Velocidad efectiva en unidades de mundo por segundo.</summary>
        public float UnitsPerSecond => pixelsPerSecond / pixelsPerUnit;

        private void Update()
        {
            transform.position += Vector3.right * (UnitsPerSecond * Time.deltaTime);
        }
    }
}
