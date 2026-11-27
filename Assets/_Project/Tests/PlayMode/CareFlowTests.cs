using System;
using System.Collections;
using System.IO;
using DragonRecipe.Care;
using DragonRecipe.Core;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DragonRecipe.Tests
{
    /// <summary>둥지 ↔ 비행 씬과 허기·성장 상태가 실제로 연결되는지 확인.</summary>
    public class CareFlowTests
    {
        string _savePath;

        [SetUp]
        public void SetUp()
        {
            _savePath = Path.Combine(Path.GetTempPath(), $"dragon_care_{Guid.NewGuid():N}.json");
            SaveSystem.PathOverride = _savePath;
            DragonCare.UnloadForTests();
        }

        [TearDown]
        public void TearDown()
        {
            DragonCare.UnloadForTests();
            SaveSystem.PathOverride = null;
            if (File.Exists(_savePath)) File.Delete(_savePath);
            GamePause.Set(false);
        }

        [UnityTest]
        public IEnumerator NestScene_LoadsState_AndShowsUI()
        {
            yield return SceneManager.LoadSceneAsync("Nest");
            yield return null;

            Assert.IsTrue(DragonCare.IsLoaded, "둥지에서 드래곤 상태를 못 불러옴");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<DragonRecipe.UI.NestUI>());
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<NestDragonView>());
            Assert.AreEqual(DragonCare.Config.care.startingHunger, DragonCare.State.hunger, 0.1f);
            Assert.IsTrue(File.Exists(_savePath), "처음 불러올 때 저장 파일이 만들어져야 함");

            var ui = UnityEngine.Object.FindFirstObjectByType<DragonRecipe.UI.NestUI>();
            Assert.IsNotNull(ui.icons, "아이콘 세트가 연결되지 않음");
            Assert.IsNotNull(ui.icons.faceHappy, "아이콘 스프라이트가 비어 있음");
            Assert.Greater(ui.statusIcons.transform.childCount, 0, "상태 아이콘이 그려지지 않음");
            var faces = ui.statusIcons.GetComponentsInChildren<UnityEngine.UI.Image>();
            Assert.IsTrue(System.Array.Exists(faces, i => i.sprite == ui.icons.faceHappy), "포만도 50%인데 기분 좋음 얼굴이 없음");
        }

        [UnityTest]
        public IEnumerator StarvingDragon_FliesWeaker()
        {
            var starving = new DragonState { hunger = 0f, lastTickUtc = DateTime.UtcNow.Ticks };
            SaveSystem.Save(starving);

            yield return SceneManager.LoadSceneAsync("FlightPrototype");
            yield return null;

            var dragon = UnityEngine.Object.FindFirstObjectByType<DragonFlightController>();
            var stageDefault = DragonCare.CurrentStageData.flight.maxStamina;
            Assert.Less(dragon.Model.Params.maxStamina, stageDefault, "굶주렸는데 스태미나가 그대로");
        }

        [UnityTest]
        public IEnumerator FlightResult_FeedsDragon_AndSaves()
        {
            yield return SceneManager.LoadSceneAsync("FlightPrototype");
            yield return null;

            float before = DragonCare.State.hunger;
            var food = ScriptableObject.CreateInstance<FoodData>();
            food.trait = FoodTrait.Fish;
            var result = new FlightResult { reason = FlightEndReason.Crashed };
            result.caught.Add(new CaughtFood { food = food, satiety = 15f, eaten = true }); // 날면서 먹음

            GameEvents.RaiseFlightEnded(result);

            Assert.AreEqual(before + 15f, DragonCare.State.hunger, 0.5f);
            Assert.IsTrue(DragonCare.State.injured);
            Assert.AreEqual(1, DragonCare.State.totalFlights);
            Assert.IsTrue(SaveSystem.TryLoad(out var saved));
            Assert.AreEqual(15f, saved.GetTrait(FoodTrait.Fish), 0.01f);
            UnityEngine.Object.Destroy(food);
        }

        [UnityTest]
        public IEnumerator CarriedFood_GoesToStorage_CrashDropsSome()
        {
            yield return SceneManager.LoadSceneAsync("FlightPrototype");
            yield return null;

            var food = DragonCare.Config.foods[0];
            float before = DragonCare.State.hunger;

            // 착지: 들고 온 2개는 창고로
            var landed = new FlightResult { reason = FlightEndReason.Landed };
            landed.caught.Add(new CaughtFood { food = food, satiety = 10f });
            landed.caught.Add(new CaughtFood { food = food, satiety = 10f, rare = true });
            GameEvents.RaiseFlightEnded(landed);
            Assert.AreEqual(before, DragonCare.State.hunger, 0.5f, "들고 온 먹이는 바로 먹지 않음");
            Assert.AreEqual(2, DragonCare.State.storage.Count);
            Assert.AreEqual(2, DragonCare.LastFlightReport.stored.Count);
            Assert.AreEqual(2, DragonCare.State.Codex(food.id).caught);
            Assert.AreEqual(1, DragonCare.State.Codex(food.id).rareCaught);

            // 추락: 떨어뜨린 건 잃고 나머지만 창고로
            var crashed = new FlightResult { reason = FlightEndReason.Crashed };
            crashed.caught.Add(new CaughtFood { food = food, satiety = 10f });
            crashed.caught.Add(new CaughtFood { food = food, satiety = 10f, dropped = true });
            GameEvents.RaiseFlightEnded(crashed);
            Assert.AreEqual(3, DragonCare.State.storage.Count);
            Assert.AreEqual(1, DragonCare.LastFlightReport.dropped.Count);
            Assert.IsTrue(DragonCare.State.injured);
        }

        [UnityTest]
        public IEnumerator Nest_StorageButtons_FeedDragon()
        {
            var s = new DragonState { hunger = 40f, lastTickUtc = DateTime.UtcNow.Ticks };
            s.storage.Add(new StoredFood { id = "sheep", satiety = 12f, trait = FoodTrait.Meat });
            s.storage.Add(new StoredFood { id = "sheep", satiety = 12f, trait = FoodTrait.Meat });
            SaveSystem.Save(s);

            yield return SceneManager.LoadSceneAsync("Nest");
            yield return null;
            yield return null;

            var panel = UnityEngine.Object.FindFirstObjectByType<DragonRecipe.UI.StoragePanel>();
            Assert.IsNotNull(panel, "창고 패널 없음");
            var buttons = panel.grid.GetComponentsInChildren<UnityEngine.UI.Button>();
            Assert.AreEqual(1, buttons.Length, "같은 먹이는 버튼 하나로 묶여야 함");
            buttons[0].onClick.Invoke();
            Assert.AreEqual(52f, DragonCare.State.hunger, 0.5f);
            Assert.AreEqual(1, DragonCare.State.storage.Count);
            Assert.Greater(DragonCare.State.GetTrait(FoodTrait.Meat), 0f);

            var codex = UnityEngine.Object.FindFirstObjectByType<DragonRecipe.UI.CodexPanel>();
            Assert.IsNotNull(codex, "도감 없음");
            codex.Open();
            Assert.IsTrue(codex.IsOpen);
            Assert.Greater(codex.foodFlow.transform.childCount, 0, "도감 먹이 줄이 없음");
        }
    }
}
