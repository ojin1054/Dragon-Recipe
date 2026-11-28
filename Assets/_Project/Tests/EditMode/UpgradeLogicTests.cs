using System.Collections.Generic;
using DragonRecipe.Care;
using DragonRecipe.Farm;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using DragonRecipe.Skills;
using DragonRecipe.Tribes;
using DragonRecipe.Upgrades;
using NUnit.Framework;

namespace DragonRecipe.Tests
{
    public class UpgradeLogicTests
    {
        readonly TribeSettings _ts = new TribeSettings();

        DragonState NewState(GrowthStage stage = GrowthStage.Adult)
        {
            var s = new DragonState { stage = stage };
            s.tribes = TribeLogic.CreateNew(_ts);
            return s;
        }

        static void Stock(DragonState s, FoodTrait t, int count, bool rare = false, float satiety = 10f)
        {
            for (int i = 0; i < count; i++)
                s.storage.Add(new StoredFood { id = t.ToString().ToLowerInvariant(), trait = t, satiety = satiety, rare = rare });
        }

        static UpgradeNode Node(string id) => UpgradeCatalog.Find(id);

        [Test]
        public void Catalog_IdsAreUnique_AndRequirementsExist()
        {
            var seen = new HashSet<string>();
            foreach (var n in UpgradeCatalog.All)
            {
                Assert.IsTrue(seen.Add(n.id), n.id);
                foreach (var req in n.requires) Assert.IsNotNull(UpgradeCatalog.Find(req), $"{n.id} → {req}");
                Assert.IsTrue(n.costs.Count > 0 || n.rareCost > 0, $"{n.id}: 값이 없음");
                Assert.IsTrue(n.effects.Count > 0, $"{n.id}: 효과가 없음");
            }
        }

        [Test]
        public void Root_IsAvailable_ChildIsLockedUntilParentOwned()
        {
            var s = NewState();
            Assert.AreEqual(UpgradeStatus.Available, UpgradeLogic.StatusOf(Node("f_glide"), s, _ts));
            Assert.AreEqual(UpgradeStatus.Locked, UpgradeLogic.StatusOf(Node("f_speed"), s, _ts));
            Stock(s, FoodTrait.Fish, 2);
            Assert.IsTrue(UpgradeLogic.Buy(Node("f_glide"), s, _ts));
            Assert.AreEqual(UpgradeStatus.Owned, UpgradeLogic.StatusOf(Node("f_glide"), s, _ts));
            Assert.AreEqual(UpgradeStatus.Available, UpgradeLogic.StatusOf(Node("f_speed"), s, _ts));
        }

        [Test]
        public void Buy_TakesCheapCommonFoodFirst_AndFailsWhenShort()
        {
            var s = NewState();
            Stock(s, FoodTrait.Fish, 1, rare: true, satiety: 30f);
            Stock(s, FoodTrait.Fish, 1, satiety: 20f);
            Assert.IsFalse(UpgradeLogic.CanAfford(Node("f_glide"), new List<StoredFood> { s.storage[0] }));
            Stock(s, FoodTrait.Fish, 1, satiety: 5f);
            Stock(s, FoodTrait.Meat, 1);
            Assert.IsTrue(UpgradeLogic.Buy(Node("f_glide"), s, _ts));
            // 희귀 물고기와 고기는 남는다
            Assert.AreEqual(2, s.storage.Count);
            Assert.IsTrue(s.storage.Exists(f => f.rare && f.trait == FoodTrait.Fish));
            Assert.IsTrue(s.storage.Exists(f => f.trait == FoodTrait.Meat));
            Assert.IsFalse(UpgradeLogic.Buy(Node("f_glide"), s, _ts), "두 번은 못 배움");
        }

        [Test]
        public void RareCost_NeedsRareFoodBeyondTraitCost()
        {
            var s = NewState();
            foreach (var id in new[] { "f_glide", "f_speed", "f_agility" }) s.upgrades.owned.Add(id);
            var n = Node("f_health"); // 고기 4 + 희귀 1
            Stock(s, FoodTrait.Meat, 4);
            Assert.IsFalse(UpgradeLogic.CanAfford(n, s.storage));
            Stock(s, FoodTrait.Plant, 1, rare: true);
            Assert.IsTrue(UpgradeLogic.CanAfford(n, s.storage));
            Assert.IsTrue(UpgradeLogic.Buy(n, s, _ts));
            Assert.AreEqual(0, s.storage.Count);
        }

