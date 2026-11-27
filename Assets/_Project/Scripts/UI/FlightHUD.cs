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
        public Text promptText;
        public Text feedbackText;
        [Tooltip("모바일 웅크리기 버튼. 이륙 전에만 보인다")]
        public GameObject crouchButton;
        [Tooltip("모바일 날갯짓 버튼 글자. 이륙 전에는 '달리기'")]
        public Text flapButtonLabel;
        public float feedbackDuration = 1.4f;

        static readonly Color StaminaOk = new Color(0.45f, 0.85f, 1f);
        static readonly Color StaminaLow = new Color(1f, 0.4f, 0.35f);
        static readonly Color RunColor = new Color(1f, 0.55f, 0.25f);
        static readonly Color CrouchColor = new Color(1f, 0.8f, 0.3f);
        static readonly Color CrouchFull = new Color(1f, 1f, 0.65f);

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
