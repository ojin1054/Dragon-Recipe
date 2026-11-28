using System.Collections;
using System.Collections.Generic;
using DragonRecipe.Care;
using DragonRecipe.Tribes;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DragonRecipe.UI
{
    /// <summary>
    /// 둥지 화면을 단순하게 다시 배치한다 (씬은 그대로 두고 실행 중에 옮김).
    /// - 항상 보이는 것: 왼쪽 위 허기·성장 게이지, 왼쪽 아래 둥근 버튼 5개(창고·우리·종족·도감·업그레이드), 오른쪽 아래 비행 버튼.
    /// - 상태 패널·창고·종족 패널은 눌렀을 때만 열린다 (한 번에 하나).
    /// - 디버그 버튼(+1시간·저장 초기화·우리 확장·DEV·WAR)은 "디버그" 버튼 하나로 여는 팝업에 모은다 (에디터 / 개발 빌드에서만).
    /// 둥지 씬 캔버스에 스스로 붙는다.
    /// </summary>
    public class NestHudLayout : MonoBehaviour
    {
        static readonly Color Chip = new Color(0.1f, 0.12f, 0.16f, 0.8f);
        static readonly Color ChipOn = new Color(1f, 0.72f, 0.3f, 0.95f);
        static readonly Color BarBack = new Color(1f, 1f, 1f, 0.18f);
        static readonly Color HungerOk = new Color(0.55f, 0.85f, 0.4f);
        static readonly Color HungerLow = new Color(1f, 0.75f, 0.3f);
        static readonly Color HungerEmpty = new Color(1f, 0.4f, 0.35f);
        static readonly Color Growth = new Color(0.36f, 0.79f, 0.65f);
        static readonly Color Alert = new Color(0.89f, 0.29f, 0.29f);

        const float ButtonSize = 110f;
        const float ButtonGap = 20f;
        const float Margin = 40f;

        class Popup
        {
            public CanvasGroup group;
            public Image button;
        }

        NestUI _nest;
        FarmPanel _farm;
        CanvasGroup _hud;
        RectTransform _hungerFill, _growthFill;
        Image _hungerImage;
        Image _tribeDot;
        readonly List<Popup> _popups = new List<Popup>();
        Popup _open;
        GameObject _debugPanel;
        Sprite _round;
        Font _font;

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
            if (FindAnyObjectByType<NestHudLayout>()) return;
            var nest = FindAnyObjectByType<NestUI>();
            if (nest) nest.gameObject.AddComponent<NestHudLayout>()._nest = nest;
        }

        // ───────── 만들기 ─────────

        IEnumerator Start()
        {
            // 스스로 붙는 다른 패널(종족·업그레이드·DEV·WAR)이 Start에서 버튼을 만든 뒤에 옮긴다
            yield return null;
            if (!_nest) _nest = GetComponent<NestUI>();
            if (!_nest) yield break;
            _farm = GetComponent<FarmPanel>();
            _round = RoundSprite();
            var anyText = GetComponentInChildren<Text>(true);
            _font = anyText && anyText.font ? anyText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var hud = NewRect("NestHud", transform);
            Stretch(hud);
            hud.SetSiblingIndex(0);
            _hud = hud.gameObject.AddComponent<CanvasGroup>();

            BuildGauges(hud, out var gaugeButton);
            BuildButtons(hud, gaugeButton);
            BuildDebug(hud);
        }

        void BuildGauges(RectTransform hud, out Image gaugeButton)
        {
            // 게이지 묶음 전체가 버튼: 누르면 자세한 상태 패널이 열린다
            var box = NewRect("Gauges", hud);
            Place(box, new Vector2(0, 1), new Vector2(Margin, -Margin), new Vector2(430f, 112f), new Vector2(0, 1));
            gaugeButton = box.gameObject.AddComponent<Image>();
            gaugeButton.sprite = _round;
            gaugeButton.type = Image.Type.Sliced;
            gaugeButton.color = Chip;

            var icons = _nest.icons;
            _hungerFill = MakeGauge(box, icons ? icons.meat : null, -32f, HungerOk, out _hungerImage);
            _growthFill = MakeGauge(box, icons ? icons.sprout : null, -80f, Growth, out _);
        }

        RectTransform MakeGauge(RectTransform parent, Sprite icon, float y, Color color, out Image fillImage)
        {
            var ic = NewRect("Icon", parent).gameObject.AddComponent<Image>();
            Place(ic.rectTransform, new Vector2(0, 1), new Vector2(44f, y), new Vector2(42f, 42f), new Vector2(0.5f, 0.5f));
            ic.sprite = icon;
            ic.preserveAspect = true;
            ic.raycastTarget = false;
            ic.enabled = icon;

            var back = NewRect("Bar", parent).gameObject.AddComponent<Image>();
            Place(back.rectTransform, new Vector2(0, 1), new Vector2(80f, y), new Vector2(320f, 20f), new Vector2(0, 0.5f));
            back.sprite = _round;
            back.type = Image.Type.Sliced;
            back.color = BarBack;
            back.raycastTarget = false;

            fillImage = NewRect("Fill", back.rectTransform).gameObject.AddComponent<Image>();
            var fill = fillImage.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.5f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            fillImage.sprite = _round;
            fillImage.type = Image.Type.Sliced;
            fillImage.color = color;
            fillImage.raycastTarget = false;
            return fill;
        }

        void BuildButtons(RectTransform hud, Image gaugeButton)
        {
            var canvas = transform;
            var icons = _nest.icons;

            // 상태 패널 + 지난 비행 결과: 게이지를 누르면 열림
            var status = NewPopup("StatusPopup", hud);
            Adopt(canvas.Find("StatusPanel"), status, new Vector2(0, 1), new Vector2(Margin, -170f), new Vector2(0, 1));
            Adopt(canvas.Find("ReportIcons"), status, new Vector2(0, 0), new Vector2(Margin, Margin + ButtonSize + 30f), new Vector2(0, 0));
            Bind(status, gaugeButton);

            // 창고: 버튼 줄 바로 위에 열림
            var storage = NewPopup("StoragePopup", hud);
            Adopt(canvas.Find("StoragePanel"), storage, new Vector2(0, 0), new Vector2(Margin, Margin + ButtonSize + 30f), new Vector2(0, 0));
            Bind(storage, MakeRound(hud, "StorageButton", 0, icons ? icons.basket : null));

            // 우리·도감: 원래 버튼을 줄에 맞춰 옮긴다 (누르면 각자 패널이 열림)
            MoveRound(canvas.Find("FarmButton"), 1);

            // 종족: 호감도·제단·전쟁 카드. 참전 요청이 오면 버튼에 빨간 점
            var tribe = NewPopup("TribePopup", hud);
            var tribePanel = FindAnyObjectByType<TribePanel>();
            if (tribePanel) tribePanel.transform.SetParent(tribe.group.transform, false);
            var tribeButton = MakeRound(hud, "TribeButton", 2, TribeLook.Face(Tribe.Human));
            Bind(tribe, tribeButton);
            _tribeDot = NewRect("Dot", tribeButton.rectTransform).gameObject.AddComponent<Image>();
            Place(_tribeDot.rectTransform, new Vector2(1, 1), new Vector2(-10f, -10f), new Vector2(26f, 26f), new Vector2(0.5f, 0.5f));
            _tribeDot.sprite = _round;
            _tribeDot.color = Alert;
            _tribeDot.raycastTarget = false;
            _tribeDot.enabled = false;

            MoveRound(canvas.Find("CodexButton"), 3);

            // 업그레이드: UpgradePanel이 만든 버튼을 같은 줄 다섯 번째로. HUD 안에 넣어 우리를 열면 같이 숨는다
            var upgrade = canvas.Find("UpgradeButton");
            if (upgrade)
            {
                upgrade.SetParent(hud, false);
                MoveRound(upgrade, 4);
            }
        }

        /// <summary>흩어진 디버그 버튼을 팝업 하나로 모은다. 디버그 버튼이 하나도 없으면(정식 빌드) 아무것도 만들지 않는다</summary>
        void BuildDebug(RectTransform hud)
        {
            var found = new List<(Button button, string label)>();
            void Take(Transform t, string label)
            {
                var b = t ? t.GetComponent<Button>() : null;
                if (b && b.gameObject.activeInHierarchy) found.Add((b, label));
            }
            var debug = _nest.debugRoot ? _nest.debugRoot.transform : null;
            if (debug)
            {
                Take(debug.Find("Advance1h"), null);
                Take(debug.Find("ResetSave"), null);
                Take(debug.Find("UpgradePen"), null);
            }
            Take(transform.Find("DevButton"), "드래곤 고르기");
            Take(transform.Find("WarDebugButton"), "종족 · 전쟁");
            if (found.Count == 0) return;

            var dim = NewRect("DebugPopup", transform).gameObject.AddComponent<Image>();
            Stretch(dim.rectTransform);
            dim.color = new Color(0f, 0f, 0f, 0.6f);
            _debugPanel = dim.gameObject;

            int rows = (found.Count + 1) / 2;
            var box = NewRect("Box", dim.rectTransform).gameObject.AddComponent<Image>();
            box.rectTransform.sizeDelta = new Vector2(820f, 230f + rows * 100f);
            box.color = new Color(0.08f, 0.1f, 0.14f, 0.97f);
            float top = box.rectTransform.sizeDelta.y * 0.5f;

            var title = MakeLabel(box.rectTransform, "디버그", 38);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, top - 60f), new Vector2(700f, 60f), new Vector2(0.5f, 0.5f));

            for (int i = 0; i < found.Count; i++)
            {
                var (button, label) = found[i];
                var rt = (RectTransform)button.transform;
                rt.SetParent(box.rectTransform, false);
                Place(rt, new Vector2(0.5f, 0.5f), new Vector2(i % 2 == 0 ? -195f : 195f, top - 160f - (i / 2) * 100f),
                    new Vector2(360f, 80f), new Vector2(0.5f, 0.5f));
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;
                var img = button.GetComponent<Image>();
                if (img) img.color = new Color(0.22f, 0.25f, 0.32f, 0.95f);
                if (label != null)
                {
                    // 다른 패널을 여는 버튼: 이름을 풀어 쓰고, 누르면 이 팝업은 닫는다
                    var text = button.GetComponentInChildren<Text>(true);
                    if (text) text.text = label;
                    button.onClick.AddListener(() => _debugPanel.SetActive(false));
                }
            }

            var close = MakeTextButton(box.rectTransform, "닫기", new Vector2(0f, -top + 60f), new Vector2(260f, 70f), 30);
            close.onClick.AddListener(() => _debugPanel.SetActive(false));
            _debugPanel.SetActive(false);

            // 여는 버튼: 화면 아래 가운데에 작게
            var open = MakeTextButton(hud, "디버그", Vector2.zero, new Vector2(170f, 52f), 26);
            Place((RectTransform)open.transform, new Vector2(0.5f, 0), new Vector2(0f, 24f), new Vector2(170f, 52f), new Vector2(0.5f, 0));
            open.GetComponent<Image>().color = new Color(1f, 0.6f, 0.3f, 0.3f);
            open.onClick.AddListener(() =>
            {
                _debugPanel.transform.SetAsLastSibling();
                _debugPanel.SetActive(true);
            });
        }

        // ───────── 갱신 ─────────

        void Update()
        {
            if (!_hud || !DragonCare.IsLoaded) return;

            // 우리(농장)를 연 동안에는 둥지 HUD를 감춘다
            bool farm = _farm && _farm.IsOpen;
            _hud.alpha = farm ? 0f : 1f;
            _hud.blocksRaycasts = _hud.interactable = !farm;

            var s = DragonCare.State;
            var c = DragonCare.Care;
            if (_hungerFill)
            {
                _hungerFill.anchorMax = new Vector2(c.maxHunger > 0 ? Mathf.Clamp01(s.hunger / c.maxHunger) : 0f, 1f);
                _hungerImage.color = CareLogic.IsStarving(s) ? HungerEmpty : CareLogic.IsHungry(s, c) ? HungerLow : HungerOk;
            }
            if (_growthFill) _growthFill.anchorMax = new Vector2(Mathf.Clamp01(CareLogic.StageProgress(s, c)), 1f);

            if (_tribeDot)
            {
                var war = s.tribes?.war;
                bool asking = war != null && war.status == WarStatus.Requested;
                _tribeDot.enabled = asking;
                if (asking) _tribeDot.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.15f);
            }
        }

        void Toggle(Popup p)
        {
            var next = _open == p ? null : p;
            _open = next;
            foreach (var x in _popups) Show(x, x == next);
        }

        static void Show(Popup p, bool on)
        {
            p.group.alpha = on ? 1f : 0f;
            p.group.blocksRaycasts = p.group.interactable = on;
            if (p.button) p.button.color = on ? ChipOn : Chip;
        }

        // ───────── 도우미 ─────────

        Popup NewPopup(string name, RectTransform hud)
        {
            var rt = NewRect(name, hud);
            Stretch(rt);
            var p = new Popup { group = rt.gameObject.AddComponent<CanvasGroup>() };
            _popups.Add(p);
            Show(p, false);
            return p;
        }

        void Bind(Popup p, Image button)
        {
            p.button = button;
            var b = button.GetComponent<Button>();
            if (!b) b = button.gameObject.AddComponent<Button>();
            b.targetGraphic = button;
            b.onClick.AddListener(() => Toggle(p));
            Show(p, false);
        }

        /// <summary>원래 있던 패널을 팝업 묶음 안으로 옮기고 자리를 잡는다 (크기는 그대로)</summary>
        static void Adopt(Transform t, Popup p, Vector2 anchor, Vector2 pos, Vector2 pivot)
        {
            if (!t) return;
            var rt = (RectTransform)t;
            rt.SetParent(p.group.transform, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
        }

        static Vector2 SlotPos(int index) => new Vector2(Margin + index * (ButtonSize + ButtonGap), Margin);

        Image MakeRound(RectTransform parent, string name, int slot, Sprite icon)
        {
            var img = NewRect(name, parent).gameObject.AddComponent<Image>();
            Place(img.rectTransform, new Vector2(0, 0), SlotPos(slot), new Vector2(ButtonSize, ButtonSize), new Vector2(0, 0));
            img.sprite = _round;
            img.color = Chip;
            var ic = NewRect("Icon", img.rectTransform).gameObject.AddComponent<Image>();
            ic.rectTransform.sizeDelta = Vector2.one * ButtonSize * 0.6f;
            ic.sprite = icon;
            ic.preserveAspect = true;
            ic.raycastTarget = false;
            ic.enabled = icon;
            return img;
        }

        /// <summary>씬에 있던 아이콘 버튼(우리·도감)을 둥글게 바꿔 버튼 줄로 옮긴다</summary>
        void MoveRound(Transform t, int slot)
        {
            if (!t) return;
            var rt = (RectTransform)t;
            Place(rt, new Vector2(0, 0), SlotPos(slot), new Vector2(ButtonSize, ButtonSize), new Vector2(0, 0));
            var img = t.GetComponent<Image>();
            if (img)
            {
                img.sprite = _round;
                img.type = Image.Type.Simple;
                img.color = Chip;
            }
            var icon = t.Find("Icon") as RectTransform;
            if (icon) icon.sizeDelta = Vector2.one * ButtonSize * 0.6f;
        }

        Button MakeTextButton(RectTransform parent, string text, Vector2 pos, Vector2 size, int fontSize)
        {
            var img = NewRect(text, parent).gameObject.AddComponent<Image>();
            Place(img.rectTransform, new Vector2(0.5f, 0.5f), pos, size, new Vector2(0.5f, 0.5f));
            img.color = new Color(0.22f, 0.25f, 0.32f, 0.95f);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var label = MakeLabel(img.rectTransform, text, fontSize);
            Stretch(label.rectTransform);
            return b;
        }

        Text MakeLabel(RectTransform parent, string text, int fontSize)
        {
            var t = NewRect("Text", parent).gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = fontSize;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = text;
            t.raycastTarget = false;
            return t;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        /// <summary>둥근 버튼·알약 모양 게이지에 쓰는 원 (가장자리를 늘려 쓰면 알약 모양이 된다)</summary>
        static Sprite RoundSprite()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d));
            }
            tex.SetPixels32(px);
            tex.Apply();
            float b = r - 1f;
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }
    }
}
