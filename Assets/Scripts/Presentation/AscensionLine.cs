using System.Collections.Generic;
using BuenosDias.Config;
using BuenosDias.Gameplay;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// La fila de almas subiendo en el final de ascensión, con retardo por puesto.
    ///
    /// **Es todo el final.** Sin esto la ascensión son cinco dibujos quietos: el
    /// cielo abierto, los rayos y una silueta parada en el haz. Lo que la hace
    /// significar algo es que suba la cantidad de gente que el jugador juntó, uno
    /// atrás del otro, y que esa cantidad sea distinta cada partida.
    ///
    /// **No toca a los seguidores de verdad, y es a propósito.** La
    /// <see cref="FollowerParade"/> ya los está manejando —el pool, el muestreo del
    /// recorrido, las caras— y meterle un segundo dueño a esos transforms sería
    /// pelearse por ellos. Además la ascensión tapa el mundo con un telón, así que
    /// los de verdad no se verían igual. El número sí es real: viene en el evento
    /// <see cref="RunDirector.EndingReached"/>, que trae la comitiva que disparó el
    /// final.
    ///
    /// Se dibuja en la capa de la cinemática, por delante del telón.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AscensionLine : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Dueño de la partida. De acá salen qué final salió y con cuántas " +
                 "almas. Se escucha; nunca se le pide nada.")]
        [SerializeField] private RunDirector runDirector;

        [Tooltip("Cómo se ve un alma subiendo. Un solo sprite: son figuritas de " +
                 "veinte píxeles a contraluz del haz, así que la cara no se lee y " +
                 "repartir tipos no agregaría nada.")]
        [SerializeField] private Sprite figureSprite;

        [Tooltip("Material SIN iluminar. Una cinemática no está dentro de la calle, " +
                 "así que la luz del atardecer no tiene nada que hacerle (§14.13).")]
        [SerializeField] private Material spriteMaterial;

        [Header("Dibujo")]
        [Tooltip("Capa de la cinemática. En la capa de los personajes el telón del " +
                 "final los taparía y no se vería subir a nadie.")]
        [SerializeField] private string sortingLayer = "FX";

        [Tooltip("Orden dentro de esa capa: entre el haz de luz y el destello.")]
        [SerializeField] private int sortingOrder = 303;

        [Tooltip("Tope de figuras. La comitiva puede ser más grande; lo que se " +
                 "corta es cuántas se dibujan, igual que en la fila de la calle.")]
        [SerializeField, Min(1)] private int maximumFigures = 12;

        [Header("Colocación, en píxeles desde el centro")]
        [Tooltip("Dónde arranca la primera, que es la que va más cerca del " +
                 "predicador. Debajo de la silueta grande, que está en -28.")]
        [SerializeField] private Vector2Int firstPositionPixels = new Vector2Int(44, -40);

        [Tooltip("Cuánto se corre cada una respecto de la anterior. En X negativo " +
                 "porque la fila viene de atrás; el poco de Y la desalinea apenas, " +
                 "que es lo que evita que doce figuras se lean como una regla.")]
        [SerializeField] private Vector2Int spacingPixels = new Vector2Int(-16, -3);

        [Header("Tiempos")]
        [Tooltip("Segundos desde que empieza el final hasta que sube la primera. " +
                 "Va después de la silueta: primero entra él al haz y después " +
                 "empieza a llevarse a los demás.")]
        [SerializeField, Min(0f)] private float appearAtSeconds = 1.6f;

        [Tooltip("Retardo entre un puesto y el siguiente. ⚠ Es TODO el efecto: con " +
                 "0 sube un bloque, y un bloque que sube se lee como un error de " +
                 "dibujo. Escalonado se lee como una fila.")]
        [SerializeField, Min(0f)] private float staggerSeconds = 0.16f;

        [Tooltip("Píxeles por segundo que sube cada una.")]
        [SerializeField, Min(1f)] private float speedPixels = 40f;

        [Tooltip("Segundos que tarda en desvanecerse mientras sube.")]
        [SerializeField, Min(0.1f)] private float fadeSeconds = 2.2f;

        private readonly List<SpriteRenderer> figures = new List<SpriteRenderer>();
        private int rising;
        private float elapsed;

        private void Awake()
        {
            if (runDirector != null && figureSprite != null) return;

            Debug.LogError(
                $"[AscensionLine] '{name}' no tiene partida o sprite asignado: la " +
                "ascensión va a salir sin nadie subiendo.", this);
            enabled = false;
        }

        private void OnEnable() => runDirector.EndingReached += OnEndingReached;

        private void OnDisable()
        {
            runDirector.EndingReached -= OnEndingReached;
            Clear();
        }

        private void OnEndingReached(EndingKind kind, int followers)
        {
            Clear();
            if (kind != EndingKind.Ascension) return;

            rising = Mathf.Min(followers, maximumFigures);
            elapsed = 0f;
        }

        /// <summary>
        /// En <c>LateUpdate</c> como el resto de la cinemática, para que todas las
        /// piezas del final se coloquen en el mismo punto del cuadro.
        /// </summary>
        private void LateUpdate()
        {
            if (rising <= 0) return;

            elapsed += Time.deltaTime;

            for (int i = 0; i < rising; i++)
            {
                // El de ADELANTE primero. La fila se vacía en la misma dirección en
                // la que venía caminando, y eso se lee como que se los está
                // llevando a todos; al revés se leería como que se escapan.
                float since = elapsed - appearAtSeconds - i * staggerSeconds;

                SpriteRenderer figure = FigureAt(i);
                figure.enabled = since >= 0f;
                if (since < 0f) continue;

                Place(figure, i, since);
            }
        }

        private void Place(SpriteRenderer figure, int place, float since)
        {
            float x = firstPositionPixels.x + spacingPixels.x * place;
            float y = firstPositionPixels.y + spacingPixels.y * place
                      + speedPixels * since;

            figure.transform.localPosition = new Vector3(
                ProjectConstants.ToUnits(x), ProjectConstants.ToUnits(y), 0f);

            Color tint = figure.color;
            tint.a = 1f - Mathf.Clamp01(since / fadeSeconds);
            figure.color = tint;
        }

        private SpriteRenderer FigureAt(int index)
        {
            while (figures.Count <= index)
            {
                var go = new GameObject($"alma_{figures.Count:00}");
                go.transform.SetParent(transform, false);

                var created = go.AddComponent<SpriteRenderer>();
                created.sprite = figureSprite;
                created.sortingLayerName = sortingLayer;
                created.sortingOrder = sortingOrder;
                if (spriteMaterial != null) created.sharedMaterial = spriteMaterial;
                figures.Add(created);
            }

            return figures[index];
        }

        /// <summary>
        /// Apaga todo y devuelve las figuras a opacas. Hace falta al empezar cada
        /// final y no solo al terminar: si no, una partida que termina en ascensión
        /// después de otra dejaría a las figuras arrancando ya desvanecidas.
        /// </summary>
        private void Clear()
        {
            rising = 0;

            foreach (SpriteRenderer figure in figures)
            {
                figure.enabled = false;

                Color tint = figure.color;
                tint.a = 1f;
                figure.color = tint;
            }
        }
    }
}
