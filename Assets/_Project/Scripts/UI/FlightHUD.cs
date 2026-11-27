using DragonRecipe.Care;
using DragonRecipe.Core;
using DragonRecipe.Flight;
using UnityEngine;
using UnityEngine.UI;

namespace DragonRecipe.UI
{
    /// <summary>비행 중 정보 표시: 고도, 속도, 스태미나, 잡은 먹이, 피드백 문구.</summary>
    public class FlightHUD : MonoBehaviour
    {
        public FlightSession session;
        public DragonFlightController dragon;

        public Text statsText;
        public Image staminaFill;
        [Tooltip("스태미나 바 이름표. 이륙 전에는 '달리기 파워' / '웅크리기'로 바뀐다 (비우면 바 안에서 찾음)")]
        public Text staminaLabel;
        [Tooltip("체력 바 채움. 비우면 스태미나 바를 복제해 그 아래에 만든다")]
        public Image healthFill;
        public Text promptText;
        public Text feedbackText;
        [Tooltip("모바일 웅크리기 버튼. 이륙 전에만 보인다")]
        public GameObject crouchButton;
        [Tooltip("모바일 날갯짓 버튼 글자. 이륙 전에는 '달리기'")]
        public Text flapButtonLabel;
        public float feedbackDuration = 1.4f;
        [Tooltip("완벽한 날갯짓 때 스태미나 바가 번뜩이는 시간 (초)")]
        public float perfectFlashDuration = 0.35f;
        [Tooltip("번뜩일 때 바가 커지는 비율")]
        public float perfectFlashPop = 0.2f;
        [Tooltip("피해를 입었을 때 체력 바가 흔들리는 시간 (초)")]
        public float hurtShakeDuration = 0.4f;

        static readonly Color StaminaOk = new Color(0.45f, 0.85f, 1f);
        static readonly Color StaminaLow = new Color(1f, 0.4f, 0.35f);
        static readonly Color RunColor = new Color(1f, 0.55f, 0.25f);
        static readonly Color CrouchColor = new Color(1f, 0.8f, 0.3f);
        static readonly Color CrouchFull = new Color(1f, 1f, 0.65f);

        static readonly Color FlashColor = new Color(1f, 1f, 0.85f);
        static readonly Color HealthOk = new Color(0.45f, 0.9f, 0.45f);
        static readonly Color HealthLow = new Color(1f, 0.35f, 0.3f);
        static readonly Color HurtColor = new Color(1f, 0.2f, 0.15f);

        float _feedbackTimer;
        float _flash; // 1 → 0
        RectTransform _bar;
        Vector3 _barScale = Vector3.one;
        Image _flashOverlay;
        float _hurt; // 1 → 0
        float _hurtStrength;
        RectTransform _healthBar;
        Vector2 _healthBarPos;

