using BuenosDias.Config;
using BuenosDias.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace BuenosDias.Tests
{
    /// <summary>Quién atiende la puerta, y a qué costo de números aleatorios.</summary>
    public sealed class HouseOccupancyResolverTests
    {
        private static HouseLayout Layout(float chance)
        {
            return new HouseLayout(
                200f, 3f, null, RoofStyle.LosaCompleta, 0f,
                new PlacedSignal[0], chance);
        }

        private static NeighborDefinition[] Neighbors(int count)
        {
            var result = new NeighborDefinition[count];
            for (int i = 0; i < count; i++)
                result[i] = ScriptableObject.CreateInstance<NeighborDefinition>();
            return result;
        }

        private static HouseOccupancyResolver Build(PityConfig config, int neighbors = 3)
        {
            return new HouseOccupancyResolver(
                new PityTracker(config), config, Neighbors(neighbors));
        }

        [Test]
        public void Una_casa_ocupada_devuelve_vecino()
        {
            PityConfig config = ConfigFactory.Pity(readableThreshold: 0f);
            HouseOccupancy result = Build(config).Resolve(Layout(1f), new System.Random(1));

            Assert.IsTrue(result.IsOccupied);
            Assert.IsNotNull(result.Neighbor);
        }

        [Test]
        public void Una_casa_vacia_no_devuelve_vecino()
        {
            PityConfig config = ConfigFactory.Pity(readableThreshold: 0f);
            HouseOccupancy result = Build(config).Resolve(Layout(0f), new System.Random(1));

            Assert.IsFalse(result.IsOccupied);
            Assert.IsNull(result.Neighbor);
        }

        /// <summary>
        /// El vecino se sortea DESPUÉS de saber que hay alguien. Si se sorteara
        /// siempre, cada casa vacía gastaría un número de la secuencia y la cuadra
        /// dejaría de ser reproducible al tocar el catálogo de vecinos.
        /// </summary>
        [Test]
        public void Una_casa_vacia_gasta_un_solo_numero_aleatorio()
        {
            PityConfig config = ConfigFactory.Pity(readableThreshold: 0f);

            var used = new System.Random(7);
            Build(config).Resolve(Layout(0f), used);

            var reference = new System.Random(7);
            reference.NextDouble();   // solo la tirada de ocupación

            Assert.AreEqual(reference.NextDouble(), used.NextDouble(), 1e-12);
        }

        [Test]
        public void Una_casa_ocupada_gasta_dos_numeros_aleatorios()
        {
            PityConfig config = ConfigFactory.Pity(readableThreshold: 0f);

            var used = new System.Random(7);
            Build(config).Resolve(Layout(1f), used);

            var reference = new System.Random(7);
            reference.NextDouble();   // ocupación
            reference.Next(3);        // vecino

            Assert.AreEqual(reference.NextDouble(), used.NextDouble(), 1e-12);
        }

        [Test]
        public void Informa_si_la_casa_era_legible()
        {
            PityConfig config = ConfigFactory.Pity(readableThreshold: 0.42f);
            var resolver = Build(config);

            Assert.IsFalse(resolver.Resolve(Layout(0.30f), new System.Random(1)).WasReadable);
            Assert.IsTrue(resolver.Resolve(Layout(0.50f), new System.Random(1)).WasReadable);
        }
    }
}
