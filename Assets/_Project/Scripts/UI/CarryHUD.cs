using DragonRecipe.Core;
using UnityEngine;

namespace DragonRecipe.UI
{
    /// <summary>
    /// 비행 중 발톱에 매단 먹이(아이콘 줄)와 연속 사냥(발톱 xN)을 보여 준다.
    /// 모바일 먹기 버튼은 무언가를 매달고 날 때만 보인다.
    /// </summary>
    public class CarryHUD : MonoBehaviour
    {
        public FlightSession session;
        public UIIconSet icons;
        [Tooltip("매달린 먹이 아이콘 줄")]
        public IconFlow carryIcons;
        [Tooltip("연속 사냥 표시 (발톱 xN)")]
        public IconFlow comboIcons;
        [Tooltip("모바일 먹기 버튼 (먹이를 매달고 있을 때만 보임)")]
        public GameObject eatButton;
        public float comboShowTime = 1.6f;

        static readonly Color ComboColor = new Color(1f, 0.85f, 0.3f);

        RectTransform _carryRt;
        Vector2 _carryPos;
        RectTransform _comboRt;
        CanvasGroup _comboGroup;
        float _comboTimer;
        float _shake;

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
            }
        }

        void OnEnable()
        {
            if (!session) return;
            session.CarryChanged += RebuildCarry;
            session.Caught += OnCaught;
            session.Struggled += OnStruggled;
        }

        void OnDisable()
        {
            if (!session) return;
            session.CarryChanged -= RebuildCarry;
            session.Caught -= OnCaught;
            session.Struggled -= OnStruggled;
        }

        void Start() => RebuildCarry();

        void OnStruggled() => _shake = 1f;

        void OnCaught(int combo)
        {
            if (combo < 2 || !comboIcons || !icons) return;
            comboIcons.Clear();
            var row = comboIcons.AddRow();
            for (int i = 0; i < Mathf.Min(combo, 5); i++) row.Icon(icons.claw, 1f + i * 0.08f);
            row.Text($"x{combo}", ComboColor).Gap(16f).Icon(icons.wind, 1.1f); // 속도가 붙음
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

            // 연속 사냥: 팟 하고 커졌다가 서서히 사라짐
            if (_comboGroup)
            {
                _comboTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(_comboTimer / comboShowTime);
                _comboGroup.alpha = Mathf.Clamp01(_comboTimer / 0.4f);
                if (_comboRt) _comboRt.localScale = Vector3.one * (1f + 0.35f * Mathf.Pow(t, 6f));
            }
        }
    }
}
