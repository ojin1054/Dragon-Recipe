using DragonRecipe.Care;
using DragonRecipe.Core;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using NUnit.Framework;
using UnityEngine;

namespace DragonRecipe.Tests
{
    /// <summary>드래곤 능력치 / 동물 힘 / 힘 겨루기</summary>
    public class DragonStatsTests
    {
        [Test]
        public void Stats_GoIntoFlightParams()
        {
            var stats = new DragonStats { strength = 35f, stamina = 140f, health = 130f, attack = 14f };
            var p = stats.ApplyTo(new FlightParams());
            Assert.AreEqual(35f, p.carryStrength, 0.001f);
            Assert.AreEqual(140f, p.maxStamina, 0.001f);
            Assert.AreEqual(130f, p.maxHealth, 0.001f);
        }

        [Test]
        public void AdultForm_AddsStats_ChildDoesNot()
        {
            var baseStats = new DragonStats { strength = 60f, stamina = 200f, health = 170f, attack = 22f };
            var hunter = new DragonState { stage = GrowthStage.Adult, form = DragonForm.Hunter };
            var tide = new DragonState { stage = GrowthStage.Adult, form = DragonForm.Tide };
            var child = new DragonState { stage = GrowthStage.Juvenile, form = DragonForm.Hunter };

            var h = CareLogic.StatsFor(baseStats, hunter);
            var t = CareLogic.StatsFor(baseStats, tide);
            Assert.Greater(h.strength, t.strength, "고기를 먹은 드래곤이 더 힘셈");
            Assert.Greater(h.attack, t.attack);
            Assert.Greater(t.stamina, h.stamina, "물고기를 먹은 드래곤이 더 오래 날음");
            Assert.AreEqual(baseStats.strength, CareLogic.StatsFor(baseStats, child).strength, 0.001f);
            Assert.AreEqual(60f, baseStats.strength, 0.001f, "원본 값은 그대로");
        }

        [Test]
        public void WeakDragon_LiftsFewerAnimals()
        {
            var h = new HuntSettings { maxOverload = 1.5f };
            const float sheep = 10f;
            Assert.AreEqual(3, Liftable(20f, sheep, h), "힘 20 → 양 3마리까지");
            Assert.AreEqual(9, Liftable(60f, sheep, h), "힘 60 → 양 9마리까지");
            Assert.IsFalse(HuntRules.CanLift(0f, 40f, 20f, h), "혼자서도 너무 센 동물은 못 든다");
        }

        static int Liftable(float strength, float prey, HuntSettings h)
        {
            int n = 0;
            float payload = 0f;
            while (HuntRules.CanLift(payload, prey, strength, h) && n < 100)
            {
                payload += prey;
                n++;
            }
            return n;
        }

        [Test]
        public void Overload_StartsAboveDragonStrength()
        {
            var h = new HuntSettings { maxOverload = 1.5f };
            Assert.AreEqual(0f, HuntRules.Overload(20f, 20f, h), 0.001f, "힘과 같으면 흔들리지 않음");
            Assert.AreEqual(0f, HuntRules.Overload(10f, 20f, h), 0.001f);
            Assert.AreEqual(0.5f, HuntRules.Overload(25f, 20f, h), 0.001f);
            Assert.AreEqual(1f, HuntRules.Overload(30f, 20f, h), 0.001f, "한계까지 매달면 최대로 흔들림");
        }

        [Test]
        public void Attack_SubduesWeakerPrey()
        {
            Assert.IsTrue(HuntRules.Kicks(16f, 8f), "어린 드래곤은 멧돼지에게 걷어차임");
            Assert.IsFalse(HuntRules.Kicks(16f, 22f), "성체는 멧돼지를 제압");
        }

        [Test]
        public void PreyStrength_RareIsStronger()
        {
            var food = ScriptableObject.CreateInstance<FoodData>();
            food.strength = 10f;
            try
            {
                var h = new HuntSettings { rareWeightMultiplier = 1.3f };
                Assert.AreEqual(10f, HuntRules.PreyStrength(food, false, h), 0.001f);
                Assert.AreEqual(13f, HuntRules.PreyStrength(food, true, h), 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(food);
            }
        }
    }
}
