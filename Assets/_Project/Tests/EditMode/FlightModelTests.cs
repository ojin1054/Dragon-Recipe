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

        // ── 이륙 힘 모으기 ──

        [Test]
        public void ChargeLevel_FillsThenDrains_WhenHeldTooLong()
        {
            var p = new FlightParams { launchChargeTime = 1f };
            Assert.AreEqual(0f, FlightModel.ChargeLevel(0f, p), 1e-4f);
            Assert.AreEqual(0.5f, FlightModel.ChargeLevel(0.5f, p), 1e-4f);
            Assert.AreEqual(1f, FlightModel.ChargeLevel(1f, p), 1e-4f);
            Assert.AreEqual(0.7f, FlightModel.ChargeLevel(1.3f, p), 1e-4f, "가득 찬 뒤 계속 누르면 줄어들어야 함");
        }

        [Test]
        public void PerfectCharge_OnlyNearFull()
        {
            var p = new FlightParams { launchPerfectWindow = 0.1f };
            Assert.IsTrue(FlightModel.IsPerfectCharge(0.95f, p));
            Assert.IsFalse(FlightModel.IsPerfectCharge(0.8f, p));
        }

        static float LaunchAndMeasure(float charge, bool perfect, out float speed)
        {
            var m = new FlightModel(new FlightParams());
            m.Launch(new Vector3(0, 10f, 0), 0f, charge, perfect);
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
        public void ChargedLaunch_IsFasterAndHigherThanTap()
        {
            float tapY = LaunchAndMeasure(0f, false, out float tapSpeed);
            float fullY = LaunchAndMeasure(1f, false, out float fullSpeed);
            float perfectY = LaunchAndMeasure(1f, true, out float perfectSpeed);

            Assert.Greater(fullSpeed, tapSpeed);
            Assert.Greater(fullY, tapY + 3f, "힘을 모으면 확실히 더 높이 올라가야 함");
            Assert.Greater(perfectSpeed, fullSpeed);
            Assert.Greater(perfectY, fullY);
        }

        [Test]
        public void TapLaunch_MatchesOldBehaviour()
        {
            var p = new FlightParams();
            var m = new FlightModel(p);
            m.Launch(Vector3.zero, 0f, 0f, false);
            Assert.AreEqual(p.launchSpeed, m.Speed, 1e-4f);
            Assert.AreEqual(p.launchRise, m.VerticalSpeed, 1e-4f);
            Assert.AreEqual(0f, m.Pitch, 1e-4f);
        }
    }
}