        [Test]
        public void StageGate_BlocksYoungDragon()
        {
            var s = NewState(GrowthStage.Hatchling);
            Assert.AreEqual(UpgradeStatus.Gated, UpgradeLogic.StatusOf(Node("s_cooldown"), s, _ts));
            s.stage = GrowthStage.Juvenile;
            Assert.AreEqual(UpgradeStatus.Available, UpgradeLogic.StatusOf(Node("s_cooldown"), s, _ts));
        }

        [Test]
        public void TribeGate_NeedsStanding_AndGoesDormantWhenRelationsSour()
        {
            var s = NewState();
            var n = Node("th_t2"); // 사람 2단: 우호
            foreach (var id in new[] { "th_a1", "th_a2", "th_t1", "th_b1", "th_b2" }) s.upgrades.owned.Add(id);
            int before = UpgradeLogic.BonusOf(s, _ts).storageSlots;
            s.tribes.Get(Tribe.Human).reputation = 0f; // 중립에서 시작
            Assert.AreEqual(UpgradeStatus.Gated, UpgradeLogic.StatusOf(n, s, _ts));
            s.tribes.Get(Tribe.Human).reputation = _ts.friendlyAt + 1f;
            Assert.AreEqual(UpgradeStatus.Available, UpgradeLogic.StatusOf(n, s, _ts));
            Stock(s, FoodTrait.Meat, 3);
            Stock(s, FoodTrait.Plant, 3);
            Assert.IsTrue(UpgradeLogic.Buy(n, s, _ts));
            Assert.AreEqual(before + 6, UpgradeLogic.BonusOf(s, _ts).storageSlots);

            s.tribes.Get(Tribe.Human).reputation = 0f; // 중립으로 내려가도 유지
            Assert.AreEqual(UpgradeStatus.Owned, UpgradeLogic.StatusOf(n, s, _ts));
            s.tribes.Get(Tribe.Human).reputation = _ts.waryAt - 1f;
            Assert.AreEqual(UpgradeStatus.Dormant, UpgradeLogic.StatusOf(n, s, _ts));
            Assert.Less(UpgradeLogic.BonusOf(s, _ts).storageSlots, before + 6, "잠든 동안 효과 없음");
            s.tribes.Get(Tribe.Human).reputation = 0f;
            Assert.AreEqual(before + 6, UpgradeLogic.BonusOf(s, _ts).storageSlots, "사이가 풀리면 다시 켜짐");
        }

        [Test]
        public void TribeTrunk_IsSkillSkillTier_ThreeTimes_WithSideBranches()
        {
            foreach (var prefix in new[] { "th", "to", "te" })
            {
                // 줄기: a1 · a2 · 1단 · b1 · b2 · 2단 · c1 · c2 · 3단
                string[] trunk = { "a1", "a2", "t1", "b1", "b2", "t2", "c1", "c2", "t3" };
                for (int i = 0; i < trunk.Length; i++)
                {
                    var n = Node($"{prefix}_{trunk[i]}");
                    Assert.IsNotNull(n, $"{prefix}_{trunk[i]}");
                    Assert.AreEqual((i + 1) % 3 == 0 ? (i + 1) / 3 : 0, n.tier, n.id);
                    if (i > 0) Assert.AreEqual($"{prefix}_{trunk[i - 1]}", n.requires[0], n.id);
                }
                Assert.AreEqual(Standing.Neutral, Node($"{prefix}_t1").standing);
                Assert.AreEqual(Standing.Friendly, Node($"{prefix}_t2").standing);
                Assert.AreEqual(Standing.Revered, Node($"{prefix}_t3").standing);

                int sides = 0;
                foreach (var n in UpgradeCatalog.All)
                    if (n.id.StartsWith(prefix + "_s")) sides++;
                Assert.GreaterOrEqual(sides, 3, "곁가지가 있어야 함");
            }
        }

