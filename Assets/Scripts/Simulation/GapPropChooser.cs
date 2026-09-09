using System.Collections.Generic;
using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.Simulation
{
    /// <summary>
    /// Decide qué decorado se lleva un hueco entre terrenos, o si queda pelado.
    ///
    /// Vive aparte del componente que los planta y es una clase plana: elegir es
    /// una función del ancho del hueco y de las probabilidades, nada más. Así el
    /// reparto se puede simular sobre miles de huecos sin entrar en Play Mode, que
    /// es como se verificó.
    /// </summary>
    public sealed class GapPropChooser
    {
        private readonly IReadOnlyList<SceneryPropSet> candidates;
        private readonly float[] requiredGaps;

        /// <summary>Cuántos decorados compiten por un hueco.</summary>
        public int Count => candidates.Count;

        /// <summary>
        /// Precalcula el hueco que pide cada candidato: manda el más exigente entre
        /// lo que ocupa su sprite y el aire que el asset pide a mano.
        /// </summary>
        public GapPropChooser(IReadOnlyList<SceneryPropSet> candidates, float[] spriteWidths)
        {
            this.candidates = candidates;
            requiredGaps = new float[candidates.Count];

            for (int i = 0; i < candidates.Count; i++)
                requiredGaps[i] = Mathf.Max(spriteWidths[i], candidates[i].MinimumGap);
        }

        /// <summary>
        /// Índice del decorado elegido, o <c>-1</c> si el hueco queda vacío.
        ///
        /// Una sola tirada acumulando la probabilidad de cada candidato. Si pasa de
        /// largo la suma, el hueco queda pelado. Se hace así, y no con pesos
        /// relativos, para que cada asset exponga una probabilidad que se lea sola
        /// en el Inspector en vez de un número que haya que dividir mentalmente por
        /// el total de la lista.
        /// </summary>
        public int Choose(System.Random random, float gapWidth)
        {
            double roll = random.NextDouble();
            double accumulated = 0d;

            for (int i = 0; i < candidates.Count; i++)
            {
                accumulated += candidates[i].ChancePerGap;
                if (roll >= accumulated) continue;

                return Fits(i, gapWidth) ? i : Substitute(random, i, gapWidth);
            }

            return -1;
        }

        /// <summary>Si el decorado entra en un hueco de ese ancho, en unidades.</summary>
        public bool Fits(int index, float gapWidth) => gapWidth >= requiredGaps[index];

        /// <summary>
        /// Reparte la parte del que no entró entre los que sí, en proporción a sus
        /// propias probabilidades. Devuelve <c>-1</c> si no entra ninguno.
        ///
        /// Acá SÍ se sustituye, al revés que en la elección de señales de una casa.
        /// La diferencia es que allá hay pesos de juego: si el auto no entra y no se
        /// lo reemplaza, las casas angostas se vuelven más negativas de lo diseñado
        /// y se corre el balance. El decorado no tiene peso de ninguna clase — un
        /// poste en lugar de un árbol no cambia ninguna probabilidad ni ninguna
        /// lectura ni ninguna decisión. Y encima queda mejor: huecos angostos con
        /// poste y anchos con árbol es como se reparte el mobiliario de una cuadra
        /// de verdad; nadie planta un árbol en un metro de espacio.
        ///
        /// Sustituir en vez de vaciar mantiene además la proporción de huecos
        /// pelados igual en toda la cuadra, que es lo que se ajustó en el Inspector.
        /// </summary>
        private int Substitute(System.Random random, int rejected, float gapWidth)
        {
            float available = 0f;
            for (int i = 0; i < candidates.Count; i++)
                if (i != rejected && Fits(i, gapWidth)) available += candidates[i].ChancePerGap;

            if (available <= 0f) return -1;

            double roll = random.NextDouble() * available;
            double accumulated = 0d;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (i == rejected || !Fits(i, gapWidth)) continue;

                accumulated += candidates[i].ChancePerGap;
                if (roll < accumulated) return i;
            }

            return -1;
        }
    }
}
