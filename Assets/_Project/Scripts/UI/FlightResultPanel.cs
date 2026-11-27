using System.Collections.Generic;
using System.Text;
using DragonRecipe.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DragonRecipe.UI
{
    /// <summary>비행이 끝나면 결과를 보여주고 "다시 날기"로 둥지에 돌아간다.</summary>
    public class FlightResultPanel : MonoBehaviour
    {
        public FlightSession session;
        public GameObject panel;
        public Text titleText;
        public Text bodyText;
        public Button retryButton;
        [Tooltip("결과 화면이 뜨기까지의 지연 (충돌 연출을 보여주려고)")]
        public float showDelay = 1.2f;

        float _showAt = -1f;

        void Awake()
        {
            if (retryButton) retryButton.onClick.AddListener(OnRetry);
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

            if (titleText)
            {
                titleText.text = r.reason switch
                {
                    FlightEndReason.Landed => "무사히 착지!",
                    FlightEndReason.Crashed => "추락! 드래곤이 다쳤어요",
                    _ => "멀리 떨어져 버렸어요… 드래곤이 다쳤어요"
                };
                titleText.color = r.Injured ? new Color(1f, 0.5f, 0.45f) : new Color(1f, 0.9f, 0.5f);
            }

            if (bodyText)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"체공 시간  {r.airtime:0.0} 초");
                sb.AppendLine($"최고 속도  {r.maxSpeed * 3.6f:0} km/h");
                sb.AppendLine($"완벽한 날갯짓  {r.perfectFlapCount} 회");
                sb.AppendLine($"아슬아슬 낚아채기  {r.nearMissCount} 회");
                sb.AppendLine();

                if (r.caught.Count == 0)
                {
                    sb.AppendLine("잡은 먹이 없음");
                }
                else
                {
                    var counts = new Dictionary<string, int>();
                    foreach (var c in r.caught)
                    {
                        string n = c.food ? c.food.displayName : "?";
                        counts[n] = counts.TryGetValue(n, out int k) ? k + 1 : 1;
                    }
                    sb.Append("잡은 먹이  ");
                    bool first = true;
                    foreach (var kv in counts)
                    {
                        if (!first) sb.Append(", ");
                        sb.Append($"{kv.Key} x{kv.Value}");
                        first = false;
                    }
                    sb.AppendLine();
                    sb.AppendLine($"포만도 합계  {r.TotalSatiety:0}");
                }

                if (r.Injured)
                {
                    sb.AppendLine();
                    sb.AppendLine("<color=#ff8877>부상 상태 — 치료가 필요해요 (상점은 다음 단계에서)</color>");
                }
                bodyText.text = sb.ToString();
            }

            if (retryButton && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
        }

        void OnRetry()
        {
            if (panel) panel.SetActive(false);
            session.ResetFlight();
        }
    }
}
