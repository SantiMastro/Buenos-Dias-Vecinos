using BuenosDias.Gameplay;
using BuenosDias.Presentation;
using UnityEngine;

namespace BuenosDias.EditorTools.Authoring
{
    /// <summary>
    /// Arma el aro del skillcheck y lo cuelga de la cámara.
    ///
    /// Va de la cámara y no del mundo a propósito: el skillcheck es lectura del
    /// jugador, no un objeto de la calle. Colgado del mundo se correría con el
    /// parallax justo cuando hay que apuntarle, que es el mismo error que se
    /// corrigió con el árbol en la fase 4.
    /// </summary>
    public static class SkillcheckRigBuilder
    {
        /// <summary>Lado del PNG del aro. La zona se pinta sobre esta misma grilla.</summary>
        private const int TextureSize = 128;

        /// <summary>
        /// Radios del canal libre entre los dos círculos de <c>ui_skillcheck_aro</c>,
        /// medidos sobre el PNG: el círculo interno cae en 45 px y el externo en 56,
        /// así que la zona entra entre 46 y 55 sin pisar ninguno de los dos trazos.
        /// </summary>
        private const float TrackInnerRadius = 46f;

        /// <summary>Radio externo del canal.</summary>
        private const float TrackOuterRadius = 55f;

        /// <summary>
        /// Alto del centro del aro respecto del centro de la cámara, en píxeles.
        /// Sube sobre la línea de techos para no taparle al jugador ni la puerta ni
        /// al predicador, que es donde mira mientras apunta.
        /// </summary>
        private const float CenterOffsetPixels = 32f;

        /// <summary>Z del aro: por delante de todo lo que se dibuja en el mundo.</summary>
        private const float RingZ = -5f;

        /// <summary>Orden de dibujo dentro de la capa FX.</summary>
        private const int RingOrder = 100;

        /// <summary>
        /// Lado de la textura del velo. Más grande que el aro porque el
        /// desvanecido necesita lugar por fuera para llegar a cero: con 128 se
        /// cortaría justo donde todavía tiene densidad.
        /// </summary>
        private const int VeilTextureSize = 192;

        /// <summary>Crea el aro bajo la cámara y lo cablea al runner.</summary>
        public static SkillcheckRing Build(Camera camera, SkillcheckRunner runner)
        {
            var root = new GameObject("Skillcheck");
            root.transform.SetParent(camera.transform, false);

            // La Z es LOCAL a la cámara, que está en -10: para que el aro quede en
            // la Z de mundo que se quiere, hay que restarle la de la cámara. Con el
            // valor sin convertir el aro queda detrás del ojo y no se dibuja nada.
            root.transform.localPosition = new Vector3(
                0f, SceneLayout.Px(CenterOffsetPixels), RingZ - SceneLayout.CameraZ);

            // De atrás para adelante. El velo apaga el mundo, el contorno se pinta
            // pegado a los trazos del aro —sus radios no se pisan, así que da igual
            // cuál de los dos quede encima— y la zona va arriba del aro porque
            // ocupa el canal que el aro deja libre.
            SpriteRenderer veil = AddLayer(root.transform, "Velo", null, -2);
            SpriteRenderer outline = AddLayer(root.transform, "Contorno", null, -1);
            SpriteRenderer ring = AddLayer(root.transform, "Aro", "ui_skillcheck_aro", 0);
            SpriteRenderer zone = AddLayer(root.transform, "Zona", null, 1);
            SpriteRenderer needle = AddLayer(root.transform, "Aguja", "ui_skillcheck_aguja", 2);

            var veilPainter = veil.gameObject.AddComponent<SkillcheckVeil>();
            SerializedFieldUtility.SetInt(veilPainter, "textureSize", VeilTextureSize);

            var outlinePainter = outline.gameObject.AddComponent<SkillcheckOutline>();
            SerializedFieldUtility.SetInt(outlinePainter, "textureSize", TextureSize);

            var view = root.AddComponent<SkillcheckRing>();
            SerializedFieldUtility.SetReference(view, "runner", runner);
            SerializedFieldUtility.SetReference(view, "veilRenderer", veil);
            SerializedFieldUtility.SetReference(view, "outlineRenderer", outline);
            SerializedFieldUtility.SetReference(view, "ringRenderer", ring);
            SerializedFieldUtility.SetReference(view, "zoneRenderer", zone);
            SerializedFieldUtility.SetReference(view, "needleRenderer", needle);
            SerializedFieldUtility.SetInt(view, "textureSize", TextureSize);
            SerializedFieldUtility.SetFloat(view, "trackInnerRadius", TrackInnerRadius);
            SerializedFieldUtility.SetFloat(view, "trackOuterRadius", TrackOuterRadius);

            return view;
        }

        /// <summary>
        /// Una capa del aro. La zona va sin sprite: se lo genera el
        /// <c>ArcPainter</c> en Awake, porque cambia con cada tirada.
        /// </summary>
        private static SpriteRenderer AddLayer(
            Transform parent, string name, string spriteName, int orderOffset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            if (spriteName != null) renderer.sprite = SpriteLibrary.Load(spriteName);
            renderer.sortingLayerName = SortingLayerAuthoring.Fx;
            renderer.sortingOrder = RingOrder + orderOffset;
            renderer.enabled = false;
            Building.UiMaterialFactory.Apply(renderer);

            return renderer;
        }
    }
}
