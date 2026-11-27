using System.Collections.Generic;
using DragonRecipe.Care;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using DragonRecipe.Skills;
using NUnit.Framework;
using UnityEngine;

namespace DragonRecipe.Tests
{
    public class SkillRulesTests
    {
        readonly SkillBook _book = new SkillBook();

        [Test]
        public void Hatchling_HasNoSkill()
        {
            Assert.IsNull(SkillRules.PickOrb(_book, GrowthStage.Hatchling, true, FoodTrait.Fire));
            Assert.IsNull(SkillRules.PickBreath(_book, GrowthStage.Hatchling, DragonForm.None));
        }

        [Test]
        public void Juvenile_ShootsOrbOfDominantTrait_NoBreath()
        {
            Assert.AreSame(_book.rockOrb, SkillRules.PickOrb(_book, GrowthStage.Juvenile, true, FoodTrait.Meat));
            Assert.AreSame(_book.waterOrb, SkillRules.PickOrb(_book, GrowthStage.Juvenile, true, FoodTrait.Fish));
            Assert.AreSame(_book.fireOrb, SkillRules.PickOrb(_book, GrowthStage.Juvenile, true, FoodTrait.Fire));
            Assert.AreSame(_book.seedOrb, SkillRules.PickOrb(_book, GrowthStage.Juvenile, true, FoodTrait.Plant));
            Assert.IsNull(SkillRules.PickBreath(_book, GrowthStage.Juvenile, DragonForm.Ember));
        }

        [Test]
        public void Juvenile_WithoutTraits_HasNoSkill()
        {
            Assert.IsNull(SkillRules.PickOrb(_book, GrowthStage.Juvenile, false, FoodTrait.Meat));
        }

        [Test]
        public void Adult_KeepsOrb_AndGainsBreathOfForm()
        {
            Assert.AreSame(_book.fireOrb, SkillRules.PickOrb(_book, GrowthStage.Adult, true, FoodTrait.Fire));
            Assert.AreSame(_book.fireBreath, SkillRules.PickBreath(_book, GrowthStage.Adult, DragonForm.Ember));
            Assert.AreSame(_book.waterJet, SkillRules.PickBreath(_book, GrowthStage.Adult, DragonForm.Tide));
            Assert.AreSame(_book.pollenMist, SkillRules.PickBreath(_book, GrowthStage.Adult, DragonForm.Grove));
            Assert.AreSame(_book.roar, SkillRules.PickBreath(_book, GrowthStage.Adult, DragonForm.Hunter));
            Assert.AreSame(_book.rainbowBreath, SkillRules.PickBreath(_book, GrowthStage.Adult, DragonForm.Prism));
            Assert.AreSame(_book.rainbowBreath, SkillRules.PickBreath(_book, GrowthStage.Adult, DragonForm.None));
        }

        [Test]
        public void Pick_FromState_UsesMostEatenTraitAndForm()
        {
            var s = new DragonState { stage = GrowthStage.Juvenile };
            s.AddTrait(FoodTrait.Fish, 30f);
            s.AddTrait(FoodTrait.Fire, 10f);
            Assert.AreSame(_book.waterOrb, SkillRules.PickOrb(_book, s));
            Assert.IsNull(SkillRules.PickBreath(_book, s));
            s.stage = GrowthStage.Adult;
            s.form = DragonForm.Tide;
            Assert.AreSame(_book.waterOrb, SkillRules.PickOrb(_book, s));
            Assert.AreSame(_book.waterJet, SkillRules.PickBreath(_book, s));
        }

        [Test]
        public void Charges_UseAndRefill()
        {
            var c = new SkillCharges(2, 1f);
            Assert.IsTrue(c.TryUse());
            Assert.IsTrue(c.TryUse());
            Assert.IsFalse(c.TryUse());
            c.Tick(0.5f);
            Assert.AreEqual(0, c.count);
            Assert.AreEqual(0.5f, c.Progress, 1e-4f);
            c.Tick(0.6f);
            Assert.AreEqual(1, c.count);
            c.Tick(5f);
            Assert.AreEqual(2, c.count);
            Assert.IsTrue(c.Full);
            Assert.AreEqual(1f, c.Progress);
        }

        [Test]
        public void Orb_NeedsChargeAndStamina()
        {
            var s = _book.rockOrb;
            var c = new SkillCharges(s.maxCharges, s.cooldown);
            Assert.IsTrue(SkillRules.CanFireOrb(s, c, s.staminaCost));
            Assert.IsFalse(SkillRules.CanFireOrb(s, c, s.staminaCost - 0.1f));
            c.count = 0;
            Assert.IsFalse(SkillRules.CanFireOrb(s, c, 999f));
        }

        [Test]
        public void Breath_NeedsMinStaminaToStart_ButContinuesUntilEmpty()
        {
            var s = _book.fireBreath;
            Assert.IsFalse(SkillRules.CanBreathe(s, s.minStamina - 1f, false));
            Assert.IsTrue(SkillRules.CanBreathe(s, s.minStamina, false));
            Assert.IsTrue(SkillRules.CanBreathe(s, 1f, true));
            Assert.IsFalse(SkillRules.CanBreathe(s, 0f, true));
            Assert.AreEqual(s.staminaPerSecond * 0.5f, SkillRules.BreathDrain(s, 0.5f, 100f), 1e-4f);
            Assert.AreEqual(3f, SkillRules.BreathDrain(s, 10f, 3f), 1e-4f);
        }

