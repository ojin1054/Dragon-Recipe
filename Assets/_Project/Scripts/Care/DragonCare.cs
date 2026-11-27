using System;
using System.Collections.Generic;
using DragonRecipe.Core;
using DragonRecipe.Farm;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using UnityEngine;

namespace DragonRecipe.Care
{
    /// <summary>비행 한 번이 드래곤에게 남긴 결과 (둥지 / 결과 화면에 아이콘으로 표시)</summary>
    public class FlightCareReport
    {
        public int foodCount;
        /// <summary>잡은 먹이 전부</summary>
        public List<FoodData> foods = new List<FoodData>();
        /// <summary>비행 중에 먹은 먹이</summary>
        public List<FoodData> eaten = new List<FoodData>();
        /// <summary>둥지까지 들고 와 창고에 넣은 먹이</summary>
        public List<FoodData> stored = new List<FoodData>();
        /// <summary>추락하며 떨어뜨린 먹이</summary>
        public List<FoodData> dropped = new List<FoodData>();
        /// <summary>창고가 가득 차 버린 먹이</summary>
        public List<FoodData> overflow = new List<FoodData>();
        /// <summary>처음으로 잡아 도감에 새로 오른 먹이</summary>
        public List<FoodData> newInCodex = new List<FoodData>();
        public int rareCount;

        // 비행 중에 먹은 먹이가 남긴 것
        public float absorbed;
        public float wasted;
        public float xpGained;
        public bool injured;
        public bool grew;
        public GrowthStage newStage;
        public DragonForm newForm;
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
        /// <summary>둥지에서 창고 먹이를 먹임</summary>
        public static event Action<FeedOutcome> Fed;
        /// <summary>새 알을 부화시킴</summary>
        public static event Action Hatched;
        /// <summary>농장 동물·조합·버프가 바뀜</summary>
        public static event Action FarmChanged;
        /// <summary>끝난 조합을 거둠 (성공 / 실패 연출용)</summary>
        public static event Action<List<CombineResult>> CombinesCollected;

        public static void EnsureLoaded(GameConfig config)
        {
            if (config) Config = config;
            if (State != null || Config == null) return;

            if (!SaveSystem.TryLoad(out var s) || s.version <= 0)
                s = DragonState.CreateNew(Config.care, Clock());
            Migrate(s);
            State = s;
            Tick();
            Save();
        }

