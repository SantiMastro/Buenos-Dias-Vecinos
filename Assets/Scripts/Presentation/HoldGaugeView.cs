using BuenosDias.Config;
using BuenosDias.Gameplay;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// El medidor del gesto mantenido, a los pies del predicador.
    ///
    /// Sin él, "mantené el botón para confirmar" es una regla que no está escrita
    /// en ninguna parte: el jugador aprieta, no pasa nada, suelta, y cambia de
    /// religión. La barra que se llena es lo que convierte una demora en una
    /// promesa.
    ///
    /// Se esconde sola cuando no hay gesto en curso, así que no hace falta que
    /// nadie la prenda y la apague por etapa.
    ///
    /// Se dibuja con <c>size</c> y no con escala, y a píxeles enteros, por lo mismo
    /// que la barra de tiempo: escalar en pixel art rompe la grilla.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HoldGaugeView : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Quien elige. De acá sale cuánto lleva el mantenido.")]
        [SerializeField] private ReligionSelector selector;

        [Tooltip("Fondo del medidor.")]
        [SerializeField] private SpriteRenderer track;

        [Tooltip("Relleno, lo que lleva cargado el gesto.")]
        [SerializeField] private SpriteRenderer fill;

        [Header("Medidas, en píxeles")]
        [Tooltip("Ancho del medidor.")]
        [SerializeField, Min(4f)] private float widthPixels = 28f;

        [Tooltip("Alto del medidor.")]
        [SerializeField, Min(1f)] private float heightPixels = 3f;

        [Tooltip("Altura sobre los pies del predicador. El sprite mide 40 px y el " +
                 "pivot está en los pies, así que por debajo de 40 el medidor le " +
                 "queda encima de la cara.")]
        [SerializeField] private float offsetYPixels = 46f;

        [Header("Colores")]
        [Tooltip("Fondo. Violeta oscuro #1D1638 de la paleta.")]
        [SerializeField] private Color trackColor = new Color32(0x1D, 0x16, 0x38, 0xFF);

        [Tooltip("Relleno. Hueso #F3ECE0 de la paleta.")]
        [SerializeField] private Color fillColor = new Color32(0xF3, 0xEC, 0xE0, 0xFF);

        private Sprite pixel;

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }

            pixel = SolidSprite.Create(Color.white, ProjectConstants.PixelsPerUnit);
            Dress(track, trackColor);
            Dress(fill, fillColor);

            track.size = Units(widthPixels);
            Show(false);

            Vector3 local = transform.localPosition;
            transform.localPosition = new Vector3(
                local.x, ProjectConstants.ToUnits(offsetYPixels), local.z);
        }

        private void OnDestroy()
        {
            SolidSprite.Dispose(pixel);
        }

        private void Dress(SpriteRenderer renderer, Color color)
        {
            renderer.sprite = pixel;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.color = color;
        }

        private void LateUpdate()
        {
            float progress = selector.HoldProgress;
            bool visible = progress > 0f;

            Show(visible);
            if (!visible) return;

            float filled = Mathf.Round(widthPixels * progress);
            fill.size = Units(filled);

            // Se llena desde la izquierda: el borde izquierdo queda clavado y lo
            // que crece es el ancho. Con el centro fijo crecería para los dos lados
            // y no se leería como algo que se está cargando.
            fill.transform.localPosition = new Vector3(
                ProjectConstants.ToUnits(filled - widthPixels) * 0.5f, 0f, 0f);
        }

        private void Show(bool visible)
        {
            track.enabled = visible;
            fill.enabled = visible;
        }

        private Vector2 Units(float pixels)
        {
            return new Vector2(
                ProjectConstants.ToUnits(pixels), ProjectConstants.ToUnits(heightPixels));
        }

        private bool ValidateSetup()
        {
            if (selector != null && track != null && fill != null) return true;

            Debug.LogError(
                $"[HoldGaugeView] '{name}' tiene referencias sin asignar " +
                "(selector, fondo o relleno).", this);
            return false;
        }
    }
}
