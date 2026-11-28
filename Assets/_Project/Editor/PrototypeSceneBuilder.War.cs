using System.Collections.Generic;
using System.Linq;
using DragonRecipe.CameraRig;
using DragonRecipe.Care;
using DragonRecipe.Core;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using DragonRecipe.Tribes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DragonRecipe.EditorTools
{
    /// <summary>
    /// 전쟁 맵 씬 (참전 중 비행 전용 전장).
    /// - 남쪽: 편든 종족 진영 절벽. 둥지와 같은 도움닫기 길에서 달려 나가 이륙한다. 절벽 아래가 지켜야 할 진영.
    /// - 북쪽 끝: 적 진영과 능선. 적 무리가 여기서 출발해 골짜기를 가로질러 몰려온다.
    /// - 양옆은 높은 바위 능선으로 막힌 골짜기, 가운데는 짓밟힌 흙길과 엄폐용 바위.
    /// 진영 깃발 색과 두 진영 배치는 실행 중에 TribeWorld가 실제 편든 종족에 맞춘다.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string WarScenePath = Root + "/Scenes/WarBattle.unity";
        static readonly Vector3 WarAllyCamp = new Vector3(0f, 0f, 45f);
        static readonly Vector3 WarEnemyCamp = new Vector3(0f, 0f, 320f);

        [MenuItem("Dragon Recipe/Build War Scene")]
        public static void BuildWarOnly()
        {
            PrepareAndBuildWar();
            Debug.Log($"[Dragon Recipe] 전쟁 맵 생성 완료: {WarScenePath}");
        }

        /// <summary>
        /// 전쟁 맵 씬 파일이 없으면 에디터가 스크립트를 불러온 뒤 한 번 만든다.
        /// 지금 열려 있는 씬은 그대로 두고, 따로 열어서 만든 뒤 저장하고 닫는다.
        /// </summary>
        [InitializeOnLoadMethod]
        static void AutoBuildWarSceneIfMissing()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WarScenePath)) return;
                if (!AssetDatabase.LoadAssetAtPath<GameConfig>($"{DataDir}/GameConfig.asset")) return; // 아직 Build All Scenes 전
                PrepareAndBuildWar();
                Debug.Log($"[Dragon Recipe] 전쟁 맵이 없어 자동으로 만들었습니다: {WarScenePath}");
            };
        }

        /// <summary>데이터를 불러와 전쟁 맵을 (열린 씬을 건드리지 않고) 만들고, 빌드 목록에 붙인다</summary>
        static void PrepareAndBuildWar()
        {
            var stages = CreateStages();
            var foods = CreateFoods();
            var config = CreateGameConfig(stages, foods);
            _icons = CreateIconSet();

            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            BuildWarScene(scene, stages, config.foods != null && config.foods.Length > 0 ? config.foods : foods, config, new Mats());
            if (previous.IsValid() && previous.isLoaded)
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }

            // 빌드 목록 맨 뒤에 붙인다 (이미 있으면 그대로)
            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(s => s.path == WarScenePath))
            {
                list.Add(new EditorBuildSettingsScene(WarScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
            AssetDatabase.SaveAssets();
        }

        static void BuildWarScene(DragonStageData[] stages, FoodData[] foods, GameConfig config, Mats mats) =>
            BuildWarScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single), stages, foods, config, mats);

        static void BuildWarScene(UnityEngine.SceneManagement.Scene scene, DragonStageData[] stages, FoodData[] foods, GameConfig config, Mats mats)
        {

            BuildLighting();
            // 해 질 녘 전장: 낮게 깔린 주황빛
            if (RenderSettings.sun)
            {
                RenderSettings.sun.color = new Color(1f, 0.8f, 0.62f);
                RenderSettings.sun.intensity = 1.05f;
                RenderSettings.sun.transform.rotation = Quaternion.Euler(30f, -25f, 0f);
            }
            RenderSettings.ambientSkyColor = new Color(0.75f, 0.68f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.52f, 0.45f);
            RenderSettings.fogColor = new Color(0.86f, 0.72f, 0.62f);
            RenderSettings.fogStartDistance = 120f;
            RenderSettings.fogEndDistance = 520f;

            var warMap = BuildWarWorld(mats, stages[0].flight);

            var nest = new GameObject("Nest").transform;
            nest.position = new Vector3(0, CliffHeight + 1.2f, CliffEdgeZ - RunUpLength);
            var edge = new GameObject("LaunchEdge").transform;
            edge.position = new Vector3(0, CliffHeight + 1.2f, CliffEdgeZ);

            var dragon = BuildDragon(stages[0], mats, out var input, out var snatch);
            dragon.transform.position = nest.position;

            // 먹이는 적게 (스태미나 보충용), 골짜기 안에만
            var spawner = new GameObject("PreySpawner").AddComponent<PreySpawner>();
            spawner.foods = foods;
            spawner.preyMaterial = mats.Prey;
            spawner.threat = dragon.transform;
            spawner.count = 14;
            spawner.areaCenter = new Vector3(0f, 0f, 185f);
            spawner.areaSize = new Vector2(240f, 330f);
            spawner.keepOutRadius = 30f;

            var sessionGo = new GameObject("FlightSession");
            var session = sessionGo.AddComponent<FlightSession>();
            session.dragon = dragon;
            session.input = input;
            session.snatchZone = snatch;
            session.spawner = spawner;
            session.nest = nest;
            session.launchEdge = edge;

            var carry = sessionGo.AddComponent<CarryVisual>();
            carry.session = session;
            carry.anchor = snatch.transform;
            carry.material = mats.Prey;

            var bridge = sessionGo.AddComponent<FlightCareBridge>();
            bridge.config = config;
            bridge.dragon = dragon;
            bridge.session = session;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.farClipPlane = 1500f;
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<FollowCamera>();
            follow.target = dragon;
            camGo.transform.position = nest.position + new Vector3(0, 3.5f, -9f);
            camGo.transform.LookAt(nest.position + Vector3.forward * 6f);

            BuildUI(session, dragon, input);

            EditorSceneManager.SaveScene(scene, WarScenePath);
        }

        static WarMap BuildWarWorld(Mats mats, FlightParams launch)
        {
            var world = new GameObject("World").transform;
            var warMap = world.gameObject.AddComponent<WarMap>();
            warMap.allyCamp = WarAllyCamp;
            warMap.enemyCamp = WarEnemyCamp;
            var allyBanners = new List<Renderer>();
            var enemyBanners = new List<Renderer>();

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(world);
            ground.transform.localScale = new Vector3(100, 1, 100);
            ground.GetComponent<Renderer>().sharedMaterial = mats.Grass;

            // 두 진영을 잇는 짓밟힌 흙길 (콜라이더 없음)
            float roadStart = WarAllyCamp.z - 10f, roadEnd = WarEnemyCamp.z + 10f;
            Prim(PrimitiveType.Cube, "WarRoad", world, new Vector3(0, 0.02f, (roadStart + roadEnd) * 0.5f),
                new Vector3(30f, 0.04f, roadEnd - roadStart), mats.Nest, keepCollider: false);

            // ── 편든 쪽 진영 절벽 (둥지 절벽과 같은 길이·높이) ──
            const float back = 8f;
            float cliffLength = RunUpLength + back;
            float cliffMidZ = CliffEdgeZ - cliffLength * 0.5f;
            Prim(PrimitiveType.Cube, "AllyCliff", world, new Vector3(0, CliffHeight * 0.5f, cliffMidZ),
                new Vector3(14, CliffHeight, cliffLength), mats.Rock);
            // 절벽 뒤 산등성이 (뒤쪽 빈 땅 가림)
            Prim(PrimitiveType.Cube, "AllyRidge", world, new Vector3(0, 20f, CliffEdgeZ - cliffLength - 30f),
                new Vector3(320f, 40f, 50f), mats.CaveRock);

            float zone = launch.jumpZoneLength;
            Prim(PrimitiveType.Cube, "JumpZone", world, new Vector3(0, CliffHeight + 0.03f, CliffEdgeZ - zone * 0.5f),
                new Vector3(4f, 0.04f, zone), mats.JumpZone, keepCollider: false);
            float line = launch.perfectJumpDistance;
            Prim(PrimitiveType.Cube, "JumpLine", world, new Vector3(0, CliffHeight + 0.05f, CliffEdgeZ - line * 0.5f),
                new Vector3(4f, 0.05f, line), mats.JumpLine, keepCollider: false);

            // 도움닫기 길 양옆 말뚝 울타리와 깃발
            var camp = new GameObject("AllyCliffCamp").transform;
            camp.SetParent(world);
            for (int i = 0; i <= 6; i++)
            {
                float z = CliffEdgeZ - 2f - i * (RunUpLength / 6f);
                foreach (int side in new[] { -1, 1 })
                {
                    Prim(PrimitiveType.Cylinder, "Stake", camp, new Vector3(side * 6.4f, CliffHeight + 1f, z),
                        new Vector3(0.35f, 1f, 0.35f), mats.Bark, keepCollider: false);
                    if (i % 2 != 0) continue;
                    Prim(PrimitiveType.Cylinder, "BannerPole", camp, new Vector3(side * 6.4f, CliffHeight + 3.5f, z),
                        new Vector3(0.18f, 3.5f, 0.18f), mats.Bark, keepCollider: false);
                    allyBanners.Add(Prim(PrimitiveType.Cube, "AllyBanner", camp, new Vector3(side * 6.4f, CliffHeight + 5.6f, z - 0.9f),
                        new Vector3(0.08f, 1.6f, 1.8f), mats.Prey, keepCollider: false).GetComponent<Renderer>());
                }
            }
            // 절벽 앞면에 늘어뜨린 큰 깃발 두 장 (전장에서 보인다)
            foreach (int side in new[] { -1, 1 })
                allyBanners.Add(Prim(PrimitiveType.Cube, "AllyCliffBanner", camp, new Vector3(side * 4.2f, CliffHeight - 6f, CliffEdgeZ + 0.1f),
                    new Vector3(3f, 10f, 0.1f), mats.Prey, keepCollider: false).GetComponent<Renderer>());

            // ── 적 진영 뒤 능선과 깃발 ──
            float ridgeZ = WarEnemyCamp.z + 60f;
            Prim(PrimitiveType.Cube, "EnemyRidge", world, new Vector3(0, 16f, ridgeZ), new Vector3(320f, 32f, 40f), mats.CaveRock);
            var enemyCamp = new GameObject("EnemyCamp").transform;
            enemyCamp.SetParent(world);
            for (int i = 0; i < 7; i++)
            {
                float x = (i - 3) * 22f;
                float y = 32f;
                Prim(PrimitiveType.Cylinder, "BannerPole", enemyCamp, new Vector3(x, y + 5f, ridgeZ - 18f),
                    new Vector3(0.4f, 5f, 0.4f), mats.Bark, keepCollider: false);
                enemyBanners.Add(Prim(PrimitiveType.Cube, "EnemyBanner", enemyCamp, new Vector3(x + 2f, y + 8f, ridgeZ - 18f),
                    new Vector3(4f, 2.4f, 0.1f), mats.Prey, keepCollider: false).GetComponent<Renderer>());
            }
            // 적 진영 앞 뾰족 말뚝 방책
            for (int i = 0; i < 24; i++)
            {
                float x = (i - 11.5f) * 5f;
                if (Mathf.Abs(x) < 16f) continue; // 가운데는 출진로
                var stake = Prim(PrimitiveType.Cylinder, "SpikeStake", enemyCamp, new Vector3(x, 1.2f, WarEnemyCamp.z - 28f),
                    new Vector3(0.4f, 1.6f, 0.4f), mats.Bark, keepCollider: false);
                stake.transform.rotation = Quaternion.Euler(-35f, 0f, 0f);
            }

            // ── 골짜기 양옆 능선 ──
            var rng = new System.Random(4321);
            for (int i = 0; i < 9; i++)
            {
                float z = -10f + i * 45f;
                foreach (int side in new[] { -1, 1 })
                {
                    float h = Range(rng, 22f, 38f);
                    Prim(PrimitiveType.Cube, "ValleyWall", world, new Vector3(side * Range(rng, 150f, 165f), h * 0.5f, z),
                        new Vector3(Range(rng, 30f, 45f), h, Range(rng, 50f, 60f)), mats.Rock)
                        .transform.rotation = Quaternion.Euler(0, Range(rng, -12f, 12f), 0);
                }
            }

            // ── 전장: 엄폐 바위, 양옆 숲, 급강하용 바위 기둥, 길가 화톳불 ──
            var field = new GameObject("Battlefield").transform;
            field.SetParent(world);
            for (int i = 0; i < 16; i++)
            {
                float x = Range(rng, 18f, 100f) * (rng.NextDouble() < 0.5 ? -1f : 1f);
                float z = Range(rng, 85f, 270f);
                float s = Range(rng, 2.5f, 6f);
                Prim(PrimitiveType.Cube, $"Rock_{i}", field, new Vector3(x, s * 0.4f, z),
                    new Vector3(s, s * Range(rng, 0.6f, 1.4f), s * Range(rng, 0.7f, 1.3f)), mats.Rock)
                    .transform.rotation = Quaternion.Euler(Range(rng, -10, 10), Range(rng, 0, 360), Range(rng, -10, 10));
            }
            for (int i = 0; i < 40; i++)
            {
                float x = Range(rng, 55f, 130f) * (rng.NextDouble() < 0.5 ? -1f : 1f);
                float z = Range(rng, 20f, 330f);
                float h = Range(rng, 5f, 10f);
                var tree = new GameObject($"Tree_{i}").transform;
                tree.SetParent(field);
                Prim(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(x, h * 0.5f, z), new Vector3(0.7f, h * 0.5f, 0.7f), mats.Bark);
                float crown = Range(rng, 3.5f, 6f);
                Prim(PrimitiveType.Sphere, "Crown", tree, new Vector3(x, h + crown * 0.3f, z), Vector3.one * crown, mats.Leaf);
            }
            foreach (var p in new[] { new Vector3(-80f, 0, 150f), new Vector3(85f, 0, 215f), new Vector3(-60f, 0, 265f) })
            {
                float h = Range(rng, 28f, 40f);
                Prim(PrimitiveType.Cube, "Spire", field, p + Vector3.up * (h * 0.5f), new Vector3(8, h, 8), mats.Rock)
                    .transform.rotation = Quaternion.Euler(0, Range(rng, 0, 90), 0);
            }
            for (int i = 0; i < 8; i++)
            {
                float z = WarAllyCamp.z + 35f + i * 30f;
                foreach (int side in new[] { -1, 1 })
                {
                    var at = new Vector3(side * 17f, 0f, z);
                    Prim(PrimitiveType.Cylinder, "Brazier", field, at + Vector3.up * 0.6f, new Vector3(1f, 0.6f, 1f), mats.CaveRock, keepCollider: false);
                    Prim(PrimitiveType.Cube, "BrazierFire", field, at + Vector3.up * 1.5f, new Vector3(0.7f, 0.7f, 0.7f), mats.JumpZone, keepCollider: false)
                        .GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            warMap.allyBanners = allyBanners.ToArray();
            warMap.enemyBanners = enemyBanners.ToArray();
            return warMap;
        }
    }
}
