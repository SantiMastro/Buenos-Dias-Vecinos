using BuenosDias.Config;
using BuenosDias.EditorTools.Building;
using BuenosDias.Gameplay;
using BuenosDias.Presentation;
using UnityEditor;
using UnityEngine;

namespace BuenosDias.EditorTools.Authoring
{
    /// <summary>
    /// Arma la capa de estado de partida: elegir religión, jugar, ver el final.
    ///
    /// Va en dos pasos, como el predicador y por lo mismo: el dueño de la partida
    /// necesita el reloj del día, que lo crea <see cref="RunRigBuilder"/>, y ese
    /// necesita al dueño de la partida para que la barra de tiempo sepa cuándo
    /// esconderse. Primero se crea, después se cierra el lazo.
    /// </summary>
    public static class RunStateBuilder
    {
        private const string ConfigPath = "Assets/ScriptableObjects/Config/GameConfig.asset";

        /// <summary>Orden del medidor: por delante del predicador, que dibuja en 10.</summary>
        private const int GaugeOrder = 12;

        /// <summary>Orden de los carteles: por encima de todo lo demás de la UI.</summary>
        private const int LabelOrder = 200;

        /// <summary>
        /// Escala del contador de almas. Es el score: tiene que leerse de reojo sin
        /// buscarlo, y a escala 1 se pierde contra el barrio.
        /// </summary>
        private const int SoulsScale = 3;

        /// <summary>Z de los carteles: la misma del skillcheck, por delante del mundo.</summary>
        private const float LabelZ = -5f;

        /// <summary>
        /// Alturas de los carteles, en píxeles desde el CENTRO de la cámara.
        ///
        /// Se miden desde el centro y no desde un borde a propósito: la Pixel
        /// Perfect Camera cambia el <c>orthographicSize</c> en runtime (§3.8), así
        /// que lo que está pegado a un borde hay que recalcularlo cada cuadro.
        /// Estos carteles no lo necesitan porque el centro no se mueve.
        /// </summary>
        private const float NameY = 34f;

        private const float TaglineY = 20f;

        /// <summary>
        /// Crea el dueño de la partida y las vistas de la selección. Devuelve el
        /// director para que el resto de la escena se pueda colgar de él.
        /// </summary>
        public static RunDirector CreateRun(Transform preacher)
        {
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            var input = preacher.GetComponent<GameInput>();

            var root = new GameObject("Partida");

            var selector = root.AddComponent<ReligionSelector>();
            SerializedFieldUtility.SetReference(selector, "gameConfig", config);
            SerializedFieldUtility.SetReference(selector, "input", input);

            var director = root.AddComponent<RunDirector>();
            SerializedFieldUtility.SetReference(director, "gameConfig", config);
            SerializedFieldUtility.SetReference(director, "selector", selector);
            SerializedFieldUtility.SetReference(director, "input", input);
            SerializedFieldUtility.SetReference(director, "preacher",
                preacher.GetComponent<PreacherController>());

            BuildPreacherViews(preacher, selector, director);
            BuildHoldGauge(preacher, selector);

            return director;
        }

        /// <summary>
        /// Cuelga los carteles de la selección de la cámara. Se hace aparte porque
        /// necesitan la cámara, que no existe cuando se crea el predicador.
        /// </summary>
        public static void BuildScreen(Camera camera, RunDirector director)
        {
            BitmapFontDefinition font = LabelFactory.Font;
            var selector = director.GetComponent<ReligionSelector>();

            var root = new GameObject("Carteles de selección");
            root.transform.SetParent(camera.transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, LabelZ - SceneLayout.CameraZ);

            TextLabel name = LabelFactory.Create(root.transform, "Nombre", font, NameY, 2);
            TextLabel tagline = LabelFactory.Create(root.transform, "Trueque", font, TaglineY, 1);

            var view = root.AddComponent<ReligionScreenView>();
            SerializedFieldUtility.SetReference(view, "selector", selector);
            SerializedFieldUtility.SetReference(view, "runDirector", director);
            SerializedFieldUtility.SetReference(view, "nameLabel", name);
            SerializedFieldUtility.SetReference(view, "taglineLabel", tagline);
        }

