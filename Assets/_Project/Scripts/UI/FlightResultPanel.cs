using DragonRecipe.Care;
using DragonRecipe.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DragonRecipe.UI
{
    /// <summary>
    /// 비행이 끝나면 결과를 보여주고, 다시 날기 / 둥지로 돌아가기.
    /// 착지·추락 장면을 가리지 않게 화면 아래 띠로 보여 준다:
    /// - 위 가운데: 결과 배지 (안전 착지 = 초록, 추락 = 빨강)
    /// - 아래 띠 왼쪽: 기록과 잡은 먹이 두 줄, 그 아래 포만도 게이지 (이번 비행으로 찬 만큼 초록)
    /// - 아래 띠 오른쪽: 둥지로 / 다시 날기
    /// 씬에 있는 패널·아이콘 줄·버튼을 실행할 때 이 배치로 다시 놓는다 (씬을 다시 만들 필요 없음).
    /// </summary>
    public class FlightResultPanel : MonoBehaviour
    {
        public FlightSession session;
        public GameObject panel;
        [Tooltip("결과 배지 (착지 / 추락)")]
        public IconFlow titleIcons;
        [Tooltip("기록·먹이·성장·부상 아이콘 줄")]
        public IconFlow bodyIcons;
        public UIIconSet icons;
        public Button retryButton;
        public Button nestButton;
        [Tooltip("결과 화면이 뜨기까지의 지연 (충돌 연출을 보여주려고)")]
        public float showDelay = 1.2f;

        [Header("하단 띠 배치 (1920x1080 기준)")]
        public float stripHeight = 230f;
        public float stripMargin = 36f;
        [Tooltip("띠 오른쪽 버튼 자리 폭")]
        public float buttonArea = 600f;

        static readonly Color StripColor = new Color(0.086f, 0.118f, 0.173f, 0.88f);
        static readonly Color LandedColor = new Color(0.11f, 0.62f, 0.46f, 0.95f);
        static readonly Color CrashedColor = new Color(0.82f, 0.25f, 0.25f, 0.95f);
        static readonly Color GaugeBack = new Color(1f, 1f, 1f, 0.14f);
        static readonly Color GaugeBefore = new Color(0.54f, 0.63f, 0.76f);
        static readonly Color GaugeGain = new Color(0.36f, 0.79f, 0.65f);

        float _showAt = -1f;
        Image _badge;
        RectTransform _gauge, _gaugeBefore, _gaugeGain;

        void Awake()
        {
            if (retryButton) retryButton.onClick.AddListener(OnRetry);
            if (nestButton) nestButton.onClick.AddListener(SceneFlow.GoToNest);
            BuildStrip();
            if (panel) panel.SetActive(false);
        }

        void OnEnable()
        {
            if (session) session.StateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            if (session) session.StateChanged -= OnStateChanged;
        }

        void OnStateChanged(FlightSession.State s)
        {
            if (s == FlightSession.State.Ended)
            {
                _showAt = Time.time + showDelay;
            }
            else
            {
                _showAt = -1f;
                if (panel) panel.SetActive(false);
            }
        }

        void Update()
        {
            if (_showAt > 0f && Time.time >= _showAt)
            {
                _showAt = -1f;
                Show(session.Result);
            }
        }

        void Show(FlightResult r)
        {
            if (!panel) return;
            panel.SetActive(true);

            // 드래곤 상태에 반영된 결과 (먹이 섭취 / 성장 / 부상)는 DragonCare가 이미 계산해 둠
            var report = DragonCare.LastFlightReport;
            if (_badge)
            {
                _badge.color = CrashedColor;
                _badge.gameObject.SetActive(r.Injured); // 무사히 내리면 배지 없음
            }
            if (icons)
            {
                if (titleIcons) CareIcons.ResultTitle(titleIcons, icons, r);
                if (bodyIcons) CareIcons.ResultStrip(bodyIcons, icons, r, report);
            }
            ShowHunger(report);

            if (retryButton && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
        }

        /// <summary>포만도 게이지: 회색 = 비행 전, 초록 = 이번 비행에서 먹어서 찬 만큼</summary>
        void ShowHunger(FlightCareReport report)
        {
            if (!_gauge) return;
            bool known = DragonCare.IsLoaded && DragonCare.Config.care.maxHunger > 0f;
            _gauge.gameObject.SetActive(known);
            if (!known) return;

            float max = DragonCare.Config.care.maxHunger;
            float after = Mathf.Clamp01(DragonCare.State.hunger / max);
            float gain = report != null ? Mathf.Clamp01(report.absorbed / max) : 0f;
            float before = Mathf.Max(0f, after - gain);
            SetSpan(_gaugeBefore, 0f, before);
            SetSpan(_gaugeGain, before, after);
        }

        static void SetSpan(RectTransform rt, float from, float to)
        {
            rt.anchorMin = new Vector2(from, 0f);
            rt.anchorMax = new Vector2(Mathf.Max(from, to), 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void OnRetry()
        {
            if (panel) panel.SetActive(false);
            session.ResetFlight();
        }

        // ───────── 배치 ─────────

        void BuildStrip()
        {
            if (!panel || _badge) return;
            var root = (RectTransform)panel.transform;
            // 둥근 모서리 스프라이트는 씬의 버튼 것을 빌려 쓴다
            Sprite round = retryButton && retryButton.targetGraphic is Image bi ? bi.sprite : null;

            // 원래 패널은 화면 전체를 덮는 투명한 그릇으로
            Place(root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var rootImg = panel.GetComponent<Image>();
            if (rootImg)
            {
                rootImg.color = Color.clear;
                rootImg.raycastTarget = false;
            }

            // 위 가운데 배지
            _badge = NewImage(root, "Badge", LandedColor, round);
            Place(_badge.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-230f, -stripMargin - 96f), new Vector2(230f, -stripMargin));
            if (titleIcons)
            {
                Restyle(titleIcons, _badge.rectTransform, TextAnchor.MiddleCenter, 52f, 40);
                Place((RectTransform)titleIcons.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            }

            // 아래 띠
            var strip = NewImage(root, "Strip", StripColor, round).rectTransform;
            Place(strip, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(stripMargin, stripMargin), new Vector2(-stripMargin, stripMargin + stripHeight));

            if (bodyIcons)
            {
                Restyle(bodyIcons, strip, TextAnchor.UpperLeft, 50f, 36);
                bodyIcons.rowSpacing = 10f;
                Place((RectTransform)bodyIcons.transform, Vector2.zero, Vector2.one, new Vector2(0f, 1f),
                    new Vector2(32f, 62f), new Vector2(-buttonArea, -22f));
            }

            // 포만도 게이지 (띠 왼쪽 아래)
            _gauge = new GameObject("HungerGauge", typeof(RectTransform)).GetComponent<RectTransform>();
            _gauge.SetParent(strip, false);
            Place(_gauge, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(32f, 22f), new Vector2(-buttonArea, 50f));
            if (icons && icons.meat)
            {
                var meat = NewImage(_gauge, "Icon", Color.white, icons.meat);
                meat.type = Image.Type.Simple;
                meat.preserveAspect = true;
                Place(meat.rectTransform, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, -8f), new Vector2(44f, 8f));
            }
            var back = NewImage(_gauge, "Back", GaugeBack, round).rectTransform;
            Place(back, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(60f, 4f), new Vector2(0f, -4f));
            _gaugeBefore = NewImage(back, "Before", GaugeBefore, null).rectTransform;
            _gaugeGain = NewImage(back, "Gain", GaugeGain, null).rectTransform;

            // 버튼 (띠 오른쪽, 세로 가운데)
            if (retryButton)
            {
                var rt = (RectTransform)retryButton.transform;
                rt.SetParent(strip, false);
                Place(rt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-32f - 310f, -55f), new Vector2(-32f, 55f));
                FitLabel(retryButton, "다시 날기");
            }
            if (nestButton)
            {
                var rt = (RectTransform)nestButton.transform;
                rt.SetParent(strip, false);
                Place(rt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-32f - 310f - 20f - 210f, -55f), new Vector2(-32f - 310f - 20f, 55f));
                FitLabel(nestButton, "둥지로");
            }
        }

        /// <summary>아이콘 줄을 새 부모로 옮기고 크기·정렬을 맞춘다</summary>
        static void Restyle(IconFlow flow, RectTransform parent, TextAnchor align, float iconSize, int fontSize)
        {
            flow.transform.SetParent(parent, false);
            flow.alignment = align;
            flow.iconSize = iconSize;
            flow.fontSize = fontSize;
            var layout = flow.GetComponent<VerticalLayoutGroup>();
            if (layout) layout.childAlignment = align;
        }

        static void FitLabel(Button button, string text)
        {
            var label = button.GetComponentInChildren<Text>(true);
            if (!label) return;
            label.text = text;
            var rt = label.rectTransform;
            Place(rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            label.alignment = TextAnchor.MiddleCenter;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 20;
            label.resizeTextMaxSize = 40;
        }

        static Image NewImage(Transform parent, string name, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            if (sprite)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
