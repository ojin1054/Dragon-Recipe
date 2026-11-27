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
            result.caught.Add(new CaughtFood { food = food, satiety = 15f });

            GameEvents.RaiseFlightEnded(result);

            Assert.AreEqual(before + 15f, DragonCare.State.hunger, 0.5f);
            Assert.IsTrue(DragonCare.State.injured);
            Assert.AreEqual(1, DragonCare.State.totalFlights);
            Assert.IsTrue(SaveSystem.TryLoad(out var saved));
            Assert.AreEqual(15f, saved.GetTrait(FoodTrait.Fish), 0.01f);
            UnityEngine.Object.Destroy(food);
        }
    }
}
