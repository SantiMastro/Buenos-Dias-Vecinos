using System.Collections.Generic;
using System.Text;
using BuenosDias.Config;
using UnityEditor;
using UnityEngine;

namespace BuenosDias.EditorTools.Building
{
    /// <summary>
    /// Genera todos los AnimationClips y AnimatorControllers del juego a partir
    /// de los PNG sueltos.
    ///
    /// Es idempotente y reescribe los assets en el lugar, así que se puede correr
    /// cada vez que llegue arte nuevo sin romper las referencias existentes.
    /// </summary>
    public static class BuildAnimationsMenu
    {
        private const string ConfigPath = "Assets/ScriptableObjects/Config/AnimationBuildConfig.asset";

        private static readonly string[] ScanFolders =
        {
            "Assets/Sprites/Characters",
            "Assets/Sprites/FX"
        };

        [MenuItem("Tools/Buenos Días/Fase 2 · Construir animaciones", priority = 200)]
        public static void BuildFromMenu()
        {
            Debug.Log(Build());
        }

        /// <summary>Construye todo y devuelve un reporte legible.</summary>
        public static string Build()
        {
            AnimationBuildConfig config = LoadOrCreateConfig();
            var report = new StringBuilder("[Fase 2] Animaciones construidas.\n");

            AssetDatabase.StartAssetEditing();
            Dictionary<string, AnimationClip> clips;
            try
            {
                clips = BuildClips(config, report);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            report.AppendLine("Controllers:");
            int overrides = CharacterControllerBuilder.BuildAll(clips, report);

            report.AppendLine(CurtainTellClipFactory.Build() != null
                ? "  Cortina.controller ........ Quieta + Asomo, trigger 'Asomar'"
                : "  Cortina.controller ........ SIN CONSTRUIR (falta el arte)");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.AppendLine($"Total: {clips.Count} clips, {overrides} overrides.");
            return report.ToString();
        }

        private static Dictionary<string, AnimationClip> BuildClips(
            AnimationBuildConfig config, StringBuilder report)
        {
            var built = new Dictionary<string, AnimationClip>();
            List<SpriteSequenceScanner.Sequence> sequences = SpriteSequenceScanner.Scan(ScanFolders);

            report.AppendLine("Clips:");
            var skipped = new List<string>();

            foreach (SpriteSequenceScanner.Sequence sequence in sequences)
            {
                if (sequence.GroupName == CurtainTellClipFactory.SpriteGroup)
                {
                    // La cortina tiene fábrica propia: su timing no es uniforme.
                    continue;
                }

                if (!config.TryResolve(sequence.GroupName, out AnimationBuildConfig.Rule rule))
                {
                    // Sin regla no se anima. La carpeta de FX tiene sprites sueltos
                    // de decoración que no son secuencias.
                    skipped.Add(sequence.GroupName);
                    continue;
                }

                string path = AnimationAssetNaming.ClipPathFor(sequence.GroupName);
                AnimationClip clip = AnimationClipFactory.Build(sequence, rule, path);

                built[AnimationAssetNaming.ClipNameFor(sequence.GroupName)] = clip;

                report.AppendLine(
                    $"  {AnimationAssetNaming.ClipNameFor(sequence.GroupName),-24} " +
                    $"{sequence.FrameCount} frames, {rule.framesPerSecond} fps, {rule.playMode}");
            }

            if (skipped.Count > 0)
            {
                report.AppendLine(
                    $"Sin animar ({skipped.Count}, sprites sueltos sin regla): " +
                    string.Join(", ", skipped));
            }

            return built;
        }

        /// <summary>
        /// Carga el asset de configuración, creándolo con los valores por defecto
        /// si es la primera corrida. Que exista como asset es lo que permite
        /// cambiar los fps sin abrir un <c>.cs</c>.
        /// </summary>
        private static AnimationBuildConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<AnimationBuildConfig>(ConfigPath);
            if (config != null) return config;

            AssetPathUtility.EnsureFolder("Assets/ScriptableObjects/Config");
            config = ScriptableObject.CreateInstance<AnimationBuildConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[BuildAnimations] Creé {ConfigPath} con los valores por defecto.");
            return config;
        }
    }
}
