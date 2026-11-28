using System.Collections;
using DragonRecipe.CameraRig;
using DragonRecipe.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DragonRecipe.Tests
{
    /// <summary>빠르게 날수록 커지는 속도감 연출</summary>
    public class SpeedJuiceTests
    {
        string _savePath;

        [SetUp]
        public void SetUp()
        {
            _savePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dragon_juice_{System.Guid.NewGuid():N}.json");
            DragonRecipe.Care.SaveSystem.PathOverride = _savePath;
            DragonRecipe.Care.DragonCare.UnloadForTests();
        }

        [TearDown]
        public void TearDown()
        {
            GamePause.Set(false);
            DragonRecipe.Care.DragonCare.UnloadForTests();
            DragonRecipe.Care.SaveSystem.PathOverride = null;
            if (System.IO.File.Exists(_savePath)) System.IO.File.Delete(_savePath);
        }

        [UnityTest]
        public IEnumerator FastFlight_AddsStreaksShakeContrails_AndTierPunch()
        {
            yield return SceneManager.LoadSceneAsync("FlightPrototype");
            yield return null;

            var session = Object.FindFirstObjectByType<FlightSession>();
            var follow = Object.FindFirstObjectByType<FollowCamera>();
            var juice = follow.GetComponent<SpeedJuice>();
            Assert.IsNotNull(juice, "카메라에 속도 연출이 붙지 않음");
            Assert.IsNotNull(juice.Streaks, "바람 줄기가 없음");
            Assert.AreEqual(0f, juice.Intensity, "이륙 전에는 연출 없음");

            typeof(FlightSession).GetMethod("Launch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(session, null);
            yield return null;
            var model = session.dragon.Model;
            var cam = follow.GetComponent<Camera>();

            // 느릴 때: 연출 없음
            for (int i = 0; i < 10; i++)
            {
                model.Position += Vector3.up * 5f;
                model.Speed = model.Params.maxSpeed * 0.3f;
                model.VerticalSpeed = 0f;
                yield return null;
            }
            Assert.AreEqual(0f, juice.Intensity, 0.01f, "느린데 속도 연출이 나옴");
            Assert.AreEqual(0, juice.CurrentTier);
            float slowFov = cam.fieldOfView;

            // 최고 속도로: 단계마다 '펑', 줄기·떨림·비행운
            int hitsBefore = juice.TierHits;
            float maxPunch = 0f, maxShake = 0f;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                model.Position += Vector3.up * 2f; // 땅에 닿지 않게
                model.Speed = model.Params.maxSpeed;
                model.VerticalSpeed = 0f;
                maxPunch = Mathf.Max(maxPunch, follow.ExtraFov);
                maxShake = Mathf.Max(maxShake, follow.ShakeOffset.magnitude);
                yield return null;
            }
            Assert.Greater(juice.Intensity, 0.9f, "최고 속도인데 연출이 약함");
            Assert.AreEqual(juice.tierRatios.Length, juice.CurrentTier, "모든 속도 단계를 넘어야 함");
            Assert.AreEqual(hitsBefore + juice.tierRatios.Length, juice.TierHits, "단계마다 한 번씩만 '펑'");
            Assert.Greater(maxPunch, 1f, "단계를 넘는 순간 시야각 펀치가 없음");
            Assert.Greater(maxShake, 0.01f, "빠른데 카메라가 떨리지 않음");
            Assert.Greater(juice.Streaks.emission.rateOverTime.constant, 10f, "빠른데 바람 줄기가 적음");
            Assert.Greater(juice.Streaks.particleCount, 0, "바람 줄기가 안 보임");
            Assert.IsNotNull(juice.LeftContrail, "날개 끝 비행운이 없음");
            Assert.IsTrue(juice.LeftContrail.emitting && juice.RightContrail.emitting, "빠른데 비행운이 안 나옴");
            Assert.Greater(cam.fieldOfView, slowFov, "빠를수록 시야각이 넓어져야 함");

            // 같은 속도를 유지하면 다시 '펑'하지 않는다
            int hits = juice.TierHits;
            for (int i = 0; i < 10; i++)
            {
                model.Position += Vector3.up * 2f;
                model.Speed = model.Params.maxSpeed;
                yield return null;
            }
            Assert.AreEqual(hits, juice.TierHits);

            // 느려지면 풀리고, 단계도 다시 열린다
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                model.Position += Vector3.up * 2f;
                model.Speed = model.Params.maxSpeed * 0.3f;
                model.VerticalSpeed = 0f;
                yield return null;
            }
            Assert.Less(juice.Intensity, 0.05f, "느려졌는데 연출이 남아 있음");
            Assert.AreEqual(0, juice.CurrentTier);
            Assert.IsFalse(juice.LeftContrail.emitting, "느려졌는데 비행운이 계속 나옴");
        }
    }
}
