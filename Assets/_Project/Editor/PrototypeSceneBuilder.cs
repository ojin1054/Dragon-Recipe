using System.IO;
using DragonRecipe.CameraRig;
using DragonRecipe.Care;
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
    /// 메뉴 "Dragon Recipe/Build All Scenes" 한 번으로
    /// 데이터 에셋, 머티리얼, 둥지 씬, 비행 씬(프리미티브 월드, 드래곤, UI)을 만든다.
    /// 이미 있는 데이터 에셋(Stage_*, Food_*, GameConfig)은 덮어쓰지 않는다.
    /// (배치 모드: -executeMethod DragonRecipe.EditorTools.PrototypeSceneBuilder.Build)
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        const string Root = "Assets/_Project";
        const string DataDir = Root + "/Data";
        const string MatDir = Root + "/Art/Materials";
        const string ScenePath = Root + "/Scenes/FlightPrototype.unity";
        const string NestScenePath = Root + "/Scenes/Nest.unity";

        const float CliffHeight = 24f;
        /// <summary>비행 씬: 절벽 끝(도약 지점)의 z, 둥지에서 절벽 끝까지 도움닫기 거리</summary>
        const float CliffEdgeZ = 6f;
        const float RunUpLength = 28f;
        /// <summary>비행 씬: 둥지에서 동굴 입구까지 거리</summary>
        const float CaveMouthAhead = 5f;

        [MenuItem("Dragon Recipe/Build All Scenes")]
        public static void Build()
        {
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            var stages = CreateStages();
            var foods = CreateFoods();
            var config = CreateGameConfig(stages, foods);
            _icons = CreateIconSet();
            AssignFoodIcons(foods);
            var mats = new Mats();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            BuildWorld(mats, stages[0].flight);
            var nest = new GameObject("Nest").transform;
            nest.position = new Vector3(0, CliffHeight + 1.2f, CliffEdgeZ - RunUpLength);
            var edge = new GameObject("LaunchEdge").transform;
            edge.position = new Vector3(0, CliffHeight + 1.2f, CliffEdgeZ);

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

            EditorSceneManager.SaveScene(scene, ScenePath);

            BuildNestScene(config, stages[0], mats);

            // 둥지가 첫 화면
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(NestScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log($"[Dragon Recipe] 씬 생성 완료: {NestScenePath}, {ScenePath}");
        }

        // ───────────────────────── 아이콘 ─────────────────────────

        const string IconDir = Root + "/Art/Icons";
        static UIIconSet _icons;

        /// <summary>Art/Icons의 PNG를 픽셀 스프라이트로 임포트하고 UIIconSet에 연결 (빈 칸만 채움)</summary>
        static UIIconSet CreateIconSet()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IconDir }))
                IconImportSettings.Apply(AssetDatabase.GUIDToAssetPath(guid), reimport: true);

            var set = LoadOrCreate<UIIconSet>($"{DataDir}/UIIconSet.asset", out _);
            foreach (var field in typeof(UIIconSet).GetFields())
            {
                if (field.FieldType != typeof(Sprite) || field.GetValue(set) as Sprite) continue;
                var sprite = LoadIcon(ToSnake(field.Name));
                if (sprite) field.SetValue(set, sprite);
                else Debug.LogWarning($"[Dragon Recipe] 아이콘 없음: {IconDir}/icon_{ToSnake(field.Name)}.png");
            }
            EditorUtility.SetDirty(set);
            return set;
        }

        static void AssignFoodIcons(FoodData[] foods)
        {
            foreach (var f in foods)
            {
                if (f.icon) continue;
                f.icon = LoadIcon($"food_{f.id}");
                EditorUtility.SetDirty(f);
            }
        }

        static Sprite LoadIcon(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/icon_{name}.png");

        static string ToSnake(string camel)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in camel)
            {
                if (char.IsUpper(ch) && sb.Length > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        static IconFlow MakeFlow(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, TextAnchor align, float iconSize, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = align;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var flow = go.AddComponent<IconFlow>();
            flow.font = UIFont;
            flow.alignment = align;
            flow.iconSize = iconSize;
            flow.fontSize = fontSize;
            return flow;
        }

        static GameConfig CreateGameConfig(DragonStageData[] stages, FoodData[] foods)
        {
            var cfg = LoadOrCreate<GameConfig>($"{DataDir}/GameConfig.asset", out bool created);
            if (created) cfg.care = new CareSettings();
            if (created || cfg.stages == null || cfg.stages.Length == 0) cfg.stages = stages;
            if (cfg.foods == null || cfg.foods.Length == 0) cfg.foods = foods;
            else
            {
                // 새로 생긴 먹이만 뒤에 붙인다 (기존 순서 유지)
                var merged = new System.Collections.Generic.List<FoodData>(cfg.foods);
                foreach (var f in foods)
                    if (f && !merged.Contains(f)) merged.Add(f);
                cfg.foods = merged.ToArray();
            }
            if (cfg.farm == null) cfg.farm = new DragonRecipe.Farm.FarmSettings();
            if (!cfg.combineTable) cfg.combineTable = FarmDataBuilder.EnsureTable();
            cfg.nestScene = Path.GetFileNameWithoutExtension(NestScenePath);
            cfg.flightScene = Path.GetFileNameWithoutExtension(ScenePath);
            EditorUtility.SetDirty(cfg);
            return cfg;
        }

        // ───────────────────────── 둥지 씬 ─────────────────────────

        static void BuildNestScene(GameConfig config, DragonStageData stage, Mats mats)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildLighting();
            RenderSettings.fogStartDistance = 80f;
            RenderSettings.fogEndDistance = 400f;

            var world = new GameObject("World").transform;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(world);
            ground.transform.localScale = new Vector3(60, 1, 60);
            ground.GetComponent<Renderer>().sharedMaterial = mats.Grass;
            Prim(PrimitiveType.Cube, "Cliff", world, new Vector3(0, CliffHeight * 0.5f, -2f), new Vector3(14, CliffHeight, 16), mats.Rock)
                .transform.rotation = Quaternion.Euler(0, 8, 0);
            Prim(PrimitiveType.Cylinder, "NestRing", world, new Vector3(0, CliffHeight + 0.15f, 0), new Vector3(4f, 0.3f, 4f), mats.Nest, keepCollider: false);
            // [임시] 둥지 동굴: 입구가 카메라(아래 Main Camera) 쪽을 본다
            BuildCave(world, new Vector3(0, CliffHeight, 0), new Vector3(5.5f, 0, -11f),
                width: 9f, height: 6f, back: 6f, front: 4f, floorDepth: CliffHeight, mats, seed: 7);
            var rng = new System.Random(77);
            for (int i = 0; i < 40; i++)
            {
                var p = new Vector3(Range(rng, -150, 150), 0, Range(rng, -150, 150));
                if (new Vector2(p.x, p.z).magnitude < 20f) continue;
                float h = Range(rng, 5f, 11f);
                Prim(PrimitiveType.Cylinder, $"Trunk_{i}", world, p + Vector3.up * (h * 0.5f), new Vector3(0.7f, h * 0.5f, 0.7f), mats.Bark);
                Prim(PrimitiveType.Sphere, $"Crown_{i}", world, p + Vector3.up * (h + 1.5f), Vector3.one * Range(rng, 3.5f, 6f), mats.Leaf);
            }

            // 둥지 드래곤: 비행용 드래곤을 만든 뒤 비행 관련 컴포넌트를 떼어 낸다 (외형만 재사용)
            var flyer = BuildDragon(stage, mats, out var input, out var snatch);
            var dragonGo = flyer.gameObject;
            dragonGo.name = "NestDragon";
            Object.DestroyImmediate(snatch.gameObject);
            Object.DestroyImmediate(flyer);
            Object.DestroyImmediate(input);
            Object.DestroyImmediate(dragonGo.GetComponent<Rigidbody>());
            dragonGo.transform.position = new Vector3(0, CliffHeight + 0.75f, 0);
            dragonGo.transform.rotation = Quaternion.Euler(0, 155f, 0);
            var view = dragonGo.AddComponent<NestDragonView>();
            view.model = dragonGo.transform.Find("Model");

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.farClipPlane = 1000f;
            cam.fieldOfView = 50f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(5.5f, CliffHeight + 5.5f, -11f);
            camGo.transform.LookAt(new Vector3(-1.5f, CliffHeight + 1.5f, 0));
            var nestCam = camGo.AddComponent<NestCamera>();

            nestCam.farmView = BuildFarmPen(world, mats);

            BuildNestUI(config, nestCam);
            EditorSceneManager.SaveScene(scene, NestScenePath);
        }

        /// <summary>우리 가운데. 절벽 왼쪽에 붙인 바위 언덕 위 (가장 큰 우리도 동굴과 겹치지 않게)</summary>
        static readonly Vector3 PenCenter = new Vector3(-15.5f, CliffHeight, -1f);
        /// <summary>가장 크게 넓힌 우리의 반지름 (FarmPenView 마지막 단계와 맞춤). 언덕은 이 크기로 만든다</summary>
        static readonly Vector2 PenMaxHalf = new Vector2(5f, 4.1f);

        /// <summary>
        /// 동물 농장 우리 자리: 바위 언덕만 만든다. 울타리와 꾸밈은 FarmPenView가 확장 단계에 맞춰 실행 중에 짓고,
        /// 잡아 온 동물도 그 안에 풀어놓는다. 농장을 열었을 때의 카메라 자리를 돌려준다.
        /// </summary>
        static Transform BuildFarmPen(Transform world, Mats mats)
        {
            var pen = new GameObject("FarmPen").transform;
            pen.SetParent(world, false);
            pen.position = PenCenter;

            // 언덕: 절벽과 동굴 바닥에 이어 붙는다 (가장 큰 우리 + 둘레 여유)
            Prim(PrimitiveType.Cube, "PenHill", pen, PenCenter + new Vector3(0.8f, -CliffHeight * 0.5f, 0f),
                new Vector3(PenMaxHalf.x * 2f + 4f, CliffHeight, PenMaxHalf.y * 2f + 4f), mats.Rock);

            var camSpot = new GameObject("FarmCameraView").transform;
            camSpot.SetParent(pen, false);
            camSpot.position = PenCenter + new Vector3(6f, 7.5f, -12f);
            camSpot.rotation = Quaternion.Euler(0f, 16f, 0f) * Quaternion.LookRotation(PenCenter - camSpot.position, Vector3.up);

            var view = pen.gameObject.AddComponent<DragonRecipe.Farm.FarmPenView>();
            view.area = pen;
            view.animalMaterial = mats.Prey;
            view.penMaterial = mats.Prey;
            view.cameraSpot = camSpot;
            return camSpot;
        }

        static void BuildNestUI(GameConfig config, NestCamera nestCam)
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

            var ui = canvasGo.AddComponent<NestUI>();
            ui.config = config;

            var panel = MakeImage(ct, "StatusPanel", new Vector2(0, 1), new Vector2(40, -40), new Vector2(760, 470), new Color(0.08f, 0.1f, 0.14f, 0.75f));
            panel.rectTransform.pivot = new Vector2(0, 1);
            var pt = panel.transform;

            ui.titleText = MakeText(pt, "Title", new Vector2(0, 1), new Vector2(30, -25), new Vector2(700, 70), 46, TextAnchor.UpperLeft);
            ui.titleText.rectTransform.pivot = new Vector2(0, 1);

            ui.hungerFill = MakeBar(pt, "HungerBar", new Vector2(30, -120), out ui.hungerLabel);
            ui.growthFill = MakeBar(pt, "GrowthBar", new Vector2(30, -215), out ui.growthLabel);
            ui.growthFill.color = new Color(1f, 0.8f, 0.35f);

            ui.icons = _icons;
            ui.statusIcons = MakeFlow(pt, "StatusIcons", new Vector2(0, 1), new Vector2(30, -290), new Vector2(700, 170), TextAnchor.UpperLeft, 52f, 34);

            ui.reportIcons = MakeFlow(ct, "ReportIcons", new Vector2(0, 0), new Vector2(40, 40), new Vector2(1000, 320), TextAnchor.LowerLeft, 52f, 36);

            BuildStoragePanel(ct);
            BuildCodex(canvasGo, ct);
            var farm = BuildFarm(canvasGo, ct);
            farm.nestCamera = nestCam;
            ui.eggButton = MakeIconButton(ct, "EggButton", new Vector2(1, 0), new Vector2(-560, 120), new Vector2(150, 150), _icons.egg, new Color(1f, 0.95f, 0.8f, 0.35f));
            ui.eggButton.gameObject.SetActive(false);

            ui.flyButton = MakeButton(ct, "FlyButton", new Vector2(1, 0), new Vector2(-230, 120), new Vector2(380, 140),
                "비행 나가기", new Color(1f, 0.75f, 0.35f), new Color(0.15f, 0.1f, 0.05f), out var flyLabel);
            flyLabel.fontSize = 52;

            var debug = new GameObject("Debug", typeof(RectTransform));
            debug.transform.SetParent(ct, false);
            Stretch(debug.GetComponent<RectTransform>());
            var dbgColor = new Color(1f, 1f, 1f, 0.25f);
            ui.debugAdvanceButton = MakeButton(debug.transform, "Advance1h", new Vector2(1, 1), new Vector2(-170, -60), new Vector2(300, 70),
                "[디버그] +1시간", dbgColor, Color.white, out var advLabel);
            advLabel.fontSize = 28;
            ui.debugResetButton = MakeButton(debug.transform, "ResetSave", new Vector2(1, 1), new Vector2(-170, -145), new Vector2(300, 70),
                "저장 초기화", dbgColor, Color.white, out ui.debugResetLabel);
            ui.debugResetLabel.fontSize = 28;
            farm.debugUpgradeButton = MakeButton(debug.transform, "UpgradePen", new Vector2(1, 1), new Vector2(-170, -230), new Vector2(300, 70),
                "[디버그] 우리 확장", dbgColor, Color.white, out var penLabel);
            penLabel.fontSize = 28;
            ui.debugRoot = debug;

            // 농장을 열어 둔 동안 우리가 가려지지 않게 감출 것들
            var hide = new System.Collections.Generic.List<CanvasGroup>();
            foreach (var name in new[] { "StatusPanel", "ReportIcons", "StoragePanel", "CodexButton", "FarmButton", "EggButton", "FlyButton" })
            {
                var t = ct.Find(name);
                if (!t) continue;
                var g = t.GetComponent<CanvasGroup>();
                hide.Add(g ? g : t.gameObject.AddComponent<CanvasGroup>());
            }
            farm.hideWhileOpen = hide.ToArray();
        }

        /// <summary>동물 농장: 울타리 버튼 → 우리 / 조합 / 버프 패널 (내용은 FarmPanel이 실행 중에 만든다)</summary>
        static FarmPanel BuildFarm(GameObject canvasGo, Transform ct)
        {
            var farm = canvasGo.AddComponent<FarmPanel>();
            farm.icons = _icons;
            farm.font = UIFont;
            farm.buttonSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            farm.openButton = MakeIconButton(ct, "FarmButton", new Vector2(0, 1), new Vector2(250, -590), new Vector2(120, 120),
                _icons.fence ? _icons.fence : _icons.basket, new Color(1f, 1f, 1f, 0.3f));

            // 덮개는 투명: 뒤의 우리가 보이고, 다른 둥지 버튼은 눌리지 않게만 막는다
            var dim = MakeImage(ct, "FarmPanel", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0f));
            Stretch(dim.rectTransform);
            // 상자는 오른쪽에 작게 (왼쪽 절반에 우리가 보인다)
            var box = MakeImage(dim.transform, "Box", new Vector2(1f, 0.5f), new Vector2(-30, -80), new Vector2(1300, 900), new Color(0.08f, 0.1f, 0.14f, 0.92f));
            box.rectTransform.pivot = new Vector2(1f, 0.5f);
            box.rectTransform.localScale = Vector3.one * 0.78f;
            farm.closeButton = MakeIconButton(box.transform, "Close", new Vector2(1, 1), new Vector2(-60, -60), new Vector2(90, 90), _icons.no, new Color(1f, 1f, 1f, 0.2f));
            farm.box = box.rectTransform;
            farm.panel = dim.gameObject;
            dim.gameObject.SetActive(false);
            return farm;
        }

        /// <summary>둥지 오른쪽 위: 먹이 창고 (누르면 하나씩 먹임)</summary>
        static void BuildStoragePanel(Transform ct)
        {
            var bg = MakeImage(ct, "StoragePanel", new Vector2(1, 1), new Vector2(-40, -380), new Vector2(560, 400), new Color(0.08f, 0.1f, 0.14f, 0.75f));
            bg.rectTransform.pivot = new Vector2(1, 1);
            var panel = bg.gameObject.AddComponent<StoragePanel>();
            panel.icons = _icons;
            panel.font = UIFont;
            panel.buttonSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            panel.header = MakeFlow(bg.transform, "Header", new Vector2(0, 1), new Vector2(24, -16), new Vector2(500, 70), TextAnchor.UpperLeft, 52f, 34);

            var gridGo = new GameObject("Grid", typeof(RectTransform));
            gridGo.transform.SetParent(bg.transform, false);
            var rt = gridGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(24, -100);
            rt.sizeDelta = new Vector2(512, 300);
            var grid = gridGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(92, 92);
            grid.spacing = new Vector2(12, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            panel.grid = rt;
        }

        /// <summary>도감: 책 버튼 → 먹이 / 성체 모습 기록 패널</summary>
        static void BuildCodex(GameObject canvasGo, Transform ct)
        {
            var codex = canvasGo.AddComponent<CodexPanel>();
            codex.icons = _icons;
            codex.openButton = MakeIconButton(ct, "CodexButton", new Vector2(0, 1), new Vector2(110, -590), new Vector2(120, 120), _icons.book, new Color(1f, 1f, 1f, 0.3f));

            var dim = MakeImage(ct, "CodexPanel", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.6f));
            Stretch(dim.rectTransform);
            var box = MakeImage(dim.transform, "Box", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 860), new Color(0.08f, 0.1f, 0.14f, 0.95f));
            var title = MakeFlow(box.transform, "Title", new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(1000, 90), TextAnchor.MiddleCenter, 72f, 40);
            title.AddRow().Icon(_icons.book);
            codex.foodFlow = MakeFlow(box.transform, "Foods", new Vector2(0, 1), new Vector2(60, -140), new Vector2(980, 480), TextAnchor.UpperLeft, 56f, 38);
            codex.formFlow = MakeFlow(box.transform, "Forms", new Vector2(0, 0), new Vector2(60, 50), new Vector2(980, 120), TextAnchor.LowerLeft, 64f, 38);
            codex.closeButton = MakeIconButton(box.transform, "Close", new Vector2(1, 1), new Vector2(-60, -60), new Vector2(90, 90), _icons.no, new Color(1f, 1f, 1f, 0.2f));
            codex.panel = dim.gameObject;
            dim.gameObject.SetActive(false);
        }

        static Button MakeIconButton(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Sprite icon, Color bgColor)
        {
            var img = MakeImage(parent, name, anchor, pos, size, bgColor);
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var ic = MakeImage(img.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, size * 0.72f, Color.white);
            ic.sprite = icon;
            ic.preserveAspect = true;
            ic.raycastTarget = false;
            return btn;
        }

        static Image MakeBar(Transform parent, string name, Vector2 pos, out Text label)
        {
            label = MakeText(parent, name + "Label", new Vector2(0, 1), pos, new Vector2(700, 40), 28, TextAnchor.UpperLeft);
            label.rectTransform.pivot = new Vector2(0, 1);
            var bg = MakeImage(parent, name, new Vector2(0, 1), pos + new Vector2(0, -42), new Vector2(700, 30), new Color(0, 0, 0, 0.5f));
            bg.rectTransform.pivot = new Vector2(0, 1);
            var fill = MakeImage(bg.transform, "Fill", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(692, 22), new Color(0.55f, 0.85f, 0.4f));
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            return fill;
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

            // 예전에 만든 에셋: 새 값 채우기 (한 번만)
            MigrateStage(hatch, awareness: 1f, new DragonStats { strength = 20f, stamina = 100f, health = 100f, attack = 8f });
            MigrateStage(juv, awareness: 0.7f, new DragonStats { strength = 35f, stamina = 140f, health = 130f, attack = 14f });
            MigrateStage(adult, awareness: 0.45f, new DragonStats { strength = 60f, stamina = 200f, health = 170f, attack = 22f });
            return new[] { hatch, juv, adult };
        }

        /// <summary>자랄수록 동물이 늦게 알아채고(awareness), 능력치(힘·스태미나·체력·공격력)가 오른다</summary>
        static void MigrateStage(DragonStageData d, float awareness, DragonStats stats)
        {
            if (d.schema >= 2) return;
            if (d.schema < 1) d.preyAwarenessScale = awareness;
            d.stats = stats;
            d.flight = stats.ApplyTo(d.flight);
            d.schema = 2;
            EditorUtility.SetDirty(d);
        }

        static FoodData[] CreateFoods()
        {
            return new[]
            {
                Hunt(Food("sheep", "양", 12, FoodTrait.Meat, new Color(0.95f, 0.95f, 0.9f), new Vector3(0.9f, 0.7f, 1.3f),
                    wander: 0.8f, flees: false, flee: 0f, weight: 3f), strength: 10f, herdMin: 3, herdMax: 5),
                Hunt(Food("boar", "멧돼지", 20, FoodTrait.Meat, new Color(0.45f, 0.3f, 0.2f), new Vector3(0.8f, 0.7f, 1.5f),
                    wander: 1.5f, flees: true, flee: 7f, weight: 2f), strength: 16f, struggles: true),
                Hunt(Food("salmon", "연어", 10, FoodTrait.Fish, new Color(1f, 0.55f, 0.45f), new Vector3(0.4f, 0.35f, 1.1f),
                    wander: 2f, flees: true, flee: 4f, weight: 2f), strength: 5f),
                Hunt(Food("ember_lizard", "불꽃도마뱀", 8, FoodTrait.Fire, new Color(1f, 0.3f, 0.05f), new Vector3(0.4f, 0.25f, 0.9f),
                    wander: 3f, flees: true, flee: 9f, weight: 1f), strength: 3f),
                Hunt(Food("golden_melon", "황금멜론", 6, FoodTrait.Plant, new Color(1f, 0.85f, 0.2f), new Vector3(0.7f, 0.7f, 0.7f),
                    wander: 0f, flees: false, flee: 0f, weight: 1.5f), strength: 6f),
                Hunt(Food("rabbit", "산토끼", 6, FoodTrait.Meat, new Color(0.92f, 0.88f, 0.8f), new Vector3(0.35f, 0.35f, 0.6f),
                    wander: 2.5f, flees: true, flee: 10f, weight: 2.5f), strength: 3f, herdMin: 2, herdMax: 4),
                Hunt(Food("deer", "사슴", 16, FoodTrait.Meat, new Color(0.66f, 0.44f, 0.26f), new Vector3(0.6f, 1f, 1.4f),
                    wander: 1.2f, flees: true, flee: 9f, weight: 1.5f), strength: 13f, herdMin: 2, herdMax: 3, struggles: true),
                Hunt(Food("crab", "바위게", 7, FoodTrait.Fish, new Color(0.85f, 0.27f, 0.2f), new Vector3(0.7f, 0.3f, 0.6f),
                    wander: 0.6f, flees: false, flee: 0f, weight: 2f), strength: 4f, herdMin: 2, herdMax: 4),
                Hunt(Food("fire_goat", "화염염소", 14, FoodTrait.Fire, new Color(0.8f, 0.3f, 0.14f), new Vector3(0.6f, 0.8f, 1.1f),
                    wander: 1.4f, flees: true, flee: 7f, weight: 1f), strength: 12f, herdMin: 1, herdMax: 2, struggles: true),
                Hunt(Food("mushroom", "숲버섯", 5, FoodTrait.Plant, new Color(0.82f, 0.2f, 0.24f), new Vector3(0.5f, 0.6f, 0.5f),
                    wander: 0f, flees: false, flee: 0f, weight: 1.5f), strength: 2f, herdMin: 2, herdMax: 4),
            };
        }

        /// <summary>사냥 관련 값 (동물 힘 / 무리 / 버둥거림). 기존 에셋에는 한 번만 채운다</summary>
        static FoodData Hunt(FoodData f, float strength, int herdMin = 1, int herdMax = 1, bool struggles = false)
        {
            if (f.schema >= 2) return f;
            if (f.schema < 1)
            {
                f.herdMin = herdMin;
                f.herdMax = herdMax;
                f.struggles = struggles;
                f.scatterSpeed = 5f;
            }
            f.strength = strength; // 예전 무게(1 = 양)를 힘 단위(10 = 양)로
            f.schema = 2;
            EditorUtility.SetDirty(f);
            return f;
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
            public readonly Material JumpZone = Mat("JumpZone", new Color(1f, 0.8f, 0.25f));
            public readonly Material CaveRock = Mat("CaveRock", new Color(0.36f, 0.33f, 0.31f));
            public readonly Material JumpLine = Mat("JumpLine", new Color(1f, 1f, 0.95f));

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

        static void BuildWorld(Mats mats, FlightParams launch)
        {
            var world = new GameObject("World").transform;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(world);
            ground.transform.localScale = new Vector3(100, 1, 100); // 1km x 1km
            ground.GetComponent<Renderer>().sharedMaterial = mats.Grass;

            // 출발 절벽: 둥지 → 도움닫기 길 → 절벽 끝 (+z 방향)
            const float back = 8f; // 둥지 뒤 여유
            float cliffLength = RunUpLength + back;
            Prim(PrimitiveType.Cube, "Cliff", world, new Vector3(0, CliffHeight * 0.5f, CliffEdgeZ - cliffLength * 0.5f),
                new Vector3(14, CliffHeight, cliffLength), mats.Rock);
            Prim(PrimitiveType.Cylinder, "NestRing", world, new Vector3(0, CliffHeight + 0.15f, CliffEdgeZ - RunUpLength),
                new Vector3(4f, 0.3f, 4f), mats.Nest, keepCollider: false);
            // [임시] 둥지 동굴: 동굴 안에서 출발해 입구로 달려 나간다.
            // 따라오는 카메라(성체는 둥지 뒤 약 20m, 위 9m)가 안쪽에 들어가도록 뒤로 깊고 천장이 높다
            BuildCave(world, new Vector3(0, CliffHeight, CliffEdgeZ - RunUpLength), Vector3.forward,
                width: 12f, height: 10f, back: 24f, front: CaveMouthAhead, floorDepth: CliffHeight, mats, seed: 3);

            // 도약 구간 (노란 띠) + 절벽 끝 흰 선 (완벽 거리)
            float zone = launch.jumpZoneLength;
            Prim(PrimitiveType.Cube, "JumpZone", world, new Vector3(0, CliffHeight + 0.03f, CliffEdgeZ - zone * 0.5f),
                new Vector3(4f, 0.04f, zone), mats.JumpZone, keepCollider: false);
            float line = launch.perfectJumpDistance;
            Prim(PrimitiveType.Cube, "JumpLine", world, new Vector3(0, CliffHeight + 0.05f, CliffEdgeZ - line * 0.5f),
                new Vector3(4f, 0.05f, line), mats.JumpLine, keepCollider: false);

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

        // ───────────────────────── 동굴 (임시) ─────────────────────────

        /// <summary>
        /// [임시] 프리미티브로 만든 동굴. 나중에 실제 모델로 바꿀 자리표시용.
        /// floor = 둥지 자리 바닥, mouthDir = 입구가 바라보는 방향.
        /// 안쪽 공간: 폭 width, 높이 height, 둥지 뒤로 back, 둥지 앞 입구까지 front.
        /// floorDepth = 바닥 덩어리를 아래로 얼마나 채울지 (절벽 높이만큼 채우면 산처럼 보임).
        /// </summary>
        static Transform BuildCave(Transform parent, Vector3 floor, Vector3 mouthDir, float width, float height,
            float back, float front, float floorDepth, Mats mats, int seed)
        {
            var cave = new GameObject("Cave").transform;
            cave.SetParent(parent, false);
            mouthDir.y = 0f;
            cave.SetPositionAndRotation(floor, Quaternion.LookRotation(mouthDir.normalized, Vector3.up));

            var rng = new System.Random(seed);
            var mat = mats.CaveRock;
            const float wall = 2.5f;
            float depth = back + front;
            float midZ = (front - back) * 0.5f;
            float outerW = width + wall * 2f;

            // 바닥 덩어리 (절벽 위로 이어지는 산의 몸통)
            CavePart(PrimitiveType.Cube, "Base", cave, new Vector3(0, -floorDepth * 0.5f, midZ - wall * 0.5f), Vector3.zero,
                new Vector3(outerW, floorDepth, depth + wall), mat);
            // 양옆 벽, 안쪽 벽, 천장
            CavePart(PrimitiveType.Cube, "WallL", cave, new Vector3(-(width + wall) * 0.5f, height * 0.5f, midZ - wall * 0.5f), Vector3.zero,
                new Vector3(wall, height, depth + wall), mat);
            CavePart(PrimitiveType.Cube, "WallR", cave, new Vector3((width + wall) * 0.5f, height * 0.5f, midZ - wall * 0.5f), Vector3.zero,
                new Vector3(wall, height, depth + wall), mat);
            CavePart(PrimitiveType.Cube, "WallBack", cave, new Vector3(0, height * 0.5f, -back - wall * 0.5f), Vector3.zero,
                new Vector3(outerW, height, wall), mat);
            CavePart(PrimitiveType.Cube, "Ceiling", cave, new Vector3(0, height + wall * 0.5f, midZ - wall * 0.5f), Vector3.zero,
                new Vector3(outerW, wall, depth + wall), mat);

            // 입구 둘레 바위: 네모난 입구를 울퉁불퉁한 아치처럼
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                float x = Mathf.Lerp(-width * 0.5f, width * 0.5f, t);
                float s = Range(rng, 2.2f, 3.4f);
                CavePart(PrimitiveType.Sphere, $"MouthTop_{i}", cave, new Vector3(x, height + Range(rng, 0.3f, 0.9f), front),
                    Vector3.zero, new Vector3(s, s * 0.8f, s * 0.7f), mat);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    float y = height * (0.15f + 0.3f * i);
                    float s = Range(rng, 2f, 3.2f);
                    CavePart(PrimitiveType.Sphere, $"MouthSide_{(side < 0 ? "L" : "R")}{i}", cave,
                        new Vector3(side * (width * 0.5f + Range(rng, 0f, 0.4f)), y, front), Vector3.zero,
                        new Vector3(s * 0.7f, s, s * 0.7f), mat);
                }
            }

            // 바깥 혹: 상자 모양 실루엣을 깨는 큰 바위들 (천장 위, 양옆)
            int lumps = Mathf.RoundToInt(depth / 3f);
            for (int i = 0; i < lumps; i++)
            {
                float z = Range(rng, -back, front - 1f);
                float s = Range(rng, 4f, 7f);
                CavePart(PrimitiveType.Sphere, $"LumpTop_{i}", cave,
                    new Vector3(Range(rng, -width * 0.4f, width * 0.4f), height + wall + s * 0.15f, z), Vector3.zero,
                    new Vector3(s * 1.2f, s * 0.6f, s), mat);
                int side = rng.Next(2) == 0 ? -1 : 1;
                CavePart(PrimitiveType.Cube, $"LumpSide_{i}", cave,
                    new Vector3(side * (outerW * 0.5f + Range(rng, -0.5f, 0.3f)), Range(rng, 0.5f, height), z),
                    new Vector3(Range(rng, -20f, 20f), Range(rng, 0f, 90f), Range(rng, -20f, 20f)),
                    new Vector3(Range(rng, 2f, 3.5f), Range(rng, 2.5f, 5f), Range(rng, 2.5f, 4.5f)), mat);
            }

            // 종유석 (천장에서 매달린 뾰족 바위, 콜라이더 없음)
            int drips = Mathf.RoundToInt(depth / 2.5f);
            for (int i = 0; i < drips; i++)
            {
                float len = Range(rng, 0.8f, 2f);
                CavePart(PrimitiveType.Cube, $"Stalactite_{i}", cave,
                    new Vector3(Range(rng, -width * 0.42f, width * 0.42f), height - len * 0.5f, Range(rng, -back + 0.5f, front - 1.5f)),
                    new Vector3(0f, 45f, 0f), new Vector3(0.35f, len, 0.35f), mat, collider: false);
            }

            // 둥지를 비추는 따뜻한 불빛
            var lightGo = new GameObject("CaveGlow");
            lightGo.transform.SetParent(cave, false);
            lightGo.transform.localPosition = new Vector3(0f, Mathf.Min(3.5f, height * 0.5f), -0.5f);
            var glow = lightGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.72f, 0.42f);
            glow.intensity = 4f;
            glow.range = width * 1.8f;
            glow.shadows = LightShadows.None;

            return cave;
        }

        static GameObject CavePart(PrimitiveType type, string name, Transform cave, Vector3 localPos, Vector3 localEuler,
            Vector3 scale, Material mat, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(cave, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static Vector3 RandomPoint(System.Random rng, float keepOut)
        {
            while (true)
            {
                var p = new Vector3(Range(rng, -200, 200), 0, Range(rng, -40, 420));
                bool inCave = Mathf.Abs(p.x) < 15f && p.z < CliffEdgeZ;
                if (!inCave && new Vector2(p.x, p.z).magnitude > keepOut) return p;
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

            root.AddComponent<GroundImpactFx>().controller = ctrl; // [임시] 지면에 닿을 때 흙 튐
            root.AddComponent<DragonLook>().model = model; // 먹은 먹이에 따라 몸 색 / 성체 모습
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
            hud.staminaLabel = MakeText(barBg.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(0, -36), new Vector2(400, 40), 24, TextAnchor.MiddleCenter);
            hud.staminaLabel.text = "스태미나";

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
            hud.flapButtonLabel = MakeText(flapBtn.transform, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 80), 40, TextAnchor.MiddleCenter);
            hud.flapButtonLabel.text = "날갯짓";

            // 웅크리기 버튼 (이륙 전에만 보임): 날갯짓 버튼 바로 위
            var crouchBtn = MakeImage(mobile.transform, "CrouchButton", new Vector2(1, 0), new Vector2(-240, 480), new Vector2(200, 200), new Color(1f, 0.85f, 0.3f, 0.5f));
            crouchBtn.sprite = stickBg.sprite;
            SetControlPath(crouchBtn.gameObject.AddComponent<OnScreenButton>(), "<Gamepad>/buttonEast");
            MakeText(crouchBtn.transform, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190, 80), 34, TextAnchor.MiddleCenter).text = "웅크리기";
            hud.crouchButton = crouchBtn.gameObject;

            // 먹기 버튼 (먹이를 매달고 날 때만 보임): 날갯짓 버튼 왼쪽
            var eatBtn = MakeImage(mobile.transform, "EatButton", new Vector2(1, 0), new Vector2(-500, 170), new Vector2(170, 170), new Color(1f, 0.55f, 0.4f, 0.5f));
            eatBtn.sprite = stickBg.sprite;
            SetControlPath(eatBtn.gameObject.AddComponent<OnScreenButton>(), "<Gamepad>/buttonWest");
            var eatIcon = MakeImage(eatBtn.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96, 96), Color.white);
            eatIcon.sprite = _icons.meat;
            eatIcon.raycastTarget = false;

            var carryHud = canvasGo.AddComponent<CarryHUD>();
            carryHud.session = session;
            carryHud.icons = _icons;
            carryHud.eatButton = eatBtn.gameObject;
            carryHud.carryIcons = MakeFlow(ct, "CarryIcons", new Vector2(0.5f, 1), new Vector2(0, -190), new Vector2(900, 70), TextAnchor.MiddleCenter, 52f, 34);
            carryHud.comboIcons = MakeFlow(ct, "ComboIcons", new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(900, 90), TextAnchor.MiddleCenter, 64f, 56);

            // 농장 버프: 일시정지 버튼 아래
            var buffHud = canvasGo.AddComponent<BuffHUD>();
            buffHud.icons = _icons;
            buffHud.flow = MakeFlow(ct, "BuffIcons", new Vector2(1, 1), new Vector2(-40, -140), new Vector2(700, 70), TextAnchor.UpperRight, 48f, 30);

            var vis = canvasGo.AddComponent<MobileControlsVisibility>();
            vis.controlsRoot = mobile;

            // 결과 패널
            var resultPanel = MakeImage(ct, "ResultPanel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 900), new Color(0.08f, 0.1f, 0.14f, 0.88f));
            var result = canvasGo.AddComponent<FlightResultPanel>();
            result.session = session;
            result.panel = resultPanel.gameObject;
            result.icons = _icons;
            result.titleIcons = MakeFlow(resultPanel.transform, "TitleIcons", new Vector2(0.5f, 1), new Vector2(0, -75), new Vector2(860, 110), TextAnchor.MiddleCenter, 60f, 40);
            result.bodyIcons = MakeFlow(resultPanel.transform, "BodyIcons", new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(860, 580), TextAnchor.UpperLeft, 46f, 34);

            var btnImg = MakeImage(resultPanel.transform, "RetryButton", new Vector2(0.5f, 0), new Vector2(-190, 80), new Vector2(340, 100), new Color(1f, 0.75f, 0.35f));
            btnImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            btnImg.type = Image.Type.Sliced;
            var btn = btnImg.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            var label = MakeText(btnImg.transform, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340, 90), 40, TextAnchor.MiddleCenter);
            label.text = "다시 날기";
            label.color = new Color(0.15f, 0.1f, 0.05f);
            result.retryButton = btn;
            result.nestButton = MakeButton(resultPanel.transform, "NestButton", new Vector2(0.5f, 0), new Vector2(190, 80), new Vector2(340, 100),
                "둥지로 돌아가기", new Color(0.55f, 0.8f, 1f), new Color(0.05f, 0.1f, 0.15f), out _);
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
            var box = MakeImage(dim.transform, "Box", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 760), new Color(0.08f, 0.1f, 0.14f, 0.92f));
            var title = MakeText(box.transform, "Title", new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(680, 90), 56, TextAnchor.MiddleCenter);
            title.text = "일시정지";

            var btnColor = new Color(1f, 0.75f, 0.35f);
            var textColor = new Color(0.15f, 0.1f, 0.05f);
            menu.resumeButton = MakeButton(box.transform, "Resume", new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(480, 100), "계속하기", btnColor, textColor, out _);
            menu.invertButton = MakeButton(box.transform, "InvertPitch", new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(480, 100), "상/하 반전: 꺼짐", btnColor, textColor, out menu.invertLabel);
            menu.restartButton = MakeButton(box.transform, "Restart", new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(480, 100), "처음부터 다시 날기", btnColor, textColor, out _);
            menu.nestButton = MakeButton(box.transform, "GiveUp", new Vector2(0.5f, 0.5f), new Vector2(0, -190), new Vector2(480, 100), "비행 포기 (둥지로)", new Color(0.55f, 0.8f, 1f), textColor, out _);
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
