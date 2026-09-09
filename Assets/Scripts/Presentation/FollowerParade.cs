using System.Collections.Generic;
using BuenosDias.Config;
using BuenosDias.Core;
using BuenosDias.Gameplay;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// La fila de seguidores detrás del predicador.
    ///
    /// Cada seguidor no va pegado a una posición fija sino a dónde estaba el
    /// predicador hace <c>delayPerPosition</c> por su puesto en la fila. Por eso
    /// la comitiva se estira al arrancar y se amontona al frenar en una puerta,
    /// que es lo que la hace leer como una víbora y no como un bloque.
    ///
    /// ⚠️ Solo DIBUJA. La cuenta de seguidores la lleva el
    /// <see cref="SkillcheckRunner"/>, porque sube al convertir y baja al fallar,
    /// y eso es economía y no presentación. Acá se escucha y se muestra.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FollowerParade : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Quien lleva la cuenta de la comitiva. Se escucha.")]
        [SerializeField] private SkillcheckRunner skillcheck;

        [Tooltip("Asset raíz de balance. De acá sale la separación y el retardo.")]
        [SerializeField] private GameConfig gameConfig;

        [Tooltip("A quién sigue la fila.")]
        [SerializeField] private Transform preacher;

        [Tooltip("Plantilla de seguidor. Se instancia una sola vez al armar el pool.")]
        [SerializeField] private SpriteRenderer followerPrefab;

        [Tooltip("Overrides de animación, uno por tipo de seguidor. Se reparten por " +
                 "puesto en la fila para que no se vean todos iguales.")]
        [SerializeField] private List<AnimatorOverrideController> looks =
            new List<AnimatorOverrideController>();

        [Header("Pool")]
        [Tooltip("Tope de seguidores en pantalla. La comitiva puede crecer más que " +
                 "esto en la cuenta; lo que se corta es cuántos se dibujan.")]
        [SerializeField, Min(1)] private int poolSize = 12;

        [Tooltip("Cuadros de historia del recorrido. Tiene que cubrir el retardo " +
                 "del último de la fila: poolSize × delayPerPosition segundos.")]
        [SerializeField, Min(16)] private int historyFrames = 256;

        [Header("Cómo se van")]
        [Tooltip("Segundos que un seguidor que abandona sigue en pantalla, " +
                 "quedándose atrás y desvaneciéndose. En 0 desaparece de golpe, " +
                 "que es como estaba antes: el jugador se enteraba de que perdió " +
                 "gente solo por el número.")]
        [SerializeField, Min(0f)] private float departureSeconds = 0.9f;

        [Tooltip("Cuántos píxeles se corre mientras se va. Negativo es hacia " +
                 "atrás, que es de donde vino y para donde se vuelve.")]
        [SerializeField] private float departureDriftPixels = -14f;

        /// <summary>Un seguidor que ya abandonó y todavía se está yendo de pantalla.</summary>
        private struct Leaving
        {
            public SpriteRenderer Renderer;
            public float SecondsLeft;
            public Vector3 From;
        }

        private ComponentPool<SpriteRenderer> pool;
        private readonly List<SpriteRenderer> active = new List<SpriteRenderer>();
        private readonly List<Leaving> leaving = new List<Leaving>();
        private float[] times;
        private float[] positions;
        private int head = -1;

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }

            pool = new ComponentPool<SpriteRenderer>(followerPrefab, poolSize, transform);
            times = new float[historyFrames];
            positions = new float[historyFrames];
        }

        private void OnEnable()
        {
            if (skillcheck != null) skillcheck.FollowersChanged += OnFollowersChanged;
        }

        private void OnDisable()
        {
            if (skillcheck != null) skillcheck.FollowersChanged -= OnFollowersChanged;
        }

        private void LateUpdate()
        {
            Record(Time.time, preacher.position.x);

            FollowerConfig config = gameConfig.Followers;
            float y = preacher.position.y;
            float z = preacher.position.z;

            for (int i = 0; i < active.Count; i++)
            {
                int place = i + 1;
                float x = Sample(Time.time - config.DelayPerPosition * place)
                          - config.Spacing * place;

                active[i].transform.position = new Vector3(x, y, z);
            }

            AdvanceLeaving(Time.deltaTime);
        }

        /// <summary>
        /// Los que abandonaron no siguen al predicador: se quedan donde estaban y
        /// se desvanecen mientras él se aleja. Eso es lo que hace que perder gente
        /// SE VEA en vez de enterarse por un número que bajó.
        /// </summary>
        private void AdvanceLeaving(float deltaTime)
        {
            float drift = ProjectConstants.ToUnits(departureDriftPixels);

            for (int i = leaving.Count - 1; i >= 0; i--)
            {
                Leaving one = leaving[i];
                one.SecondsLeft -= deltaTime;

                if (one.SecondsLeft <= 0f)
                {
                    Restore(one.Renderer);
                    pool.Release(one.Renderer);
                    leaving.RemoveAt(i);
                    continue;
                }

                float gone = 1f - one.SecondsLeft / Mathf.Max(0.0001f, departureSeconds);

                one.Renderer.transform.position =
                    one.From + new Vector3(drift * gone, 0f, 0f);

                Color tint = one.Renderer.color;
                tint.a = 1f - gone;
                one.Renderer.color = tint;

                leaving[i] = one;
            }
        }

        /// <summary>Devuelve el renderer a opaco antes de que vuelva al pool.</summary>
        private static void Restore(SpriteRenderer renderer)
        {
            Color tint = renderer.color;
            tint.a = 1f;
            renderer.color = tint;
        }

        /// <summary>
        /// Ajusta cuántos se ven. Al fallar la comitiva baja de tramo y se van
        /// varios de una, así que esto no es "sumar o restar uno".
        /// </summary>
        private void OnFollowersChanged(int count)
        {
            int target = Mathf.Min(count, poolSize);

            while (active.Count > target)
            {
                SpriteRenderer last = active[active.Count - 1];
                active.RemoveAt(active.Count - 1);
                Depart(last);
            }

            while (active.Count < target)
            {
                SpriteRenderer follower = pool.Get();
                if (follower == null) return;   // pool agotado

                Dress(follower, active.Count);
                active.Add(follower);
            }
        }

        /// <summary>
        /// Saca a uno de la fila. Con <c>departureSeconds</c> en 0 vuelve al pool
        /// en el acto, que es el comportamiento viejo.
        /// </summary>
        private void Depart(SpriteRenderer renderer)
        {
            if (departureSeconds <= 0f)
            {
                Restore(renderer);
                pool.Release(renderer);
                return;
            }

            leaving.Add(new Leaving
            {
                Renderer = renderer,
                SecondsLeft = departureSeconds,
                From = renderer.transform.position
            });
        }

        /// <summary>
        /// Le da cara y paso propios. El offset de animación es lo que evita que
        /// la fila parezca un solo cuerpo con varias copias: sin esto, todos
        /// levantan la misma pierna en el mismo cuadro.
        /// </summary>
        private void Dress(SpriteRenderer follower, int place)
        {
            Restore(follower);

            var animator = follower.GetComponent<Animator>();
            if (animator == null || looks.Count == 0) return;

            animator.runtimeAnimatorController = looks[place % looks.Count];
            animator.Play(0, 0, place * gameConfig.Followers.DelayPerPosition);
        }

        private void Record(float time, float x)
        {
            head = (head + 1) % times.Length;
            times[head] = time;
            positions[head] = x;
        }

        /// <summary>
        /// Dónde estaba el predicador en ese instante. Si el pedido queda fuera de
        /// la historia —al arrancar la partida, cuando todavía no hay pasado— se
        /// devuelve la muestra más vieja, que deja a la fila saliendo desde atrás
        /// en vez de apareciendo de la nada.
        /// </summary>
        private float Sample(float time)
        {
            if (head < 0) return preacher.position.x;

            for (int step = 0; step < times.Length; step++)
            {
                int index = (head - step + times.Length) % times.Length;
                if (times[index] <= time) return positions[index];
            }

            return positions[(head + 1) % times.Length];
        }

        private bool ValidateSetup()
        {
            if (skillcheck == null || gameConfig == null
                || preacher == null || followerPrefab == null)
            {
                Debug.LogError(
                    $"[FollowerParade] '{name}' tiene referencias sin asignar " +
                    "(skillcheck, GameConfig, predicador o plantilla).", this);
                return false;
            }

            if (gameConfig.Followers == null)
            {
                Debug.LogError("[FollowerParade] El GameConfig no tiene FollowerConfig.", this);
                return false;
            }

            return true;
        }
    }
}
