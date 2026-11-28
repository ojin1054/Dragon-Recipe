using DragonRecipe.Food;
using NUnit.Framework;
using UnityEngine;

namespace DragonRecipe.Tests
{
    public class HabitatTests
    {
        static HabitatMap Map()
        {
            var m = new HabitatMap();
            m.ponds.Add(new PondArea { center = new Vector3(0, 0, 0), radiusX = 20f, radiusZ = 10f });
            m.trees.Add(new Vector3(100, 0, 0));
            m.rocks.Add(new RockSpot { position = new Vector3(0, 0, 100), radius = 3f });
            return m;
        }

        [Test]
        public void Defaults_PutEachAnimalWhereItBelongs()
        {
            Assert.AreEqual(Habitat.Water, HabitatRules.DefaultFor("salmon", FoodTrait.Fish));
            Assert.AreEqual(Habitat.Shore, HabitatRules.DefaultFor("crab", FoodTrait.Fish));
            Assert.AreEqual(Habitat.Forest, HabitatRules.DefaultFor("boar", FoodTrait.Meat));
            Assert.AreEqual(Habitat.Forest, HabitatRules.DefaultFor("mushroom", FoodTrait.Plant));
            Assert.AreEqual(Habitat.Meadow, HabitatRules.DefaultFor("sheep", FoodTrait.Meat));
            Assert.AreEqual(Habitat.Rocky, HabitatRules.DefaultFor("ember_lizard", FoodTrait.Fire));
            Assert.AreEqual(Habitat.Water, HabitatRules.DefaultFor("unknown_fish", FoodTrait.Fish), "모르는 물고기는 물");
            Assert.AreEqual(Habitat.Anywhere, HabitatRules.DefaultFor("unknown", FoodTrait.Meat));
        }

        [Test]
        public void Water_IsOnlyInsideThePond()
        {
            var m = Map();
            Assert.IsTrue(m.Fits(Habitat.Water, new Vector3(10, 0, 0)));
            Assert.IsFalse(m.Fits(Habitat.Water, new Vector3(0, 0, 12)), "타원의 짧은 쪽 밖");
            Assert.IsFalse(m.Fits(Habitat.Water, new Vector3(50, 0, 50)));
        }

        [Test]
        public void Shore_IsTheRingAroundThePond()
        {
            var m = Map();
            Assert.IsFalse(m.Fits(Habitat.Shore, new Vector3(5, 0, 0)), "물속");
            Assert.IsTrue(m.Fits(Habitat.Shore, new Vector3(24, 0, 0)));
            Assert.IsFalse(m.Fits(Habitat.Shore, new Vector3(60, 0, 0)), "너무 멂");
        }

        [Test]
        public void LandHabitats_StayOutOfWater_AndNearTheirFeature()
        {
            var m = Map();
            Assert.IsTrue(m.Fits(Habitat.Forest, new Vector3(104, 0, 3)));
            Assert.IsFalse(m.Fits(Habitat.Forest, new Vector3(60, 0, 60)));
            Assert.IsTrue(m.Fits(Habitat.Rocky, new Vector3(0, 0, 106)));
            Assert.IsFalse(m.Fits(Habitat.Rocky, new Vector3(60, 0, 60)));
            Assert.IsTrue(m.Fits(Habitat.Meadow, new Vector3(60, 0, 60)));
            Assert.IsFalse(m.Fits(Habitat.Meadow, new Vector3(103, 0, 0)), "나무 바로 옆");
            Assert.IsFalse(m.Fits(Habitat.Meadow, new Vector3(0, 0, 0)), "물속");
            Assert.IsFalse(m.Fits(Habitat.Anywhere, new Vector3(0, 0, 0)), "땅 동물은 물에 안 나옴");
        }

        [Test]
        public void MissingFeatures_FallBackToAnywhere()
        {
            var empty = new HabitatMap();
            Assert.IsTrue(empty.Fits(Habitat.Water, new Vector3(5, 0, 5)), "연못 없는 씬에서도 물고기가 나온다");
            Assert.IsTrue(empty.Fits(Habitat.Forest, new Vector3(5, 0, 5)));
            Assert.IsFalse(empty.TrySample(Habitat.Water, out _));
        }

        [Test]
        public void Samples_LandInsideTheirHabitat()
        {
            var m = Map();
            for (int i = 0; i < 40; i++)
            {
                Assert.IsTrue(m.TrySample(Habitat.Water, out var w));
                Assert.IsTrue(m.Fits(Habitat.Water, w), $"물 {w}");
                Assert.IsTrue(m.TrySample(Habitat.Shore, out var s));
                Assert.IsTrue(m.Fits(Habitat.Shore, s), $"물가 {s}");
                Assert.IsTrue(m.TrySample(Habitat.Forest, out var f));
                Assert.IsTrue(m.Fits(Habitat.Forest, f), $"숲 {f}");
                Assert.IsTrue(m.TrySample(Habitat.Rocky, out var r));
                Assert.IsTrue(m.Fits(Habitat.Rocky, r), $"바위 {r}");
            }
        }
    }
}
