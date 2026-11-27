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
        string _savePath;

        [SetUp]
        public void SetUp()
        {
            // 실제 저장 파일을 건드리지 않게 임시 경로 사용
            _savePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dragon_play_{System.Guid.NewGuid():N}.json");
            DragonRecipe.Care.SaveSystem.PathOverride = _savePath;
            DragonRecipe.Care.DragonCare.UnloadForTests();
        }

        [TearDown]
        public void TearDown()
        {
            DragonRecipe.Care.DragonCare.UnloadForTests();
            DragonRecipe.Care.SaveSystem.PathOverride = null;
            if (System.IO.File.Exists(_savePath)) System.IO.File.Delete(_savePath);
            GamePause.Set(false);
        }

        /// <summary>연타로 절벽 끝 근처까지 달린 뒤 도약 (Launch는 private이라 리플렉션)</summary>
        static IEnumerator RunToEdgeAndLaunch(FlightSession session)
        {
            float t = 0f;
            while (session.Current == FlightSession.State.Ready && t < 10f)
            {
                var run = session.dragon.RunUp;
                if (run == null || run.Remaining <= 1f) break;
                run.Tap(session.dragon.Model.Params);
                t += Time.deltaTime;
                yield return null;
            }
            if (session.Current == FlightSession.State.Ready)
                typeof(FlightSession).GetMethod("Launch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(session, null);
        }

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

            float startY = session.dragon.transform.position.y;
            yield return RunToEdgeAndLaunch(session);
            Assert.AreEqual(FlightSession.State.Flying, session.Current);

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

            // 결과 화면이 아이콘으로 그려지는지 (기본 1.2초 뒤 표시)
            var panel = Object.FindFirstObjectByType<DragonRecipe.UI.FlightResultPanel>();
            yield return new WaitForSeconds(panel.showDelay + 0.2f);
            Assert.IsTrue(panel.panel.activeSelf, "결과 화면이 안 뜸");
            Assert.Greater(panel.titleIcons.transform.childCount, 0, "결과 제목 아이콘이 없음");
            Assert.Greater(panel.bodyIcons.transform.childCount, 2, "결과 본문 아이콘이 부족함");
            var titleImages = panel.titleIcons.GetComponentsInChildren<UnityEngine.UI.Image>();
            var expected = ended.Injured ? panel.icons.boom : panel.icons.check;
            Assert.IsTrue(System.Array.Exists(titleImages, i => i.sprite == expected), "착지/추락 아이콘이 결과와 맞지 않음");

            session.ResetFlight();
            Assert.AreEqual(FlightSession.State.Ready, session.Current);
        }

        [UnityTest]
        public IEnumerator Pause_FreezesFlight_AndShowsMenu()
        {
            yield return SceneManager.LoadSceneAsync("FlightPrototype");
            yield return null;

            var session = Object.FindFirstObjectByType<FlightSession>();
            var menu = Object.FindFirstObjectByType<DragonRecipe.UI.PauseMenu>();
            Assert.IsNotNull(menu, "PauseMenu 없음");

            yield return RunToEdgeAndLaunch(session);
            yield return new WaitForSeconds(0.3f);

            GamePause.Set(true);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(menu.panel.activeSelf, "일시정지 메뉴가 안 뜸");
            Vector3 pos = session.dragon.transform.position;
            float airtime = session.Result.airtime;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(pos, session.dragon.transform.position, "일시정지 중에 드래곤이 움직임");
            Assert.AreEqual(airtime, session.Result.airtime, "일시정지 중에 체공 시간이 흐름");

            GamePause.Set(false);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(menu.panel.activeSelf);
            yield return new WaitForSeconds(0.2f);
            Assert.AreNotEqual(pos, session.dragon.transform.position, "재개 후 드래곤이 안 움직임");
        }
    }
}
