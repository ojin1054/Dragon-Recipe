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
        public Text promptText;
        public Text feedbackText;
        public float feedbackDuration = 1.4f;

        static readonly Color StaminaOk = new Color(0.45f, 0.85f, 1f);
        static readonly Color StaminaLow = new Color(1f, 0.4f, 0.35f);

        float _feedbackTimer;

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
                    $"먹이 {session.Result.caught.Count}  (아슬아슬 {session.Result.nearMissCount})";
            }

            if (staminaFill)
            {
                float t = m.Params.maxStamina > 0 ? m.Stamina / m.Params.maxStamina : 0f;
                staminaFill.fillAmount = t;
                staminaFill.color = m.Stamina < m.Params.flapStaminaCost ? StaminaLow : StaminaOk;
            }

            if (promptText)
            {
                promptText.enabled = session.Current == FlightSession.State.Ready;
                promptText.text = "날갯짓(Space / A / 화면 버튼)으로 이륙!\n조이스틱: 위·아래로 기수, 좌·우로 기울여 선회 (손을 떼도 유지)\n일시정지: Esc / P / 패드 Start";
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
