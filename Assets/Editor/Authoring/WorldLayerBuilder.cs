using BuenosDias.Config;
using BuenosDias.Gameplay;
using BuenosDias.Presentation;
using UnityEditor;
using UnityEngine;

namespace BuenosDias.EditorTools.Authoring
{
    /// <summary>
    /// Arma el árbol de capas del mundo: parallax, suelo y el contenedor vacío
    /// donde la fase 4 va a instanciar las casas.
    /// </summary>
    public static class WorldLayerBuilder
    {
        /// <summary>Crea la jerarquía completa bajo un root llamado "World".</summary>
        public static GameObject Build(Camera camera)
        {
            var world = new GameObject("World");

            BuildSkyline(world.transform, camera);
            BuildFar(world.transform, camera);
            BuildGround(world.transform, camera);

            HouseSpawner houses = BuildHouses(world.transform, camera);
            BuildGapProps(world.transform, camera, houses);

            BuildForeground(world.transform, camera);

            return world;
        }

        private static void BuildSkyline(Transform parent, Camera camera)
        {
            Transform layer = CreateLayer(parent, "Parallax_Skyline",
                SceneLayout.SkylineParallax, SceneLayout.BackgroundZ, camera);

            CreateTiledStrip(layer, "Skyline", "bg_skyline_lejano",
                SortingLayerAuthoring.Background, 0, SceneLayout.SkylineBottomY, camera);
        }

        /// <summary>
        /// Capa media: la silueta de la cuadra de atrás, copas y techos en un tono
        /// plano, como el skyline pero más cerca.
        ///
        /// Va como TIRA y no como props sueltos, y esa es toda la diferencia: una
        /// tira tolera derivar sobre los huecos porque es continua y no tiene base.
        /// Un prop con base definida no puede vivir en una capa que se desplaza
        /// respecto del mundo, y por eso el árbol se fue a parallax 1.
        ///
        /// Apoya a la misma altura que el skyline, así las dos bandas nacen desde
        /// detrás de los techos.
        /// </summary>
        private static void BuildFar(Transform parent, Camera camera)
        {
            Transform layer = CreateLayer(parent, "Parallax_Far",
                SceneLayout.FarParallax, SceneLayout.FarZ, camera);

            CreateTiledStrip(layer, "CopasLejanas", "bg_arboles_lejanos",
                SortingLayerAuthoring.Far, 0, SceneLayout.SkylineBottomY, camera);
        }

        /// <summary>
        /// Contenedor de casas con su spawner. El pool se instancia bajo este
        /// transform, así la jerarquía de Play queda ordenada.
        /// </summary>
        private static HouseSpawner BuildHouses(Transform parent, Camera camera)
        {
            var houses = new GameObject("Houses");
            houses.transform.SetParent(parent, false);

            var spawner = houses.AddComponent<HouseSpawner>();
            SerializedFieldUtility.SetReference(spawner, "targetCamera", camera);
            SerializedFieldUtility.SetReference(spawner, "gameConfig",
                AssetDatabase.LoadAssetAtPath<GameConfig>(
                    "Assets/ScriptableObjects/Config/GameConfig.asset"));
            SerializedFieldUtility.SetReference(spawner, "housePrefab",
                AssetDatabase.LoadAssetAtPath<HouseInstance>(HousePrefabBuilder.PrefabPath));

            return spawner;
        }

