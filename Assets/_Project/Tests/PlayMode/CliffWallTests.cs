using System.Collections;
using DragonRecipe.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DragonRecipe.Tests
{
    /// <summary>비행 맵 출발 지점: 쭉 이어진 절벽 벽 + 동굴 둥지 + 발판</summary>
    public class CliffWallTests
    {
        string _savePath;

        [SetUp]
        public void SetUp()
        {
            _savePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dragon_wall_{System.Guid.NewGuid():N}.json");
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
        public IEnumerator Nest_IsInCave_OnOneLongWall()
        {
            yield return SceneManager.LoadSceneAsync("FlightPrototype");
            yield return null;
            yield return new WaitForFixedUpdate();

            var world = GameObject.Find("World").transform;
            Assert.IsNotNull(world.Find(CliffWall.RootName), "절벽 벽이 없음");
            Assert.IsNull(world.Find("Cliff"), "따로 솟은 예전 절벽이 남아 있음");

            var session = Object.FindFirstObjectByType<FlightSession>();
            Vector3 nest = session.nest.position;
            float edgeZ = session.launchEdge.position.z;
            float wallZ = CliffWall.WallZ(edgeZ);
            Assert.Less(nest.z, wallZ, "둥지는 벽 안쪽(동굴)에 있어야 함");

            // 벽은 좌우·위아래로 쭉 이어진다
            foreach (float x in new[] { -450f, -200f, -40f, 40f, 200f, 450f })
            {
                foreach (float y in new[] { 5f, 60f, 130f })
                {
                    Assert.IsTrue(Physics.Raycast(new Vector3(x, y, 80f), Vector3.back, out var hit, 300f, ~0, QueryTriggerInteraction.Ignore),
                        $"x={x}, y={y}에 벽이 없음");
                    Assert.AreEqual(wallZ, hit.point.z, 0.6f, $"x={x}, y={y}의 벽면이 한 줄로 이어지지 않음");
                }
            }

            // 동굴: 입구로 쏜 레이는 벽면을 지나 둥지 뒤까지 들어간다
            Assert.IsTrue(Physics.Raycast(new Vector3(nest.x, nest.y + 3f, 80f), Vector3.back, out var deep, 300f, ~0, QueryTriggerInteraction.Ignore));
            Assert.Less(deep.point.z, nest.z - 5f, "동굴이 둥지 뒤까지 파여 있지 않음");

            // 둥지 위는 바위 천장, 아래는 바닥
            Assert.IsTrue(Physics.Raycast(nest + Vector3.up * 0.5f, Vector3.up, out var roof, 40f, ~0, QueryTriggerInteraction.Ignore), "동굴 천장이 없음");
            Assert.Greater(roof.distance, 5f, "천장이 너무 낮음");
            Assert.IsTrue(Physics.Raycast(nest + Vector3.up * 0.5f, Vector3.down, out var floor, 5f, ~0, QueryTriggerInteraction.Ignore), "동굴 바닥이 없음");

            // 발판: 벽 밖으로 튀어나와 있고, 끝을 지나면 낭떠러지
            Assert.IsTrue(Physics.Raycast(new Vector3(nest.x, nest.y + 2f, edgeZ - 1f), Vector3.down, out var ledge, 5f, ~0, QueryTriggerInteraction.Ignore), "발판이 없음");
            Assert.AreEqual(floor.point.y, ledge.point.y, 0.2f, "동굴 바닥과 발판 높이가 다름");
            Assert.IsTrue(Physics.Raycast(new Vector3(nest.x, nest.y + 2f, edgeZ + 2f), Vector3.down, out var drop, 100f, ~0, QueryTriggerInteraction.Ignore));
            Assert.Less(drop.point.y, 1f, "발판 끝 너머는 땅까지 낭떠러지여야 함");

            // 벽 속에 묻힌 나무·바위는 없다
            var obstacles = world.Find("Obstacles");
            if (obstacles)
                foreach (Transform o in obstacles)
                    if (o.gameObject.activeSelf) Assert.GreaterOrEqual(o.position.z, edgeZ, $"{o.name}이(가) 벽 속에 있음");
        }
    }
}
