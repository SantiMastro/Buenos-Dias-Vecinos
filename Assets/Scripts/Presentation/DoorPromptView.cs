using BuenosDias.Config;
using BuenosDias.Gameplay;
using BuenosDias.Simulation;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// El cartelito de "acá podés tocar": un chevrón que aparece sobre la puerta
    /// mientras el predicador camina dentro del alcance del timbre.
    ///
    /// Hace falta porque el alcance es INVISIBLE. Frenar bien es toda la mecánica
    /// del felpudo, pero hasta que el pie toca la plancha el jugador no tiene un
    /// solo píxel que le diga si está en zona: aprende el alcance a fuerza de
    /// frenar de más y perder segundos, que es aprender por castigo.
    ///
    /// ⚠️ NO reimplementa el alcance. Pregunta con el MISMO
    /// <see cref="DoorApproach.At"/> que usa la FSM para decidir a qué puerta te
    /// lleva pisar la plancha, así que el indicador no puede prometer una cosa y el
    /// juego hacer otra. Copiar la cuenta habría sido más corto y habría dejado dos
    /// reglas que se separan la primera vez que alguien toque una.
    ///
    /// El sprite se genera acá y no es un PNG: son veinte píxeles y tres colores,
    /// y un archivo de arte para eso cuesta más de mantener que de dibujar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorPromptView : MonoBehaviour
    {
        /// <summary>
        /// El dibujo, leído de arriba abajo como se escribe. '#' es tinta, 'o' es
        /// borde y '.' es nada. El borde no es adorno: sin él, el chevrón hueso
        /// desaparece contra una pared clara, que es justo el caso en el que hace
        /// falta.
        /// </summary>
        private static readonly string[] IconRows =
        {
            "ooooooo",
            "o#####o",
            ".o###o.",
            "..o#o..",
            "...o..."
        };

        [Header("De quién se lee")]
        [Tooltip("La FSM del predicador: de acá salen el estado y la posición.")]
        [SerializeField] private PreacherController preacher;

        [Tooltip("Dueño de las casas activas. Se le pregunta cuál es la puerta " +
                 "más cercana; no se le pide nada.")]
        [SerializeField] private HouseSpawner houseSpawner;

        [Tooltip("Dueño de la partida. Fuera del día no hay puerta que tocar.")]
        [SerializeField] private RunDirector runDirector;

        [Tooltip("Asset raíz de balance. De acá sale el alcance del timbre.")]
        [SerializeField] private GameConfig gameConfig;

        [Header("Dibujo")]
        [Tooltip("Renderer del ícono. Va en una capa por encima de la casa.")]
        [SerializeField] private SpriteRenderer icon;

        [Tooltip("A qué altura sobre la BASE de la puerta apunta la punta del " +
                 "chevrón. La puerta mide 56 px, así que con 64 queda ocho por " +
                 "encima del dintel.")]
        [SerializeField, Min(0f)] private float heightPixels = 64f;

        [Tooltip("Cuántos píxeles sube y baja. Enteros: medio píxel de flotación " +
                 "rompe la grilla y el ícono tiembla en vez de flotar.")]
        [SerializeField, Min(0)] private int bobPixels = 1;

        [Tooltip("Subidas y bajadas por segundo.")]
        [SerializeField, Range(0.1f, 6f)] private float bobHertz = 1.5f;

        [Tooltip("Tinta del chevrón. Hueso #F3ECE0 de la paleta.")]
        [SerializeField] private Color32 inkColor = new Color32(0xF3, 0xEC, 0xE0, 0xFF);

        [Tooltip("Borde del chevrón. Violeta oscuro #1D1638 de la paleta.")]
        [SerializeField] private Color32 edgeColor = new Color32(0x1D, 0x16, 0x38, 0xFF);

        private Sprite built;

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }

            built = BuildIcon();
            icon.sprite = built;
            icon.enabled = false;
        }

        private void OnDestroy()
        {
            SolidSprite.Dispose(built);
        }

        /// <summary>
        /// En <c>LateUpdate</c> para leer la posición que el predicador ya dejó este
        /// cuadro. Al revés, el ícono aparecería y desaparecería un cuadro tarde,
        /// que en el borde exacto del alcance es exactamente donde se mira.
        /// </summary>
        private void LateUpdate()
        {
            HouseInstance house = DoorInReach();
            icon.enabled = house != null;
            if (house == null) return;

            float bob = Mathf.Round(
                Mathf.Sin(Time.unscaledTime * bobHertz * 2f * Mathf.PI) * bobPixels);

            Vector3 door = house.DoorPosition;
            Vector3 own = icon.transform.position;
            icon.transform.position = new Vector3(
                door.x, door.y + ProjectConstants.ToUnits(heightPixels + bob), own.z);
        }

        /// <summary>
        /// La puerta que se alcanza desde donde está parado, o <c>null</c>.
        ///
        /// Solo mientras CAMINA: subido al felpudo el indicador sobra —ya frenó— y
        /// dejarlo prendido lo convertiría en decoración que no dice nada.
        /// </summary>
        private HouseInstance DoorInReach()
        {
            if (runDirector.Phase != RunPhase.Jugando) return null;
            if (preacher.State != PreacherState.Caminando) return null;

            float x = preacher.transform.position.x;
            HouseInstance house = houseSpawner.NearestDoor(x, out float signedDistance);

            // El ritmo sale del predicador y no se recalcula acá: es el mismo
            // número que decide la hitbox de verdad, y dos cuentas que tienen que
            // dar igual terminan no dando igual.
            DoorApproach approach =
                DoorApproach.At(house, x, signedDistance, gameConfig.Doorbell, preacher.Pace);

            // El coyote no se anuncia: es un perdón para el que se pasó por poco, y
            // si el chevrón lo mostrara pasaría a ser parte del alcance.
            return approach.IsCoyote ? null : approach.House;
        }

        private Sprite BuildIcon()
        {
            int height = IconRows.Length;
            int width = IconRows[0].Length;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[width * height];
            for (int row = 0; row < height; row++)
                for (int column = 0; column < width; column++)
                {
                    // La tabla se lee de arriba abajo, como se escribe, y las
                    // texturas de Unity tienen el origen abajo: la fila se da
                    // vuelta. Es la misma vuelta que hace la fuente de bitmap.
                    char cell = IconRows[height - 1 - row][column];
                    pixels[row * width + column] = ColorFor(cell);
                }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            // El pivote va en la PUNTA de abajo: así la altura del Inspector es la
            // distancia a lo que el chevrón está señalando, que es lo que uno mide
            // mirando la pantalla.
            Sprite sprite = Sprite.Create(
                texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0f),
                ProjectConstants.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.HideAndDontSave;

            return sprite;
        }

        private Color32 ColorFor(char cell)
        {
            if (cell == '#') return inkColor;
            if (cell == 'o') return edgeColor;

            return new Color32(0, 0, 0, 0);
        }

        private bool ValidateSetup()
        {
            if (preacher != null && houseSpawner != null && runDirector != null
                && gameConfig != null && gameConfig.Doorbell != null
                && icon != null) return true;

            Debug.LogError(
                $"[DoorPromptView] '{name}' tiene referencias sin asignar " +
                "(predicador, spawner, partida, GameConfig o renderer).", this);
            return false;
        }
    }
}
