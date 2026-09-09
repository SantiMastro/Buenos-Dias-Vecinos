using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BuenosDias.EditorTools.Building
{
    /// <summary>
    /// Arma los AnimatorControllers de personajes y sus overrides a partir de los
    /// clips ya generados.
    ///
    /// Las variantes (seguidores, tipos de vecino) se descubren leyendo los clips
    /// que existen, no de una lista escrita a mano: agregar un sexto vecino tiene
    /// que ser cuestión de tirar los PNG en la carpeta y volver a construir.
    /// </summary>
    public static class CharacterControllerBuilder
    {
        /// <summary>Parámetro entero que elige el estado. Lo escribe la FSM del juego.</summary>
        public const string StateParameter = "Estado";

        private const string PredicadorController = "Assets/Animations/Predicador/Predicador.controller";
        private const string SeguidorController = "Assets/Animations/Seguidores/Seguidor.controller";
        private const string VecinoController = "Assets/Animations/Vecinos/Vecino.controller";
        private const string OndasController = "Assets/Animations/FX/Ondas_Timbre.controller";

        private static readonly (string state, string clip, int value)[] PredicadorStates =
        {
            ("Idle",   "Predicador_Idle",   0),
            ("Walk",   "Predicador_Walk",   1),
            ("Espera", "Predicador_Espera", 2),
            ("Timbre", "Predicador_Timbre", 3),
            ("Exito",  "Predicador_Exito",  4),
        };

        private static readonly (string state, string suffix, int value)[] VecinoStates =
        {
            ("Idle", "Idle", 0),
            ("Si",   "Si",   1),
            ("No",   "No",   2),
        };

        /// <summary>Construye los cuatro controllers y sus overrides.</summary>
        public static int BuildAll(IReadOnlyDictionary<string, AnimationClip> clips, StringBuilder report)
        {
            int overrides = 0;
            BuildPredicador(clips, report);
            overrides += BuildSeguidores(clips, report);
            overrides += BuildVecinos(clips, report);
            BuildOndas(clips, report);
            return overrides;
        }

        private static void BuildPredicador(
            IReadOnlyDictionary<string, AnimationClip> clips, StringBuilder report)
        {
            var states = PredicadorStates
                .Select(s => new AnimatorGraphFactory.StateDefinition(s.state, Find(clips, s.clip), s.value))
                .ToList();

            AnimatorGraphFactory.Build(PredicadorController, StateParameter, states);
            report.AppendLine($"  Predicador.controller ..... {states.Count} estados");
        }

        private static int BuildSeguidores(
            IReadOnlyDictionary<string, AnimationClip> clips, StringBuilder report)
        {
            List<string> variants = MatchingClips(clips, @"^Seguidor_(\d+)_Walk$");
            if (variants.Count == 0) return 0;

            string baseClip = variants[0];
            var states = new[]
            {
                new AnimatorGraphFactory.StateDefinition("Walk", clips[baseClip], 0)
            };
            var controller = AnimatorGraphFactory.Build(SeguidorController, string.Empty, states);

            foreach (string variant in variants)
            {
                string id = Regex.Match(variant, @"^Seguidor_(\d+)_Walk$").Groups[1].Value;
                AnimatorGraphFactory.BuildOverride(
                    controller,
                    new Dictionary<string, AnimationClip> { [baseClip] = clips[variant] },
                    $"Assets/Animations/Seguidores/Seguidor_{id}.overrideController");
            }

            report.AppendLine($"  Seguidor.controller ....... 1 estado, {variants.Count} overrides");
            return variants.Count;
        }

        private static int BuildVecinos(
            IReadOnlyDictionary<string, AnimationClip> clips, StringBuilder report)
        {
            List<string> types = MatchingClips(clips, @"^Vecino_(.+)_Idle$")
                .Select(name => Regex.Match(name, @"^Vecino_(.+)_Idle$").Groups[1].Value)
                .ToList();

            if (types.Count == 0) return 0;

            string baseType = types[0];
            var states = VecinoStates
                .Select(s => new AnimatorGraphFactory.StateDefinition(
                    s.state, Find(clips, $"Vecino_{baseType}_{s.suffix}"), s.value))
                .ToList();

            var controller = AnimatorGraphFactory.Build(VecinoController, StateParameter, states);

            foreach (string type in types)
            {
                var map = VecinoStates.ToDictionary(
                    s => $"Vecino_{baseType}_{s.suffix}",
                    s => Find(clips, $"Vecino_{type}_{s.suffix}"));

                AnimatorGraphFactory.BuildOverride(
                    controller, map, $"Assets/Animations/Vecinos/Vecino_{type}.overrideController");
            }

            report.AppendLine(
                $"  Vecino.controller ......... {states.Count} estados, {types.Count} overrides " +
                $"({string.Join(", ", types)})");
            return types.Count;
        }

        private static void BuildOndas(
            IReadOnlyDictionary<string, AnimationClip> clips, StringBuilder report)
        {
            AnimationClip clip = Find(clips, "Ondas_Timbre");
            if (clip == null) return;

            var states = new[] { new AnimatorGraphFactory.StateDefinition("Ondas", clip, 0) };
            AnimatorGraphFactory.Build(OndasController, string.Empty, states);
            report.AppendLine("  Ondas_Timbre.controller ... 1 estado");
        }

        private static List<string> MatchingClips(
            IReadOnlyDictionary<string, AnimationClip> clips, string pattern)
        {
            return clips.Keys.Where(k => Regex.IsMatch(k, pattern)).OrderBy(k => k).ToList();
        }

        private static AnimationClip Find(
            IReadOnlyDictionary<string, AnimationClip> clips, string clipName)
        {
            if (clips.TryGetValue(clipName, out AnimationClip clip)) return clip;

            Debug.LogError(
                $"[CharacterControllerBuilder] Falta el clip '{clipName}'. " +
                "¿Faltan los PNG de esa animación?");
            return null;
        }
    }
}