        [Test]
        public void Cone_ContainsFrontNotBehind()
        {
            var a = SkillArea.Of(_book.fireBreath, Vector3.zero, Vector3.forward);
            Assert.IsTrue(a.Contains(new Vector3(0, 0, 10)));
            Assert.IsTrue(a.Contains(new Vector3(2, 0, 10)));
            Assert.IsFalse(a.Contains(new Vector3(10, 0, 3)));
            Assert.IsFalse(a.Contains(new Vector3(0, 0, -5)));
            Assert.IsFalse(a.Contains(new Vector3(0, 0, a.range + 2f)));
        }

        [Test]
        public void Line_IsLongAndThin()
        {
            var a = SkillArea.Of(_book.waterJet, Vector3.zero, Vector3.right);
            Assert.IsTrue(a.Contains(new Vector3(30, 0, 0)));
            Assert.IsTrue(a.Contains(new Vector3(20, 1, 0)));
            Assert.IsFalse(a.Contains(new Vector3(20, 4, 0)));
            Assert.IsFalse(a.Contains(new Vector3(-3, 0, 0)));
        }

        [Test]
        public void Ring_IsSphereAroundDragon_AndBoundsCoverArea()
        {
            var a = SkillArea.Of(_book.roar, new Vector3(5, 5, 5), Vector3.forward);
            Assert.IsTrue(a.Contains(new Vector3(5, 5 - 10, 5)));
            Assert.IsFalse(a.Contains(new Vector3(5, 5, 5 + a.range + 2f)));
            var cone = SkillArea.Of(_book.fireBreath, Vector3.zero, Vector3.forward);
            var far = new Vector3(0, 0, cone.range - 0.1f);
            Assert.IsTrue(cone.Contains(far));
            Assert.LessOrEqual(Vector3.Distance(cone.Center, far), cone.Radius);
        }

        [Test]
        public void Breaths_ReachFartherThanOrbBursts()
        {
            float maxBurst = Mathf.Max(_book.rockOrb.burstRadius, _book.waterOrb.burstRadius, _book.fireOrb.burstRadius, _book.seedOrb.burstRadius);
            foreach (var b in new[] { _book.fireBreath, _book.waterJet, _book.pollenMist, _book.roar, _book.rainbowBreath })
                Assert.Greater(b.range, maxBurst * 4f, b.id);
        }

        [Test]
        public void EnemyHit_MapsEffects()
        {
            var fire = SkillRules.EnemyHit(_book.fireOrb, Vector3.forward);
            Assert.Greater(fire.burn, 0f);
            Assert.AreEqual(0f, fire.stun);
            var water = SkillRules.EnemyHit(_book.waterOrb, Vector3.forward * 3f);
            Assert.IsTrue(water.clearArrows);
            Assert.AreEqual(_book.waterOrb.pushForce, water.push.magnitude, 1e-4f);
            var tick = SkillRules.EnemyHit(_book.fireBreath, Vector3.forward, 0.1f);
            Assert.AreEqual(_book.fireBreath.damage * 0.1f, tick.damage, 1e-4f);
        }

        [Test]
        public void PreyHold_OnlyForStoppingEffects()
        {
            Assert.Greater(SkillRules.PreyHold(_book.rockOrb), 0f);
            Assert.Greater(SkillRules.PreyHold(_book.seedOrb), 0f);
            Assert.AreEqual(0f, SkillRules.PreyHold(_book.fireOrb));
            Assert.Greater(SkillRules.PreyHold(_book.pollenMist), _book.pollenMist.effectDuration);
        }

        [Test]
        public void AimTarget_PicksSmallestAngleInsideCone()
        {
            var list = new List<Vector3> { new Vector3(10, 0, 10), new Vector3(1, 0, 20), new Vector3(0, 0, -10), new Vector3(0, 0, 500) };
            Assert.AreEqual(1, SkillRules.PickAimTarget(Vector3.zero, Vector3.forward, list, 100f, 30f));
            Assert.AreEqual(-1, SkillRules.PickAimTarget(Vector3.zero, Vector3.back * -1f, new List<Vector3> { new Vector3(0, 0, -10) }, 100f, 30f));
        }

        [Test]
        public void LaunchVelocity_ReachesTargetUnderGravity()
        {
            var from = new Vector3(0, 20, 0);
            var target = new Vector3(0, 0, 30);
            float speed = 40f, g = 10f;
            var v = SkillRules.LaunchVelocity(from, target, speed, g);
            Assert.AreEqual(speed, v.magnitude, 1e-3f);
            // 앞으로 날아가 target 높이에 닿는 순간의 z를 확인
            Vector3 p = from;
            Vector3 vel = v;
            const float dt = 0.002f;
            for (int i = 0; i < 5000 && p.y > target.y; i++)
            {
                vel += Vector3.down * g * dt;
                p += vel * dt;
            }
            Assert.AreEqual(target.z, p.z, 0.5f);
        }
    }
}
