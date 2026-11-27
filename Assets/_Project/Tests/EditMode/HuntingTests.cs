using System;
using System.Collections.Generic;
using DragonRecipe.Care;
using DragonRecipe.Core;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using NUnit.Framework;
using UnityEngine;

namespace DragonRecipe.Tests
{
    /// <summary>먹이 매달기 / 연속 사냥 / 창고 / 성체 모습 / 도감</summary>
    public class HuntingTests
    {
        const float Dt = 0.02f;
        static readonly DateTime T0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        static CareSettings Care() => new CareSettings
        {
            maxHunger = 100f, startingHunger = 50f, xpToJuvenile = 150f, xpToAdult = 450f,
            starvationRecovery = 20f, storageCapacity = 3, formDominance = 0.4f
        };

        static FlightModel Glider(float payload)
        {
            var m = new FlightModel(new FlightParams());
            m.Reset(new Vector3(0, 80, 0), 0f, 16f);
            m.Payload = payload;
            return m;
        }

        // ───────────── 무게 ─────────────

        [Test]
        public void HeavyPayload_SinksFaster()
        {
            var light = Glider(0f);
            var heavy = Glider(3f);
            for (int i = 0; i < 150; i++)
            {
                light.Step(Dt, Vector2.zero);
                heavy.Step(Dt, Vector2.zero);
            }
            Assert.Less(heavy.Position.y, light.Position.y - 1f);
        }

        [Test]
        public void HeavyPayload_TurnsSlower()
        {
            var light = Glider(0f);
            var heavy = Glider(3f);
            for (int i = 0; i < 20; i++) // 최대 기울기에 닿기 전까지
            {
                light.Step(Dt, new Vector2(1, 0));
                heavy.Step(Dt, new Vector2(1, 0));
            }
            Assert.Less(heavy.Yaw, light.Yaw);
            Assert.Less(heavy.Bank, light.Bank);
        }

        [Test]
        public void HeavyPayload_WeakensFlap()
        {
            var light = Glider(0f);
            var heavy = Glider(3f);
            light.TryFlap();
            heavy.TryFlap();
            Assert.Less(heavy.VerticalSpeed, light.VerticalSpeed);
        }

        [Test]
        public void StrongerDragon_CarriesSameWeightEasier()
        {
            var weak = Glider(2f);
            var strongParams = new FlightParams { carryStrength = 6f };
            var strong = new FlightModel(strongParams);
            strong.Reset(Vector3.zero, 0f, 16f);
            strong.Payload = 2f;
            Assert.Greater(strong.LiftLoadMultiplier, weak.LiftLoadMultiplier);
        }

        [Test]
        public void Eat_RestoresStamina_Jolt_CostsStamina()
        {
            var m = Glider(0f);
            m.Stamina = 20f;
            m.Eat(30f);
            Assert.AreEqual(50f, m.Stamina, 0.01f);
            m.Jolt(10f, 0f, 5f);
            Assert.AreEqual(45f, m.Stamina, 0.01f);
            Assert.AreEqual(10f, m.Bank, 0.01f);
        }

        // ───────────── 사냥 규칙 ─────────────

        [Test]
        public void Combo_StaminaAndBoost_GrowAndCap()
        {
            var h = new HuntSettings { comboStaminaPerStep = 15f, comboStaminaMax = 60f, comboBoostPerStep = 4f };
            Assert.AreEqual(0f, HuntRules.ComboStamina(1, h), 0.001f, "단독 사냥은 보상 없음");
            Assert.AreEqual(0f, HuntRules.ComboBoost(1, h), 0.001f);
            Assert.AreEqual(30f, HuntRules.ComboStamina(3, h), 0.001f);
            Assert.AreEqual(8f, HuntRules.ComboBoost(3, h), 0.001f);
            Assert.AreEqual(60f, HuntRules.ComboStamina(10, h), 0.001f);
            Assert.AreEqual(36f, HuntRules.ComboBoost(10, h), 0.001f, "추가 속도는 상한 없이 계속 커짐");
        }

        [Test]
        public void Boost_ExceedsMaxSpeed_ThenFadesBack()
        {
            var m = Glider(0f);
            m.Speed = m.Params.maxSpeed;
            m.Boost(8f, 1f);
            Assert.Greater(m.Speed, m.Params.maxSpeed + 7f, "바로 빨라짐");
            Assert.IsTrue(m.IsBoosting);

            for (int i = 0; i < 15; i++) m.Step(Dt, Vector2.zero); // 0.3초
            Assert.Greater(m.Speed, m.Params.maxSpeed, "붙어 있는 동안은 최고 속도를 넘음");
            m.TryFlap();
            Assert.Greater(m.Speed, m.Params.maxSpeed, "날갯짓이 추가 속도를 깎지 않음");

            for (int i = 0; i < 250; i++) m.Step(Dt, Vector2.zero); // 5초
            Assert.IsFalse(m.IsBoosting);
            Assert.LessOrEqual(m.Speed, m.Params.maxSpeed + 0.01f, "끝나면 최고 속도로 돌아옴");
        }

        [Test]
        public void Crash_DropsHalfRoundedUp()
        {
            Assert.AreEqual(0, HuntRules.DropCount(0, 0.5f));
            Assert.AreEqual(1, HuntRules.DropCount(1, 0.5f));
            Assert.AreEqual(1, HuntRules.DropCount(2, 0.5f));
            Assert.AreEqual(2, HuntRules.DropCount(3, 0.5f));
            Assert.AreEqual(0, HuntRules.DropCount(3, 0f));
        }

