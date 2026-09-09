using BuenosDias.EditorTools.Building;
using BuenosDias.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BuenosDias.EditorTools.Authoring
{
    /// <summary>
    /// Arma <c>Assets/Prefabs/World/House.prefab</c> con todos sus slots y cablea
    /// las referencias de <see cref="HouseInstance"/>.
    ///
    /// Los slots son fijos y se prenden o apagan: nada se instancia en runtime,
    /// porque el spawner recicla casas continuamente y crear objetos por señal
    /// metería basura en cada scroll.
    /// </summary>
    public static class HousePrefabBuilder
    {
        /// <summary>Ruta del prefab de casa.</summary>
        public const string PrefabPath = "Assets/Prefabs/World/House.prefab";

        private const float Px = 1f / 32f;
        private const float WallHeightPixels = 64f;
        private const float WindowSizePixels = 50f;

        [MenuItem("Tools/Buenos Días/Fase 4 · Construir prefab de casa", priority = 401)]
        public static void BuildFromMenu()
        {
            Debug.Log(Build() != null
                ? $"[Fase 4] Prefab de casa creado en {PrefabPath}"
                : "[Fase 4] No se pudo crear el prefab de casa.");
        }

        /// <summary>Construye y guarda el prefab. Devuelve el asset resultante.</summary>
        public static GameObject Build()
        {
            SpriteLibrary.Invalidate();

            var root = new GameObject("House");
            var instance = root.AddComponent<HouseInstance>();

            SpriteRenderer wall = Tiled(root, "Wall", "env_pared_a", "Houses", 0, 0f);
            SpriteRenderer slab = Tiled(root, "RoofSlab", "env_techo_losa", "Houses", 5,
                WallHeightPixels * Px);
            SpriteRenderer gable = Simple(root, "RoofGable", "env_techo_dosaguas", "Houses", 6,
                new Vector3(0f, WallHeightPixels * Px, 0f));
            SpriteRenderer fence = Tiled(root, "Fence", "env_reja_segmento", "HouseDetails", 10, 0f);

            Transform door = BuildDoor(root);
            Transform signalWindow = BuildWindow(root, "SignalWindow");
            Transform tellWindow = BuildTellWindow(root);

            var props = new GameObject("Props");
            props.transform.SetParent(root.transform, false);
            HousePrefabWiring.PropSlotParts[] slots = { BuildPropSlot(props, 0), BuildPropSlot(props, 1) };

            var strips = new GameObject("Strips");
            strips.transform.SetParent(root.transform, false);
            SpriteRenderer[] segments =
            {
                Tiled(strips, "Strip_L", "prop_pasto_alto", "HouseDetails", 1, 0f),
                Tiled(strips, "Strip_R", "prop_pasto_alto", "HouseDetails", 1, 0f)
            };

            HousePrefabWiring.WireStructure(instance, wall, slab, gable, fence, door, tellWindow);
            HousePrefabWiring.WireMounter(instance, slots, segments, signalWindow);

            AssetPathUtility.EnsureFolder("Assets/Prefabs/World");
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return asset;
        }

        private static Transform BuildDoor(GameObject root)
        {
            // La puerta es un transform vacío en el CENTRO del vano, con el sprite
            // corrido media puerta. Así el jitter es setear una X, y DoorPosition
            // apunta al centro real, que es lo que necesita el rango del timbre.
            var pivot = new GameObject("Door");
            pivot.transform.SetParent(root.transform, false);

            Simple(pivot, "DoorSprite", "env_puerta_cerrada", "Houses", 10,
                new Vector3(-16f * Px, 0f, 0f));
            Simple(pivot, "Doorbell", "prop_timbre", "HouseDetails", 5,
                new Vector3(22f * Px, 30f * Px, 0f));

            // El portón va en la reja, a la altura del camino, y se mueve con la
            // puerta. Sin él la reja pasaba entera por delante del sendero y la
            // casa no tenía por dónde entrar.
            Simple(pivot, "Gate", "env_reja_porton", "HouseDetails", 11,
                new Vector3(-20f * Px, 0f, 0f));

            var neighbor = new GameObject("NeighborAnchor");
            neighbor.transform.SetParent(pivot.transform, false);

            return pivot.transform;
        }

        private static Transform BuildWindow(GameObject root, string name)
        {
            // Anclada a 4 px del borde superior de la pared: centrada quedaría
            // flotando y sin alféizar visible abajo.
            float y = (WallHeightPixels - 4f - WindowSizePixels) * Px;
            return Simple(root, name, "env_ventana_normal", "Houses", 10,
                new Vector3(0f, y, 0f)).transform;
        }

        private static Transform BuildTellWindow(GameObject root)
        {
            Transform window = BuildWindow(root, "TellWindow");

            // La cortina va en TODAS las casas, ocupadas o no: si apareciera solo
            // cuando hay alguien, su sola presencia delataría el resultado.
            SpriteRenderer curtain = Simple(window.gameObject, "Curtain",
                "fx_cortina_movimiento_00", "Houses", 11, Vector3.zero);

            if (curtain.sprite != null)
            {
                // Offset +4,+2 desde el origen de la ventana, corregido porque la
                // cortina importa con pivot Center y la ventana con BottomLeft.
                Rect rect = curtain.sprite.rect;
                curtain.transform.localPosition = new Vector3(
                    (4f + rect.width * 0.5f) * Px, (2f + rect.height * 0.5f) * Px, 0f);
            }

            var animator = curtain.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    CurtainTellClipFactory.ControllerPath);

            BuildWindowGlow(window);
            return window;
        }

        /// <summary>
        /// El halo de ventana prendida. Va en la capa Houses y no en FX: FX se
        /// dibuja delante de TODO, incluida la reja, y el halo quedaría flotando
        /// por delante del portón en vez de saliendo de adentro de la casa. Es el
        /// mismo motivo por el que el farol de las señales tampoco va en FX.
        /// </summary>
        private static void BuildWindowGlow(Transform window)
        {
            // La ventana importa con pivot BottomLeft y el halo con Center, así
            // que hay que correrlo media ventana para que queden concéntricos.
            float centre = WindowSizePixels * 0.5f * Px;

            SpriteRenderer glow = Simple(window.gameObject, "Glow",
                "fx_ventana_encendida", "Houses", 12, new Vector3(centre, centre, 0f));

            glow.sharedMaterial = SignalAssetFactory.BuildAdditiveMaterial();
            glow.enabled = false;   // lo prende el anochecer
        }

        private static HousePrefabWiring.PropSlotParts BuildPropSlot(GameObject parent, int index)
        {
            var holder = new GameObject($"Prop_{index}");
            holder.transform.SetParent(parent.transform, false);

            SpriteRenderer prop = Simple(holder, "Sprite", "prop_buzon_lleno",
                "HouseDetails", 6, Vector3.zero);
            prop.enabled = false;

            // En HouseDetails, justo por encima del prop. NO en FX: ese layer va
            // delante de TODO, incluida la reja, y el halo aparecía flotando
            // delante del portón como una estrella suelta.
            SpriteRenderer glow = Simple(holder, "Glow", "fx_farol_luz",
                "HouseDetails", 7, Vector3.zero);
            glow.enabled = false;
            glow.sharedMaterial = SignalAssetFactory.BuildAdditiveMaterial();

            var animator = glow.gameObject.AddComponent<Animator>();
            animator.enabled = false;

            return new HousePrefabWiring.PropSlotParts(holder.transform, prop, glow, animator);
        }

        private static SpriteRenderer Simple(
            GameObject parent, string name, string spriteName,
            string sortingLayer, int order, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPosition;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteLibrary.Load(spriteName);
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static SpriteRenderer Tiled(
            GameObject parent, string name, string spriteName,
            string sortingLayer, int order, float y)
        {
            SpriteRenderer renderer = Simple(
                parent, name, spriteName, sortingLayer, order, new Vector3(0f, y, 0f));

            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;

            if (renderer.sprite != null)
            {
                renderer.size = new Vector2(
                    6f, renderer.sprite.rect.height / renderer.sprite.pixelsPerUnit);
            }
            return renderer;
        }
    }
}