        /// <summary>
        /// Arma el HUD del día: el contador de almas con su ícono, el multiplicador
        /// de objeciones y los segundos que quedan.
        ///
        /// ⚠️ Esto faltaba. El HUD se había agregado A MANO a la escena y ningún
        /// builder lo armaba, así que reconstruir la escena desde el menú devolvía
        /// una partida sin contador, sin reloj en número y sin multiplicador —y no
        /// habría fallado nada: simplemente no se veían—. Es la clase de deuda que
        /// aparece dos meses después, cuando ya nadie se acuerda de que la escena
        /// tenía piezas hechas a mano.
        ///
        /// Cuelga de la cámara y en Z positiva, como la barra de tiempo. Colgarlo
        /// sin la Z ya costó dos vueltas: en 0 queda sobre el plano de la cámara y
        /// el near clip se lo come entero (§14, trampa 11).
        /// </summary>
        public static void BuildHud(
            Camera camera, RunDirector run, DayDirector day, SkillcheckRunner runner)
        {
            BitmapFontDefinition font = LabelFactory.Font;

            var root = new GameObject("HUD");
            root.transform.SetParent(camera.transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, LabelZ - SceneLayout.CameraZ);

            // La Y de los tres la fija el componente cada cuadro contra el borde de
            // pantalla, así que acá va en 0: lo único que importa es la Z del padre.
            TextLabel time = LabelFactory.Create(root.transform, "Tiempo", font, 0f, 1);
            TextLabel links = LabelFactory.Create(root.transform, "Objeciones", font, 0f, 1);
            TextLabel souls = LabelFactory.Create(root.transform, "Almas", font, 0f, SoulsScale);

            LabelFactory.LeftAlign(time);
            LabelFactory.LeftAlign(links);
            LabelFactory.LeftAlign(souls);
            LabelFactory.InvertInk(souls);

            var icon = new GameObject("Ícono de almas").AddComponent<SpriteRenderer>();
            icon.transform.SetParent(root.transform, false);
            icon.sprite = SpriteLibrary.Load("ui_icono_alma");
            icon.sortingLayerName = SortingLayerAuthoring.Fx;
            icon.sortingOrder = LabelOrder;
            UiMaterialFactory.Apply(icon);

            var view = root.AddComponent<RunHudView>();
            SerializedFieldUtility.SetReference(view, "director", day);
            SerializedFieldUtility.SetReference(view, "skillcheck", runner);
            SerializedFieldUtility.SetReference(view, "runDirector", run);
            SerializedFieldUtility.SetReference(view, "targetCamera", camera);
            SerializedFieldUtility.SetReference(view, "timeLabel", time);
            SerializedFieldUtility.SetReference(view, "soulsLabel", souls);
            SerializedFieldUtility.SetReference(view, "linksLabel", links);
            SerializedFieldUtility.SetReference(view, "soulsIcon", icon);
        }

        /// <summary>
        /// Cierra el lazo con el reloj del día, que se crea después. Sin esto el
        /// director queda sin reloj y se apaga solo al arrancar.
        /// </summary>
        public static void WireDay(RunDirector director, DayDirector day)
        {
            SerializedFieldUtility.SetReference(director, "day", day);
        }

        /// <summary>
        /// Lo que se le cuelga al predicador: el intercambiador de paleta que le
        /// pone los colores de la religión, y la vista que traduce su estado a su
        /// Animator.
        /// </summary>
        private static void BuildPreacherViews(
            Transform preacher, ReligionSelector selector, RunDirector director)
        {
            var swapper = preacher.gameObject.AddComponent<PaletteSwapper>();

            var preview = preacher.gameObject.AddComponent<ReligionPreviewView>();
            SerializedFieldUtility.SetReference(preview, "selector", selector);
            SerializedFieldUtility.SetReference(preview, "swapper", swapper);

            var view = preacher.gameObject.AddComponent<PreacherView>();
            SerializedFieldUtility.SetReference(view, "preacher",
                preacher.GetComponent<PreacherController>());
            SerializedFieldUtility.SetReference(view, "runDirector", director);
            SerializedFieldUtility.SetReference(view, "skillcheck",
                preacher.GetComponent<SkillcheckRunner>());
            SerializedFieldUtility.SetReference(view, "animator",
                preacher.GetComponent<Animator>());

            // La vista mira el input para una sola cosa: dejar el brazo estirado
            // mientras el timbre siga apretado. No decide nada.
            SerializedFieldUtility.SetReference(view, "input",
                preacher.GetComponent<GameInput>());

            var doors = preacher.gameObject.AddComponent<DoorOpeningView>();
            SerializedFieldUtility.SetReference(doors, "preacher",
                preacher.GetComponent<PreacherController>());
        }

        /// <summary>
        /// El medidor del mantenido cuelga del predicador y no de la cámara: es un
        /// gesto suyo, y arriba de su cabeza se lee sin tener que buscarlo en un
        /// borde de la pantalla.
        /// </summary>
        private static void BuildHoldGauge(Transform preacher, ReligionSelector selector)
        {
            var root = new GameObject("Medidor de confirmación");
            root.transform.SetParent(preacher, false);

            SpriteRenderer track = AddGaugeLayer(root.transform, "Riel", 0);
            SpriteRenderer fill = AddGaugeLayer(root.transform, "Relleno", 1);

            var gauge = root.AddComponent<HoldGaugeView>();
            SerializedFieldUtility.SetReference(gauge, "selector", selector);
            SerializedFieldUtility.SetReference(gauge, "track", track);
            SerializedFieldUtility.SetReference(gauge, "fill", fill);
        }

        private static SpriteRenderer AddGaugeLayer(
            Transform parent, string name, int orderOffset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = SortingLayerAuthoring.Fx;
            renderer.sortingOrder = GaugeOrder + orderOffset;
            UiMaterialFactory.Apply(renderer);

            return renderer;
        }
    }
}
