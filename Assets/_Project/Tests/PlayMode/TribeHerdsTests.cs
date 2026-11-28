using System.Collections;
using System.Linq;
using DragonRecipe.Care;
using DragonRecipe.Core;
using DragonRecipe.Food;
using DragonRecipe.Tribes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DragonRecipe.Tests
{
    /// <summary>종족이 기르는 가축(소 등)과 목동: 낚아채면 그 종족 호감도가 떨어진다</summary>
    public class TribeHerdsTests
    {
        string _savePath;

        [SetUp]
        public void SetUp()
        {
            _savePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dragon_herds_{System.Guid.NewGuid():N}.json");
            SaveSystem.PathOverride = _savePath;
            DragonCare.UnloadForTests();
        }

        [TearDown]
        public void TearDown()
        {
            GamePause.Set(false);
            DragonCare.UnloadForTests();
            SaveSystem.PathOverride = null;
            if (System.IO.File.Exists(_savePath)) System.IO.File.Delete(_savePath);
        }

        static IEnumerator LoadAndWaitForHerds()
        {
            yield return SceneManager.LoadSceneAsync("FlightPrototype");
            for (float t = 0f; t < 5f && !(TribeHerds.Instance && TribeHerds.Instance.IsBuilt); t += Time.deltaTime)
                yield return null;
        }

        [UnityTest]
        public IEnumerator EachTribe_HasHerders_AndMarkedLivestock()
        {
            yield return LoadAndWaitForHerds();
            var herds = TribeHerds.Instance;
            Assert.IsNotNull(herds, "가축 무리가 붙지 않음");
            Assert.IsTrue(herds.IsBuilt, "가축 무리가 세워지지 않음");

            foreach (var tribe in TribeLogic.All)
                Assert.GreaterOrEqual(herds.Herds.Count(h => h.def.tribe == tribe), 2, $"{tribe}: 목동이 둘은 있어야 함");
            Assert.IsTrue(herds.Herds.Any(h => h.def.tribe == Tribe.Human && h.def.foodId == "sheep"), "양치기가 없음");
            Assert.IsTrue(herds.Herds.Any(h => h.def.tribe == Tribe.Human && h.def.foodId == "cow"), "소몰이가 없음");

            foreach (var h in herds.Herds)
            {
                Assert.IsNotNull(h.herder, $"{h.def.tribe} {h.def.foodId}: 목동이 없음");
                Assert.AreEqual(h.def.count, h.animals.Count, $"{h.def.foodId}: 가축 수가 다름");
                foreach (var a in h.animals)
                {
                    Assert.AreEqual(h.def.foodId, a.Data.id);
                    Assert.AreEqual(h.def.tribe, a.Owner, "가축 주인이 다름");
                    var flat = a.transform.position - h.center;
                    flat.y = 0f;
                    Assert.LessOrEqual(flat.magnitude, herds.herdRadius + 1f, "가축이 무리에서 떨어져 놓임");
                    var mark = a.transform.Find("OwnerMark");
                    Assert.IsTrue(mark && mark.gameObject.activeSelf, "가축에 종족 색 띠가 없음");
                }
            }

            // 울타리에 묶여 있지 않고 종족 땅 안을 옮겨 다닌다
            foreach (var h in herds.Herds)
            {
                var d = h.center - h.regionCenter;
                d.y = 0f;
                Assert.LessOrEqual(d.magnitude, h.regionRadius, $"{h.def.tribe} {h.def.foodId}: 무리가 종족 땅 밖에 있음");
                Assert.AreEqual(DragonCare.TribeSettings.territoryRadius, h.regionRadius, 0.01f, "다니는 땅 = 종족 땅");
            }
            var roamer = herds.Herds.First(h => h.def.foodId == "sheep");
            Vector3 from = roamer.center;
            herds.RoamNow(roamer);
            yield return null;
            Assert.Greater(Vector3.Distance(from, roamer.roamTarget), 1f, "새로 갈 자리를 정하지 않음");
            yield return new WaitForSeconds(2f);
            Assert.Greater(Vector3.Distance(from, roamer.center), 0.5f, "무리가 옮겨 가지 않음");
            var off = roamer.center - roamer.regionCenter;
            off.y = 0f;
            Assert.LessOrEqual(off.magnitude, roamer.regionRadius, "옮겨 간 자리가 종족 땅 밖");
            foreach (var a in roamer.animals)
                Assert.Less(Vector3.Distance(a.Home, roamer.center), 0.01f, "가축이 무리 자리를 따라가지 않음");
            Assert.IsNull(GameObject.Find("Post"), "울타리 말뚝이 남아 있음");

            // 가축 전용 동물(소·들소·달사슴)은 야생으로 나오지 않는다
            var spawner = Object.FindFirstObjectByType<PreySpawner>();
            foreach (var p in spawner.All.Where(p => !p.Owner.HasValue))
                Assert.Greater(p.Data.spawnWeight, 0f, $"{p.Data.id}이(가) 야생으로 나옴");
            Assert.IsTrue(spawner.All.Any(p => p.Data.id == "cow"), "소가 없음");
        }

        [UnityTest]
        public IEnumerator SnatchingLivestock_LowersOwnersReputation_WildDoesNot()
        {
            yield return LoadAndWaitForHerds();
            var herds = TribeHerds.Instance;
            Assert.IsTrue(herds && herds.IsBuilt);
            Assert.IsTrue(DragonCare.IsLoaded);

            var session = Object.FindFirstObjectByType<FlightSession>();
            var spawner = Object.FindFirstObjectByType<PreySpawner>();
            typeof(FlightSession).GetMethod("Launch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(session, null);
            yield return null;
            session.dragon.Model.Position += Vector3.up * 150f; // 바닥·마을에서 멀리
            var snatch = typeof(FlightSession).GetMethod("OnSnatched", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var st = DragonCare.Tribes;
            float human = TribeLogic.Reputation(st, Tribe.Human);
            float orc = TribeLogic.Reputation(st, Tribe.Orc);
            float elf = TribeLogic.Reputation(st, Tribe.Elf);

            // 야생 동물: 아무 종족도 신경 쓰지 않는다
            var wild = spawner.All.First(p => p.IsAvailable && !p.Owner.HasValue && !p.Data.struggles && p.Data.strength <= 6f);
            snatch.Invoke(session, new object[] { wild });
            Assert.AreEqual(human, TribeLogic.Reputation(st, Tribe.Human), 0.001f);
            Assert.AreEqual(orc, TribeLogic.Reputation(st, Tribe.Orc), 0.001f);
            Assert.AreEqual(elf, TribeLogic.Reputation(st, Tribe.Elf), 0.001f);
            Assert.AreEqual(0, herds.Stolen);

            // 사람의 양을 낚아챔: 사람만 호감도가 떨어지고 양치기가 화낸다
            var sheepHerd = herds.Herds.First(h => h.def.tribe == Tribe.Human && h.def.foodId == "sheep");
            var sheep = sheepHerd.animals.First(a => a.IsAvailable);
            int carried = session.Result.CarriedCount;
            snatch.Invoke(session, new object[] { sheep });
            Assert.AreEqual(carried + 1, session.Result.CarriedCount, "가축도 먹이로 낚아챌 수 있어야 함");
            Assert.AreEqual(human - sheepHerd.def.reputationLoss, TribeLogic.Reputation(st, Tribe.Human), 0.001f, "사람 호감도가 떨어지지 않음");
            Assert.AreEqual(orc, TribeLogic.Reputation(st, Tribe.Orc), 0.001f, "남의 가축인데 오크 호감도가 바뀜");
            Assert.AreEqual(elf, TribeLogic.Reputation(st, Tribe.Elf), 0.001f);
            Assert.IsTrue(herds.IsAngry(sheepHerd), "양치기가 화내지 않음");
            Assert.AreEqual(1, herds.Stolen);

            // 다른 종족도 마찬가지
            foreach (var tribe in new[] { Tribe.Orc, Tribe.Elf })
            {
                var herd = herds.Herds.First(h => h.def.tribe == tribe);
                float before = TribeLogic.Reputation(st, tribe);
                session.EatInAir(); // 무게를 덜어 다음 가축을 들 수 있게
                yield return new WaitForSeconds(1.2f);
                session.dragon.Model.Position += Vector3.up * 60f;
                int had = session.Result.CarriedCount;
                var animal = herd.animals.First(a => a.IsAvailable);
                snatch.Invoke(session, new object[] { animal });
                if (session.Result.CarriedCount == had) continue; // 힘이 모자라 못 든 경우는 도둑질이 아님
                Assert.AreEqual(before - herd.def.reputationLoss, TribeLogic.Reputation(st, tribe), 0.001f, $"{tribe} 호감도가 떨어지지 않음");
                Assert.IsTrue(herds.IsAngry(herd));
            }

            // 다시 날면 가축이 목장으로 돌아온다
            session.ResetFlight();
            yield return null;
            foreach (var a in sheepHerd.animals)
            {
                Assert.IsTrue(a.IsAvailable, "가축이 다시 나오지 않음");
                var flat = a.transform.position - sheepHerd.center;
                flat.y = 0f;
                Assert.LessOrEqual(flat.magnitude, herds.herdRadius + 1f);
            }
            Assert.AreEqual(0, herds.Stolen);
        }
    }
}
