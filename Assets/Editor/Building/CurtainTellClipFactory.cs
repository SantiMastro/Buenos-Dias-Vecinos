using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BuenosDias.EditorTools.Building
{
    /// <summary>
    /// Construye la animación del tell de cortina, que es la señal más
    /// importante del juego: lo único que le avisa al jugador que hay alguien
    /// adentro antes de que se abra la puerta.
    ///
    /// Tiene fábrica propia y no pasa por el pipeline genérico porque su timing
    /// no es un ciclo uniforme: el frame asomado dura 0.4 s y después vuelve al
    /// reposo, y eso no se puede expresar con "N frames a X fps".
    ///
    /// El grafo es reposo + golpe con Trigger, así que disparar el asomo desde
    /// el juego es una sola llamada y el volver a la posición quieta lo resuelve
    /// la vista. La repetición cada 1.2 s NO vive acá: es lógica de espera y la
    /// configura <c>WaitConfig</c>.
    /// </summary>
    public static class CurtainTellClipFactory
    {
        /// <summary>Clip de reposo: la cortina quieta.</summary>
        public const string RestClipPath = "Assets/Animations/FX/Cortina_Quieta.anim";

        /// <summary>Clip del asomo.</summary>
        public const string AsomoClipPath = "Assets/Animations/FX/Cortina_Asomo.anim";

        /// <summary>Controller que reproduce las dos.</summary>
        public const string ControllerPath = "Assets/Animations/FX/Cortina.controller";

        /// <summary>Trigger que dispara el asomo. Lo levanta la espera.</summary>
        public const string AsomarTrigger = "Asomar";

        /// <summary>Nombre del grupo de sprites que consume esta fábrica.</summary>
        public const string SpriteGroup = "fx_cortina_movimiento";

        /// <summary>
        /// Cuánto se queda asomada, en segundos. Vive acá porque es la duración
        /// del clip; se puede reajustar abriendo el clip en la ventana de
        /// Animation sin tocar código.
        /// </summary>
        private const float AsomoSeconds = 0.4f;

        // 5 fps hace que 0.4 s caiga en un límite exacto de frame (frame 2).
        // Con 12 fps daría 4.8 y el keyframe quedaría a mitad de camino.
        private const int FramesPerSecond = 5;

        /// <summary>Construye los dos clips y el controller. Devuelve null si falta el arte.</summary>
        public static AnimatorController Build()
        {
            Sprite quieta = LoadFrame("fx_cortina_movimiento_00");
            Sprite asomada = LoadFrame("fx_cortina_movimiento_01");
            if (quieta == null || asomada == null) return null;

            AnimationClip restClip = BuildClip(
                RestClipPath, new[] { (0f, quieta) }, loop: true);

            // El asomo empieza YA en el frame asomado y vuelve al quieto a los
            // 0.4 s. Arrancar por el frame quieto retrasaría la lectura.
            AnimationClip asomoClip = BuildClip(
                AsomoClipPath, new[] { (0f, asomada), (AsomoSeconds, quieta) }, loop: false);

            return AnimatorGraphFactory.BuildTriggeredOneShot(
                ControllerPath, AsomarTrigger,
                new AnimatorGraphFactory.StateDefinition("Quieta", restClip, 0),
                new AnimatorGraphFactory.StateDefinition("Asomo", asomoClip, 1));
        }

        private static Sprite LoadFrame(string spriteName)
        {
            string[] guids = AssetDatabase.FindAssets($"t:Sprite {spriteName}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != spriteName) continue;
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            Debug.LogError($"[CurtainTellClipFactory] Falta el sprite '{spriteName}.png'.");
            return null;
        }

        private static AnimationClip BuildClip(
            string assetPath, IReadOnlyList<(float time, Sprite sprite)> frames, bool loop)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            bool isNew = clip == null;

            if (isNew) clip = new AnimationClip();
            else clip.ClearCurves();

            clip.frameRate = FramesPerSecond;

            var binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = string.Empty,
                propertyName = "m_Sprite"
            };

            var keyframes = new ObjectReferenceKeyframe[frames.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = frames[i].time,
                    value = frames[i].sprite
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (isNew)
            {
                AssetPathUtility.EnsureFolder("Assets/Animations/FX");
                AssetDatabase.CreateAsset(clip, assetPath);
            }
            else
            {
                EditorUtility.SetDirty(clip);
            }

            return clip;
        }
    }
}
