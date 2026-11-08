using BuenosDias.Config;
using BuenosDias.Gameplay;
using BuenosDias.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BuenosDias.Tests
{
    /// <summary>
    /// Volver a una puerta que ya se tocó. Bajarse del felpudo y volver a subirse a
    /// la misma casa no puede sacar gente nueva: la ocupación es de la CASA, no del
    /// timbrazo, y una casa que ya abrió no vuelve a abrir.
    /// </summary>
    public sealed class DoorRevisitTests
    {
        private GameObject go;
        private HouseInstance house;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("casa de prueba");
            house = go.AddComponent<HouseInstance>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        private static HouseLayout Layout(float chance)
        {
            return new HouseLayout(
                200f, 3f, null, RoofStyle.LosaCompleta, 0f,
                new PlacedSignal[0], chance);
        }

        private static NeighborDefinition Neighbor() =>
            ScriptableObject.CreateInstance<NeighborDefinition>();

        private static HouseOccupancyResolver Resolver(NeighborDefinition neighbor)
        {
            PityConfig config = ConfigFactory.Pity(readableThreshold: 0f);
            return new HouseOccupancyResolver(
                new PityTracker(config), config, new[] { neighbor });
        }

        private static DoorbellConfig Doorbell(float openChanceFirst)
        {
            var config = ScriptableObject.CreateInstance<DoorbellConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("openChanceFirst").floatValue = openChanceFirst;
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        private DoorAttempt Ring(HouseOccupancyResolver resolver, DoorbellConfig doorbell, int seed = 1)
        {
            return DoorAttempt.Ring(
                house, 1f, resolver, new System.Random(seed),
                ConfigFactory.Wait(), doorbell, null);
        }

        private void GiveLayout(float chance)
        {
            typeof(HouseInstance).GetProperty("Layout").GetSetMethod(true)
                .Invoke(house, new object[] { Layout(chance) });
        }

        [Test]
        public void La_primera_vez_se_guarda_lo_que_salio()
        {
            GiveLayout(1f);
            Assert.IsFalse(house.KnownOccupancy.HasValue);

            DoorAttempt attempt = Ring(Resolver(Neighbor()), Doorbell(1f));

            Assert.IsTrue(house.KnownOccupancy.HasValue);
            Assert.IsTrue(house.KnownOccupancy.Value.IsOccupied);
            Assert.AreSame(attempt.Neighbor, house.KnownOccupancy.Value.Neighbor);
        }

        [Test]
        public void Volver_a_tocar_no_cambia_al_vecino()
        {
            GiveLayout(1f);
            NeighborDefinition first = Neighbor();

            DoorAttempt one = Ring(Resolver(first), Doorbell(1f));
            DoorAttempt two = Ring(Resolver(Neighbor()), Doorbell(1f));

            Assert.AreSame(first, one.Neighbor);
            Assert.AreSame(first, two.Neighbor, "Se volvió a sortear quién vive en la casa.");
        }

        [Test]
        public void Una_casa_vacia_sigue_vacia_aunque_se_vuelva_a_tocar()
        {
            GiveLayout(0f);

            DoorAttempt one = Ring(Resolver(Neighbor()), Doorbell(1f));
            GiveLayout(1f);   // si se volviera a sortear, ahora saldría alguien
            DoorAttempt two = Ring(Resolver(Neighbor()), Doorbell(1f));

            Assert.IsFalse(one.SomeoneHome);
            Assert.IsFalse(two.SomeoneHome, "Una casa vacía no puede llenarse sola al volver.");
        }

        [Test]
        public void Volver_a_tocar_no_gasta_otra_tirada_de_ocupacion()
        {
            GiveLayout(1f);
            PityConfig config = ConfigFactory.Pity(readableThreshold: 0f);
            var pity = new PityTracker(config);
            var resolver = new HouseOccupancyResolver(pity, config, new[] { Neighbor() });

            Ring(resolver, Doorbell(1f));
            float after = pity.Pity;
            int emptyRun = pity.EmptyRun;
            Ring(resolver, Doorbell(1f));

            Assert.AreEqual(after, pity.Pity, 1e-6f);
            Assert.AreEqual(emptyRun, pity.EmptyRun);
        }

        [Test]
        public void Una_casa_atendida_no_vuelve_a_abrir()
        {
            GiveLayout(1f);
            Ring(Resolver(Neighbor()), Doorbell(1f));
            house.MarkAttended();

            for (int seed = 0; seed < 50; seed++)
            {
                DoorAttempt again = Ring(Resolver(Neighbor()), Doorbell(1f), seed);

                Assert.IsTrue(again.SomeoneHome, "Sigue habiendo alguien: solo que no abre.");
                Assert.IsFalse(again.WillOpen, $"Una casa atendida abrió de nuevo (semilla {seed}).");
            }
        }

        [Test]
        public void Una_casa_ocupada_sin_atender_abre_si_la_chance_es_total()
        {
            GiveLayout(1f);

            for (int seed = 0; seed < 20; seed++)
                Assert.IsTrue(Ring(Resolver(Neighbor()), Doorbell(1f), seed).WillOpen);
        }
    }
}
