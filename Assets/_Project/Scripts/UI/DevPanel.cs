using System;
using System.Collections.Generic;
using DragonRecipe.Care;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DragonRecipe.UI
{
    /// <summary>
    /// 개발자 모드 (에디터 / 개발 빌드에서만): 새끼·청소년(성질별)·성체(모습별) 드래곤을 골라 바로 써 본다.
    /// 처음 바꿀 때 원래 드래곤을 백업하고, "원래 드래곤" 버튼으로 되돌린다.
    /// 왼쪽 아래 DEV 버튼이나 F2로 연다. 둥지·비행 씬 캔버스에 스스로 붙는다.
    /// </summary>
    public class DevPanel : MonoBehaviour
    {
        struct Choice
        {
            public string label;
            public GrowthStage stage;
            public FoodTrait trait;
            public DragonForm form;
        }

        static readonly Choice[][] Rows =
        {
            new[] { new Choice { label = "새끼", stage = GrowthStage.Hatchling } },
            new[]
            {
                new Choice { label = "청소년\n고기", stage = GrowthStage.Juvenile, trait = FoodTrait.Meat },
                new Choice { label = "청소년\n물고기", stage = GrowthStage.Juvenile, trait = FoodTrait.Fish },
                new Choice { label = "청소년\n불", stage = GrowthStage.Juvenile, trait = FoodTrait.Fire },
                new Choice { label = "청소년\n식물", stage = GrowthStage.Juvenile, trait = FoodTrait.Plant },
            },
            new[]
            {
                new Choice { label = "성체\n사냥꾼", stage = GrowthStage.Adult, form = DragonForm.Hunter },
                new Choice { label = "성체\n물빛", stage = GrowthStage.Adult, form = DragonForm.Tide },
                new Choice { label = "성체\n불꽃", stage = GrowthStage.Adult, form = DragonForm.Ember },
                new Choice { label = "성체\n잎사귀", stage = GrowthStage.Adult, form = DragonForm.Grove },
                new Choice { label = "성체\n무지개", stage = GrowthStage.Adult, form = DragonForm.Prism },
            }
        };

        static readonly Color BtnIdle = new Color(0.22f, 0.25f, 0.32f, 0.95f);
        static readonly Color BtnOn = new Color(1f, 0.72f, 0.3f, 1f);
        static readonly Color BtnOff = new Color(0.15f, 0.16f, 0.2f, 0.6f);

        Font _font;
        RectTransform _panel;
        Button _restore;
        Image _devImage;
        readonly List<(Choice c, Image img)> _buttons = new List<(Choice, Image)>();
        InputAction _toggle;

        // ───────── 자동 부착 ─────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

        static void Attach()
        {
            if (!(Application.isEditor || Debug.isDebugBuild)) return;
            if (FindAnyObjectByType<DevPanel>()) return;
            Component host = FindAnyObjectByType<NestUI>();
            if (!host) host = FindAnyObjectByType<FlightHUD>();
            if (!host) return;
            var canvas = host.GetComponentInParent<Canvas>();
            if (canvas) canvas.gameObject.AddComponent<DevPanel>();
        }

        // ───────── 수명 ─────────

        void Awake()
        {
            _toggle = new InputAction("DevPanel", InputActionType.Button, "<Keyboard>/f2");
            _toggle.performed += _ => Toggle();
        }

        void OnEnable() => _toggle.Enable();
        void OnDisable() => _toggle.Disable();
        void OnDestroy() => _toggle.Dispose();

        void Start()
        {
            var anyText = GetComponentInChildren<Text>(true);
            _font = anyText && anyText.font ? anyText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        void Build()
        {
            // 여는 버튼 (왼쪽 아래)
            var dev = MakeButton(transform, "DevButton", "DEV", new Vector2(0f, 0f), new Vector2(80f, 34f), new Vector2(130f, 52f), 26, Toggle);
            _devImage = dev.GetComponent<Image>();

            // 패널
            var go = new GameObject("DevPanel", typeof(RectTransform), typeof(Image));
            _panel = (RectTransform)go.transform;
            _panel.SetParent(transform, false);
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(1000f, 560f);
            go.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.1f, 0.94f);

            MakeLabel(_panel, "개발자 모드: 드래곤 고르기", new Vector2(0f, 235f), new Vector2(900f, 60f), 36);

            const float w = 170f, h = 100f, gap = 18f;
            for (int r = 0; r < Rows.Length; r++)
            {
                var row = Rows[r];
                float total = row.Length * w + (row.Length - 1) * gap;
                for (int i = 0; i < row.Length; i++)
                {
                    var choice = row[i];
                    var pos = new Vector2(-total * 0.5f + w * 0.5f + i * (w + gap), 130f - r * (h + 24f));
                    var b = MakeButton(_panel, "Pick", choice.label, new Vector2(0.5f, 0.5f), pos, new Vector2(w, h), 28, () => Pick(choice));
                    _buttons.Add((choice, b.GetComponent<Image>()));
                }
            }

            _restore = MakeButton(_panel, "Restore", "원래 드래곤", new Vector2(0.5f, 0.5f), new Vector2(-150f, -225f), new Vector2(260f, 70f), 28, Restore);
            MakeButton(_panel, "Close", "닫기", new Vector2(0.5f, 0.5f), new Vector2(150f, -225f), new Vector2(260f, 70f), 28, Toggle);

            _panel.gameObject.SetActive(false);
            Refresh();
        }

        void Toggle()
        {
            if (!_panel) return;
            _panel.gameObject.SetActive(!_panel.gameObject.activeSelf);
            _panel.SetAsLastSibling();
            Refresh();
        }

        void Pick(Choice c)
        {
            DragonCare.DevSetDragon(c.stage, c.trait, c.form);
            Reload();
        }

        void Restore()
        {
            DragonCare.DevRestore();
            Reload();
        }

        /// <summary>바뀐 드래곤이 크기·비행 수치·스킬까지 깔끔하게 반영되도록 지금 씬을 다시 연다</summary>
        static void Reload()
        {
            if (Core.GamePause.IsPaused) Core.GamePause.Set(false);
            var scene = SceneManager.GetActiveScene();
            // 빌드 설정에 없는 씬(에디터에서 바로 연 씬)은 이름으로도 못 여니 그대로 둔다
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
        }

        void Refresh()
        {
            if (!DragonCare.IsLoaded) return;
            var s = DragonCare.State;
            bool has = CareLogic.DominantTrait(s, out var trait, out _);
            foreach (var (c, img) in _buttons)
            {
                bool on = c.stage == s.stage && c.stage switch
                {
                    GrowthStage.Juvenile => has && c.trait == trait,
                    GrowthStage.Adult => c.form == s.form,
                    _ => true
                };
                img.color = on ? BtnOn : BtnIdle;
            }
            if (_restore)
            {
                _restore.interactable = DragonCare.DevModeActive;
                _restore.GetComponent<Image>().color = DragonCare.DevModeActive ? BtnIdle : BtnOff;
            }
            if (_devImage) _devImage.color = DragonCare.DevModeActive ? BtnOn : new Color(1f, 1f, 1f, 0.25f);
        }

        // ───────── UI 만들기 ─────────

        Button MakeButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = BtnIdle;
            var b = go.GetComponent<Button>();
            b.onClick.AddListener(() => onClick());
            var t = MakeLabel(rt, label, Vector2.zero, size, fontSize);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            return b;
        }

        Text MakeLabel(Transform parent, string text, Vector2 pos, Vector2 size, int fontSize)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = fontSize;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = text;
            t.raycastTarget = false;
            return t;
        }
    }
}
