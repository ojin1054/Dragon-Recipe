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
        [Tooltip("스태미나 바 이름표. 힘 모으는 동안 '도약 힘'으로 바뀐다 (비우면 바 안에서 찾음)")]
        public Text staminaLabel;
        public Text promptText;
        public Text feedbackText;
        public float feedbackDuration = 1.4f;

        static readonly Color StaminaOk = new Color(0.45f, 0.85f, 1f);
        static readonly Color StaminaLow = new Color(1f, 0.4f, 0.35f);
        static readonly Color ChargeColor = new Color(1f, 0.7f, 0.25f);
        static readonly Color ChargePerfect = new Color(1f, 1f, 0.6f);

        float _feedbackTimer;

        void Awake()
        {
            if (!staminaLabel && staminaFill && staminaFill.transform.parent)
                staminaLabel = staminaFill.transform.parent.Find("Label")?.GetComponent<Text>();
        }

        void OnEnable()
        {
            if (session) session.Feedback += ShowFeedback;
        }

        void OnDisable()
        {
            if (session) session.Feedback -= ShowFeedback;
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

        void Update()
        {
            if (!session || !dragon) return;
            var m = dragon.Model;

            if (statsText)
            {
                float speed = dragon.IsFlying ? m.Velocity.magnitude : 0f;
                string stall = dragon.IsFlying && m.IsStalling ? "  <color=#ff7766>실속!</color>" : "";
                statsText.text =
                    $"고도 {dragon.Altitude:0} m\n" +
                    $"속도 {speed * 3.6f:0} km/h{stall}\n" +
                    $"체공 {session.Result.airtime:0.0} s\n" +
                    $"먹이 {session.Result.caught.Count}  (아슬아슬 {session.Result.nearMissCount})" +
                    CareLine();
            }

            bool charging = dragon.IsCharging;
            if (staminaFill)
            {
                if (charging)
                {
                    // 이륙 전에는 같은 바를 도약 게이지로 쓴다
                    staminaFill.fillAmount = dragon.Charge;
                    staminaFill.color = dragon.ChargeIsPerfect ? ChargePerfect : ChargeColor;
                }
                else
                {
                    float t = m.Params.maxStamina > 0 ? m.Stamina / m.Params.maxStamina : 0f;
                    staminaFill.fillAmount = t;
                    staminaFill.color = m.Stamina < m.Params.flapStaminaCost ? StaminaLow : StaminaOk;
                }
            }
            if (staminaLabel)
                staminaLabel.text = charging ? (dragon.ChargeIsPerfect ? "지금 떼세요!" : "도약 힘") : "스태미나";

            if (promptText)
            {
                promptText.enabled = session.Current == FlightSession.State.Ready;
                promptText.text = charging
                    ? "힘을 모으는 중… 게이지가 가득 찼을 때 떼면 완벽한 도약!"
                    : "날갯짓(Space / A / 화면 버튼)을 꾹 눌러 힘을 모았다가 떼면 이륙! (오래 모을수록 더 빠르고 높게)\n조이스틱: 위·아래로 기수, 좌·우로 기울여 선회 (손을 떼도 유지)\n일시정지: Esc / P / 패드 Start";
            }

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
