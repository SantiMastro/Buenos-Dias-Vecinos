using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.EditorTools.Authoring
{
    /// <summary>
    /// Siembra los tres tipos de decorado de la cuadra: árbol de vereda, poste de
    /// luz y arbustos de frente.
    ///
    /// Los números salen de medir los sprites, no de probar a ojo; la cuenta de
    /// cada uno está comentada donde se usa. Como todo seeder del proyecto, no
    /// pisa un asset que ya exista.
    /// </summary>
    public static class SceneryCatalogSeeder
    {
        private const string Folder = ScriptableObjectSeeder.Root + "/Scenery";

        /// <summary>Asset del árbol de vereda.</summary>
        public const string TreesPath = Folder + "/ArbolDeVereda.asset";

        /// <summary>Asset de los postes de luz.</summary>
        public const string LampsPath = Folder + "/PosteDeLuz.asset";

        /// <summary>Asset de los arbustos que pasan por delante del cuadro.</summary>
        public const string BushesPath = Folder + "/ArbustosDeFrente.asset";

        /// <summary>
        /// Los decorados que compiten por un hueco entre terrenos, en el orden en
        /// que se acumulan sus probabilidades.
        /// </summary>
        public static readonly string[] GapCandidatePaths = { LampsPath, TreesPath };

        /// <summary>Crea los tres assets si faltan. Devuelve cuántos creó.</summary>
        public static int Seed()
        {
            int created = 0;
            SeedTrees(ref created);
            SeedLamps(ref created);
            SeedBushes(ref created);
            return created;
        }

        /// <summary>
        /// Árbol de vereda, a parallax 1 y solo en los huecos, igual que el poste.
        ///
        /// Se intentó primero como árbol de fondo a 0.55 y no cierra por dos
        /// razones, una de números y una de fondo:
        ///
        /// El sprite mide 120 px y los techos llegan a 112, así que apoyado la
        /// copa asoma 8 px y se lee como un bulto sobre el techo. Levantarlo para
        /// despegarla obliga a recortar el tronco.
        ///
        /// Y lo de fondo: a parallax distinto la alineación con las casas deriva
        /// sola, así que no se puede garantizar que un árbol quede detrás de una
        /// casa. Sobre un hueco, una copa recortada levita. Un prop con base
        /// definida no puede vivir en una capa que se desplaza respecto del mundo.
        ///
        /// Apoyado en la línea de caminata mide 0..120 px, o sea que asoma 8 px
        /// sobre los techos vecinos — pero desde adelante, que es como se ve un
        /// árbol de vereda de verdad.
        ///
        /// Pide 100 px de hueco aunque el sprite mida 72: en un hueco de 85 entra
        /// pero queda encajado tocando las dos rejas. Con 100 le quedan 14 px por
        /// lado, y sigue entrando en el 83% de los huecos.
        /// </summary>
        private static void SeedTrees(ref int created)
        {
            var asset = Get(TreesPath, ref created);
            if (asset == null) return;

            ScriptableObjectSeeder.SetList(asset, "sprites",
                SpriteLibrary.Load("env_arbol_vereda"));
            Set(asset, "groundOffsetPixels", 0f);
            Set(asset, "minimumGapPixels", 100f);
            Set(asset, "sortingLayer", SortingLayerAuthoring.HouseDetails);
            Set(asset, "sortingOrder", 20);
            Set(asset, "chancePerGap", 0.30f);
        }

        /// <summary>
        /// Postes de luz, a parallax 1 y solo en los huecos entre terrenos.
        ///
        /// Miden 40x160 con la parte opaca terminando en la fila 150, así que
        /// apoyados en la línea de caminata entran justo bajo el borde superior
        /// del cuadro (152 px) sin recortar el farol.
        ///
        /// El orden 20 los deja delante de la reja (10) y detrás de los
        /// personajes, que están en otro sorting layer.
        ///
        /// 0.35 acá más 0.30 del árbol deja el 35% de los huecos pelado.
        /// </summary>
        private static void SeedLamps(ref int created)
        {
            var asset = Get(LampsPath, ref created);
            if (asset == null) return;

            ScriptableObjectSeeder.SetList(asset, "sprites",
                SpriteLibrary.Load("env_poste_luz"));
            Set(asset, "groundOffsetPixels", 0f);
            Set(asset, "sortingLayer", SortingLayerAuthoring.HouseDetails);
            Set(asset, "sortingOrder", 20);
            Set(asset, "chancePerGap", 0.35f);
        }

        /// <summary>
        /// Arbustos de frente, a parallax 1.25.
        ///
        /// Tres matas de 48x36 con pivot BottomCenter, ninguna tocando sus bordes
        /// laterales: son props de verdad, así que no hace falta recorte y no queda
        /// el corte plano que dejaba la tira vieja al partirla.
        ///
        /// Están a parallax 1.25, o sea DELANTE del jugador: si subieran, taparían
        /// al predicador y a la fila de seguidores justo cuando hay que verlos.
        ///
        /// ⚠️ **Van RECORTADAS por el borde de abajo, no apoyadas en él.** La
        /// primera versión las apoyaba en −64, que es el borde inferior de la caja
        /// de DISEÑO, y salían enteras: 36 px de mata entera más una franja de
        /// calle abajo. Jugando se leían como manchas oscuras repartidas sobre el
        /// asfalto, no como un primer plano. Un prop de frente tiene que entrar
        /// cortado por el marco; si se lo ve entero, deja de ser marco y pasa a ser
        /// un objeto en la escena.
        ///
        /// ⚠️ Y el borde real no es −64 sino **−74**: la Pixel Perfect Camera sube
        /// el tamaño ortográfico a 3,6875 en runtime (§3.8), así que la ventana
        /// muestra 10 px más abajo que el diseño. Con −90 la base queda 16 px fuera
        /// del cuadro y se ven unos 18, la mitad que antes. Si algún día cambia la
        /// relación de aspecto, este número hay que volver a mirarlo.
        ///
        /// La separación se duplicó (50–160 → 90–240). En 384 px de ancho eso pasa
        /// de 2,4–7,7 matas en pantalla a 1,6–4,3.
        /// </summary>
        private static void SeedBushes(ref int created)
        {
            var asset = Get(BushesPath, ref created);
            if (asset == null) return;

            ScriptableObjectSeeder.SetList(asset, "sprites",
                SpriteLibrary.Load("prop_arbusto_a"),
                SpriteLibrary.Load("prop_arbusto_b"),
                SpriteLibrary.Load("prop_arbusto_c"));

            Set(asset, "groundOffsetPixels", -90f);   // por DEBAJO del borde real
            Set(asset, "sortingLayer", SortingLayerAuthoring.Foreground);
            Set(asset, "spacingMinPixels", 90f);
            Set(asset, "spacingMaxPixels", 240f);
        }

        private static SceneryPropSet Get(string path, ref int created)
        {
            var asset = ScriptableObjectSeeder.GetOrCreate<SceneryPropSet>(path, out bool made);
            if (!made) return null;   // ya existía: se respeta lo tuneado a mano

            created++;
            return asset;
        }

        private static void Set(SceneryPropSet asset, string field, object value)
        {
            ScriptableObjectSeeder.Set(asset, field, value);
        }

        private static Color Hex(string rgb)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out Color color);
            return color;
        }
    }
}
