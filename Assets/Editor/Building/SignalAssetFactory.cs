using UnityEditor;
using UnityEngine;

namespace BuenosDias.EditorTools.Building
{
    /// <summary>
    /// Assets que necesitan las señales con capa de luz: el material aditivo y
    /// el clip de parpadeo del TV.
    /// </summary>
    public static class SignalAssetFactory
    {
        /// <summary>Material aditivo compartido por todas las capas de luz.</summary>
        public const string AdditiveMaterialPath = "Assets/Materials/SpriteAdditive.mat";

        /// <summary>Clip de parpadeo de la capa de luz del TV.</summary>
        public const string TelevisionFlickerPath = "Assets/Animations/FX/Tv_Parpadeo.anim";

        private const string ShaderName = "BuenosDias/Sprite Additive";
        private const int FlickerFps = 12;

        /// <summary>Crea o recupera el material aditivo.</summary>
        public static Material BuildAdditiveMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(AdditiveMaterialPath);
            if (existing != null) return existing;

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError(
                    $"[SignalAssetFactory] No encuentro el shader '{ShaderName}'. " +
                    "¿Compiló SpriteAdditive.shader?");
                return null;
            }

            AssetPathUtility.EnsureFolder("Assets/Materials");
            var material = new Material(shader) { name = "SpriteAdditive" };
            AssetDatabase.CreateAsset(material, AdditiveMaterialPath);
            return material;
        }

        /// <summary>
        /// Clip de parpadeo del TV.
        ///
        /// Anima el alpha de la capa aditiva, no el sprite: <c>env_ventana_tv</c>
        /// es un sprite único y no tiene frames. Al ser curva float le
        /// corresponde el keyframe de cierre —a diferencia de las curvas de
        /// sprite, donde ese keyframe extra alarga el clip un frame.
        /// </summary>
        public static AnimationClip BuildTelevisionFlicker()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(TelevisionFlickerPath);
            bool isNew = clip == null;

            if (isNew) clip = new AnimationClip();
            else clip.ClearCurves();

            clip.frameRate = FlickerFps;

            // Irregular a propósito: un parpadeo regular se lee como un LED, no
            // como una tele. Los valores no siguen ningún patrón reconocible.
            float[] alphas = { 0.55f, 0.90f, 0.45f, 1.00f, 0.70f, 0.85f,
                               0.50f, 0.95f, 0.65f, 0.80f, 0.60f, 1.00f };

            var binding = EditorCurveBinding.FloatCurve(
                string.Empty, typeof(SpriteRenderer), "m_Color.a");

            AnimationUtility.SetEditorCurve(clip, binding, BuildSteppedCurve(alphas));

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (isNew)
            {
                AssetPathUtility.EnsureFolder("Assets/Animations/FX");
                AssetDatabase.CreateAsset(clip, TelevisionFlickerPath);
            }
            else
            {
                EditorUtility.SetDirty(clip);
            }

            return clip;
        }

        private static AnimationCurve BuildSteppedCurve(float[] values)
        {
            float step = 1f / FlickerFps;

            // Un keyframe de cierre con el valor inicial: en curvas float el clip
            // termina EN el último keyframe, así que sin esto el loop pegaría un
            // salto al volver al principio.
            var keys = new Keyframe[values.Length + 1];

            for (int i = 0; i < values.Length; i++) keys[i] = Stepped(i * step, values[i]);
            keys[values.Length] = Stepped(values.Length * step, values[0]);

            return new AnimationCurve(keys);
        }

        /// <summary>Keyframe con tangentes infinitas: escalón, sin interpolar.</summary>
        private static Keyframe Stepped(float time, float value)
        {
            return new Keyframe(time, value, float.PositiveInfinity, float.PositiveInfinity);
        }
    }
}
