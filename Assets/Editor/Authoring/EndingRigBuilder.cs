using BuenosDias.Config;
using BuenosDias.EditorTools.Building;
using BuenosDias.Gameplay;
using BuenosDias.Presentation;
using UnityEngine;

namespace BuenosDias.EditorTools.Authoring
{
    /// <summary>
    /// Arma la cinemática de finales: el telón, los dos carteles y la fila de
    /// almas que asciende.
    ///
    /// Salió de <c>RunStateBuilder</c> cuando pasó de 200 líneas. El corte no fue
    /// por el largo sino por dónde estaba la costura: el final es otra pantalla,
    /// con su propia capa de dibujo —telón en 300, carteles en 400, por encima de
    /// TODO lo de la partida— y su propio catálogo de assets.
    /// </summary>
    public static class EndingRigBuilder
    {
        /// <summary>
        /// El telón del final tapa TODO lo de la partida: skillcheck (100-102),
        /// barra (110) y carteles de selección (200).
        /// </summary>
        private const int BackdropOrder = 300;

        /// <summary>Los carteles del final van por delante del telón.</summary>
        private const int TitleOrder = 400;

        /// <summary>Z de los carteles: la misma del skillcheck, por delante del mundo.</summary>
        private const float LabelZ = -5f;

        /// <summary>
        /// Alturas de los carteles del final, desde el centro de la cámara.
        ///
        /// Van abajo y no al medio: la cruz mide 200 px en una pantalla de 216, así
        /// que cualquier cartel a media altura le cruza el travesaño por encima. Y
        /// abajo caen sobre la loma, que es una masa oscura pareja — el mejor fondo
        /// posible para tinta hueso.
        /// </summary>
        private const float TitleY = -46f;

        private const float SubtitleY = -62f;

        /// <summary>
        /// Arma la cinemática de finales. El telón y los carteles cuelgan de la
        /// cámara, por delante de todo lo demás de la partida.
        /// </summary>
        public static void Build(Camera camera, RunDirector director)
        {
            BitmapFontDefinition font = LabelFactory.Font;

            var root = new GameObject("Final");
            root.transform.SetParent(camera.transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, LabelZ - SceneLayout.CameraZ);

            var backdrop = new GameObject("Telón").AddComponent<SpriteRenderer>();
            backdrop.transform.SetParent(root.transform, false);
            backdrop.sortingLayerName = SortingLayerAuthoring.Fx;
            backdrop.sortingOrder = BackdropOrder;
            UiMaterialFactory.Apply(backdrop);

            TextLabel title = LabelFactory.Create(root.transform, "Titular", font, TitleY, 2, TitleOrder);
            TextLabel subtitle = LabelFactory.Create(root.transform, "Subtitular", font, SubtitleY, 1, TitleOrder);

            var view = root.AddComponent<EndingView>();
            SerializedFieldUtility.SetReference(view, "runDirector", director);
            SerializedFieldUtility.SetReference(view, "targetCamera", camera);
            SerializedFieldUtility.SetReference(view, "backdrop", backdrop);
            SerializedFieldUtility.SetReference(view, "titleLabel", title);
            SerializedFieldUtility.SetReference(view, "subtitleLabel", subtitle);
            SerializedFieldUtility.SetReference(view, "spriteMaterial", UiMaterialFactory.Load());
            SerializedFieldUtility.SetReferenceList(view, "endings", EndingCatalogSeeder.Build());

            BuildAscensionLine(root.transform, director);
        }

        /// <summary>
        /// La fila de almas que sube en la ascensión.
        ///
        /// Va aparte de las piezas del asset porque su CANTIDAD no es un dato del
        /// final: sale de cuántas almas juntó el jugador esa partida, y eso recién
        /// se sabe cuando el final se dispara.
        /// </summary>
        private static void BuildAscensionLine(Transform parent, RunDirector director)
        {
            var go = new GameObject("Almas que suben");
            go.transform.SetParent(parent, false);

            var view = go.AddComponent<AscensionLine>();
            SerializedFieldUtility.SetReference(view, "runDirector", director);
            SerializedFieldUtility.SetReference(view, "figureSprite",
                SpriteLibrary.Load("chr_seguidor_01_walk_00"));
            SerializedFieldUtility.SetReference(view, "spriteMaterial", UiMaterialFactory.Load());
        }
    }
}
