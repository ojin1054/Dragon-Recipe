using System;
using System.Text;
using DragonRecipe.Core;
using DragonRecipe.Flight;
using UnityEngine;

namespace DragonRecipe.Care
{
    /// <summary>비행 한 번이 드래곤에게 남긴 결과 (둥지 화면에 표시)</summary>
    public class FlightCareReport
    {
        public int foodCount;
        /// <summary>잡아 온 먹이 (아이콘 표시용)</summary>
        public System.Collections.Generic.List<DragonRecipe.Food.FoodData> foods = new System.Collections.Generic.List<DragonRecipe.Food.FoodData>();
        public float absorbed;
        public float wasted;
        public float xpGained;
        public bool injured;
        public bool grew;
        public GrowthStage newStage;

        public string Describe()
        {
            var sb = new StringBuilder();
            if (foodCount == 0)
            {
                sb.Append("먹이를 못 잡았어요");
            }
            else
            {
                sb.Append($"먹이 {foodCount}개 → 포만도 +{absorbed:0}, 성장 +{xpGained:0}");
                if (wasted > 0.5f) sb.Append($"\n배가 불러 {wasted:0}만큼은 버렸어요 (먹이 창고는 다음 단계)");
            }
            if (grew) sb.Append($"\n<color=#ffd84d>{CareLogic.StageName(newStage)}(으)로 성장했어요!</color>");
            if (injured) sb.Append("\n<color=#ff8877>추락해서 다쳤어요</color>");
            return sb.ToString();
        }
    }

    /// <summary>
    /// 씬을 넘나드는 드래곤 상태 관리 (정적). 각 씬의 부트스트랩이 EnsureLoaded를 호출한다.
    /// </summary>
    public static class DragonCare
    {
        public static GameConfig Config { get; private set; }
        public static DragonState State { get; private set; }
        public static bool IsLoaded => State != null && Config != null;
        public static FlightCareReport LastFlightReport { get; private set; }

        /// <summary>테스트에서 시간을 조작할 때 사용</summary>
        public static Func<DateTime> Clock = () => DateTime.UtcNow;

        public static event Action<GrowthStage> StageChanged;

        public static void EnsureLoaded(GameConfig config)
        {
            if (config) Config = config;
            if (State != null || Config == null) return;

            if (!SaveSystem.TryLoad(out var s) || s.version <= 0)
                s = DragonState.CreateNew(Config.care, Clock());
            State = s;
            Tick();
            Save();
        }

        /// <summary>흐른 시간만큼 허기 반영 (매 프레임 불러도 됨)</summary>
        public static void Tick()
        {
            if (!IsLoaded) return;
            CareLogic.ApplyElapsed(State, Config.care, Clock());
        }

        public static void Save()
        {
            if (State != null) SaveSystem.Save(State);
        }

        public static DragonStageData CurrentStageData => IsLoaded ? Config.StageData(State.stage) : null;

        /// <summary>현재 단계 + 굶주림을 반영한 비행 수치</summary>
        public static FlightParams CurrentFlightParams()
        {
            var data = CurrentStageData;
            if (data == null) return new FlightParams();
            Tick();
            return CareLogic.FlightParamsFor(data.flight, State, Config.care);
        }

        /// <summary>잡은 먹이를 먹이고 부상/성장을 기록한다.</summary>
        public static FlightCareReport ApplyFlightResult(FlightResult result)
        {
            if (!IsLoaded || result == null) return null;
            Tick();

            var report = new FlightCareReport { foodCount = result.caught.Count, injured = result.Injured };
            foreach (var c in result.caught)
            {
                if (c.food == null) continue;
                report.foods.Add(c.food);
                var o = CareLogic.Feed(State, Config.care, c.satiety, c.food.trait);
                report.absorbed += o.absorbed;
                report.wasted += o.wasted;
                report.xpGained += o.xpGained;
                if (o.stageChanged)
                {
                    report.grew = true;
                    report.newStage = o.newStage;
                }
            }

            if (result.Injured) State.injured = true;
            State.totalFlights++;
            LastFlightReport = report;
            Save();

            if (report.grew) StageChanged?.Invoke(report.newStage);
            return report;
        }

        /// <summary>저장을 지우고 새 드래곤으로 시작</summary>
        public static void ResetSave()
        {
            SaveSystem.Delete();
            State = Config != null ? DragonState.CreateNew(Config.care, Clock()) : null;
            LastFlightReport = null;
            Save();
        }

        /// <summary>디버그: 시간을 앞당긴 것처럼 허기를 줄인다</summary>
        public static void DebugAdvanceHours(float hours)
        {
            if (!IsLoaded) return;
            Tick();
            State.lastTickUtc -= TimeSpan.FromHours(hours).Ticks;
            Tick();
            Save();
        }

        // 도메인 리로드를 꺼도 Play마다 새로 읽게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            State = null;
            Config = null;
            LastFlightReport = null;
            StageChanged = null;
            Clock = () => DateTime.UtcNow;
        }

        /// <summary>테스트용: 메모리 상태만 비운다</summary>
        public static void UnloadForTests() => ResetStatics();
    }
}
