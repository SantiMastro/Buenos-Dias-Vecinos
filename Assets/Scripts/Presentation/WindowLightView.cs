using BuenosDias.Config;
using BuenosDias.Gameplay;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Enciende las ventanas del barrio a medida que cae la tarde.
    ///
    /// Va aparte de <see cref="DayCycleView"/> porque son dos cosas distintas: esa
    /// pinta el cielo y tiñe el mundo, y esta le habla a las casas, que van y
    /// vienen del pool. Juntarlas obligaría a que la que pinta el cielo conozca al
    /// spawner sin necesitarlo.
    ///
    /// Se aplica a TODAS las activas cada cuadro, que nunca pasan de diez. Es más
    /// barato que llevar la cuenta de cuál casa entró al pool y cuál salió, y de
    /// paso una casa recién reciclada aparece ya con la luz correcta en vez de
    /// arrastrar la del vecindario anterior por un cuadro.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WindowLightView : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("De dónde sale la hora del día. Se lee; nunca se le pide nada.")]
        [SerializeField] private DayDirector director;

        [Tooltip("Asset raíz de balance. De acá sale a qué hora se prenden.")]
        [SerializeField] private GameConfig gameConfig;

        [Tooltip("Quién sabe qué casas están en pantalla ahora mismo.")]
        [SerializeField] private HouseSpawner houseSpawner;

        private DayCycleConfig cycle;

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }
            cycle = gameConfig.DayCycle;
        }

        /// <summary>
        /// En LateUpdate y no en Update: el spawner crea y recicla casas en su
        /// propio LateUpdate, y el orden entre dos componentes no está definido.
        /// Aplicar acá le da a una casa recién nacida, en el peor caso, un solo
        /// cuadro con la luz del cuadro anterior — que es el mismo valor, porque
        /// todas las casas comparten la hora.
        /// </summary>
        /// <remarks>
        /// La luz ya no es la misma para todas: cada casa prende según lo habitada
        /// que SE VE, que es su chance. Las que se ven habitadas prenden antes y
        /// además la segunda ventana; las que se ven vacías quedan a oscuras.
        /// </remarks>
        private void LateUpdate()
        {
            float progress = director.SunsetProgress;

            var houses = houseSpawner.Active;
            for (int i = 0; i < houses.Count; i++)
            {
                HouseInstance house = houses[i];
                if (house.Layout == null) continue;

                float chance = house.Layout.Chance;
                house.SetWindowLight(cycle.WindowLightAt(progress, chance));
                house.SetSignalWindowLight(cycle.SecondWindowLightAt(progress, chance));
            }
        }

        private bool ValidateSetup()
        {
            if (director == null || gameConfig == null || houseSpawner == null)
            {
                Debug.LogError(
                    $"[WindowLightView] '{name}' tiene referencias sin asignar " +
                    "(director, GameConfig o spawner de casas).", this);
                return false;
            }

            if (gameConfig.DayCycle == null)
            {
                Debug.LogError("[WindowLightView] El GameConfig no tiene DayCycleConfig.", this);
                return false;
            }

            return true;
        }
    }
}
