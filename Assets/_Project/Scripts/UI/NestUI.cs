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
        [Tooltip("드래곤 기분·상태를 아이콘으로")]
        public IconFlow statusIcons;
        [Tooltip("지난 비행 결과를 아이콘으로")]
        public IconFlow reportIcons;
        public UIIconSet icons;
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
        string _statusKey;
        FlightCareReport _shownReport;
        int _shownFlights = -1;

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

            RefreshStatusIcons(s, c);
            RefreshReportIcons(s);

            if (debugResetLabel)
                debugResetLabel.text = Time.unscaledTime < _resetConfirmUntil ? "한 번 더 누르면 초기화" : "저장 초기화";
        }

        // 아이콘은 GameObject를 만들기 때문에, 내용이 바뀔 때만 다시 그린다
        void RefreshStatusIcons(DragonState s, CareSettings c)
        {
            if (!statusIcons || !icons) return;
            bool celebrating = Time.time < _celebrateUntil;
            string key = $"{CareIcons.MoodOf(s, c)}|{s.injured}|{celebrating}";
            if (key == _statusKey) return;
            _statusKey = key;
            CareIcons.Status(statusIcons, icons, s, c, celebrating);
        }

        void RefreshReportIcons(DragonState s)
        {
            if (!reportIcons || !icons) return;
            var r = DragonCare.LastFlightReport;
            if (r == _shownReport && s.totalFlights == _shownFlights) return;
            _shownReport = r;
            _shownFlights = s.totalFlights;

            reportIcons.Clear();
            if (r != null)
            {
                reportIcons.AddRow().Icon(icons.wing, 1.2f); // 지난 비행
                CareIcons.Report(reportIcons, icons, r);
            }
            else
            {
                reportIcons.AddRow().Icon(icons.wing).Text($"x{s.totalFlights}"); // 총 비행 횟수
            }
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