        /// <summary>예전 저장 파일에 없던 값 채우기</summary>
        static void Migrate(DragonState s)
        {
            s.storage ??= new List<StoredFood>();
            s.codexFoods ??= new List<CodexFood>();
            s.codexForms ??= new List<DragonForm>();
            s.farm ??= new FarmState();
            s.farm.animals ??= new List<FarmAnimal>();
            s.farm.jobs ??= new List<CombineJob>();
            s.farm.buffs ??= new List<ActiveBuff>();
            s.farm.recipes ??= new List<RecipeRecord>();
            if (s.generation <= 0) s.generation = 1;
            if (s.stage == GrowthStage.Adult && s.form == DragonForm.None)
            {
                s.form = CareLogic.FormFor(s, Config.care);
                if (!s.codexForms.Contains(s.form)) s.codexForms.Add(s.form);
            }
            s.version = DragonState.CurrentVersion;
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

        public static FoodData FindFood(string id) => Config ? Config.FindFood(id) : null;

        /// <summary>현재 단계 + 굶주림 + 농장 버프를 반영한 비행 수치</summary>
        public static FlightParams CurrentFlightParams()
        {
            var data = CurrentStageData;
            if (data == null) return new FlightParams();
            Tick();
            var p = CareLogic.FlightParamsFor(data.flight, State, Config.care);
            return FarmBuffs.Apply(p, State.farm.buffs);
        }

        /// <summary>
        /// 비행 결과 반영: 비행 중에 먹은 먹이는 바로 배를 채우고, 들고 온 먹이는 창고로,
        /// 떨어뜨린 먹이는 잃는다. 잡은 먹이는 모두 도감에 기록된다.
        /// </summary>
        public static FlightCareReport ApplyFlightResult(FlightResult result)
        {
            if (!IsLoaded || result == null) return null;
            Tick();

            var report = new FlightCareReport { foodCount = result.caught.Count, injured = result.Injured };
            foreach (var c in result.caught)
            {
                if (c.food == null) continue;
                report.foods.Add(c.food);
                if (c.rare) report.rareCount++;
                if (State.Codex(c.food.id) == null && !report.newInCodex.Contains(c.food)) report.newInCodex.Add(c.food);
                CareLogic.RecordCatch(State, c.food.id, c.rare);

                if (c.eaten)
                {
                    report.eaten.Add(c.food);
                    var o = CareLogic.Feed(State, Config.care, c.satiety, c.food.trait);
                    report.absorbed += o.absorbed;
                    report.wasted += o.wasted;
                    report.xpGained += o.xpGained;
                    if (o.stageChanged)
                    {
                        report.grew = true;
                        report.newStage = o.newStage;
                        if (o.newForm != DragonForm.None) report.newForm = o.newForm;
                    }
                }
                else if (c.dropped)
                {
                    report.dropped.Add(c.food);
                }
                else if (CareLogic.Store(State, Config.care, c.food.id, c.satiety, c.food.trait, c.rare))
                {
                    report.stored.Add(c.food);
                }
                else
                {
                    report.overflow.Add(c.food);
                }
            }

            if (result.Injured) State.injured = true;
            State.totalFlights++;
            // 버프는 비행 횟수로 유지된다: 한 번 날고 오면 1씩 줄어든다
            if (State.farm.buffs.Count > 0)
            {
                FarmLogic.CountDownBuffs(State.farm);
                FarmChanged?.Invoke();
            }
            LastFlightReport = report;
            Save();

            if (report.grew) StageChanged?.Invoke(report.newStage);
            return report;
        }

        /// <summary>둥지에서 창고 먹이 하나를 먹인다. 배가 꽉 찼으면 false</summary>
        public static bool FeedFromStorage(int index)
        {
            if (!IsLoaded) return false;
            Tick();
            if (!CareLogic.FeedFromStorage(State, Config.care, index, out var o)) return false;
            Save();
            Fed?.Invoke(o);
            if (o.stageChanged) StageChanged?.Invoke(o.newStage);
            return true;
        }

        // ───────────── 동물 농장 ─────────────

        public static FarmState Farm => State?.farm;
        public static FarmSettings FarmSettings => Config ? Config.farm : null;
        public static CombineTable CombineTable => Config ? Config.combineTable : null;
        static bool FarmReady => IsLoaded && Config.farm != null;

        /// <summary>
        /// [임시] 창고 먹이를 산 채로 우리에 옮긴다. 포획해서 바로 데려오는 기능이 합쳐지면 그쪽으로 바꾼다.
        /// 우리가 가득 차면 false
        /// </summary>
        public static bool MoveStorageToFarm(int index)
        {
            if (!FarmReady || index < 0 || index >= State.storage.Count) return false;
            var f = State.storage[index];
            if (FarmLogic.AddAnimal(State.farm, Config.farm, f.id, f.trait, f.satiety, f.rare) == null) return false;
            State.storage.RemoveAt(index);
            Save();
            FarmChanged?.Invoke();
            return true;
        }

        /// <summary>우리의 동물을 드래곤에게 먹인다. 배가 꽉 찼으면 false</summary>
        public static bool FeedFarmAnimal(string uid)
        {
            if (!FarmReady) return false;
            Tick();
            if (!FarmLogic.Feed(State.farm, Config.farm, Config.combineTable, State, Config.care, uid, out var o)) return false;
            Save();
            FarmChanged?.Invoke();
            Fed?.Invoke(o.feed);
            if (o.feed.stageChanged) StageChanged?.Invoke(o.feed.newStage);
            return true;
        }

        public static CombineBlock CanCombine(string uidA, string uidB, out CombineRecipe recipe)
        {
            recipe = null;
            if (!FarmReady || !Config.combineTable) return CombineBlock.NoRecipe;
            return FarmLogic.CanCombine(State.farm, Config.farm, Config.combineTable, uidA, uidB, out recipe);
        }

        public static bool StartCombine(string uidA, string uidB)
        {
            if (!FarmReady || !Config.combineTable) return false;
            if (FarmLogic.StartCombine(State.farm, Config.farm, Config.combineTable, uidA, uidB, Clock()) == null) return false;
            Save();
            FarmChanged?.Invoke();
            return true;
        }

        /// <summary>끝난 조합을 거둔다 (둥지에서 주기적으로 부름)</summary>
        public static List<CombineResult> CollectCombines()
        {
            if (!FarmReady || !Config.combineTable || State.farm.jobs.Count == 0) return null;
            var results = FarmLogic.CollectReady(State.farm, Config.farm, Config.combineTable, Clock());
            if (results.Count == 0) return results;
            Save();
            FarmChanged?.Invoke();
            CombinesCollected?.Invoke(results);
            return results;
        }

        /// <summary>우리 확장. 상점이 생기면 상점에서 값을 치른 뒤 부른다</summary>
        public static bool UpgradePen()
        {
            if (!FarmReady || !FarmLogic.UpgradePen(State.farm, Config.farm)) return false;
            Save();
            FarmChanged?.Invoke();
            return true;
        }

        /// <summary>성체가 된 뒤 새 알을 부화시킨다 (도감은 이어짐)</summary>
        public static bool HatchNewEgg()
        {
            if (!IsLoaded || State.stage != GrowthStage.Adult) return false;
            State = CareLogic.HatchNewEgg(State, Config.care, Clock());
            LastFlightReport = null;
            Save();
            Hatched?.Invoke();
            FarmChanged?.Invoke();
            StageChanged?.Invoke(State.stage);
            return true;
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
            FarmLogic.DebugAdvance(State.farm, TimeSpan.FromHours(hours));
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
            Fed = null;
            Hatched = null;
            FarmChanged = null;
            CombinesCollected = null;
            Clock = () => DateTime.UtcNow;
        }

        /// <summary>테스트용: 메모리 상태만 비운다</summary>
        public static void UnloadForTests() => ResetStatics();
    }
}
