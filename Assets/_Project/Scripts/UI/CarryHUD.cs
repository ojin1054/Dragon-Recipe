using DragonRecipe.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DragonRecipe.UI
{
    /// <summary>
    /// 비행 중 매단 먹이(아이콘 줄), 힘 게이지(동물 힘의 합 / 드래곤 힘), 연속 사냥(오른쪽 combo N)을 보여 준다.
    /// 모바일 먹기 버튼은 무언가를 매달고 날 때만 보인다.
    /// </summary>
    public class CarryHUD : MonoBehaviour
    {
        public FlightSession session;
        public UIIconSet icons;
        [Tooltip("매달린 먹이 아이콘 줄")]
        public IconFlow carryIcons;
        [Tooltip("연속 사냥 표시 (combo N)")]
        public IconFlow comboIcons;
        [Tooltip("모바일 먹기 버튼 (먹이를 매달고 있을 때만 보임)")]
        public GameObject eatButton;
        public float comboShowTime = 1.6f;
        [Tooltip("연속 사냥(combo N)을 화면 오른쪽에 표시")]
        public bool comboOnRight = true;
        [Tooltip("오른쪽 가장자리 기준 위치")]
        public Vector2 comboRightOffset = new Vector2(-60f, 120f);
        [Tooltip("힘 게이지 (비우면 먹이 줄 아래에 만든다). 눈금 = 드래곤 힘, 끝 = 낚을 수 있는 한계")]
        public RectTransform loadGauge;

        static readonly Color GaugeOk = new Color(0.95f, 0.95f, 0.85f);
        static readonly Color GaugeOver = new Color(1f, 0.55f, 0.2f);
        static readonly Color GaugeMax = new Color(1f, 0.25f, 0.2f);

        static readonly Color ComboColor = new Color(1f, 0.85f, 0.3f);

        RectTransform _carryRt;
        Vector2 _carryPos;
        RectTransform _comboRt;
        CanvasGroup _comboGroup;
        float _comboTimer;
        float _shake;
        RectTransform _gaugeFill;
        Image _gaugeFillImage;
        RectTransform _gaugeMark;
        Vector2 _gaugePos;
        float _refused; // 1 → 0

        void Awake()
        {
            if (carryIcons)
            {
                _carryRt = (RectTransform)carryIcons.transform;
                _carryPos = _carryRt.anchoredPosition;
            }
            if (comboIcons)
            {
                _comboRt = (RectTransform)comboIcons.transform;
                _comboGroup = comboIcons.GetComponent<CanvasGroup>();
                if (!_comboGroup) _comboGroup = comboIcons.gameObject.AddComponent<CanvasGroup>();
                _comboGroup.alpha = 0f;
                if (comboOnRight) PlaceComboRight();
            }
            if (!loadGauge && carryIcons) loadGauge = BuildGauge();
            if (loadGauge)
            {
                _gaugePos = loadGauge.anchoredPosition;
                _gaugeFill = loadGauge.Find("Bar/Fill") as RectTransform;
                if (_gaugeFill) _gaugeFillImage = _gaugeFill.GetComponent<Image>();
                _gaugeMark = loadGauge.Find("Bar/Mark") as RectTransform;
            }
        }

        /// <summary>[힘 아이콘] [━━━━━|━━] : 채워진 만큼 동물 힘의 합, 눈금이 드래곤 힘, 끝이 한계</summary>
        RectTransform BuildGauge()
        {
            var src = (RectTransform)carryIcons.transform;
            var root = new GameObject("LoadGauge", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(src.parent, false);
            root.anchorMin = root.anchorMax = src.anchorMin;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = src.anchoredPosition + new Vector2(0f, -src.sizeDelta.y * 0.5f - 30f);
            root.sizeDelta = new Vector2(420f, 36f);

            if (icons && icons.strength)
            {
                var icon = NewImage("Icon", root, icons.strength, Color.white);
                icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
                icon.sizeDelta = new Vector2(40f, 40f);
                icon.anchoredPosition = new Vector2(20f, 0f);
                icon.GetComponent<Image>().preserveAspect = true;
            }

            var bar = NewImage("Bar", root, null, new Color(0f, 0f, 0f, 0.45f));
            bar.anchorMin = new Vector2(0f, 0.5f);
            bar.anchorMax = new Vector2(1f, 0.5f);
            bar.offsetMin = new Vector2(52f, -9f);
            bar.offsetMax = new Vector2(0f, 9f);

            var fill = NewImage("Fill", bar, null, GaugeOk);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;

            var mark = NewImage("Mark", bar, null, Color.white);
            mark.anchorMin = mark.anchorMax = new Vector2(1f, 0.5f);
            mark.sizeDelta = new Vector2(4f, 30f);
            return root;
        }

        static RectTransform NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        void OnEnable()
        {
            if (!session) return;
            session.CarryChanged += RebuildCarry;
            session.Caught += OnCaught;
            session.Struggled += OnStruggled;
            session.SnatchRefused += OnRefused;
        }

        void OnDisable()
        {
            if (!session) return;
            session.CarryChanged -= RebuildCarry;
            session.Caught -= OnCaught;
            session.Struggled -= OnStruggled;
            session.SnatchRefused -= OnRefused;
        }

        void Start() => RebuildCarry();

        void OnStruggled() => _shake = 1f;

        void OnRefused(Food.Prey prey) => _refused = 1f;

        /// <summary>연속 사냥 표시를 화면 오른쪽 가운데에 붙인다</summary>
        void PlaceComboRight()
        {
            _comboRt.anchorMin = _comboRt.anchorMax = new Vector2(1f, 0.5f);
            _comboRt.pivot = new Vector2(1f, 0.5f);
            _comboRt.anchoredPosition = comboRightOffset;
            comboIcons.alignment = TextAnchor.MiddleRight;
            var layout = comboIcons.GetComponent<VerticalLayoutGroup>();
            if (layout) layout.childAlignment = TextAnchor.MiddleRight;
        }

        void OnCaught(int combo)
        {
            if (combo < 1 || !comboIcons) return;
            comboIcons.Clear();
            var row = comboIcons.AddRow().Text($"combo {combo}", ComboColor);
            if (combo >= 2 && icons) row.Gap(12f).Icon(icons.wind, 1.1f); // 속도가 붙음
            _comboTimer = comboShowTime;
        }

        void RebuildCarry()
        {
            if (!carryIcons || !icons || !session) return;
            carryIcons.Clear();
            if (session.Result.CarriedCount == 0) return;

            var row = carryIcons.AddRow().Icon(icons.claw, 0.9f).Gap(12f);
            foreach (var c in session.Result.caught)
            {
                if (!c.Carried || !c.food) continue;
                row.Icon(c.food.icon ? c.food.icon : icons.meat);
                if (c.rare) row.Icon(icons.sparkle, 0.6f);
            }
        }

        void Update()
        {
            if (!session) return;

            bool flying = session.Current == FlightSession.State.Flying;
            bool canEat = flying && session.Result.CarriedCount > 0;
            if (eatButton && eatButton.activeSelf != canEat) eatButton.SetActive(canEat);
            if (carryIcons && carryIcons.gameObject.activeSelf != flying) carryIcons.gameObject.SetActive(flying);

            // 버둥거림: 매달린 먹이 줄이 흔들림
            if (_carryRt)
            {
                _shake = Mathf.Max(0f, _shake - Time.deltaTime * 3f);
                _carryRt.anchoredPosition = _carryPos + new Vector2(Mathf.Sin(Time.time * 60f) * 10f * _shake, 0f);
            }

            UpdateGauge(flying);

            // 연속 사냥: 팟 하고 커졌다가 서서히 사라짐
            if (_comboGroup)
            {
                _comboTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(_comboTimer / comboShowTime);
                _comboGroup.alpha = Mathf.Clamp01(_comboTimer / 0.4f);
                if (_comboRt) _comboRt.localScale = Vector3.one * (1f + 0.35f * Mathf.Pow(t, 6f));
            }
        }

        void UpdateGauge(bool flying)
        {
            if (!loadGauge) return;
            _refused = Mathf.Max(0f, _refused - Time.deltaTime * 2f);
            bool show = flying && (session.Result.CarriedCount > 0 || _refused > 0f);
            if (loadGauge.gameObject.activeSelf != show) loadGauge.gameObject.SetActive(show);
            if (!show) return;

            float strength = Mathf.Max(0.01f, session.DragonStrength);
            float limit = strength * Mathf.Max(1f, session.hunt.maxOverload);
            float fill = Mathf.Clamp01(session.CarriedStrength / limit);
            float over = session.OverloadAmount;

            if (_gaugeFill) _gaugeFill.anchorMax = new Vector2(fill, 1f);
            if (_gaugeMark) _gaugeMark.anchorMin = _gaugeMark.anchorMax = new Vector2(strength / limit, 0.5f);
            if (_gaugeFillImage)
            {
                var c = over <= 0f ? GaugeOk : Color.Lerp(GaugeOver, GaugeMax, over);
                // 넘치면 깜빡이고, 못 들어 올리면 새빨갛게
                if (over > 0f) c = Color.Lerp(c, Color.white, (Mathf.Sin(Time.time * (8f + 10f * over)) * 0.5f + 0.5f) * 0.35f);
                _gaugeFillImage.color = Color.Lerp(c, GaugeMax, _refused);
            }
            float shake = Mathf.Max(_refused, over * 0.3f);
            loadGauge.anchoredPosition = _gaugePos + new Vector2(Mathf.Sin(Time.time * 55f) * 9f * shake, 0f);
            loadGauge.localScale = Vector3.one * (1f + 0.15f * _refused);
        }
    }
}
