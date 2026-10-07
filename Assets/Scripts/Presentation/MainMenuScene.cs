using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;

namespace BuenosDias.Presentation
{
    /// <summary>
    /// Escena de menu principal: musica, nubes que se mueven, titulo que respira,
    /// texto parpadeante y, al tocar cualquier boton/click/pantalla, carga la escena de juego.
    /// </summary>
    public sealed class MainMenuScene : MonoBehaviour
    {
        [SerializeField] private string gameScene = "Game";
        [SerializeField] private float ignoreInputSeconds = 0.5f;
        [SerializeField] private float musicVolume = 0.5f;
        [SerializeField] private float fadeOutSeconds = 0.45f;

        [Header("Animacion")]
        [SerializeField] private Transform[] clouds;
        [SerializeField] private float[] cloudSpeeds;
        [SerializeField] private float cloudWrapWidth = 24f;
        [SerializeField] private Transform logo;
        [SerializeField] private float logoBobPixels = 2f;
        [SerializeField] private GameObject prompt;
        [SerializeField] private float blinkHertz = 1.4f;

        private const float PixelsPerUnit = 32f;
        private AudioSource music;
        private System.IDisposable subscription;
        private float logoBaseY;
        private float[] cloudStartX;
        private bool leaving;
        private float born;

        private void Awake()
        {
            Time.timeScale = 1f;
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.spatialBlend = 0f;
            music.volume = 0f;
            AudioClip clip = Resources.Load<AudioClip>("Audio/musica_menu");
            if (clip == null) clip = Resources.Load<AudioClip>("Audio/musica_fondo");
            if (clip != null)
            {
                music.clip = clip;
                music.Play();
            }

            if (logo != null) logoBaseY = logo.localPosition.y;
            if (clouds != null)
            {
                cloudStartX = new float[clouds.Length];
                for (int i = 0; i < clouds.Length; i++)
                    cloudStartX[i] = clouds[i] != null ? clouds[i].localPosition.x : 0f;
            }
            born = Time.unscaledTime;
        }

        private void OnEnable()
        {
            subscription = InputSystem.onAnyButtonPress.Call(OnPressed);
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;
        }

        private void OnPressed(InputControl control)
        {
            if (leaving || Time.unscaledTime - born < ignoreInputSeconds) return;
            leaving = true;
            StartCoroutine(Leave());
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            if (!leaving && music.clip != null)
                music.volume = Mathf.MoveTowards(music.volume, musicVolume, Time.unscaledDeltaTime / 1.2f);

            if (clouds != null && cloudSpeeds != null)
            {
                for (int i = 0; i < clouds.Length; i++)
                {
                    if (clouds[i] == null) continue;
                    float speed = i < cloudSpeeds.Length ? cloudSpeeds[i] : 0.1f;
                    float offset = (t * speed) % cloudWrapWidth;
                    Vector3 p = clouds[i].localPosition;
                    p.x = Snap(cloudStartX[i] - offset);
                    clouds[i].localPosition = p;
                }
            }

            if (logo != null)
            {
                Vector3 p = logo.localPosition;
                p.y = logoBaseY + Snap(Mathf.Sin(t * 1.6f) * logoBobPixels / PixelsPerUnit);
                logo.localPosition = p;
            }

            if (prompt != null)
                prompt.SetActive(leaving || Mathf.Repeat(t * blinkHertz, 1f) < 0.65f);
        }

        private static float Snap(float v)
        {
            return Mathf.Round(v * PixelsPerUnit) / PixelsPerUnit;
        }

        private IEnumerator Leave()
        {
            float start = music.volume;
            float elapsed = 0f;
            while (elapsed < fadeOutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                music.volume = Mathf.Lerp(start, 0f, elapsed / fadeOutSeconds);
                yield return null;
            }
            SceneManager.LoadScene(gameScene);
        }
    }
}
