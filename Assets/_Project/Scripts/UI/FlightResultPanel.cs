using DragonRecipe.Care;
using DragonRecipe.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DragonRecipe.UI
{
    /// <summary>비행이 끝나면 결과를 아이콘으로 보여주고, 다시 날기 / 둥지로 돌아가기.</summary>
    public class FlightResultPanel : MonoBehaviour
    {
        public FlightSession session;
        public GameObject panel;
        [Tooltip("착지 / 추락을 큰 아이콘으로")]
        public IconFlow titleIcons;
        [Tooltip("체공·속도·먹이·성장·부상을 아이콘으로")]
        public IconFlow bodyIcons;
        public UIIconSet icons;
        public Button retryButton;
        public Button nestButton;
        [Tooltip("결과 화면이 뜨기까지의 지연 (충돌 연출을 보여주려고)")]
        public float showDelay = 1.2f;

        float _showAt = -1f;

        void Awake()
        {
            if (retryButton) retryButton.onClick.AddListener(OnRetry);
            if (nestButton) nestButton.onClick.AddListener(SceneFlow.GoToNest);
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

            if (icons)
            {
                if (titleIcons) CareIcons.ResultTitle(titleIcons, icons, r);
                // 드래곤 상태에 반영된 결과 (먹이 섭취 / 성장 / 부상)는 DragonCare가 이미 계산해 둠
                if (bodyIcons) CareIcons.ResultBody(bodyIcons, icons, r, DragonCare.LastFlightReport);
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