        void Awake()
        {
            if (!staminaLabel && staminaFill && staminaFill.transform.parent)
                staminaLabel = staminaFill.transform.parent.Find("Label")?.GetComponent<Text>();

            if (!healthFill && staminaFill && staminaFill.transform.parent is RectTransform staminaBar)
            {
                // 체력 바: 스태미나 바를 복제해 바로 아래에 둔다 (번뜩임 덮개를 붙이기 전에 복제)
                var clone = Instantiate(staminaBar.gameObject, staminaBar.parent);
                clone.name = "HealthBar";
                var rt = (RectTransform)clone.transform;
                rt.anchoredPosition = staminaBar.anchoredPosition + new Vector2(0f, -80f);
                healthFill = clone.transform.Find(staminaFill.name)?.GetComponent<Image>();
                var label = clone.transform.Find("Label")?.GetComponent<Text>();
                if (label) label.text = "체력";
            }
            if (healthFill)
            {
                _healthBar = healthFill.transform.parent as RectTransform;
                if (_healthBar) _healthBarPos = _healthBar.anchoredPosition;
            }

            if (staminaFill)
            {
                _bar = staminaFill.transform.parent as RectTransform;
                if (_bar) _barScale = _bar.localScale;

                // 번뜩임용 흰 덮개 (바 위에 겹쳐서 순간적으로 밝힘)
                var go = new GameObject("PerfectFlash", typeof(RectTransform));
                go.transform.SetParent(_bar ? _bar : staminaFill.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(-6f, -6f);
                rt.offsetMax = new Vector2(6f, 6f);
                _flashOverlay = go.AddComponent<Image>();
                _flashOverlay.sprite = staminaFill.sprite;
                _flashOverlay.raycastTarget = false;
                _flashOverlay.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        void OnEnable()
        {
            if (session) session.Feedback += ShowFeedback;
            if (dragon) dragon.Flapped += OnFlapped;
            if (dragon) dragon.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            if (session) session.Feedback -= ShowFeedback;
            if (dragon) dragon.Flapped -= OnFlapped;
            if (dragon) dragon.Damaged -= OnDamaged;
        }

        void OnDamaged(float damage)
        {
            // 크게 다칠수록 세게 흔들림
            _hurt = 1f;
            _hurtStrength = Mathf.Lerp(4f, 24f, Mathf.Clamp01(damage / 40f));
        }

        /// <summary>체력 바: 남은 체력, 피해 순간 빨갛게 번쩍이며 흔들림</summary>
        void UpdateHealth(FlightModel m)
        {
            if (!healthFill) return;
            float t = m.Params.maxHealth > 0 ? m.Health / m.Params.maxHealth : 0f;
            healthFill.fillAmount = t;
            var color = t < 0.3f ? HealthLow : HealthOk;

            if (_hurt > 0f)
            {
                _hurt = Mathf.Max(0f, _hurt - Time.deltaTime / Mathf.Max(0.01f, hurtShakeDuration));
                float k = _hurt * _hurt;
                color = Color.Lerp(color, HurtColor, k);
                if (_healthBar)
                    _healthBar.anchoredPosition = _healthBarPos + Random.insideUnitCircle * _hurtStrength * k;
            }
            else if (_healthBar)
            {
                _healthBar.anchoredPosition = _healthBarPos;
            }
            healthFill.color = color;
        }

        void OnFlapped(FlapOutcome outcome)
        {
            if (outcome == FlapOutcome.Perfect) _flash = 1f;
        }

        /// <summary>팟: 순간 커졌다가 원래대로, 바와 덮개가 하얗게 번뜩였다가 사라진다</summary>
        void UpdatePerfectFlash()
        {
            if (_flash <= 0f) return;
            _flash = Mathf.Max(0f, _flash - Time.deltaTime / Mathf.Max(0.01f, perfectFlashDuration));
            float k = _flash * _flash; // 처음에 세게, 빠르게 사그라듦
            if (_bar) _bar.localScale = _barScale * (1f + perfectFlashPop * k);
            if (staminaFill) staminaFill.color = Color.Lerp(staminaFill.color, FlashColor, k);
            if (_flashOverlay) _flashOverlay.color = new Color(1f, 1f, 1f, 0.7f * k);
        }

        void ShowFeedback(string msg, Color color)
        {
            if (!feedbackText) return;
            feedbackText.text = msg;
            feedbackText.color = color;
            _feedbackTimer = feedbackDuration;
        }

        static string CareLine()
        {
            if (!DragonCare.IsLoaded) return "";
            DragonCare.Tick();
            var s = DragonCare.State;
            var c = DragonCare.Config.care;
            string status = CareLogic.IsStarving(s) ? "  <color=#ff7766>굶주림! 힘이 없어요</color>"
                : CareLogic.IsHungry(s, c) ? "  <color=#ffcc55>배고픔</color>" : "";
            return $"\n{CareLogic.StageName(s.stage)} · 포만도 {s.hunger / c.maxHunger * 100f:0}%{status}";
        }

        static string RunUpPrompt(RunUpModel run, FlightParams p)
        {
            if (run == null) return "";
            string toEdge = $"절벽 끝까지 {Mathf.Max(0f, run.Remaining):0.0} m";
            if (run.IsCrouching)
                return run.CrouchDepth(p) < 1f
                    ? $"웅크리는 중…\n{toEdge}"
                    : $"흰 선에서 웅크리기 버튼을 떼세요!\n{toEdge}";
            if (run.InJumpZone(p))
                return $"지금 웅크리기(Shift / B)를 꾹!\n{toEdge}";
            if (run.Started)
                return $"날갯짓 연타! 노란 구간에서 웅크리기(Shift / B)를 꾹\n{toEdge}";
            return "날갯짓 버튼(Space / A) 연타로 절벽까지 달리기! (빠를수록 빠르게)\n" +
                   "노란 구간에서 웅크리기(Shift / B) 꾹 → 흰 선에서 떼기! (잘 뛸수록 높게)\n" +
                   "조이스틱: 기수·선회 · 일시정지: Esc / P / Start";
        }

        void Update()
        {
            if (!session || !dragon) return;
            var m = dragon.Model;

            if (statsText)
            {
                float speed = dragon.IsFlying ? m.Velocity.magnitude : dragon.RunUp?.RunSpeed ?? 0f;
                string stall = dragon.IsFlying && m.IsStalling ? "  <color=#ff7766>실속!</color>" : "";
                statsText.text =
                    $"고도 {dragon.Altitude:0} m\n" +
                    $"속도 {speed * 3.6f:0} km/h{stall}\n" +
                    $"체공 {session.Result.airtime:0.0} s\n" +
                    $"먹이 {session.Result.caught.Count}  (아슬아슬 {session.Result.nearMissCount})" +
                    CareLine();
            }

            var run = dragon.IsFlying ? null : dragon.RunUp;
            var p = m.Params;
            if (staminaFill)
            {
                if (run != null && run.IsCrouching)
                {
                    float d = run.CrouchDepth(p);
                    staminaFill.fillAmount = d;
                    staminaFill.color = d >= 1f ? CrouchFull : CrouchColor;
                }
                else if (run != null)
                {
                    // 이륙 전에는 같은 바를 달리기 파워로 쓴다
                    staminaFill.fillAmount = run.PowerFraction(p);
                    staminaFill.color = RunColor;
                }
                else
                {
                    float t = p.maxStamina > 0 ? m.Stamina / p.maxStamina : 0f;
                    staminaFill.fillAmount = t;
                    staminaFill.color = m.Stamina < p.flapStaminaCost ? StaminaLow : StaminaOk;
                }
            }
            UpdatePerfectFlash();
            UpdateHealth(m);
            if (staminaLabel)
                staminaLabel.text = run == null ? "스태미나" : run.IsCrouching ? "웅크리기 (높이)" : "달리기 파워 (속도)";

            if (promptText)
            {
                promptText.enabled = session.Current == FlightSession.State.Ready;
                promptText.text = RunUpPrompt(run, p);
            }

            bool ready = session.Current == FlightSession.State.Ready;
            if (crouchButton && crouchButton.activeSelf != ready) crouchButton.SetActive(ready);
            if (flapButtonLabel) flapButtonLabel.text = ready ? "달리기" : "날갯짓";

            if (feedbackText)
            {
                _feedbackTimer -= Time.deltaTime;
                var c = feedbackText.color;
                c.a = Mathf.Clamp01(_feedbackTimer / 0.4f);
                feedbackText.color = c;
            }
        }
    }
}
