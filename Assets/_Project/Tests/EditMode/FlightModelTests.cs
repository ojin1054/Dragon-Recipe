using DragonRecipe.Flight;
using NUnit.Framework;
using UnityEngine;

namespace DragonRecipe.Tests
{
    public class FlightModelTests
    {
        const float Dt = 0.02f;

        static FlightModel NewHatchling(float altitude = 50f, float speed = 14f)
        {
            var m = new FlightModel(new FlightParams());
            m.Reset(new Vector3(0, altitude, 0), 0f, speed);
            return m;
        }

        static void Run(FlightModel m, float seconds, Vector2 stick, float flapInterval = -1f)
        {
            float nextFlap = 0f;
            for (float t = 0; t < seconds; t += Dt)
            {
                if (flapInterval > 0 && t >= nextFlap)
                {
                    m.TryFlap();
                    nextFlap = t + flapInterval;
                }
                m.Step(Dt, stick);
            }
        }

        [Test]
        public void Diving_IncreasesSpeed()
        {
            var m = NewHatchling();
            Run(m, 2f, new Vector2(0, -1));
            Assert.Greater(m.Speed, 20f);
        }

        [Test]
        public void Climbing_DecreasesSpeed()
        {
            var m = NewHatchling(speed: 20f);
            Run(m, 2f, new Vector2(0, 1));
            Assert.Less(m.Speed, 20f);
        }

        [Test]
        public void Hatchling_WithoutFlapping_LosesAltitude()
        {
            var m = NewHatchling();
            Run(m, 5f, Vector2.zero);
            Assert.Less(m.Position.y, 50f - 8f);
        }

        [Test]
        public void RhythmicFlapping_StaysAirborneLongerThanGliding()
        {
            var glide = NewHatchling();
            var flap = NewHatchling();
            Run(glide, 8f, Vector2.zero);
            Run(flap, 8f, Vector2.zero, flapInterval: 0.6f);
            Assert.Greater(flap.Position.y, glide.Position.y + 10f);
        }

        [Test]
        public void SpamFlapping_IsLessEfficientThanRhythm()
        {
            var spam = NewHatchling();
            var rhythm = NewHatchling();
            Run(spam, 6f, Vector2.zero, flapInterval: 0.1f);
            Run(rhythm, 6f, Vector2.zero, flapInterval: 0.6f);
            Assert.Greater(rhythm.Position.y, spam.Position.y);
        }

        [Test]
        public void FlapTiming_IsJudged()
        {
            var p = new FlightParams();
            Assert.AreEqual(FlapOutcome.Weak, FlightModel.JudgeFlapTiming(0.1f, p, out _));
            Assert.AreEqual(FlapOutcome.Perfect, FlightModel.JudgeFlapTiming(p.idealFlapInterval, p, out float e));
            Assert.Greater(e, 1f);
            Assert.AreEqual(FlapOutcome.Normal, FlightModel.JudgeFlapTiming(5f, p, out _));
        }

        [Test]
        public void Flap_FailsWithoutStamina()
        {
            var m = NewHatchling();
            m.Stamina = 0f;
            Assert.AreEqual(FlapOutcome.Failed, m.TryFlap());
        }

        [Test]
        public void Stamina_Regenerates_WhileGliding()
        {
            var m = NewHatchling();
            m.TryFlap();
            float after = m.Stamina;
            Run(m, 2f, Vector2.zero);
            Assert.Greater(m.Stamina, after);
        }

        [Test]
        public void Pitch_IsHeld_AfterReleasingStick()
        {
            var m = NewHatchling(speed: 25f); // 실속하지 않을 속도
            Run(m, 0.5f, new Vector2(0, -1));
            float pitched = m.Pitch;
            Assert.Less(pitched, -20f);
            Run(m, 1f, Vector2.zero);
            Assert.AreEqual(pitched, m.Pitch, 0.01f, "손을 뗐는데 기수가 수평으로 돌아감");
        }

        [Test]
        public void Bank_IsHeld_AndKeepsTurning_AfterReleasingStick()
        {
            var m = NewHatchling(speed: 25f);
            Run(m, 0.3f, new Vector2(1, 0));
            float banked = m.Bank;
            Assert.Greater(banked, 15f);
            float yawBefore = m.Yaw;
            Run(m, 1f, Vector2.zero);
            Assert.AreEqual(banked, m.Bank, 0.01f, "손을 뗐는데 기울기가 수평으로 돌아감");
            Assert.Greater(m.Yaw, yawBefore + 10f, "기울어진 채로 선회가 계속되지 않음");
        }

        [Test]
        public void Bank_ReturnsToLevel_OnlyWithOppositeInput()
        {
            var m = NewHatchling(speed: 25f);
            Run(m, 0.3f, new Vector2(1, 0));
            Run(m, 0.3f, new Vector2(-1, 0));
            Assert.AreEqual(0f, m.Bank, 2f);
        }

