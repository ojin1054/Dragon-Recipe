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
        public IEnumerator Snatching_CarriesFood_ComboAndEatInAir()
        {
            yield return SceneManager.LoadSceneAsync("FlightPrototype");
            yield return null;

            var session = Object.FindFirstObjectByType<FlightSession>();
            var spawner = Object.FindFirstObjectByType<PreySpawner>();
            typeof(FlightSession).GetMethod("Launch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(session, null);
            yield return null;

            var snatch = typeof(FlightSession).GetMethod("OnSnatched", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var light = spawner.All.Where(p => p.IsAvailable && !p.Data.struggles).Take(2).ToArray();
            Assert.AreEqual(2, light.Length);
            snatch.Invoke(session, new object[] { light[0] });
            session.dragon.Model.Stamina = 10f;
            float speedBefore = session.dragon.Model.Speed;
            snatch.Invoke(session, new object[] { light[1] });
            Assert.AreEqual(10f + session.hunt.comboStaminaPerStep, session.dragon.Model.Stamina, 0.5f, "연속 사냥이면 스태미나가 참");
            Assert.IsTrue(session.dragon.Model.IsBoosting, "연속 사냥이면 잠깐 속도가 붙음");
            Assert.Greater(session.dragon.Model.Speed, speedBefore);

            Assert.AreEqual(2, session.Result.CarriedCount);
            Assert.AreEqual(2, session.CurrentCombo, "연달아 잡으면 연속 사냥");
            Assert.AreEqual(2, session.Result.caught[1].combo);
            Assert.AreEqual(2, session.Result.maxCombo);
            Assert.Greater(session.dragon.Model.Payload, 0f, "매단 먹이 무게가 비행에 반영돼야 함");
            var carry = Object.FindFirstObjectByType<CarryVisual>();
            Assert.IsNotNull(carry);
            var stack = session.dragon.transform.Find("Model/CarriedFood");
            Assert.IsNotNull(stack, "드래곤 등 위에 먹이 탑이 없음");
            Assert.AreEqual(2, stack.childCount, "등 위에 쌓인 먹이가 안 보임");
            Assert.Greater(stack.GetChild(1).localPosition.y, stack.GetChild(0).localPosition.y, "먹이가 위로 쌓이지 않음");

            float stamina = session.dragon.Model.Stamina;
            float payload = session.dragon.Model.Payload;
            Assert.IsTrue(session.EatInAir());
            Assert.IsTrue(session.dragon.IsEating);
            Assert.AreEqual(1, session.Result.CarriedCount);
            Assert.Less(session.dragon.Model.Payload, payload);
            Assert.GreaterOrEqual(session.dragon.Model.Stamina, stamina);
            Assert.IsFalse(session.EatInAir(), "먹는 중에는 또 먹지 않음");
        }

        [UnityTest]
        public IEnumerator TooManyAnimals_Refused_AndOverloadShakes()
        {
            yield return SceneManager.LoadSceneAsync("FlightPrototype");
            yield return null;

            var session = Object.FindFirstObjectByType<FlightSession>();
            var spawner = Object.FindFirstObjectByType<PreySpawner>();
            typeof(FlightSession).GetMethod("Launch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(session, null);
            yield return null;

            int refused = 0;
            session.SnatchRefused += _ => refused++;
            var snatch = typeof(FlightSession).GetMethod("OnSnatched", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var prey in spawner.All.Where(p => p.IsAvailable).ToArray())
                snatch.Invoke(session, new object[] { prey });

            float strength = session.DragonStrength;
            Assert.Greater(strength, 0f);
            Assert.Greater(refused, 0, "힘이 모자라면 더 낚지 못해야 함");
            Assert.LessOrEqual(session.CarriedStrength, strength * session.hunt.maxOverload + 0.01f, "한계를 넘게 매달면 안 됨");
            Assert.Less(session.Result.CarriedCount, spawner.All.Count());

            yield return null;
            var hud = Object.FindFirstObjectByType<DragonRecipe.UI.CarryHUD>();
            Assert.IsNotNull(hud.loadGauge, "힘 게이지가 없음");
            Assert.IsTrue(hud.loadGauge.gameObject.activeInHierarchy, "매단 동물이 있으면 힘 게이지가 보여야 함");

            // 힘이 넘치면 비행이 흔들린다
            if (session.OverloadAmount > 0f)
            {
                int shakes = 0;
                session.Overloaded += _ => shakes++;
                // 흔들림을 보기 전에 땅에 닿지 않게 높이 띄워 둔다
                session.dragon.Model.Position += Vector3.up * 150f;
                for (float t = 0; t < 2f && shakes == 0; t += Time.deltaTime) yield return null;
                Assert.Greater(shakes, 0, "힘이 넘치는데 흔들리지 않음");
            }
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
