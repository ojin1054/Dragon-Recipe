using System;
using System.Collections.Generic;
using DragonRecipe.Care;
using DragonRecipe.Farm;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using NUnit.Framework;
using UnityEngine;
using Random = System.Random;

namespace DragonRecipe.Tests
{
    public class FarmLogicTests
    {
        static readonly DateTime T0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>정해 둔 값을 차례로 돌려주는 난수</summary>
        class FixedRandom : Random
        {
            readonly Queue<double> _doubles;
            readonly int _pick;
            public FixedRandom(int pick, params double[] doubles)
            {
                _pick = pick;
                _doubles = new Queue<double>(doubles);
            }
            public override double NextDouble() => _doubles.Count > 0 ? _doubles.Dequeue() : 0.5;
            public override int Next(int maxValue) => Math.Min(_pick, maxValue - 1);
        }

        static Random Succeed => new FixedRandom(0, 0.0);
        static Random Fail(int returned = 0) => new FixedRandom(returned, 0.999);

        readonly List<UnityEngine.Object> _made = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) UnityEngine.Object.DestroyImmediate(o);
            _made.Clear();
        }

        HybridAnimalData Hybrid(string id, int satiety, int flights, (FoodTrait, float)[] traits,
            params (BuffStat, float)[] buffs)
        {
            var h = ScriptableObject.CreateInstance<HybridAnimalData>();
            _made.Add(h);
            h.id = id;
            h.satiety = satiety;
            h.buffFlights = flights;
            h.traits = new List<TraitShare>();
            foreach (var (t, s) in traits) h.traits.Add(new TraitShare { trait = t, share = s });
            h.buffs = new List<BuffEffect>();
            foreach (var (st, m) in buffs) h.buffs.Add(new BuffEffect { stat = st, multiplier = m });
            return h;
        }

        static RecipeInput T(FoodTrait t) => new RecipeInput { kind = RecipeInputKind.Trait, trait = t };
        static RecipeInput A(string id) => new RecipeInput { kind = RecipeInputKind.Animal, animalId = id };

        HybridAnimalData _ironBoar, _lotusFrog, _goldenKoi;

        CombineTable Table()
        {
            _ironBoar = Hybrid("iron_boar", 8, 3, new[] { (FoodTrait.Meat, 1f) }, (BuffStat.MaxHealth, 1.3f));
            _lotusFrog = Hybrid("lotus_frog", 6, 3, new[] { (FoodTrait.Fish, .5f), (FoodTrait.Plant, .5f) },
                (BuffStat.DamageTaken, 0.6f));
            _goldenKoi = Hybrid("golden_koi", 8, 4, new[] { (FoodTrait.Fish, .5f), (FoodTrait.Plant, .5f) },
                (BuffStat.Glide, 1.15f));

            var t = ScriptableObject.CreateInstance<CombineTable>();
            _made.Add(t);
            t.rareBonus = 0.1f;
            t.pityPerFail = 0.1f;
            t.maxSuccessChance = 0.95f;
            t.failReturnsOne = true;
            t.recipes = new List<CombineRecipe>
            {
                new CombineRecipe { id = "meat_meat", a = T(FoodTrait.Meat), b = T(FoodTrait.Meat), result = _ironBoar,
                    successChance = 0.8f, durationMinutes = 20 },
                new CombineRecipe { id = "fish_plant", a = T(FoodTrait.Fish), b = T(FoodTrait.Plant), result = _lotusFrog,
                    successChance = 0.65f, durationMinutes = 35 },
                new CombineRecipe { id = "salmon_golden_melon", a = A("salmon"), b = A("golden_melon"), result = _goldenKoi,
                    successChance = 0.5f, durationMinutes = 60 },
            };
            return t;
        }

        static FarmSettings Settings() => new FarmSettings
        {
            basePenCapacity = 4,
            penUpgrades = new List<PenUpgrade>
            {
                new PenUpgrade { cost = 100, addCapacity = 2 },
                new PenUpgrade { cost = 250, addCapacity = 2 }
            },
            combineSlots = 1,
            maxActiveBuffs = 3
        };

        static CareSettings Care() => new CareSettings
        {
            maxHunger = 100f, startingHunger = 50f, xpToJuvenile = 150f, xpToAdult = 450f, starvationRecovery = 20f
        };

        static FarmAnimal Add(FarmState f, FarmSettings s, string id, FoodTrait trait, bool rare = false)
            => FarmLogic.AddAnimal(f, s, id, trait, 10f, rare);

        // ───────────── 우리 ─────────────

        [Test]
        public void Pen_HoldsFourByDefault()
        {
            var f = new FarmState();
            var s = Settings();
            for (int i = 0; i < 4; i++) Assert.IsNotNull(Add(f, s, "sheep", FoodTrait.Meat));
            Assert.IsNull(Add(f, s, "sheep", FoodTrait.Meat));
            Assert.AreEqual(4, f.animals.Count);
        }

        [Test]
        public void Pen_UpgradesInOrderUntilDone()
        {
            var f = new FarmState();
            var s = Settings();
            Assert.AreEqual(100, FarmLogic.NextPenUpgrade(f, s).cost);
            Assert.IsTrue(FarmLogic.UpgradePen(f, s));
            Assert.AreEqual(6, FarmLogic.PenCapacity(f, s));
            Assert.AreEqual(250, FarmLogic.NextPenUpgrade(f, s).cost);
            Assert.IsTrue(FarmLogic.UpgradePen(f, s));
            Assert.AreEqual(8, FarmLogic.PenCapacity(f, s));
            Assert.IsNull(FarmLogic.NextPenUpgrade(f, s));
            Assert.IsFalse(FarmLogic.UpgradePen(f, s));
            Assert.AreEqual(8, FarmLogic.PenCapacity(f, s));
        }

        // ───────────── 조합표 ─────────────

        [Test]
        public void Recipe_MatchesInEitherOrder()
        {
            var t = Table();
            var f = new FarmState();
            var s = Settings();
            var fish = Add(f, s, "salmon_x", FoodTrait.Fish);
            var plant = Add(f, s, "melon_x", FoodTrait.Plant);
            Assert.AreEqual("fish_plant", t.Find(fish, plant).id);
            Assert.AreEqual("fish_plant", t.Find(plant, fish).id);
        }

        [Test]
        public void Recipe_SpecificAnimalsWinOverTraits()
        {
            var t = Table();
            var f = new FarmState();
            var s = Settings();
            var salmon = Add(f, s, "salmon", FoodTrait.Fish);
            var melon = Add(f, s, "golden_melon", FoodTrait.Plant);
            Assert.AreEqual("salmon_golden_melon", t.Find(melon, salmon).id);
        }

        [Test]
        public void Recipe_TraitInputIgnoresHybrids()
        {
            var t = Table();
            var f = new FarmState();
            var s = Settings();
            var boar = Add(f, s, "boar", FoodTrait.Meat);
            var hybrid = FarmLogic.NewHybrid(_ironBoar); // 성질은 고기지만 조합 동물
            f.animals.Add(hybrid);
            Assert.AreEqual(CombineBlock.NoRecipe, FarmLogic.CanCombine(f, s, t, boar.uid, hybrid.uid, out _));
        }

        [Test]
        public void SuccessChance_AddsRareAndPity_Capped()
        {
            var t = Table();
            var r = t.ById("meat_meat");
            var plain = new FarmAnimal { trait = FoodTrait.Meat };
            var rare = new FarmAnimal { trait = FoodTrait.Meat, rare = true };
            Assert.AreEqual(0.8f, t.SuccessChance(r, plain, plain, 0), 1e-4f);
            Assert.AreEqual(0.9f, t.SuccessChance(r, rare, plain, 0), 1e-4f);
            Assert.AreEqual(0.9f, t.SuccessChance(r, plain, plain, 1), 1e-4f);
            Assert.AreEqual(0.95f, t.SuccessChance(r, rare, rare, 3), 1e-4f);
        }

        // ───────────── 조합 진행 ─────────────

        [Test]
        public void Combine_BlockedWhenSlotsFull()
        {
            var t = Table();
            var f = new FarmState();
            var s = Settings();
            var a = Add(f, s, "boar", FoodTrait.Meat);
            var b = Add(f, s, "sheep", FoodTrait.Meat);
            var c = Add(f, s, "boar", FoodTrait.Meat);
            var d = Add(f, s, "sheep", FoodTrait.Meat);
            Assert.IsNotNull(FarmLogic.StartCombine(f, s, t, a.uid, b.uid, T0, Succeed));
            Assert.AreEqual(CombineBlock.NoSlot, FarmLogic.CanCombine(f, s, t, c.uid, d.uid, out _));
            Assert.AreEqual(CombineBlock.SameAnimal, FarmLogic.CanCombine(f, s, t, c.uid, c.uid, out _));
        }

        [Test]
        public void Combine_TakesRealTime_ThenYieldsHybrid()
        {
            var t = Table();
            var f = new FarmState();
            var s = Settings();
            var a = Add(f, s, "boar", FoodTrait.Meat);
            var b = Add(f, s, "sheep", FoodTrait.Meat);

            var job = FarmLogic.StartCombine(f, s, t, a.uid, b.uid, T0, Succeed);
            Assert.AreEqual(0, f.animals.Count, "재료는 우리에서 빠진다");
            Assert.AreEqual(0.5f, job.Progress(T0.AddMinutes(10)), 1e-3f);

            Assert.IsEmpty(FarmLogic.CollectReady(f, s, t, T0.AddMinutes(19)));
            var results = FarmLogic.CollectReady(f, s, t, T0.AddMinutes(20));

            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].success);
            Assert.IsTrue(results[0].firstDiscovery);
            Assert.AreEqual("iron_boar", f.animals[0].id);
            Assert.IsTrue(f.animals[0].hybrid);
            Assert.IsEmpty(f.jobs);
            Assert.IsTrue(f.Discovered("meat_meat"));
        }

        [Test]
        public void Combine_OutcomeIsDecidedAtStart()
        {
            var t = Table();
            var f = new FarmState();
            var s = Settings();
            var a = Add(f, s, "boar", FoodTrait.Meat);
            var b = Add(f, s, "sheep", FoodTrait.Meat);
            var job = FarmLogic.StartCombine(f, s, t, a.uid, b.uid, T0, Fail(returned: 1));
            Assert.IsFalse(job.success);
            Assert.AreEqual(1, job.returnedInput);
        }

        [Test]
        public void Combine_FailReturnsOneInput_AndRaisesPity()
        {
            var t = Table();
            var f = new FarmState();
            var s = Settings();
            var a = Add(f, s, "boar", FoodTrait.Meat);
            var b = Add(f, s, "sheep", FoodTrait.Meat);
            FarmLogic.StartCombine(f, s, t, a.uid, b.uid, T0, Fail(returned: 1));

            var results = FarmLogic.CollectReady(f, s, t, T0.AddHours(1));

            Assert.IsFalse(results[0].success);
            Assert.AreSame(b, results[0].animal);
            Assert.AreEqual(1, f.animals.Count);
            Assert.AreSame(b, f.animals[0]);
            Assert.AreEqual(1, f.Record("meat_meat").failStreak);
            Assert.IsFalse(f.Discovered("meat_meat"));

            var c = Add(f, s, "boar", FoodTrait.Meat);
            Assert.AreEqual(0.9f, FarmLogic.SuccessChance(f, t, b, c), 1e-4f);
        }

        [Test]
        public void Combine_FailCanLoseBoth()
        {
            var t = Table();
            t.failReturnsOne = false;
            var f = new FarmState();
            var s = Settings();
            var a = Add(f, s, "boar", FoodTrait.Meat);
            var b = Add(f, s, "sheep", FoodTrait.Meat);
            FarmLogic.StartCombine(f, s, t, a.uid, b.uid, T0, Fail());

            var results = FarmLogic.CollectReady(f, s, t, T0.AddHours(1));

            Assert.IsNull(results[0].animal);
            Assert.IsEmpty(f.animals);
        }

        [Test]
        public void Combine_SuccessResetsPity()
        {
            var t = Table();
            var f = new FarmState();
            var s = Settings();
            f.recipes.Add(new RecipeRecord { recipeId = "meat_meat", failStreak = 2 });
            var a = Add(f, s, "boar", FoodTrait.Meat);
            var b = Add(f, s, "sheep", FoodTrait.Meat);
            FarmLogic.StartCombine(f, s, t, a.uid, b.uid, T0, Succeed);
            FarmLogic.CollectReady(f, s, t, T0.AddHours(1));
            Assert.AreEqual(0, f.Record("meat_meat").failStreak);
            Assert.AreEqual(1, f.Record("meat_meat").successes);
        }

        [Test]
        public void Combine_WaitsWhenPenIsFull()
        {
            var t = Table();
            var f = new FarmState();
            var s = Settings();
            var a = Add(f, s, "boar", FoodTrait.Meat);
            var b = Add(f, s, "sheep", FoodTrait.Meat);
            FarmLogic.StartCombine(f, s, t, a.uid, b.uid, T0, Succeed);
            for (int i = 0; i < 4; i++) Add(f, s, "salmon", FoodTrait.Fish);

            Assert.IsEmpty(FarmLogic.CollectReady(f, s, t, T0.AddHours(1)));
            Assert.AreEqual(1, f.jobs.Count, "자리가 날 때까지 조합 칸에 남는다");

            f.animals.RemoveAt(0);
            Assert.AreEqual(1, FarmLogic.CollectReady(f, s, t, T0.AddHours(1)).Count);
            Assert.IsEmpty(f.jobs);
        }

        // ───────────── 먹이기 / 버프 ─────────────

        [Test]
        public void Feed_PlainAnimal_FillsHungerAndTrait()
        {
            var t = Table();
            var f = new FarmState();
            var s = Settings();
            var care = Care();
            var dragon = DragonState.CreateNew(care, T0);
            var boar = Add(f, s, "boar", FoodTrait.Meat);

            Assert.IsTrue(FarmLogic.Feed(f, s, t, dragon, care, boar.uid, out var o));

            Assert.AreEqual(60f, dragon.hunger, 1e-3f);
            Assert.AreEqual(10f, dragon.GetTrait(FoodTrait.Meat), 1e-3f);
            Assert.IsNull(o.buff);
            Assert.IsEmpty(f.animals);
            Assert.IsEmpty(f.buffs);
        }

        [Test]
        public void Feed_Hybrid_SplitsTraitsAndAddsBuff()
        {
            var t = Table();
            var f = new FarmState();
            var s = Settings();
            var care = Care();
            var dragon = DragonState.CreateNew(care, T0);
            var frog = FarmLogic.NewHybrid(_lotusFrog);
            f.animals.Add(frog);

            Assert.IsTrue(FarmLogic.Feed(f, s, t, dragon, care, frog.uid, out var o));

            Assert.AreEqual(56f, dragon.hunger, 1e-3f);
            Assert.AreEqual(3f, dragon.GetTrait(FoodTrait.Fish), 1e-3f);
            Assert.AreEqual(3f, dragon.GetTrait(FoodTrait.Plant), 1e-3f);
            Assert.AreEqual(6f, o.feed.absorbed, 1e-3f);
            Assert.AreEqual("lotus_frog", o.buff.sourceId);
            Assert.AreEqual(3, f.buffs[0].flightsLeft);
        }

        [Test]
        public void Feed_RefusedWhenFull()
        {
            var t = Table();
            var f = new FarmState();
            var s = Settings();
            var care = Care();
            var dragon = DragonState.CreateNew(care, T0);
            dragon.hunger = care.maxHunger;
            var boar = Add(f, s, "boar", FoodTrait.Meat);

            Assert.IsFalse(FarmLogic.Feed(f, s, t, dragon, care, boar.uid, out _));
            Assert.AreEqual(1, f.animals.Count);
        }

        [Test]
        public void Buff_SameSourceRefreshesInsteadOfStacking()
        {
            Table();
            var f = new FarmState();
            var s = Settings();
            FarmLogic.AddBuff(f, s, _ironBoar, out _);
            f.buffs[0].flightsLeft = 1;
            FarmLogic.AddBuff(f, s, _ironBoar, out _);
            Assert.AreEqual(1, f.buffs.Count);
            Assert.AreEqual(3, f.buffs[0].flightsLeft);
        }

        [Test]
        public void Buff_OverCapDropsTheOneClosestToEnding()
        {
            Table();
            var f = new FarmState();
            var s = Settings();
            s.maxActiveBuffs = 2;
            FarmLogic.AddBuff(f, s, _ironBoar, out _);
            FarmLogic.AddBuff(f, s, _lotusFrog, out _);
            f.buffs[1].flightsLeft = 1;

            FarmLogic.AddBuff(f, s, _goldenKoi, out var removed);

            Assert.AreEqual("lotus_frog", removed.sourceId);
            Assert.AreEqual(2, f.buffs.Count);
        }

        [Test]
        public void BeginFlight_AppliesBuffsThenCountsDown()
        {
            Table();
            var f = new FarmState();
            var s = Settings();
            var source = new FlightParams { maxHealth = 100f, touchDamage = 2f };
            FarmLogic.AddBuff(f, s, _ironBoar, out _);   // 최대 체력 ×1.3, 3회
            FarmLogic.AddBuff(f, s, _lotusFrog, out _);  // 받는 피해 ×0.6, 3회
            f.buffs[1].flightsLeft = 1;

            var p = FarmLogic.BeginFlight(f, source);

            Assert.AreEqual(130f, p.maxHealth, 1e-3f);
            Assert.AreEqual(1.2f, p.touchDamage, 1e-3f);
            Assert.AreEqual(100f, source.maxHealth, "원본은 그대로");
            Assert.AreEqual(1, f.buffs.Count, "마지막 횟수를 쓴 버프는 사라진다");
            Assert.AreEqual(2, f.buffs[0].flightsLeft);
        }

        [Test]
        public void SameStatBuffsMultiply()
        {
            var buffs = new List<ActiveBuff>
            {
                new ActiveBuff { flightsLeft = 1, effects = { new BuffEffect { stat = BuffStat.Speed, multiplier = 1.1f } } },
                new ActiveBuff { flightsLeft = 1, effects = { new BuffEffect { stat = BuffStat.Speed, multiplier = 1.2f } } }
            };
            var p = FarmBuffs.Apply(new FlightParams { maxSpeed = 40f }, buffs);
            Assert.AreEqual(40f * 1.1f * 1.2f, p.maxSpeed, 1e-3f);
        }

        [Test]
        public void NewEgg_KeepsFarmButClearsBuffs()
        {
            Table();
            var f = new FarmState();
            var s = Settings();
            Add(f, s, "boar", FoodTrait.Meat);
            f.penUpgradeLevel = 1;
            FarmLogic.AddBuff(f, s, _ironBoar, out _);

            FarmLogic.OnNewEgg(f);

            Assert.IsEmpty(f.buffs);
            Assert.AreEqual(1, f.animals.Count);
            Assert.AreEqual(1, f.penUpgradeLevel);
        }

        [Test]
        public void FarmState_SurvivesJsonRoundTrip()
        {
            var t = Table();
            var f = new FarmState();
            var s = Settings();
            var a = Add(f, s, "boar", FoodTrait.Meat, rare: true);
            var b = Add(f, s, "sheep", FoodTrait.Meat);
            Add(f, s, "salmon", FoodTrait.Fish);
            FarmLogic.StartCombine(f, s, t, a.uid, b.uid, T0, Fail(returned: 0));
            FarmLogic.AddBuff(f, s, _ironBoar, out _);

            var loaded = JsonUtility.FromJson<FarmState>(JsonUtility.ToJson(f));

            Assert.AreEqual(1, loaded.animals.Count);
            Assert.AreEqual(1, loaded.jobs.Count);
            Assert.IsFalse(loaded.jobs[0].success);
            Assert.AreEqual(0, loaded.jobs[0].returnedInput);
            Assert.IsTrue(loaded.jobs[0].inputA.rare);
            Assert.AreEqual(T0.AddMinutes(20).Ticks, loaded.jobs[0].readyUtcTicks);
            Assert.AreEqual(1.3f, loaded.buffs[0].effects[0].multiplier, 1e-4f);
        }
    }
}