        [Test]
        public void Turning_ChangesYaw()
        {
            var m = NewHatchling();
            Run(m, 1f, new Vector2(1, 0));
            Assert.Greater(m.Yaw, 30f);
        }

        [Test]
        public void GentleTouchdown_IsLanding()
        {
            var p = new FlightParams();
            var v = new Vector3(0, -2f, 12f);
            Assert.AreEqual(ImpactType.Landed, FlightRules.ClassifyImpact(v, Vector3.up, p));
        }

        [Test]
        public void SteepDive_IntoGround_IsCrash()
        {
            var p = new FlightParams();
            var v = new Vector3(0, -15f, 20f);
            Assert.AreEqual(ImpactType.Crashed, FlightRules.ClassifyImpact(v, Vector3.up, p));
        }

        [Test]
        public void HittingWall_IsAlwaysCrash()
        {
            var p = new FlightParams();
            var v = new Vector3(0, 0f, 5f);
            Assert.AreEqual(ImpactType.Crashed, FlightRules.ClassifyImpact(v, Vector3.back, p));
        }

        [Test]
        public void NearMiss_IsHigherWhenLowAndFast()
        {
            var p = new FlightParams();
            float lowFast = FlightRules.NearMissFactor(0.5f, 30f, p, 4f);
            float highSlow = FlightRules.NearMissFactor(3.5f, 10f, p, 4f);
            Assert.Greater(lowFast, highSlow);
            Assert.Greater(FlightRules.SnatchMultiplier(lowFast), FlightRules.SnatchMultiplier(highSlow));
        }

        // ── 이륙: 달리기(파워) + 웅크리기(높이) ──

        [Test]
        public void RunUp_TappingBuildsSpeed_AndStoppingSlowsDown()
        {
            var p = new FlightParams();
            var run = new RunUpModel(100f);
            for (int i = 0; i < 4; i++) run.Tap(p);
            Assert.AreEqual(p.runTapAccel * 4f, run.RunSpeed, 1e-4f);
            float before = run.RunSpeed;
            for (int i = 0; i < 25; i++) run.Step(Dt, p);
            Assert.Less(run.RunSpeed, before, "탭을 멈추면 느려져야 함");
            Assert.Greater(run.Distance, 0f);
        }

        [Test]
        public void RunUp_CanOnlyCrouchInJumpZone()
        {
            var p = new FlightParams { jumpZoneLength = 5f };
            var run = new RunUpModel(20f);
            Assert.IsFalse(run.TryCrouch(p), "구간 밖에서는 웅크릴 수 없음");
            run.Tap(p);
            while (!run.InJumpZone(p)) { run.Tap(p); run.Step(Dt, p); }
            Assert.IsTrue(run.TryCrouch(p));
            Assert.IsTrue(run.IsCrouching);
        }

        [Test]
        public void RunUp_PassingTheEdge_IsStumble()
        {
            var p = new FlightParams();
            var plan = RunUpModel.Evaluate(p.runMaxSpeed, -0.2f, p.crouchFullTime, p);
            Assert.AreEqual(LaunchGrade.Stumble, plan.grade);
            Assert.AreEqual(0f, plan.height);
        }

        [Test]
        public void Crouch_FullDepthAtEdge_IsPerfect_EarlyOrShallowIsWorse()
        {
            var p = new FlightParams();
            var perfect = RunUpModel.Evaluate(p.runMaxSpeed, 0.5f, p.crouchFullTime, p);
            var early = RunUpModel.Evaluate(p.runMaxSpeed, p.jumpZoneLength, p.crouchFullTime, p);
            var shallow = RunUpModel.Evaluate(p.runMaxSpeed, 0.5f, p.crouchFullTime * 0.3f, p);

            Assert.AreEqual(LaunchGrade.Perfect, perfect.grade);
            Assert.AreEqual(1f, perfect.height, 1e-4f);
            Assert.AreEqual(LaunchGrade.Weak, early.grade);
            Assert.AreEqual(LaunchGrade.Weak, shallow.grade);
            Assert.Less(early.height, perfect.height);
            Assert.Less(shallow.height, perfect.height);
        }

        [Test]
        public void RunPower_OnlyAffectsSpeed_CrouchOnlyAffectsHeight()
        {
            var p = new FlightParams();
            var slow = RunUpModel.Evaluate(0f, 0.5f, p.crouchFullTime, p);
            var fast = RunUpModel.Evaluate(p.runMaxSpeed, 0.5f, p.crouchFullTime, p);
            Assert.AreEqual(0f, slow.power, 1e-4f);
            Assert.AreEqual(1f, fast.power, 1e-4f);
            Assert.AreEqual(slow.height, fast.height, 1e-4f);
        }

