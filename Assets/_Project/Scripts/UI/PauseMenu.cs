using DragonRecipe.Care;
using DragonRecipe.Core;
using DragonRecipe.Flight;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DragonRecipe.UI
{
    /// <summary>
    /// 일시정지 메뉴: 계속하기 / 상·하 반전 / 둥지로 돌아가기.
    /// Esc·P·패드 Start 또는 화면 오른쪽 위 버튼으로 연다.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public DragonInput input;
        public FlightSession session;
        public GameObject panel;
        [Tooltip("화면에 항상 떠 있는 일시정지 버튼 (모바일용, PC에서도 클릭 가능)")]
        public Button pauseButton;
        public Button resumeButton;
        public Button invertButton;
        public Text invertLabel;
        public Button restartButton;
        [Tooltip("비행을 포기하고 둥지 씬으로 (잡은 먹이는 사라짐)")]
        public Button nestButton;

        void Awake()
        {
            if (pauseButton) pauseButton.onClick.AddListener(GamePause.Toggle);
            if (resumeButton) resumeButton.onClick.AddListener(() => GamePause.Set(false));
            if (invertButton) invertButton.onClick.AddListener(ToggleInvert);
            if (restartButton) restartButton.onClick.AddListener(Restart);
            if (nestButton) nestButton.onClick.AddListener(SceneFlow.GoToNest);
            if (panel) panel.SetActive(false);
        }

        void OnEnable()
        {
            GamePause.Changed += OnPauseChanged;
            if (input) input.PausePressed += GamePause.Toggle;
            RefreshInvertLabel();
        }

        void OnDisable()
        {
            GamePause.Changed -= OnPauseChanged;
            if (input) input.PausePressed -= GamePause.Toggle;
        }

        void OnDestroy()
        {
            // 씬을 나갈 때 멈춘 채로 남지 않게
            GamePause.Set(false);
        }

        void OnPauseChanged(bool paused)
        {
            if (panel) panel.SetActive(paused);
            if (pauseButton) pauseButton.gameObject.SetActive(!paused);
            RefreshInvertLabel();
            if (paused && resumeButton && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
            else if (!paused && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(null);
        }

        void ToggleInvert()
        {
            if (!input) return;
            input.InvertPitch = !input.InvertPitch;
            RefreshInvertLabel();
        }

        void RefreshInvertLabel()
        {
            if (invertLabel && input)
                invertLabel.text = input.InvertPitch ? "상/하 반전: 켜짐" : "상/하 반전: 꺼짐";
        }

        void Restart()
        {
            GamePause.Set(false);
            if (session) session.ResetFlight();
        }
    }
}
