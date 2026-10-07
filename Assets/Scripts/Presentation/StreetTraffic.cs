using System.Collections.Generic;
using BuenosDias.Config;
using BuenosDias.Gameplay;
using UnityEngine;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Tráfico de la calle: autos que pasan por la banda de asfalto y largan humo
    /// por el caño de escape.
    ///
    /// Es puro decorado: no habla con la simulación ni con el reloj, así que no
    /// necesita eventos ni se entera de lo que pasa en la partida. Lee solo la
    /// posición de la cámara para saber dónde nacer y cuándo irse.
    ///
    /// Dos carriles, uno por sentido, para que se crucen sin pisarse: el de abajo
    /// (más cerca de cámara) va hacia la derecha y el de arriba hacia la izquierda.
    /// Los autos del mismo carril no se superponen: el que viene atrás iguala la
    /// velocidad del de adelante en vez de atravesarlo.
    ///
    /// Todo se mueve en píxeles enteros (la cámara es Pixel Perfect), si no el
    /// auto vibra al avanzar. El humo son sprites chicos con un fundido de alpha,
    /// no un ParticleSystem: así queda con la misma grilla de píxeles que el
    /// resto del arte y se tiñe con la luz global como cualquier sprite.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StreetTraffic : MonoBehaviour
    {
        private const float PixelsPerUnit = 32f;
        private const int PuffStages = 3;

        [Header("Referencias")]
        [Tooltip("Cámara que sigue al jugador. Se asigna por Inspector.")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Material de sprite lit, el mismo del resto del mundo. Sin él " +
                 "los autos no se teñirían con el día y la noche.")]
        [SerializeField] private Material spriteMaterial;

        [Tooltip("Autos posibles. Pivot abajo al centro, mirando a la derecha.")]
        [SerializeField] private Sprite[] carSprites;

        [Header("Patrulla")]
        [Tooltip("Sprite de la patrulla, con la barra de luces ya dibujada en el techo " +
                 "(sin colores encendidos). Vacío = no hay patrullas.")]
        [SerializeField] private Sprite policeSprite;

        [Tooltip("Misma patrulla con la sirena prendida. Se alterna con la apagada.")]
        [SerializeField] private Sprite policeSirenSprite;

        [Tooltip("Probabilidad de que el próximo auto sea la patrulla.")]
        [SerializeField, Range(0f, 1f)] private float policeChance = 0.14f;

        [Tooltip("Material ADITIVO para las luces de la sirena y su halo.")]
        [SerializeField] private Material glowMaterial;

        [Tooltip("Centro de la baliza respecto del pivot del sprite: X desde el " +
                 "centro, Y desde el piso, en píxeles. Mirando a la derecha.")]
        [SerializeField] private Vector2 sirenOffsetPixels = new Vector2(-7.5f, 46.5f);

        [Tooltip("Segundos que dura un ciclo rojo-azul.")]
        [SerializeField, Min(0.2f)] private float sirenPeriod = 1f;

        [Header("Luces de noche")]
        [Tooltip("Director del día: de ahí sale la hora para prender los faros. " +
                 "Vacío = los autos nunca prenden luces.")]
        [SerializeField] private DayDirector director;

        [Tooltip("Config del juego (ciclo del día). Vacío = sin luces.")]
        [SerializeField] private GameConfig gameConfig;

        [Tooltip("Altura de los faros sobre el piso, en píxeles.")]
        [SerializeField] private float headlightHeightPixels = 11f;

        [Tooltip("Cuánto entran los faros hacia adentro del auto, en píxeles.")]
        [SerializeField] private float lightInsetPixels = 3f;

        [Tooltip("Opacidad máxima del haz de luz sobre el asfalto.")]
        [SerializeField, Range(0f, 1f)] private float beamAlpha = 0.9f;

        [Header("Tráfico")]
        [Tooltip("Segundos entre un auto y el siguiente (mínimo, máximo).")]
        [SerializeField] private Vector2 spawnIntervalSeconds = new Vector2(3.5f, 8f);

        [Tooltip("Velocidad en el mundo de los que van hacia la derecha, unidades/s.")]
        [SerializeField] private Vector2 rightSpeed = new Vector2(6f, 9f);

        [Tooltip("Velocidad en el mundo de los que van hacia la izquierda, unidades/s.")]
        [SerializeField] private Vector2 leftSpeed = new Vector2(4f, 7f);

        [Tooltip("Separación mínima entre dos autos del mismo carril, en unidades.")]
        [SerializeField] private float minGapUnits = 1.5f;

        [Header("Carriles")]
        [Tooltip("Y de las ruedas del carril de abajo (hacia la derecha).")]
        [SerializeField] private float frontLaneGroundY = -1.95f;

        [Tooltip("Y de las ruedas del carril de arriba (hacia la izquierda).")]
        [SerializeField] private float backLaneGroundY = -1.72f;

        [SerializeField] private string sortingLayerName = "Ground";
        [SerializeField] private int frontLaneOrder = 22;
        [SerializeField] private int backLaneOrder = 18;

        [Header("Humo de escape")]
        [Tooltip("Segundos entre bocanadas.")]
        [SerializeField] private float puffInterval = 0.11f;

        [Tooltip("Cuánto dura una bocanada, en segundos.")]
        [SerializeField] private float puffLife = 1f;

        [Tooltip("Cuánto sube el humo, unidades/s.")]
        [SerializeField] private float puffRise = 0.6f;

        [Tooltip("Cuánto se corre hacia atrás del auto, unidades/s.")]
        [SerializeField] private float puffDrift = 0.45f;

        [Tooltip("Opacidad con la que nace la bocanada.")]
        [SerializeField, Range(0f, 1f)] private float puffAlpha = 0.6f;

        [Tooltip("Altura del caño sobre el piso, en píxeles.")]
        [SerializeField] private float exhaustHeightPixels = 5f;

        private sealed class Car
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public float X;
            public float Speed;
            public float HalfWidth;
            public int Direction;
            public float GroundY;
            public float NextPuff;
            public bool Siren;
            public SpriteRenderer FlareRed, FlareBlue;
            public float SirenClock;
            public SpriteRenderer Beam, HeadSpot, TailSpot;
        }

        private sealed class Puff
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public float X;
            public float Y;
            public float Age;
            public float Life;
            public float DriftDirection;
        }

        private readonly List<Car> cars = new List<Car>();
        private readonly Stack<Car> carPool = new Stack<Car>();
        private readonly List<Puff> puffs = new List<Puff>();
        private readonly Stack<Puff> puffPool = new Stack<Puff>();

        private Sprite[] puffSprites;
        private DayCycleConfig cycle;
        private Transform carsRoot;
        private Transform puffsRoot;
        private float nextSpawnIn;

        private void Awake()
        {
            if (!ValidateSetup()) { enabled = false; return; }

            if (gameConfig != null) cycle = gameConfig.DayCycle;

            carsRoot = new GameObject("Autos").transform;
            carsRoot.SetParent(transform, false);
            puffsRoot = new GameObject("Humo").transform;
            puffsRoot.SetParent(transform, false);

            puffSprites = new[]
            {
                MakeDisc(3, new Color32(222, 214, 230, 255)),
                MakeDisc(5, new Color32(206, 198, 218, 255)),
                MakeDisc(7, new Color32(190, 182, 204, 255)),
            };

            // el primer auto no tarda en aparecer
            nextSpawnIn = Random.Range(0.5f, 1.5f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float camX = targetCamera.transform.position.x;
            float halfView = targetCamera.orthographicSize * targetCamera.aspect;

            nextSpawnIn -= dt;
            if (nextSpawnIn <= 0f) TrySpawn(camX, halfView);

            UpdateCars(dt, camX, halfView);
            UpdatePuffs(dt);
        }

        // --- autos --------------------------------------------------------------

        private void TrySpawn(float camX, float halfView)
        {
            int direction = Random.value < 0.5f ? 1 : -1;
            bool police = policeSprite != null && Random.value < policeChance;
            Sprite sprite = police ? policeSprite : carSprites[Random.Range(0, carSprites.Length)];
            float halfWidth = sprite.bounds.extents.x;

            float spawnX = camX - direction * (halfView + halfWidth + 1f);

            // si el carril está ocupado en el borde, se reintenta enseguida
            for (int i = 0; i < cars.Count; i++)
            {
                Car other = cars[i];
                if (other.Direction != direction) continue;
                if (Mathf.Abs(other.X - spawnX) < other.HalfWidth + halfWidth + minGapUnits)
                {
                    nextSpawnIn = 0.4f;
                    return;
                }
            }

            Car car = RentCar();
            car.Direction = direction;
            car.HalfWidth = halfWidth;
            car.X = spawnX;
            car.GroundY = direction > 0 ? frontLaneGroundY : backLaneGroundY;
            car.Speed = direction > 0
                ? Random.Range(rightSpeed.x, rightSpeed.y)
                : Random.Range(leftSpeed.x, leftSpeed.y);
            car.NextPuff = Random.Range(0f, puffInterval);

            car.Renderer.sprite = sprite;
            car.Renderer.flipX = direction < 0;
            car.Renderer.sortingOrder = direction > 0 ? frontLaneOrder : backLaneOrder;
            ConfigureSiren(car, police);
            ConfigureLights(car, sprite);
            car.Transform.gameObject.SetActive(true);
            Place(car);
            cars.Add(car);

            nextSpawnIn = Random.Range(spawnIntervalSeconds.x, spawnIntervalSeconds.y);
        }

        private void UpdateCars(float dt, float camX, float halfView)
        {
            float nightAmount = NightAmount();

            for (int i = cars.Count - 1; i >= 0; i--)
            {
                Car car = cars[i];

                LimitSpeedToLeader(car);
                car.X += car.Direction * car.Speed * dt;
                Place(car);
                EmitExhaust(car, dt);
                if (car.Siren) UpdateSiren(car, dt);
                UpdateLights(car, nightAmount);

                float away = (car.X - camX) * car.Direction;
                if (away > halfView + car.HalfWidth + 2f)
                {
                    car.Transform.gameObject.SetActive(false);
                    carPool.Push(car);
                    cars.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Si hay otro auto del mismo carril justo adelante, se iguala su
        /// velocidad: un auto nunca atraviesa a otro.
        /// </summary>
        private void LimitSpeedToLeader(Car car)
        {
            for (int i = 0; i < cars.Count; i++)
            {
                Car other = cars[i];
                if (other == car || other.Direction != car.Direction) continue;

                float ahead = (other.X - car.X) * car.Direction;
                if (ahead <= 0f) continue;

                float gap = ahead - other.HalfWidth - car.HalfWidth;
                if (gap < minGapUnits && car.Speed > other.Speed) car.Speed = other.Speed;
            }
        }

        private void Place(Car car)
        {
            car.Transform.position = new Vector3(Snap(car.X), Snap(car.GroundY), 0f);
        }

        private Car RentCar()
        {
            if (carPool.Count > 0) return carPool.Pop();

            var go = new GameObject("Auto");
            go.transform.SetParent(carsRoot, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = spriteMaterial;
            renderer.sortingLayerName = sortingLayerName;

            return new Car { Transform = go.transform, Renderer = renderer };
        }

        // --- luces de noche -----------------------------------------------------

        /// <summary>
        /// Cuánto de noche está la calle, de 0 a 1. Sigue la misma curva que las
        /// farolas, así los autos prenden los faros cuando ellas prenden.
        /// </summary>
        private float NightAmount()
        {
            if (director == null || cycle == null || glowMaterial == null) return 0f;

            float windows = cycle.WindowLightAt(director.SunsetProgress);
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(windows * 1.6f));
        }

        /// <summary>
        /// Acomoda los faros del auto que sale: haz y punto blanco adelante, punto
        /// rojo atrás. Se crean una sola vez por auto del pool; acá solo se ubican
        /// según el largo del sprite y el sentido de marcha.
        /// </summary>
        private void ConfigureLights(Car car, Sprite sprite)
        {
            if (glowMaterial == null || director == null) return;

            if (car.Beam == null)
            {
                car.Beam = MakeLight(car, "Haz", LampLightSprites.Beam);
                car.HeadSpot = MakeLight(car, "Faro", LampLightSprites.Spot);
                car.TailSpot = MakeLight(car, "LuzTrasera", LampLightSprites.Spot);
                car.TailSpot.color = new Color(0.86f, 0.12f, 0.14f, 0f);
            }

            float side = car.Renderer.flipX ? -1f : 1f;
            float half = sprite.bounds.extents.x;
            float inset = lightInsetPixels / PixelsPerUnit;
            float y = headlightHeightPixels / PixelsPerUnit;

            car.HeadSpot.transform.localPosition = new Vector3(side * (half - inset), y, 0f);
            car.Beam.transform.localPosition = new Vector3(side * (half - inset), y, 0f);
            car.Beam.flipX = side < 0f;
            car.TailSpot.transform.localPosition = new Vector3(-side * (half - inset), y, 0f);

            // el haz va DETRÁS del auto (la carrocería lo tapa en el arranque) y los
            // puntos de luz delante
            car.Beam.sortingOrder = car.Renderer.sortingOrder - 1;
            car.HeadSpot.sortingOrder = car.Renderer.sortingOrder + 1;
            car.TailSpot.sortingOrder = car.Renderer.sortingOrder + 1;
        }

        private SpriteRenderer MakeLight(Car car, string name, Sprite sprite)
        {
            SpriteRenderer renderer = MakeFlare(car, name, sprite);
            renderer.enabled = false;
            return renderer;
        }

        private void UpdateLights(Car car, float night)
        {
            if (car.Beam == null) return;

            bool on = night > 0.01f;
            car.Beam.enabled = on;
            car.HeadSpot.enabled = on;
            car.TailSpot.enabled = on;
            if (!on) return;

            car.Beam.color = new Color(1f, 1f, 1f, beamAlpha * night);
            car.HeadSpot.color = new Color(1f, 1f, 1f, night);
            car.TailSpot.color = new Color(0.86f, 0.12f, 0.14f, night);
        }

        // --- sirena -------------------------------------------------------------

        private const int FlareSize = 15;
        private static Sprite flareRed;
        private static Sprite flareBlue;

        /// <summary>
        /// La sirena son dos imágenes del auto —apagada y con las luces rojas y
        /// azules prendidas— que se alternan, más un DESTELLO de color sobre la
        /// baliza en cada prendida. El destello es rojo o azul de verdad, nunca
        /// blanco: se dibuja con esos colores y con alpha escalonado, así que no hay
        /// un núcleo quemado. Es hijo del auto y sigue su movimiento y su snap.
        /// </summary>
        private void ConfigureSiren(Car car, bool police)
        {
            car.Siren = police && policeSirenSprite != null;
            car.SirenClock = Random.value * sirenPeriod;

            if (car.FlareRed != null)
            {
                car.FlareRed.gameObject.SetActive(car.Siren);
                car.FlareBlue.gameObject.SetActive(car.Siren);
            }

            if (!car.Siren || glowMaterial == null) return;

            if (car.FlareRed == null)
            {
                car.FlareRed = MakeFlare(car, "DestelloRojo", FlareSprite(true));
                car.FlareBlue = MakeFlare(car, "DestelloAzul", FlareSprite(false));
            }

            // mitad roja y mitad azul de la baliza, en píxeles desde el centro del sprite
            float side = car.Renderer.flipX ? -1f : 1f;
            float y = sirenOffsetPixels.y / PixelsPerUnit;
            car.FlareRed.transform.localPosition =
                new Vector3(side * (sirenOffsetPixels.x - 2f) / PixelsPerUnit, y, 0f);
            car.FlareBlue.transform.localPosition =
                new Vector3(side * (sirenOffsetPixels.x + 3f) / PixelsPerUnit, y, 0f);

            car.FlareRed.sortingOrder = car.Renderer.sortingOrder + 1;
            car.FlareBlue.sortingOrder = car.Renderer.sortingOrder + 1;
            car.FlareRed.enabled = false;
            car.FlareBlue.enabled = false;
        }

        private SpriteRenderer MakeFlare(Car car, string name, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(car.Transform, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = glowMaterial;
            renderer.sortingLayerName = sortingLayerName;
            return renderer;
        }

        /// <summary>
        /// Rojo, apagado, azul, apagado. Onda cuadrada: se lee como luz de píxel art.
        /// </summary>
        private void UpdateSiren(Car car, float dt)
        {
            car.SirenClock += dt;
            float t = Mathf.Repeat(car.SirenClock / sirenPeriod, 1f);

            bool red = t < 0.22f;
            bool blue = t >= 0.5f && t < 0.72f;

            Sprite frame = (red || blue) ? policeSirenSprite : policeSprite;
            if (car.Renderer.sprite != frame) car.Renderer.sprite = frame;

            if (car.FlareRed != null) car.FlareRed.enabled = red;
            if (car.FlareBlue != null) car.FlareBlue.enabled = blue;
        }

        /// <summary>
        /// Destello en cruz con anillos de píxel: núcleo de color, cuatro rayitas y
        /// dos coronas más tenues. Impar de ancho para que el centro caiga en un
        /// píxel. Colores saturados y NUNCA blancos: el material aditivo los suma a
        /// lo que haya detrás, y un núcleo claro se quemaba a blanco.
        /// </summary>
        private static Sprite FlareSprite(bool red)
        {
            Sprite cached = red ? flareRed : flareBlue;
            if (cached != null) return cached;

            Color32 core = red ? new Color32(205, 30, 42, 255) : new Color32(34, 84, 215, 255);

            var texture = new Texture2D(FlareSize, FlareSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            int c = FlareSize / 2;
            for (int y = 0; y < FlareSize; y++)
            {
                for (int x = 0; x < FlareSize; x++)
                {
                    int dx = Mathf.Abs(x - c), dy = Mathf.Abs(y - c);
                    int manhattan = dx + dy;
                    float a = 0f;

                    if (manhattan <= 1) a = 1f;
                    else if ((dx == 0 || dy == 0) && manhattan <= 6) a = 0.75f;
                    else if (manhattan <= 3) a = 0.5f;
                    else if (manhattan <= 5) a = 0.22f;

                    texture.SetPixel(x, y, new Color32(core.r, core.g, core.b, (byte)(a * 255f)));
                }
            }
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, FlareSize, FlareSize),
                new Vector2(0.5f, 0.5f), PixelsPerUnit);
            if (red) flareRed = sprite; else flareBlue = sprite;
            return sprite;
        }

        // --- humo ---------------------------------------------------------------

        private void EmitExhaust(Car car, float dt)
        {
            car.NextPuff -= dt;
            if (car.NextPuff > 0f) return;
            car.NextPuff += puffInterval * Random.Range(0.8f, 1.3f);

            // el caño está en la cola: atrás de donde mira el auto
            float rearX = car.X - car.Direction * (car.HalfWidth - 2f / PixelsPerUnit);
            float y = car.GroundY + exhaustHeightPixels / PixelsPerUnit;

            Puff puff = RentPuff();
            puff.X = rearX;
            puff.Y = y;
            puff.Age = 0f;
            puff.Life = puffLife * Random.Range(0.8f, 1.2f);
            puff.DriftDirection = -car.Direction;
            puff.Renderer.sortingOrder = car.Renderer.sortingOrder + 1;
            puff.Transform.gameObject.SetActive(true);
            ApplyPuff(puff);
            puffs.Add(puff);
        }

        private void UpdatePuffs(float dt)
        {
            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                Puff puff = puffs[i];
                puff.Age += dt;

                if (puff.Age >= puff.Life)
                {
                    puff.Transform.gameObject.SetActive(false);
                    puffPool.Push(puff);
                    puffs.RemoveAt(i);
                    continue;
                }

                puff.Y += puffRise * dt;
                puff.X += puff.DriftDirection * puffDrift * dt;
                ApplyPuff(puff);
            }
        }

        private void ApplyPuff(Puff puff)
        {
            float t = Mathf.Clamp01(puff.Age / puff.Life);

            // tres tamaños en vez de una escala continua: crece de a saltos de
            // píxel, como el resto del arte
            int stage = Mathf.Min(PuffStages - 1, (int)(t * PuffStages));
            puff.Renderer.sprite = puffSprites[stage];
            puff.Renderer.color = new Color(1f, 1f, 1f, puffAlpha * (1f - t));
            puff.Transform.position = new Vector3(Snap(puff.X), Snap(puff.Y), 0f);
        }

        private Puff RentPuff()
        {
            if (puffPool.Count > 0) return puffPool.Pop();

            var go = new GameObject("Bocanada");
            go.transform.SetParent(puffsRoot, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = spriteMaterial;
            renderer.sortingLayerName = sortingLayerName;

            return new Puff { Transform = go.transform, Renderer = renderer };
        }

        // --- utilidades -----------------------------------------------------------

        private static float Snap(float value)
        {
            return Mathf.Round(value * PixelsPerUnit) / PixelsPerUnit;
        }

        private static Sprite MakeDisc(int diameter, Color32 color)
        {
            var texture = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            float radius = diameter * 0.5f;
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    bool inside = dx * dx + dy * dy <= radius * radius;
                    texture.SetPixel(x, y, inside ? (Color)color : Color.clear);
                }
            }
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, diameter, diameter),
                new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        private bool ValidateSetup()
        {
            if (targetCamera == null || spriteMaterial == null)
            {
                Debug.LogError(
                    $"[StreetTraffic] '{name}' tiene referencias sin asignar (cámara o material).", this);
                return false;
            }

            if (carSprites == null || carSprites.Length == 0)
            {
                Debug.LogError($"[StreetTraffic] '{name}' no tiene autos asignados.", this);
                return false;
            }

            return true;
        }
    }
}