        [Test]
        public void TribeSkill_BeforeFirstTier_OnlyNeedsNotHostile()
        {
            var s = NewState();
            var n = Node("to_a1");
            s.tribes.Get(Tribe.Orc).reputation = _ts.waryAt - 1f; // 경계
            Assert.AreEqual(UpgradeStatus.Available, UpgradeLogic.StatusOf(n, s, _ts));
            s.upgrades.owned.Add(n.id);
            Assert.AreEqual(UpgradeStatus.Owned, UpgradeLogic.StatusOf(n, s, _ts), "경계여도 켜져 있음");
            s.tribes.Get(Tribe.Orc).reputation = _ts.hostileAt - 1f;
            Assert.AreEqual(UpgradeStatus.Dormant, UpgradeLogic.StatusOf(n, s, _ts));
        }

        [Test]
        public void TimedOffering_AddsToAltar_PerInterval_AndStopsWhenFull()
        {
            var s = NewState();
            foreach (var id in new[] { "th_a1", "th_a2", "th_t1" }) s.upgrades.owned.Add(id);
            var t0 = new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
            var bonus = UpgradeLogic.BonusOf(s, _ts);
            Assert.AreEqual(4f, bonus.offeringHours[(int)Tribe.Human], 0.001f);
            Assert.AreEqual(0f, bonus.offeringHours[(int)Tribe.Orc]);

            Assert.AreEqual(0, UpgradeLogic.TickOfferings(s, _ts, bonus, t0), "처음에는 세기 시작만");
            Assert.AreEqual(0, UpgradeLogic.TickOfferings(s, _ts, bonus, t0.AddHours(3.9)));
            Assert.AreEqual(2, UpgradeLogic.TickOfferings(s, _ts, bonus, t0.AddHours(9)), "4시간마다 하나");
            Assert.AreEqual(Tribe.Human, s.tribes.altar[0].from);
            Assert.AreEqual(_ts.OfferingFood(Tribe.Human), s.tribes.altar[0].foodId);

            // 넉넉한 제물: 25% 자주
            s.upgrades.owned.Add("th_b1");
            Assert.AreEqual(3f, UpgradeLogic.BonusOf(s, _ts).offeringHours[(int)Tribe.Human], 0.001f);

            // 오래 비워 둬도 제단 크기까지만
            UpgradeLogic.TickOfferings(s, _ts, bonus, t0.AddHours(1000));
            Assert.AreEqual(_ts.altarCapacity, s.tribes.altar.Count);

            // 사이가 틀어지면 멈춘다
            s.tribes.altar.Clear();
            s.tribes.Get(Tribe.Human).reputation = _ts.waryAt - 1f;
            var off = UpgradeLogic.BonusOf(s, _ts);
            Assert.AreEqual(0f, off.offeringHours[(int)Tribe.Human]);
            Assert.AreEqual(0, UpgradeLogic.TickOfferings(s, _ts, off, t0.AddHours(2000)));
        }

        [Test]
        public void Layout_PlacesEveryNode_WithoutOverlap_OutwardFromParent()
        {
            var all = UpgradeCatalog.All;
            var layout = UpgradeLayout.Build(all);
            foreach (var n in all) Assert.IsTrue(layout.positions.ContainsKey(n.id), n.id);

            for (int i = 0; i < all.Count; i++)
            {
                var a = layout[all[i].id];
                Assert.Greater(a.magnitude, 120f, $"{all[i].id}: 심장과 겹침");
                for (int j = i + 1; j < all.Count; j++)
                {
                    float d = UnityEngine.Vector2.Distance(a, layout[all[j].id]);
                    Assert.Greater(d, 100f, $"{all[i].id} 와 {all[j].id} 가 겹침 ({d:0})");
                }
                // 자식은 부모보다 바깥 고리
                if (all[i].requires.Length > 0)
                    Assert.Greater(a.magnitude, layout[all[i].requires[0]].magnitude, all[i].id);
            }
            // 엘프 줄기는 위쪽
            Assert.Greater(layout["te_a1"].y, 0f);
            Assert.AreEqual(0f, layout["te_a1"].x, 1f);
        }

