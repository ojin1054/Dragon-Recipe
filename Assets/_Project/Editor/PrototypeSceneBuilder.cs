using System.IO;
using DragonRecipe.CameraRig;
using DragonRecipe.Core;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using DragonRecipe.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace DragonRecipe.EditorTools
{
    /// <summary>
    /// 메뉴 "Dragon Recipe/Build Flight Prototype Scene" 한 번으로
    /// 데이터 에셋, 머티리얼, 프리미티브 월드, 드래곤, UI가 들어간 테스트 씬을 만든다.
    /// (배치 모드: -executeMethod DragonRecipe.EditorTools.PrototypeSceneBuilder.Build)
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        const string Root = "Assets/_Project";
        const string DataDir = Root + "/Data";
        const string MatDir = Root + "/Art/Materials";
        const string ScenePath = Root + "/Scenes/FlightPrototype.unity";

        const float CliffHeight = 24f;

        [MenuItem("Dragon Recipe/Build Flight Prototype Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            var stages = CreateStages();
            var foods = CreateFoods();
            var mats = new Mats();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            BuildWorld(mats);
            var nest = new GameObject("Nest").transform;
            nest.position = new Vector3(0, CliffHeight + 1.2f, 0);

            var dragon = BuildDragon(stages[0], mats, out var input, out var snatch);
            dragon.transform.position = nest.position;

            var spawnerGo = new GameObject("PreySpawner");
            var spawner = spawnerGo.AddComponent<PreySpawner>();
            spawner.foods = foods;
            spawner.preyMaterial = mats.Prey;
            spawner.threat = dragon.transform;

            var sessionGo = new GameObject("FlightSession");
            var session = sessionGo.AddComponent<FlightSession>();
            session.dragon = dragon;
            session.input = input;
            session.snatchZone = snatch;
            session.spawner = spawner;
            session.nest = nest;

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

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[Dragon Recipe] 씬 생성 완료: {ScenePath}");
        }

        // ───────────────────────── 데이터 ─────────────────────────

        static DragonStageData[] CreateStages()
        {
            // 이미 있는 에셋은 건드리지 않는다 (인스펙터에서 튜닝한 값 보존)
            var hatch = LoadOrCreate<DragonStageData>($"{DataDir}/Stage_Hatchling.asset", out bool newHatch);
            if (newHatch)
            {
            hatch.stage = GrowthStage.Hatchling;
            hatch.displayName = "어린 드래곤";
            hatch.bodyScale = 1f;
            hatch.flight = new FlightParams(); // 기본값이 어린 드래곤 튜닝
            EditorUtility.SetDirty(hatch);
            }

            var juv = LoadOrCreate<DragonStageData>($"{DataDir}/Stage_Juvenile.asset", out bool newJuv);
            if (newJuv)
            {
            juv.stage = GrowthStage.Juvenile;
            juv.displayName = "청소년 드래곤";
            juv.bodyScale = 1.5f;
            juv.flight = new FlightParams
            {
                liftCoefficient = 0.8f, flapLift = 4.5f, flapThrust = 2f, maxStamina = 140f,
                staminaRegen = 9f, turnRate = 65f, safeLandingSink = 5.5f, safeLandingSpeed = 19f
            };
            EditorUtility.SetDirty(juv);
            }

            var adult = LoadOrCreate<DragonStageData>($"{DataDir}/Stage_Adult.asset", out bool newAdult);
            if (newAdult)
            {
            adult.stage = GrowthStage.Adult;
            adult.displayName = "성체 드래곤";
            adult.bodyScale = 2.2f;
            adult.flight = new FlightParams
            {
                needsFlapping = false, liftCoefficient = 1.15f, flapLift = 5f, flapThrust = 3f,
                maxStamina = 200f, staminaRegen = 14f, maxSpeed = 48f, turnRate = 60f,
                safeLandingSink = 7f, safeLandingSpeed = 22f
            };
            EditorUtility.SetDirty(adult);
            }

            return new[] { hatch, juv, adult };
        }

        static FoodData[] CreateFoods()
        {
            return new[]
            {
                Food("sheep", "양", 12, FoodTrait.Meat, new Color(0.95f, 0.95f, 0.9f), new Vector3(0.9f, 0.7f, 1.3f),
                    wander: 0.8f, flees: false, flee: 0f, weight: 3f),
                Food("boar", "멧돼지", 20, FoodTrait.Meat, new Color(0.45f, 0.3f, 0.2f), new Vector3(0.8f, 0.7f, 1.5f),
                    wander: 1.5f, flees: true, flee: 7f, weight: 2f),
                Food("salmon", "연어", 10, FoodTrait.Fish, new Color(1f, 0.55f, 0.45f), new Vector3(0.4f, 0.35f, 1.1f),
                    wander: 2f, flees: true, flee: 4f, weight: 2f),
                Food("ember_lizard", "불꽃도마뱀", 8, FoodTrait.Fire, new Color(1f, 0.3f, 0.05f), new Vector3(0.4f, 0.25f, 0.9f),
                    wander: 3f, flees: true, flee: 9f, weight: 1f),
                Food("golden_melon", "황금멜론", 6, FoodTrait.Plant, new Color(1f, 0.85f, 0.2f), new Vector3(0.7f, 0.7f, 0.7f),
                    wander: 0f, flees: false, flee: 0f, weight: 1.5f),
            };
        }

        static FoodData Food(string id, string name, int satiety, FoodTrait trait, Color color, Vector3 size,
            float wander, bool flees, float flee, float weight)
        {
            var f = LoadOrCreate<FoodData>($"{DataDir}/Food_{id}.asset", out bool created);
            if (!created) return f; // 기존 값 보존
            f.id = id;
            f.displayName = name;
            f.satiety = satiety;
            f.trait = trait;
            f.color = color;
            f.bodySize = size;
            f.wanderSpeed = wander;
            f.flees = flees;
            f.fleeSpeed = flee;
            f.fleeRadius = 25f;
            f.spawnWeight = weight;
            EditorUtility.SetDirty(f);
            return f;
        }

        static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            created = !a;
            if (a) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        // ───────────────────────── 머티리얼 ─────────────────────────

        class Mats
        {
            public readonly Material Grass = Mat("Grass", new Color(0.42f, 0.66f, 0.32f));
            public readonly Material Rock = Mat("Rock", new Color(0.52f, 0.5f, 0.48f));
            public readonly Material Bark = Mat("Bark", new Color(0.4f, 0.27f, 0.17f));
            public readonly Material Leaf = Mat("Leaf", new Color(0.2f, 0.48f, 0.22f));
            public readonly Material Nest = Mat("Nest", new Color(0.6f, 0.45f, 0.25f));
            public readonly Material Water = Mat("Water", new Color(0.25f, 0.5f, 0.85f));
            public readonly Material DragonBody = Mat("DragonBody", new Color(0.3f, 0.65f, 0.55f));
            public readonly Material DragonWing = Mat("DragonWing", new Color(0.85f, 0.55f, 0.35f));
            public readonly Material DragonEye = Mat("DragonEye", new Color(1f, 0.9f, 0.2f));
            public readonly Material Prey = Mat("Prey", Color.white);

            static Material Mat(string name, Color c)
            {
                string path = $"{MatDir}/M_{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    m = new Material(shader);
                    AssetDatabase.CreateAsset(m, path);
                }
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                m.color = c;
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
                EditorUtility.SetDirty(m);
                return m;
            }
        }

        // ───────────────────────── 월드 ─────────────────────────

        static void BuildLighting()
        {
            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.65f, 0.75f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.6f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.3f, 0.25f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.75f, 0.85f, 0.95f);
            RenderSettings.fogStartDistance = 150f;
            RenderSettings.fogEndDistance = 600f;
        }

        static void BuildWorld(Mats mats)
        {
            var world = new GameObject("World").transform;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(world);
            ground.transform.localScale = new Vector3(100, 1, 100); // 1km x 1km
            ground.GetComponent<Renderer>().sharedMaterial = mats.Grass;

            // 출발 절벽 + 둥지
            var cliff = Prim(PrimitiveType.Cube, "Cliff", world, new Vector3(0, CliffHeight * 0.5f, -2f),
                new Vector3(14, CliffHeight, 16), mats.Rock);
            cliff.transform.rotation = Quaternion.Euler(0, 8, 0);
            Prim(PrimitiveType.Cylinder, "NestRing", world, new Vector3(0, CliffHeight + 0.15f, 0),
                new Vector3(4f, 0.3f, 4f), mats.Nest, keepCollider: false);

            // 연못 (장식, 콜라이더 없음)
            Prim(PrimitiveType.Cylinder, "Pond", world, new Vector3(-40, 0.02f, 140), new Vector3(40, 0.02f, 30), mats.Water, keepCollider: false);
            Prim(PrimitiveType.Cylinder, "Pond2", world, new Vector3(60, 0.02f, 260), new Vector3(30, 0.02f, 45), mats.Water, keepCollider: false);

            var rng = new System.Random(1234);
            var obstacles = new GameObject("Obstacles").transform;
            obstacles.SetParent(world);

            // 나무
            for (int i = 0; i < 90; i++)
            {
                Vector3 p = RandomPoint(rng, 30f);
                float h = Range(rng, 5f, 11f);
                var tree = new GameObject($"Tree_{i}").transform;
                tree.SetParent(obstacles);
                tree.position = p;
                Prim(PrimitiveType.Cylinder, "Trunk", tree, p + Vector3.up * (h * 0.5f), new Vector3(0.7f, h * 0.5f, 0.7f), mats.Bark);
                float crown = Range(rng, 3.5f, 6f);
                Prim(PrimitiveType.Sphere, "Crown", tree, p + Vector3.up * (h + crown * 0.3f), Vector3.one * crown, mats.Leaf);
            }

            // 바위 / 기둥
            for (int i = 0; i < 35; i++)
            {
                Vector3 p = RandomPoint(rng, 35f);
                float s = Range(rng, 2f, 7f);
                var rock = Prim(PrimitiveType.Cube, $"Rock_{i}", obstacles, p + Vector3.up * (s * 0.4f),
                    new Vector3(s, s * Range(rng, 0.6f, 1.8f), s * Range(rng, 0.7f, 1.3f)), mats.Rock);
                rock.transform.rotation = Quaternion.Euler(Range(rng, -10, 10), Range(rng, 0, 360), Range(rng, -10, 10));
            }

            // 멀리 보이는 높은 바위 기둥 (급강하 목표물)
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = new Vector3(Range(rng, -180, 180), 0, Range(rng, 120, 420));
                float h = Range(rng, 25f, 45f);
                Prim(PrimitiveType.Cube, $"Spire_{i}", obstacles, p + Vector3.up * (h * 0.5f), new Vector3(8, h, 8), mats.Rock)
                    .transform.rotation = Quaternion.Euler(0, Range(rng, 0, 90), 0);
            }
        }

        static Vector3 RandomPoint(System.Random rng, float keepOut)
        {
            while (true)
            {
                var p = new Vector3(Range(rng, -200, 200), 0, Range(rng, -40, 420));
                if (new Vector2(p.x, p.z).magnitude > keepOut) return p;
            }
        }

        static float Range(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat,
            bool keepCollider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        // ───────────────────────── 드래곤 ─────────────────────────

        static DragonFlightController BuildDragon(DragonStageData stage, Mats mats, out DragonInput input, out SnatchZone snatch)
        {
            var root = new GameObject("Dragon");
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            input = root.AddComponent<DragonInput>();
            var ctrl = root.AddComponent<DragonFlightController>();
            ctrl.stage = stage;
            ctrl.input = input;

            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);

            Part(PrimitiveType.Capsule, "Body", model, new Vector3(0, 0, 0), new Vector3(0, 0, 0), new Vector3(0.9f, 0.9f, 1.2f), mats.DragonBody)
                .transform.localRotation = Quaternion.Euler(90, 0, 0);
            Part(PrimitiveType.Capsule, "Neck", model, new Vector3(0, 0.35f, 1.0f), new Vector3(60, 0, 0), new Vector3(0.45f, 0.5f, 0.45f), mats.DragonBody);
            Part(PrimitiveType.Sphere, "Head", model, new Vector3(0, 0.6f, 1.45f), Vector3.zero, new Vector3(0.6f, 0.5f, 0.8f), mats.DragonBody);
            Part(PrimitiveType.Sphere, "EyeL", model, new Vector3(-0.2f, 0.72f, 1.7f), Vector3.zero, Vector3.one * 0.13f, mats.DragonEye);
            Part(PrimitiveType.Sphere, "EyeR", model, new Vector3(0.2f, 0.72f, 1.7f), Vector3.zero, Vector3.one * 0.13f, mats.DragonEye);
            Part(PrimitiveType.Cube, "HornL", model, new Vector3(-0.15f, 0.9f, 1.3f), new Vector3(-30, 0, 0), new Vector3(0.08f, 0.35f, 0.08f), mats.DragonWing);
            Part(PrimitiveType.Cube, "HornR", model, new Vector3(0.15f, 0.9f, 1.3f), new Vector3(-30, 0, 0), new Vector3(0.08f, 0.35f, 0.08f), mats.DragonWing);

            var tail = new GameObject("TailPivot").transform;
            tail.SetParent(model, false);
            tail.localPosition = new Vector3(0, 0, -1.0f);
            Part(PrimitiveType.Capsule, "Tail", tail, new Vector3(0, 0, -0.8f), new Vector3(90, 0, 0), new Vector3(0.35f, 0.9f, 0.35f), mats.DragonBody);
            Part(PrimitiveType.Cube, "TailTip", tail, new Vector3(0, 0, -1.75f), new Vector3(0, 45, 0), new Vector3(0.4f, 0.05f, 0.4f), mats.DragonWing);

            var wingL = Wing("WingL", model, -1, mats);
            var wingR = Wing("WingR", model, 1, mats);

            // 발톱 (낚아채기 위치 표시)
            Part(PrimitiveType.Cube, "ClawL", model, new Vector3(-0.25f, -0.55f, 0.3f), Vector3.zero, new Vector3(0.12f, 0.3f, 0.12f), mats.DragonWing);
            Part(PrimitiveType.Cube, "ClawR", model, new Vector3(0.25f, -0.55f, 0.3f), Vector3.zero, new Vector3(0.12f, 0.3f, 0.12f), mats.DragonWing);

            var snatchGo = new GameObject("SnatchZone");
            snatchGo.transform.SetParent(root.transform, false);
            snatchGo.transform.localPosition = new Vector3(0, -1.0f, 0.3f);
            var sphere = snatchGo.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 1.6f;
            snatch = snatchGo.AddComponent<SnatchZone>();

            var visual = root.AddComponent<DragonVisual>();
            visual.controller = ctrl;
            visual.leftWing = wingL;
            visual.rightWing = wingR;
            visual.tail = tail;

            return ctrl;
        }

        static Transform Wing(string name, Transform parent, int side, Mats mats)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = new Vector3(0.35f * side, 0.3f, 0.2f);
            Part(PrimitiveType.Cube, "Arm", pivot, new Vector3(1.0f * side, 0, 0.1f), Vector3.zero, new Vector3(2.0f, 0.12f, 0.2f), mats.DragonBody);
            Part(PrimitiveType.Cube, "Membrane", pivot, new Vector3(1.0f * side, 0, -0.45f), new Vector3(0, -8f * side, 0), new Vector3(1.9f, 0.04f, 1.0f), mats.DragonWing);
            Part(PrimitiveType.Cube, "Tip", pivot, new Vector3(2.15f * side, 0, -0.3f), new Vector3(0, -25f * side, 0), new Vector3(0.6f, 0.04f, 0.8f), mats.DragonWing);
            return pivot;
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localEuler, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // 충돌은 SphereCast로만
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        // ───────────────────────── UI ─────────────────────────

        static Font _font;
        static Font UIFont => _font ? _font : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        static void BuildUI(FlightSession session, DragonFlightController dragon, DragonInput input)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var ct = canvasGo.transform;

            // HUD
            var hud = canvasGo.AddComponent<FlightHUD>();
            hud.session = session;
            hud.dragon = dragon;

            hud.statsText = MakeText(ct, "Stats", new Vector2(0, 1), new Vector2(40, -40), new Vector2(600, 220), 34, TextAnchor.UpperLeft);
            hud.statsText.rectTransform.pivot = new Vector2(0, 1);

            var barBg = MakeImage(ct, "StaminaBar", new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(520, 28), new Color(0, 0, 0, 0.45f));
            var fill = MakeImage(barBg.transform, "Fill", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(510, 20), Color.cyan);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            hud.staminaFill = fill;
            MakeText(barBg.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(0, -36), new Vector2(400, 40), 24, TextAnchor.MiddleCenter).text = "스태미나";

            hud.promptText = MakeText(ct, "Prompt", new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(1400, 140), 40, TextAnchor.MiddleCenter);
            hud.feedbackText = MakeText(ct, "Feedback", new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(1400, 90), 56, TextAnchor.MiddleCenter);

            // 모바일 컨트롤
            var mobile = new GameObject("MobileControls", typeof(RectTransform));
            mobile.transform.SetParent(ct, false);
            Stretch(mobile.GetComponent<RectTransform>());

            var stickBg = MakeImage(mobile.transform, "StickArea", new Vector2(0, 0), new Vector2(240, 240), new Vector2(300, 300), new Color(1, 1, 1, 0.12f));
            stickBg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            stickBg.raycastTarget = false;
            var knob = MakeImage(stickBg.transform, "Knob", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130, 130), new Color(1, 1, 1, 0.55f));
            knob.sprite = stickBg.sprite;
            var stick = knob.gameObject.AddComponent<OnScreenStick>();
            stick.movementRange = 110f;
            SetControlPath(stick, "<Gamepad>/leftStick");

            var flapBtn = MakeImage(mobile.transform, "FlapButton", new Vector2(1, 0), new Vector2(-240, 240), new Vector2(240, 240), new Color(1f, 0.75f, 0.4f, 0.5f));
            flapBtn.sprite = stickBg.sprite;
            var onBtn = flapBtn.gameObject.AddComponent<OnScreenButton>();
            SetControlPath(onBtn, "<Gamepad>/buttonSouth");
            MakeText(flapBtn.transform, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 80), 40, TextAnchor.MiddleCenter).text = "날갯짓";

            var vis = canvasGo.AddComponent<MobileControlsVisibility>();
            vis.controlsRoot = mobile;

            // 결과 패널
            var resultPanel = MakeImage(ct, "ResultPanel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 700), new Color(0.08f, 0.1f, 0.14f, 0.88f));
            var result = canvasGo.AddComponent<FlightResultPanel>();
            result.session = session;
            result.panel = resultPanel.gameObject;
            result.titleText = MakeText(resultPanel.transform, "Title", new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(860, 90), 52, TextAnchor.MiddleCenter);
            result.bodyText = MakeText(resultPanel.transform, "Body", new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(780, 400), 32, TextAnchor.UpperLeft);

            var btnImg = MakeImage(resultPanel.transform, "RetryButton", new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(360, 100), new Color(1f, 0.75f, 0.35f));
            btnImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            btnImg.type = Image.Type.Sliced;
            var btn = btnImg.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            var label = MakeText(btnImg.transform, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340, 90), 40, TextAnchor.MiddleCenter);
            label.text = "다시 날기";
            label.color = new Color(0.15f, 0.1f, 0.05f);
            result.retryButton = btn;
            resultPanel.gameObject.SetActive(false);

            BuildPauseMenu(canvasGo, ct, session, input);
        }

        static void BuildPauseMenu(GameObject canvasGo, Transform ct, FlightSession session, DragonInput input)
        {
            var menu = canvasGo.AddComponent<PauseMenu>();
            menu.session = session;
            menu.input = input;

            // 항상 떠 있는 일시정지 버튼 (오른쪽 위)
            menu.pauseButton = MakeButton(ct, "PauseButton", new Vector2(1, 1), new Vector2(-80, -70), new Vector2(110, 90),
                "II", new Color(1f, 1f, 1f, 0.35f), Color.white, out _);

            // 메뉴 패널 (반투명 전체 화면 배경 + 가운데 상자)
            var dim = MakeImage(ct, "PauseMenu", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.55f));
            Stretch(dim.rectTransform);
            var box = MakeImage(dim.transform, "Box", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 620), new Color(0.08f, 0.1f, 0.14f, 0.92f));
            var title = MakeText(box.transform, "Title", new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(680, 90), 56, TextAnchor.MiddleCenter);
            title.text = "일시정지";

            var btnColor = new Color(1f, 0.75f, 0.35f);
            var textColor = new Color(0.15f, 0.1f, 0.05f);
            menu.resumeButton = MakeButton(box.transform, "Resume", new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(480, 100), "계속하기", btnColor, textColor, out _);
            menu.invertButton = MakeButton(box.transform, "InvertPitch", new Vector2(0.5f, 0.5f), new Vector2(0, -35), new Vector2(480, 100), "상/하 반전: 꺼짐", btnColor, textColor, out menu.invertLabel);
            menu.restartButton = MakeButton(box.transform, "Restart", new Vector2(0.5f, 0.5f), new Vector2(0, -160), new Vector2(480, 100), "둥지로 돌아가기", btnColor, textColor, out _);
            var hint = MakeText(box.transform, "Hint", new Vector2(0.5f, 0), new Vector2(0, 45), new Vector2(680, 50), 26, TextAnchor.MiddleCenter);
            hint.text = "Esc / P / 패드 Start 로 열고 닫기";
            hint.color = new Color(1, 1, 1, 0.6f);

            menu.panel = dim.gameObject;
            dim.gameObject.SetActive(false);
        }

        static Button MakeButton(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, string label,
            Color color, Color textColor, out Text labelText)
        {
            var img = MakeImage(parent, name, anchor, pos, size, color);
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            labelText = MakeText(img.transform, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(20, 10), 38, TextAnchor.MiddleCenter);
            labelText.text = label;
            labelText.color = textColor;
            return btn;
        }

        static void SetControlPath(Object control, string path)
        {
            var so = new SerializedObject(control);
            var prop = so.FindProperty("m_ControlPath");
            if (prop != null)
            {
                prop.stringValue = path;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogError($"m_ControlPath 필드를 찾지 못했습니다: {control.GetType().Name}");
            }
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static Text MakeText(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.font = UIFont;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Color.white;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.6f);
            shadow.effectDistance = new Vector2(2, -2);
            return t;
        }

        static Image MakeImage(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }
    }
}