        /// <summary>
        /// Mobiliario de vereda —poste de luz y árbol— a parallax 1, en el mismo
        /// plano que el jugador y solo en los huecos entre terrenos.
        ///
        /// En una capa de parallax distinto ninguno de los dos funciona: el poste
        /// porque en la fase 7 es una fuente de luz y tiene que iluminar la vereda
        /// por la que se camina, y el árbol porque su alineación con las casas
        /// derivaría hasta quedar sobre un hueco.
        /// </summary>
        private static void BuildGapProps(
            Transform parent, Camera camera, HouseSpawner houses)
        {
            var props = new GameObject("StreetProps");
            props.transform.SetParent(parent, false);
            props.transform.localPosition =
                new Vector3(0f, 0f, SceneLayout.StreetFurnitureZ);

            var placer = props.AddComponent<GapPropPlacer>();
            SerializedFieldUtility.SetReference(placer, "houseSpawner", houses);
            SerializedFieldUtility.SetReference(placer, "targetCamera", camera);

            var so = new SerializedObject(placer);
            SerializedProperty list = so.FindProperty("candidates");
            list.arraySize = SceneryCatalogSeeder.GapCandidatePaths.Length;
            for (int i = 0; i < list.arraySize; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<SceneryPropSet>(
                        SceneryCatalogSeeder.GapCandidatePaths[i]);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildGround(Transform parent, Camera camera)
        {
            // Sin ParallaxLayer: el suelo es el plano de juego, factor 1.
            var ground = new GameObject("Ground");
            ground.transform.SetParent(parent, false);
            ground.transform.localPosition = new Vector3(0f, 0f, SceneLayout.GroundZ);

            CreateTiledStrip(ground.transform, "Street", "env_calle",
                SortingLayerAuthoring.Ground, 0, SceneLayout.ScreenBottomY, camera);

            CreateTiledStrip(ground.transform, "Sidewalk", "env_vereda",
                SortingLayerAuthoring.Ground, 10, -SceneLayout.SidewalkHeight, camera);
        }

        /// <summary>
        /// Arbustos de frente: matas sueltas con huecos, no una tira continua.
        /// Están delante del jugador, así que viven pegadas al borde inferior del
        /// cuadro y no llegan a la banda de vereda: si subieran, taparían al
        /// predicador y a la fila de seguidores justo cuando hay que verlos.
        /// </summary>
        private static void BuildForeground(Transform parent, Camera camera)
        {
            Transform layer = CreateLayer(parent, "Parallax_Front",
                SceneLayout.ForegroundParallax, SceneLayout.ForegroundZ, camera);

            CreateScenerySpawner(layer, "Arbustos", SceneryCatalogSeeder.BushesPath, camera);
        }

        /// <summary>Cuelga de una capa un sembrador de decorado ya cableado.</summary>
        private static void CreateScenerySpawner(
            Transform parent, string name, string propSetPath, Camera camera)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var spawner = go.AddComponent<ScenerySpawner>();
            SerializedFieldUtility.SetReference(spawner, "targetCamera", camera);
            SerializedFieldUtility.SetReference(spawner, "propSet",
                AssetDatabase.LoadAssetAtPath<SceneryPropSet>(propSetPath));
        }

        private static Transform CreateLayer(
            Transform parent, string name, float parallaxFactor, float z, Camera camera)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);

            var layer = go.AddComponent<ParallaxLayer>();
            SerializedFieldUtility.SetFloat(layer, "parallaxFactor", parallaxFactor);
            SerializedFieldUtility.SetReference(layer, "cameraTransform", camera.transform);

            return go.transform;
        }

        /// <summary>
        /// Crea una tira que se repite al infinito. El tamaño inicial se calcula
        /// con la misma fórmula que usa el componente en runtime, así lo que se
        /// ve con la escena abierta coincide con lo que se ve en Play.
        /// </summary>
        private static void CreateTiledStrip(
            Transform parent, string name, string spriteName,
            string sortingLayer, int order, float bottomY, Camera camera)
        {
            Sprite sprite = SpriteLibrary.Load(spriteName);
            if (sprite == null) return;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = order;

            float tileWidth = InfiniteScrollingSprite.TileWidthOf(sprite);
            float viewWidth = camera.orthographicSize * 2f * camera.aspect;
            float coverWidth = InfiniteScrollingSprite.CalculateCoverWidth(tileWidth, viewWidth, 1);
            float height = sprite.rect.height / sprite.pixelsPerUnit;

            renderer.size = new Vector2(coverWidth, height);
            go.transform.localPosition = new Vector3(-coverWidth * 0.5f, bottomY, 0f);

            var scroller = go.AddComponent<InfiniteScrollingSprite>();
            SerializedFieldUtility.SetReference(scroller, "targetCamera", camera);
        }

    }
}