        [Test]
        public void ExclusiveChoice_LocksTheOtherPath()
        {
            var s = NewState();
            foreach (var id in new[] { "s_cooldown", "s_charge", "s_burst", "s_breath_range", "s_breath_cost" }) s.upgrades.owned.Add(id);
            Assert.AreEqual(UpgradeStatus.Available, UpgradeLogic.StatusOf(Node("s_control"), s, _ts));
            Stock(s, FoodTrait.Fire, 3);
            Stock(s, FoodTrait.Fire, 1, rare: true);
            Assert.IsTrue(UpgradeLogic.Buy(Node("s_power"), s, _ts));
            Assert.AreEqual(UpgradeStatus.Excluded, UpgradeLogic.StatusOf(Node("s_control"), s, _ts));
            Stock(s, FoodTrait.Plant, 4, rare: true);
            Assert.IsFalse(UpgradeLogic.Buy(Node("s_control"), s, _ts));
        }

        [Test]
        public void Bonus_AppliesToSkills_WithoutTouchingTheOriginal()
        {
            var s = NewState();
            foreach (var id in new[] { "s_cooldown", "s_charge", "s_burst", "s_breath_range", "s_power" }) s.upgrades.owned.Add(id);
            var b = UpgradeLogic.BonusOf(s, _ts);
            var book = new SkillBook();
            var orb = b.Apply(book.fireOrb);
            Assert.AreNotSame(book.fireOrb, orb);
            Assert.AreEqual(book.fireOrb.maxCharges + 1, orb.maxCharges);
            Assert.AreEqual(book.fireOrb.cooldown * 0.75f, orb.cooldown, 1e-4f);
            Assert.AreEqual(book.fireOrb.burstRadius * 1.3f, orb.burstRadius, 1e-4f);
            Assert.AreEqual(book.fireOrb.damage * 1.4f, orb.damage, 1e-4f);
            var breath = b.Apply(book.fireBreath);
            Assert.AreEqual(book.fireBreath.range * 1.25f, breath.range, 1e-3f);
            Assert.AreEqual(2, new SkillBook().fireOrb.maxCharges, "원본은 그대로");
            Assert.AreSame(book.fireOrb, new UpgradeBonus().Apply(book.fireOrb), "바뀌는 게 없으면 원본");
        }

        [Test]
        public void Bonus_FlightStats_GoThroughBuffs()
        {
            var s = NewState();
            s.upgrades.owned.Add("f_glide");
            s.upgrades.owned.Add("f_stamina1");
            var b = UpgradeLogic.BonusOf(s, _ts);
            var p = FarmBuffs.Apply(new FlightParams { maxStamina = 100f }, new[] { b.AsBuff() });
            Assert.AreEqual(115f, p.maxStamina, 1e-3f);
        }

        [Test]
        public void NestUpgrades_ChangeFarmAndStorage()
        {
            var s = NewState();
            s.upgrades.owned.Add("n_storage1");
            Stock(s, FoodTrait.Meat, 2);
            Stock(s, FoodTrait.Plant, 1);
            Assert.IsTrue(UpgradeLogic.Buy(Node("n_pen1"), s, _ts));
            Assert.AreEqual(1, s.farm.penUpgradeLevel);
            Stock(s, FoodTrait.Fire, 2);
            Stock(s, FoodTrait.Fish, 2);
            Assert.IsTrue(UpgradeLogic.Buy(Node("n_combine"), s, _ts));
            Assert.AreEqual(1, s.farm.bonusCombineSlots);
            Assert.AreEqual(4, UpgradeLogic.BonusOf(s, _ts).storageSlots);
        }

        [Test]
        public void NewEgg_KeepsUpgrades()
        {
            var s = NewState();
            s.upgrades.owned.Add("f_glide");
            var next = CareLogic.HatchNewEgg(s, new CareSettings(), System.DateTime.UtcNow);
            Assert.IsTrue(next.upgrades.Has("f_glide"));
        }
    }
}
