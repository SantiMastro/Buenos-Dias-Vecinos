using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.Simulation
{
    /// <summary>Los números de una tirada de skillcheck, ya resueltos.</summary>
    public readonly struct SkillcheckSetup
    {
        /// <summary>Ancho de la zona buena, en radianes.</summary>
        public float ZoneWidth { get; }

        /// <summary>Velocidad de la aguja, en radianes por segundo.</summary>
        public float NeedleSpeed { get; }

        /// <summary>Cuántas objeciones seguidas hay que ganar para convertir.</summary>
        public int ChainLinks { get; }

        /// <summary>Arma los números de una tirada.</summary>
        public SkillcheckSetup(float zoneWidth, float needleSpeed, int chainLinks)
        {
            ZoneWidth = zoneWidth;
            NeedleSpeed = needleSpeed;
            ChainLinks = chainLinks;
        }
    }

    /// <summary>
    /// Combina las cuatro cosas que endurecen el skillcheck: la puntería del
    /// timbrazo, el progreso del día, el tamaño de la comitiva y la religión.
    ///
    /// Es una clase plana porque acá es donde la dificultad se puede volver
    /// injugable sin que nada se rompa: los cuatro multiplicadores se apilan, y
    /// con los valores equivocados la zona se hace más fina que la aguja. Fuera de
    /// un MonoBehaviour se puede barrer todo el espacio de combinaciones.
    ///
    /// El único que NO es continuo es la cadena de objeciones: son eslabones
    /// enteros, así que van por tramo.
    /// </summary>
    public sealed class SkillcheckDifficulty
    {
        private readonly SkillcheckConfig skillcheck;
        private readonly FollowerConfig followers;
        private readonly int convertsForMaxDifficulty;

        /// <summary>Toma los dos configs y el techo de progresión del día.</summary>
        public SkillcheckDifficulty(
            SkillcheckConfig skillcheck, FollowerConfig followers, int convertsForMaxDifficulty)
        {
            this.skillcheck = skillcheck;
            this.followers = followers;
            this.convertsForMaxDifficulty = Mathf.Max(1, convertsForMaxDifficulty);
        }

        /// <summary>
        /// Progreso de la partida, de 0 a 1. Es lo que endurece todo salvo la
        /// comitiva y la puntería.
        /// </summary>
        public float ProgressAt(int converts)
        {
            return Mathf.Clamp01(converts / (float)convertsForMaxDifficulty);
        }

        /// <summary>
        /// Resuelve los números de una tirada.
        ///
        /// El ancho se clampea contra el piso absoluto DESPUÉS de apilar TODOS los
        /// multiplicadores: es la única garantía de que la zona siga siendo
        /// apuntable con la peor combinación posible —final del día, comitiva
        /// llena, religión difícil, timbrazo al borde del alcance y un vecino al
        /// que hubo que insistirle tres veces—.
        ///
        /// <paramref name="zoneScale"/> es el castigo por haber insistido: salen
        /// molestos. Va en 1 cuando abrieron al primer timbrazo. Es un parámetro y
        /// no una consulta a la config porque esta clase no sabe de puertas: le
        /// llega el número ya resuelto por quien sí sabe cuántas veces se tocó.
        /// </summary>
        public SkillcheckSetup Resolve(
            float precision, int converts, int followerCount, ReligionDefinition religion,
            float zoneScale = 1f)
        {
            float progress = ProgressAt(converts);
            float religionWidth = religion != null ? religion.ZoneWidth : 1f;
            float religionSpeed = religion != null ? religion.NeedleSpeed : 1f;

            float width = skillcheck.BaseWidthFor(precision)
                          * skillcheck.WidthMultiplierAt(progress)
                          * followers.WidthMultiplier(followerCount)
                          * religionWidth
                          * Mathf.Max(0f, zoneScale);

            float speed = skillcheck.NeedleSpeedAt(progress)
                          * followers.SpeedMultiplier(followerCount)
                          * religionSpeed;

            return new SkillcheckSetup(
                Mathf.Max(width, skillcheck.MinimumWidthRadians),
                speed,
                ChainLinksFor(followerCount, religion));
        }

        /// <summary>
        /// Eslabones de la cadena: los del tramo de la comitiva más el extra de la
        /// religión. El extra se suma DESPUÉS del techo por tramo, así que una
        /// religión de +1 siempre cuesta un eslabón más aunque la comitiva ya
        /// estuviera en el máximo.
        /// </summary>
        public int ChainLinksFor(int followerCount, ReligionDefinition religion)
        {
            int extra = religion != null ? religion.ExtraChainLinks : 0;
            return followers.ChainLinks(followerCount) + extra;
        }
    }
}
