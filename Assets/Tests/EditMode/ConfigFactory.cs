using BuenosDias.Config;
using UnityEditor;
using UnityEngine;

namespace BuenosDias.Tests
{
    /// <summary>
    /// Arma configs en memoria para los tests.
    ///
    /// No se usan los assets del proyecto a propósito: si un test dependiera del
    /// balance tuneado, cambiar un número en el Inspector rompería tests que no
    /// tienen nada que ver con ese número, y el diseñador terminaría aprendiendo
    /// a ignorar el rojo.
    /// </summary>
    public static class ConfigFactory
    {
        /// <summary>Config de anti-racha con los valores que se le pidan.</summary>
        public static PityConfig Pity(
            float readableThreshold = 0.42f, float increment = 0.22f,
            float cap = 0.55f, int guaranteedAfter = 3)
        {
            var config = ScriptableObject.CreateInstance<PityConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("readableChanceThreshold").floatValue = readableThreshold;
            so.FindProperty("increment").floatValue = increment;
            so.FindProperty("cap").floatValue = cap;
            so.FindProperty("guaranteedAfterEmptyRun").intValue = guaranteedAfter;

            // Sin clampear, para que los tests midan la mecánica y no el clamp.
            so.FindProperty("rollClampMin").floatValue = 0f;
            so.FindProperty("rollClampMax").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        /// <summary>Config de skillcheck. Los defaults son los de la spec.</summary>
        public static SkillcheckConfig Skillcheck(float minimumWidth = 0.12f)
        {
            var config = ScriptableObject.CreateInstance<SkillcheckConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("minimumWidthRadians").floatValue = minimumWidth;
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        /// <summary>Config de comitiva. Los defaults son los de la spec.</summary>
        public static FollowerConfig Followers(
            bool continuousScaling = true,
            FollowerPenaltyMode penaltyMode = FollowerPenaltyMode.BajarDeTramo,
            int abandonThreshold = 5)
        {
            var config = ScriptableObject.CreateInstance<FollowerConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("continuousScaling").boolValue = continuousScaling;
            so.FindProperty("penaltyMode").enumValueIndex = (int)penaltyMode;
            so.FindProperty("abandonThreshold").intValue = abandonThreshold;
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        /// <summary>Religión con los multiplicadores que se le pidan.</summary>
        public static ReligionDefinition Religion(
            float zoneWidth = 1f, float needleSpeed = 1f, int extraChainLinks = 0,
            bool canAscend = true)
        {
            var asset = ScriptableObject.CreateInstance<ReligionDefinition>();
            var so = new SerializedObject(asset);
            so.FindProperty("zoneWidth").floatValue = zoneWidth;
            so.FindProperty("needleSpeed").floatValue = needleSpeed;
            so.FindProperty("extraChainLinks").intValue = extraChainLinks;
            so.FindProperty("canAscend").boolValue = canAscend;
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        /// <summary>
        /// Config raíz con los umbrales de los finales que se le pidan. Los otros
        /// campos quedan en su default: acá solo se testea qué final sale.
        /// </summary>
        public static GameConfig Game(int crucifixionBelow = 1, int ascensionAtLeast = 10)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("crucifixionBelow").intValue = crucifixionBelow;
            so.FindProperty("ascensionAtLeast").intValue = ascensionAtLeast;
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        /// <summary>Config de espera con los valores que se le pidan.</summary>
        public static WaitConfig Wait(
            float tellFraction = 0.55f, float tellRepeat = 1.2f,
            float stepStart = 0.34f, float stepEnd = 0.17f)
        {
            var config = ScriptableObject.CreateInstance<WaitConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("tellFraction").floatValue = tellFraction;
            so.FindProperty("tellRepeatSeconds").floatValue = tellRepeat;
            so.FindProperty("stepIntervalStart").floatValue = stepStart;
            so.FindProperty("stepIntervalEnd").floatValue = stepEnd;
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }
    }
}
