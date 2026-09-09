using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.Gameplay
{
    /// <summary>
    /// Un lugar reutilizable donde puede ir un prop de señal, con su capa de luz
    /// opcional.
    ///
    /// Existe como slot fijo del prefab y no como algo que se instancia: el
    /// spawner recicla casas todo el tiempo y crear objetos por señal metería
    /// basura en cada scroll.
    /// </summary>
    [System.Serializable]
    public sealed class SignalPropSlot
    {
        [Tooltip("Contenedor del slot. Es lo ÚNICO que se mueve: si se moviera el " +
                 "sprite del prop, la capa de luz se quedaría atrás en el origen.")]
        [SerializeField] private Transform root;

        [Tooltip("Sprite del prop en sí.")]
        [SerializeField] private SpriteRenderer prop;

        [Tooltip("Capa de luz que se dibuja encima, con material aditivo. " +
                 "La usan el farol de entrada y el TV.")]
        [SerializeField] private SpriteRenderer glow;

        [Tooltip("Animator de la capa de luz. Solo lo usa el parpadeo del TV; " +
                 "para el farol queda apagado.")]
        [SerializeField] private Animator glowAnimator;

        /// <summary>Apaga el slot.</summary>
        public void Hide()
        {
            if (prop != null) prop.enabled = false;
            if (glow != null) glow.enabled = false;
            if (glowAnimator != null) glowAnimator.enabled = false;
        }

        /// <summary>Muestra una señal en este slot, en la posición local indicada.</summary>
        public void Show(HouseSignalDefinition signal, Vector3 localPosition)
        {
            if (prop == null) return;

            if (root != null) root.localPosition = localPosition;

            prop.enabled = true;
            prop.sprite = signal.Sprite;

            if (glow == null) return;

            bool hasGlow = signal.HasAdditiveLayer;
            glow.enabled = hasGlow;

            if (hasGlow)
            {
                glow.sprite = signal.AdditiveLayer;
                CenterGlowOn(signal.Sprite);
            }

            if (glowAnimator == null) return;

            // El animator solo se prende si esta señal trae animación de capa.
            // El halo del farol es una luz quieta y no necesita máquina de estados.
            bool animated = hasGlow && signal.AdditiveLayerAnimation != null;
            glowAnimator.enabled = animated;
            if (animated) glowAnimator.Play(0, 0, 0f);
        }

        /// <summary>
        /// Sube la capa de luz hasta el centro del prop.
        ///
        /// Hace falta porque los pivots no coinciden: los props importan con
        /// BottomCenter (prefijo <c>prop_</c>) y los sprites de luz con Center
        /// (<c>fx_</c>). Sin esto el halo queda centrado en la BASE del farol y
        /// se lee como una estrella suelta, despegada del farol.
        /// </summary>
        private void CenterGlowOn(Sprite propSprite)
        {
            if (propSprite == null) return;

            float height = propSprite.rect.height / propSprite.pixelsPerUnit;
            Vector3 local = glow.transform.localPosition;
            glow.transform.localPosition = new Vector3(local.x, height * 0.5f, local.z);
        }
    }
}
