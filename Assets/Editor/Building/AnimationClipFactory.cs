using BuenosDias.Config;
using UnityEditor;
using UnityEngine;

namespace BuenosDias.EditorTools.Building
{
    /// <summary>
    /// Construye AnimationClips de sprites como assets reales, editables desde
    /// la ventana de Animation.
    /// </summary>
    public static class AnimationClipFactory
    {
        /// <summary>
        /// Crea o actualiza el clip en <paramref name="assetPath"/> con los frames
        /// de la secuencia.
        ///
        /// Si el asset ya existe se reescribe en el lugar en vez de borrarlo y
        /// crearlo de nuevo: así conserva su GUID y no se rompen las referencias
        /// de los AnimatorControllers ni de los prefabs al reconstruir.
        /// </summary>
        public static AnimationClip Build(
            SpriteSequenceScanner.Sequence sequence,
            AnimationBuildConfig.Rule rule,
            string assetPath)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            bool isNew = clip == null;

            if (isNew) clip = new AnimationClip();
            else clip.ClearCurves();

            clip.frameRate = rule.framesPerSecond;

            AnimationUtility.SetObjectReferenceCurve(
                clip, SpriteBinding, BuildKeyframes(sequence, rule.framesPerSecond));

            ApplyLoopSetting(clip, rule.playMode);

            if (isNew)
            {
                AssetPathUtility.EnsureFolder(
                    System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));
                AssetDatabase.CreateAsset(clip, assetPath);
            }
            else
            {
                EditorUtility.SetDirty(clip);
            }

            return clip;
        }

        /// <summary>
        /// Binding al sprite del SpriteRenderer que cuelga del mismo GameObject
        /// que el Animator (por eso <c>path</c> vacío).
        /// </summary>
        private static EditorCurveBinding SpriteBinding => new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = string.Empty,
            propertyName = "m_Sprite"
        };

        private static ObjectReferenceKeyframe[] BuildKeyframes(
            SpriteSequenceScanner.Sequence sequence, int framesPerSecond)
        {
            int count = sequence.FrameCount;
            float step = 1f / framesPerSecond;

            // Un keyframe por sprite y nada más. Para curvas de referencia a
            // objeto, Unity calcula length = tiempoDelÚltimoKey + 1/fps, o sea
            // que al último frame ya le da su duración completa. Agregar un
            // keyframe extra al final (que es lo que uno haría por analogía con
            // las curvas float) alarga el clip un frame y deja el último sprite
            // en pantalla el doble de tiempo: en la caminata se siente como un
            // tirón en cada vuelta del ciclo.
            var keyframes = new ObjectReferenceKeyframe[count];

            for (int i = 0; i < count; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i * step,
                    value = sequence.Frames[i]
                };
            }

            return keyframes;
        }

        private static void ApplyLoopSetting(AnimationClip clip, ClipPlayMode playMode)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = playMode == ClipPlayMode.Loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }
    }
}
