using System;
using DragonRecipe.Care;
using DragonRecipe.Flight;
using UnityEngine;
using UnityEngine.UI;

namespace DragonRecipe.UI
{
    /// <summary>
    /// 둥지 화면: 허기·성장 표시, 상태 안내, 지난 비행 결과, 비행 나가기.
    /// </summary>
    public class NestUI : MonoBehaviour
    {
        public GameConfig config;

        public Text titleText;
        public Image hungerFill;
        public Text hungerLabel;
        public Image growthFill;
        public Text growthLabel;
        public Text statusText;
        public Text reportText;
        public Button flyButton;

        [Header("디버그 (에디터 / 개발 빌드에서만 보임)")]
        public GameObject debugRoot;
        public Button debugAdvanceButton;
        public Button debugResetButton;
        public Text debugResetLabel;

        static readonly Color HungerOk = new Color(0.55f, 0.85f, 0.4f);
        static readonly Color HungerLow = new Color(1f, 0.75f, 0.3f);
        static readonly Color HungerEmpty = new Color(1f, 0.4f, 0.35f);

        float _resetConfirmUntil;
        GrowthStage _shownStage;
        float _celebrateUntil;

        void Awake()
        {
            DragonCare.EnsureLoaded(config);
            if (flyButton) flyButton.onClick.AddListener(SceneFlow.GoToFlight);
            if (debugAdvanceButton) debugAdvanceButton.onClick.AddListener(() => DragonCare.DebugAdvanceHours(1f));
            if (debugResetButton) debugResetButton.onClick.AddListener(OnResetPressed);
            if (debugRoot) debugRoot.SetActive(Application.isEditor || Debug.isDebugBuild);
            if (DragonCare.IsLoaded) _shownStage = DragonCare.State.stage;
        }

        void OnResetPressed()
        {
            if (Time.unscaledTime < _resetConfirmUntil)
            {
                DragonCare.ResetSave();
                _resetConfirmUntil = 0f;
                _shownStage = DragonCare.State.stage;
                return;
            }
            _resetConfirmUntil = Time.unscaledTime + 3f; // 3초 안에 한 번 더 누르면 초기화
        }

        void Update()
        {
            if (!DragonCare.IsLoaded) return;
            DragonCare.Tick();
            var s = DragonCare.State;
            var c = DragonCare.Config.care;

            if (s.stage != _shownStage)
            {
                _shownStage = s.stage;
                _celebrateUntil = Time.time + 4f;
            }

            if (titleText) titleText.text = $"{s.dragonName} · {CareLogic.StageName(s.stage)}";

            float h = c.maxHunger > 0 ? s.hunger / c.maxHunger : 0f;
            if (hungerFill)
            {
                hungerFill.fillAmount = h;
                hungerFill.color = CareLogic.IsStarving(s) ? HungerEmpty : CareLogic.IsHungry(s, c) ? HungerLow : HungerOk;
            }
            if (hungerLabel)
            {
                string eta = s.hunger > 0 && c.hungerDecayPerHour > 0
                    ? $" · {FormatHours(s.hunger / c.hungerDecayPerHour)} 뒤 굶주림"
                    : "";
                hungerLabel.text = $"포만도 {h * 100f:0}%{eta}";
            }

            if (growthFill) growthFill.fillAmount = CareLogic.StageProgress(s, c);
            if (growthLabel)
            {
                growthLabel.text = s.stage switch
                {
                    GrowthStage.Hatchling => $"성장 {s.growthXp:0} / {c.xpToJuvenile:0}  → 청소년",
                    GrowthStage.Juvenile => $"성장 {s.growthXp:0} / {c.xpToAdult:0}  → 성체",
                    _ => "다 자랐어요!"
                };
            }

            if (statusText) statusText.text = StatusLine(s, c);

            if (reportText)
            {
                var r = DragonCare.LastFlightReport;
                reportText.text = r != null ? $"지난 비행\n{r.Describe()}" : $"총 비행 {s.totalFlights}회";
            }

            if (debugResetLabel)
                debugResetLabel.text = Time.unscaledTime < _resetConfirmUntil ? "한 번 더 누르면 초기화" : "저장 초기화";
        }

        string StatusLine(DragonState s, CareSettings c)
        {
            if (Time.time < _celebrateUntil)
                return $"<color=#ffd84d>{CareLogic.StageName(s.stage)}(으)로 성장했어요!</color>";

            string line;
            if (CareLogic.IsStarving(s))
                line = "<color=#ff7766>굶주리고 있어요! 성장이 멈추고 비행할 힘이 약해졌어요.\n먹이를 잡아 오세요.</color>";
            else if (CareLogic.IsHungry(s, c))
                line = "<color=#ffcc55>배가 고파요. 사냥하러 나가요!</color>";
            else if (s.hunger >= c.maxHunger - 0.5f)
                line = "배가 불러요. 지금 잡아 온 먹이는 버려져요.";
            else
                line = "기분이 좋아요.";

            if (s.injured) line += "\n<color=#ff8877>다친 상태예요 (치료약은 상점 단계에서)</color>";
            return line;
        }

        static string FormatHours(float hours)
        {
            var t = TimeSpan.FromHours(hours);
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}시간 {t.Minutes}분" : $"{t.Minutes}분";
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) DragonCare.Save();
        }

        void OnApplicationQuit() => DragonCare.Save();
    }
}
