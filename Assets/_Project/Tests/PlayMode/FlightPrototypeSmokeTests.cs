using System.Collections;
using System.Linq;
using DragonRecipe.Core;
using DragonRecipe.Food;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DragonRecipe.Tests
{
    /// <summary>실제 씬을 띄워 이륙 → (입력 없이) 착지/추락 → 결과까지 한 바퀴 도는지 확인.</summary>
    public class FlightPrototypeSmokeTests
    {
        [UnityTest]
        public IEnumerator Scene_FullFlightLoop_EndsWithResult()
        {
            yield return SceneManager.LoadSceneAsync("FlightPrototype");
            yield return null;
            yield return null;

            var session = Object.FindFirstObjectByType<FlightSession>();
            Assert.IsNotNull(session, "FlightSession 없음");
            Assert.AreEqual(FlightSession.State.Ready, session.Current);

            var spawner = Object.FindFirstObjectByType<PreySpawner>();
            Assert.Greater(spawner.All.Count(p => p.IsAvailable), 10, "먹이가 충분히 스폰되지 않음");

            FlightResult ended = null;
            GameEvents.FlightEnded += r => ended = r;

            // 이륙 (Launch는 private이므로 SendMessage 대신 리플렉션)
            typeof(FlightSession).GetMethod("Launch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(session, null);
            Assert.AreEqual(FlightSession.State.Flying, session.Current);
            float startY = session.dragon.transform.position.y;

            float t = 0f;
            while (ended == null && t < 40f)
            {
                t += Time.deltaTime;
                yield return null;
            }

            Assert.IsNotNull(ended, "40초 안에 비행이 끝나지 않음");
            Assert.AreEqual(FlightSession.State.Ended, session.Current);
            Assert.Greater(ended.airtime, 1f);
            Assert.Less(session.dragon.transform.position.y, startY);
            Debug.Log($"[Smoke] 결과={ended.reason}, 체공={ended.airtime:0.0}s, 최고속도={ended.maxSpeed:0.0}m/s, 먹이={ended.caught.Count}");

            session.ResetFlight();
            Assert.AreEqual(FlightSession.State.Ready, session.Current);
        }
    }
}