        [Test]
        public void Payload_CountsOnlyCarried()
        {
            var food = ScriptableObject.CreateInstance<FoodData>();
            food.carryWeight = 1f;
            try
            {
                var h = new HuntSettings { rareWeightMultiplier = 1.5f };
                var list = new List<CaughtFood>
                {
                    new CaughtFood { food = food },
                    new CaughtFood { food = food, rare = true },
                    new CaughtFood { food = food, eaten = true },
                    new CaughtFood { food = food, dropped = true },
                };
                Assert.AreEqual(2.5f, HuntRules.Payload(list, h), 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(food);
            }
        }

        // ───────────── 창고 ─────────────

        [Test]
        public void Storage_FillsToCapacity()
        {
            var c = Care();
            var s = DragonState.CreateNew(c, T0);
            for (int i = 0; i < 3; i++) Assert.IsTrue(CareLogic.Store(s, c, "sheep", 10f, FoodTrait.Meat, false));
            Assert.IsFalse(CareLogic.Store(s, c, "sheep", 10f, FoodTrait.Meat, false));
            Assert.AreEqual(3, s.storage.Count);
        }

        [Test]
        public void FeedFromStorage_FeedsAndRemoves_ButNotWhenFull()
        {
            var c = Care();
            var s = DragonState.CreateNew(c, T0);
            CareLogic.Store(s, c, "salmon", 20f, FoodTrait.Fish, false);
            CareLogic.Store(s, c, "salmon", 20f, FoodTrait.Fish, false);

            Assert.IsTrue(CareLogic.FeedFromStorage(s, c, 0, out var o));
            Assert.AreEqual(70f, s.hunger, 0.01f);
            Assert.AreEqual(20f, o.absorbed, 0.01f);
            Assert.AreEqual(20f, s.GetTrait(FoodTrait.Fish), 0.01f);
            Assert.AreEqual(1, s.storage.Count);

            s.hunger = c.maxHunger;
            Assert.IsFalse(CareLogic.FeedFromStorage(s, c, 0, out _), "배가 꽉 찼으면 먹이지 않음");
            Assert.AreEqual(1, s.storage.Count);
        }

        // ───────────── 성체 모습 ─────────────

        [Test]
        public void Form_FollowsDominantTrait()
        {
            var c = Care();
            var s = DragonState.CreateNew(c, T0);
            s.AddTrait(FoodTrait.Fish, 60f);
            s.AddTrait(FoodTrait.Meat, 40f);
            Assert.AreEqual(DragonForm.Tide, CareLogic.FormFor(s, c));
        }

        [Test]
        public void Form_IsPrismWhenBalanced()
        {
            var c = Care();
            var s = DragonState.CreateNew(c, T0);
            foreach (FoodTrait t in Enum.GetValues(typeof(FoodTrait))) s.AddTrait(t, 25f);
            Assert.AreEqual(DragonForm.Prism, CareLogic.FormFor(s, c));
        }

        [Test]
        public void BecomingAdult_SetsFormAndRecordsCodex()
        {
            var c = Care();
            var s = DragonState.CreateNew(c, T0);
            s.stage = GrowthStage.Juvenile;
            s.growthXp = 440f;
            s.hunger = 0.5f; // 굶주림 아님
            s.AddTrait(FoodTrait.Fire, 300f);
            var o = CareLogic.Feed(s, c, 20f, FoodTrait.Fire);
            Assert.IsTrue(o.stageChanged);
            Assert.AreEqual(GrowthStage.Adult, s.stage);
            Assert.AreEqual(DragonForm.Ember, s.form);
            Assert.AreEqual(DragonForm.Ember, o.newForm);
            Assert.IsTrue(s.HasForm(DragonForm.Ember));
        }

        // ───────────── 도감 / 새 알 ─────────────

        [Test]
        public void Codex_CountsCatchesAndRares()
        {
            var s = DragonState.CreateNew(Care(), T0);
            CareLogic.RecordCatch(s, "boar", false);
            CareLogic.RecordCatch(s, "boar", true);
            CareLogic.RecordCatch(s, "sheep", false);
            Assert.AreEqual(2, s.Codex("boar").caught);
            Assert.AreEqual(1, s.Codex("boar").rareCaught);
            Assert.AreEqual(1, s.Codex("sheep").caught);
            Assert.IsNull(s.Codex("salmon"));
        }

        [Test]
        public void NewEgg_KeepsCodex_ResetsDragon()
        {
            var c = Care();
            var s = DragonState.CreateNew(c, T0);
            s.stage = GrowthStage.Adult;
            s.form = DragonForm.Grove;
            s.codexForms.Add(DragonForm.Grove);
            s.growthXp = 500f;
            s.totalFlights = 12;
            s.AddTrait(FoodTrait.Plant, 300f);
            CareLogic.RecordCatch(s, "golden_melon", false);
            CareLogic.Store(s, c, "sheep", 10f, FoodTrait.Meat, false);

            var egg = CareLogic.HatchNewEgg(s, c, T0);
            Assert.AreEqual(GrowthStage.Hatchling, egg.stage);
            Assert.AreEqual(DragonForm.None, egg.form);
            Assert.AreEqual(0f, egg.growthXp);
            Assert.AreEqual(0f, egg.TotalTraits);
            Assert.AreEqual(0, egg.storage.Count);
            Assert.AreEqual(2, egg.generation);
            Assert.AreEqual(12, egg.totalFlights);
            Assert.IsTrue(egg.HasForm(DragonForm.Grove));
            Assert.AreEqual(1, egg.Codex("golden_melon").caught);
        }
    }
}