        static float LaunchAndMeasure(float power, float height, LaunchGrade grade, out float speed)
        {
            var m = new FlightModel(new FlightParams());
            m.Launch(new Vector3(0, 30f, 0), 0f, new LaunchPlan { power = power, height = height, grade = grade });
            speed = m.Speed;
            float maxY = m.Position.y;
            for (float t = 0; t < 3f; t += Dt)
            {
                m.Step(Dt, Vector2.zero);
                maxY = Mathf.Max(maxY, m.Position.y);
            }
            return maxY;
        }

        [Test]
        public void Launch_PowerGivesSpeed_HeightGivesAltitude()
        {
            LaunchAndMeasure(0f, 0f, LaunchGrade.Weak, out float weakSpeed);
            LaunchAndMeasure(1f, 0f, LaunchGrade.Weak, out float fastSpeed);
            Assert.Greater(fastSpeed, weakSpeed, "잘 달리면 더 빠르게 출발");

            float lowY = LaunchAndMeasure(0.5f, 0f, LaunchGrade.Weak, out _);
            float highY = LaunchAndMeasure(0.5f, 1f, LaunchGrade.Good, out _);
            float perfectY = LaunchAndMeasure(0.5f, 1f, LaunchGrade.Perfect, out _);
            Assert.Greater(highY, lowY + 3f, "잘 웅크리면 확실히 더 높이");
            Assert.Greater(perfectY, highY, "완벽한 도약 보너스");
        }

        [Test]
        public void Stumble_DoesNotGainHeight()
        {
            float stumbleY = LaunchAndMeasure(1f, 1f, LaunchGrade.Stumble, out _);
            Assert.LessOrEqual(stumbleY, 30.01f);
        }

        // ── 지면에 살짝 닿기 ──

        [Test]
        public void GroundAndWall_AreTellApart()
        {
            Assert.IsTrue(FlightRules.IsGround(Vector3.up));
            Assert.IsFalse(FlightRules.IsGround(Vector3.back));
        }

        [Test]
        public void ShallowSkim_CostsLittleHealth()
        {
            var m = NewHatchling(speed: 30f);
            float damage = m.TakeGroundImpact(new Vector3(0, -1f, 30f), Vector3.up); // 빠르지만 거의 수평
            Assert.AreEqual(m.Params.touchDamage, damage, 1e-4f);
            Assert.Greater(m.Health, m.Params.maxHealth * 0.95f);
        }

        [Test]
        public void SteepFastImpact_CostsMuchMore_AndCanKill()
        {
            var p = new FlightParams();
            float shallow = FlightRules.GroundImpactDamage(new Vector3(0, -2f, 20f), Vector3.up, p);
            float steep = FlightRules.GroundImpactDamage(new Vector3(0, -10f, 20f), Vector3.up, p);
            Assert.Greater(steep, shallow * 5f, "가파르게 부딪히면 훨씬 크게 깎임");

            var m = NewHatchling(speed: 35f);
            m.TakeGroundImpact(new Vector3(0, -25f, 25f), Vector3.up); // 거의 내리꽂음
            Assert.IsTrue(m.IsDead);
        }

        [Test]
        public void Skidding_SlowlyDrainsHealth()
        {
            var m = NewHatchling();
            m.Skid(1f);
            Assert.AreEqual(m.Params.maxHealth - m.Params.skidDamagePerSecond, m.Health, 1e-4f);
        }

        [Test]
        public void Skid_StopsSinking_AndSlowsDown()
        {
            var m = NewHatchling(speed: 20f);
            m.Pitch = -10f;
            m.VerticalSpeed = -3f;
            m.Skid(0.5f);
            Assert.AreEqual(0f, m.VerticalSpeed);
            Assert.AreEqual(0f, m.Pitch);
            Assert.Less(m.Speed, 20f);
        }

        [Test]
        public void TouchdownAngle_SteeperLosesMoreSpeed()
        {
            var shallow = NewHatchling(speed: 20f);
            var steep = NewHatchling(speed: 20f);
            float sShallow = shallow.Touchdown(new Vector3(0, -0.5f, 20f), Vector3.up);
            float sSteep = steep.Touchdown(new Vector3(0, -4f, 12f), Vector3.up);

            Assert.Greater(shallow.Speed, steep.Speed, "가파르게 닿을수록 더 많이 감속");
            Assert.Less(sShallow, sSteep, "가파를수록 충격이 큼");
            Assert.Greater(shallow.Speed, 18f, "스치듯 닿으면 거의 그대로");
        }

        [Test]
        public void ImpactAngle_IsZeroWhenMovingAway()
        {
            Assert.AreEqual(0f, FlightRules.ImpactAngle(new Vector3(0, 2f, 10f), Vector3.up));
            Assert.AreEqual(90f, FlightRules.ImpactAngle(Vector3.down * 5f, Vector3.up), 1e-3f);
        }
    }
}
