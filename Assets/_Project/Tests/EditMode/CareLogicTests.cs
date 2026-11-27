using System;
using System.IO;
using DragonRecipe.Care;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using NUnit.Framework;

namespace DragonRecipe.Tests
{
    public class CareLogicTests
    {
        static readonly DateTime T0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        static CareSettings Settings() => new CareSettings
        {
            maxHunger = 100f, startingHunger = 50f, hungerDecayPerHour = 20f, offlineCapHours = 8f,
            xpToJuvenile = 150f, xpToAdult = 450f, starvationRecovery = 20f,
            starvingStaminaMultiplier = 0.6f, starvingFlapLiftMultiplier = 0.8f
        };

        [Test]
        public void Hunger_DecaysWithRealTime()
        {
            var c = Settings();
            var s = DragonState.CreateNew(c, T0);
            CareLogic.ApplyElapsed(s, c, T0.AddHours(1));
            Assert.AreEqual(30f, s.hunger, 0.01f);
        }

        [Test]
        public void Hunger_NeverBelowZero()
        {
            var c = Settings();
            var s = DragonState.CreateNew(c, T0);
            CareLogic.ApplyElapsed(s, c, T0.AddHours(5));
            Assert.AreEqual(0f, s.hunger);
            Assert.IsTrue(CareLogic.IsStarving(s));
        }

        [Test]
        public void OfflineDecay_IsCapped()
        {
            var c = Settings();
            c.hungerDecayPerHour = 5f;
            var s = DragonState.CreateNew(c, T0);
            s.hunger = 100f;
            CareLogic.ApplyElapsed(s, c, T0.AddDays(3)); // 72시간 → 8시간만 반영
            Assert.AreEqual(100f - 8f * 5f, s.hunger, 0.01f);
        }

        [Test]
        public void ClockGoingBackwards_DoesNothing()
        {
            var c = Settings();
            var s = DragonState.CreateNew(c, T0);
            CareLogic.ApplyElapsed(s, c, T0.AddHours(-3));
            Assert.AreEqual(50f, s.hunger);
            Assert.AreEqual(T0.AddHours(-3).Ticks, s.lastTickUtc);
        }

        [Test]
        public void Feeding_FillsHunger_AndWastesOverflow()
        {
            var c = Settings();
            var s = DragonState.CreateNew(c, T0); // 50
            var o = CareLogic.Feed(s, c, 70f, FoodTrait.Meat);
            Assert.AreEqual(100f, s.hunger, 0.01f);
            Assert.AreEqual(50f, o.absorbed, 0.01f);
            Assert.AreEqual(20f, o.wasted, 0.01f);
            Assert.AreEqual(50f, s.growthXp, 0.01f, "경험치는 실제로 먹은 양만큼");
        }

        [Test]
        public void Feeding_RecordsTraits()
        {
            var c = Settings();
            var s = DragonState.CreateNew(c, T0);
            CareLogic.Feed(s, c, 10f, FoodTrait.Fish);
            CareLogic.Feed(s, c, 5f, FoodTrait.Fish);
            CareLogic.Feed(s, c, 8f, FoodTrait.Fire);
            Assert.AreEqual(15f, s.GetTrait(FoodTrait.Fish), 0.01f);
            Assert.AreEqual(8f, s.GetTrait(FoodTrait.Fire), 0.01f);
            Assert.AreEqual(0f, s.GetTrait(FoodTrait.Plant));
        }

        [Test]
        public void Starving_FirstPartOfMeal_GivesNoXp()
        {
            var c = Settings();
            var s = DragonState.CreateNew(c, T0);
            s.hunger = 0f;
            var o = CareLogic.Feed(s, c, 30f, FoodTrait.Meat);
            Assert.AreEqual(30f, s.hunger, 0.01f);
            Assert.AreEqual(10f, o.xpGained, 0.01f);
        }

        [Test]
        public void Growth_ChangesStage_AtThresholds()
        {
            var c = Settings();
            var s = DragonState.CreateNew(c, T0);
            s.growthXp = 140f;
            s.hunger = 0.5f; // 굶주림 아님
            var o = CareLogic.Feed(s, c, 20f, FoodTrait.Meat);
            Assert.IsTrue(o.stageChanged);
            Assert.AreEqual(GrowthStage.Juvenile, s.stage);

            s.growthXp = 449f;
            s.hunger = 50f;
            CareLogic.Feed(s, c, 5f, FoodTrait.Meat);
            Assert.AreEqual(GrowthStage.Adult, s.stage);
        }

        [Test]
        public void StageProgress_IsWithinStage()
        {
            var c = Settings();
            var s = DragonState.CreateNew(c, T0);
            s.growthXp = 75f;
            Assert.AreEqual(0.5f, CareLogic.StageProgress(s, c), 0.001f);
            s.stage = GrowthStage.Juvenile;
            s.growthXp = 300f;
            Assert.AreEqual(0.5f, CareLogic.StageProgress(s, c), 0.001f);
        }

        [Test]
        public void Starving_WeakensFlight_WithoutTouchingOriginal()
        {
            var c = Settings();
            var s = DragonState.CreateNew(c, T0);
            var original = new FlightParams();
            float stamina = original.maxStamina, lift = original.flapLift;

            var normal = CareLogic.FlightParamsFor(original, s, c);
            Assert.AreEqual(stamina, normal.maxStamina);

            s.hunger = 0f;
            var weak = CareLogic.FlightParamsFor(original, s, c);
            Assert.AreEqual(stamina * 0.6f, weak.maxStamina, 0.01f);
            Assert.AreEqual(lift * 0.8f, weak.flapLift, 0.01f);
            Assert.AreEqual(stamina, original.maxStamina, "원본 에셋 값이 바뀌면 안 됨");
        }

        [Test]
        public void Save_RoundTrips()
        {
            string path = Path.Combine(Path.GetTempPath(), $"dragon_save_test_{Guid.NewGuid():N}.json");
            SaveSystem.PathOverride = path;
            try
            {
                var c = Settings();
                var s = DragonState.CreateNew(c, T0);
                s.growthXp = 123f;
                s.stage = GrowthStage.Juvenile;
                s.injured = true;
                s.AddTrait(FoodTrait.Fire, 7f);
                SaveSystem.Save(s);

                Assert.IsTrue(SaveSystem.TryLoad(out var loaded));
                Assert.AreEqual(123f, loaded.growthXp);
                Assert.AreEqual(GrowthStage.Juvenile, loaded.stage);
                Assert.IsTrue(loaded.injured);
                Assert.AreEqual(7f, loaded.GetTrait(FoodTrait.Fire));
                Assert.AreEqual(T0.Ticks, loaded.lastTickUtc);
            }
            finally
            {
                SaveSystem.PathOverride = null;
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
