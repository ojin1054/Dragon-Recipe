using System;
using System.Collections.Generic;
using DragonRecipe.Core;
using DragonRecipe.Farm;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using DragonRecipe.Tribes;
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
        /// <summary>종족 호감도·제단·전쟁이 바뀜</summary>
        public static event Action TribesChanged;

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
            s.tribes ??= TribeLogic.CreateNew(Config.tribes ?? new TribeSettings());
            TribeLogic.Ensure(s.tribes, Config.tribes ?? new TribeSettings());
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
            var p = CareLogic.FlightParamsFor(data.FlightWithStats(CareStats.Base()), State, Config.care); // 능력치(힘·스태미나·체력) → 굶주림 → 버프
            p = FarmBuffs.Apply(p, State.farm.buffs);
            return FarmBuffs.Apply(p, TribeLogic.Blessings(State.tribes, TribeSettings));
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
            // 종족: 호감도가 조금 돌아가고, 제물이 오고, 전쟁이 진행된다
            LastTribeReport = TribeLogic.AfterFlight(State.tribes, TribeSettings, () => UnityEngine.Random.value);
            CollectOfferings();
            TribesChanged?.Invoke();
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

        // ───────────── 종족 ─────────────

        public static TribeState Tribes => State?.tribes;
        public static TribeSettings TribeSettings => Config && Config.tribes != null ? Config.tribes : _defaultTribes ??= new TribeSettings();
        static TribeSettings _defaultTribes;
        /// <summary>마지막 비행 뒤 종족 쪽에서 일어난 일</summary>
        public static TribeReport LastTribeReport { get; private set; }

        /// <summary>
        /// 드래곤이 종족을 공격함 (공격 기능이 부른다). 바로 호감도에 반영되고, 병사들도 즉시 태도를 바꾼다.
        /// 저장은 비행이 끝날 때 한꺼번에.
        /// </summary>
        /// <param name="repScale">호감도 변화 배율 (방어전 침입자는 작게)</param>
        /// <param name="contribution">참전 중 승률 기여 (쓰러뜨린 수)</param>
        public static void ReportTribeAttack(Tribe target, float repScale = 1f, float contribution = 1f)
        {
            if (!IsLoaded) return;
            TribeLogic.RegisterAttack(State.tribes, TribeSettings, target, repScale, contribution);
            TribesChanged?.Invoke();
        }

        /// <summary>방어전: 적이 편든 쪽 마을까지 뚫고 들어옴</summary>
        public static void ReportWarBreach()
        {
            if (!IsLoaded) return;
            TribeLogic.RegisterBreach(State.tribes, TribeSettings);
            TribesChanged?.Invoke();
        }

        /// <summary>엘프 땅에서 먹이를 사냥함</summary>
        public static void ReportElfLandHunt()
        {
            if (!IsLoaded) return;
            TribeLogic.RegisterElfLandHunt(State.tribes, TribeSettings);
            TribesChanged?.Invoke();
        }

        /// <summary>제단의 제물을 창고로 옮긴다 (창고에 자리가 있는 만큼). 옮긴 수</summary>
        public static int CollectOfferings()
        {
            if (!IsLoaded || State.tribes.altar.Count == 0) return 0;
            int moved = 0;
            for (int i = 0; i < State.tribes.altar.Count;)
            {
                var o = State.tribes.altar[i];
                var food = FindFood(o.foodId);
                if (!food)
                {
                    State.tribes.altar.RemoveAt(i); // 없는 먹이 id는 버린다
                    continue;
                }
                float satiety = food.satiety * (o.rare ? 2f : 1f) /* 희귀 = 포만도 2배 */;
                if (!CareLogic.Store(State, Config.care, food.id, satiety, food.trait, o.rare)) break;
                State.tribes.altar.RemoveAt(i);
                moved++;
            }
            if (moved > 0)
            {
                Save();
                TribesChanged?.Invoke();
            }
            return moved;
        }

        /// <summary>전쟁 참여 요청에 답한다: 한쪽 편으로 참전</summary>
        public static bool JoinWar(Tribe side)
        {
            if (!IsLoaded || !TribeLogic.JoinWar(State.tribes, TribeSettings, side)) return false;
            Save();
            TribesChanged?.Invoke();
            return true;
        }

        /// <summary>디버그: 비행 한 번이 끝난 것처럼 종족 쪽만 진행한다 (rand를 주면 승패·제물을 정할 수 있음)</summary>
        public static TribeReport DebugTribeAfterFlight(Func<float> rand = null)
        {
            if (!IsLoaded) return null;
            LastTribeReport = TribeLogic.AfterFlight(State.tribes, TribeSettings, rand ?? (() => UnityEngine.Random.value));
            CollectOfferings();
            Save();
            TribesChanged?.Invoke();
            return LastTribeReport;
        }

        /// <summary>디버그: 종족 상태를 직접 고친 뒤 저장하고 알린다</summary>
        public static void DebugTribesEdited()
        {
            if (!IsLoaded) return;
            Save();
            TribesChanged?.Invoke();
        }

        /// <summary>전쟁 참여 요청을 거절한다</summary>
        public static bool DeclineWar()
        {
            if (!IsLoaded || !TribeLogic.DeclineWar(State.tribes, TribeSettings)) return false;
            Save();
            TribesChanged?.Invoke();
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
            if (State != null) TribeLogic.Ensure(State.tribes, TribeSettings);
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

        // ───────────── 개발자 모드 ─────────────

        const string DevBackupKey = "DragonRecipe.DevBackup";

        /// <summary>개발자 모드로 드래곤을 바꿔 둔 상태 (원래 드래곤이 백업돼 있음)</summary>
        public static bool DevModeActive => PlayerPrefs.HasKey(DevBackupKey);

        /// <summary>
        /// 개발자 모드: 지금 드래곤을 처음 한 번 백업해 두고, 원하는 성장 단계·주 성질·성체 모습으로 바꾼다.
        /// 포만도는 가득, 부상은 낫는다. 농장·종족·도감은 그대로 둔다.
        /// </summary>
        public static void DevSetDragon(GrowthStage stage, FoodTrait trait, DragonForm form = DragonForm.None)
        {
            if (!IsLoaded) return;
            if (!DevModeActive)
            {
                PlayerPrefs.SetString(DevBackupKey, JsonUtility.ToJson(State));
                PlayerPrefs.Save();
            }
            var c = Config.care;
            State.stage = stage;
            State.growthXp = stage switch
            {
                GrowthStage.Adult => c.xpToAdult,
                GrowthStage.Juvenile => c.xpToJuvenile,
                _ => 0f
            };
            State.traits.Clear();
            if (stage == GrowthStage.Adult && form == DragonForm.Prism)
            {
                // 골고루 먹은 드래곤
                foreach (FoodTrait t in Enum.GetValues(typeof(FoodTrait))) State.AddTrait(t, 25f);
            }
            else if (stage != GrowthStage.Hatchling) // 새끼는 아직 아무것도 안 먹은 상태
            {
                if (stage == GrowthStage.Adult && form != DragonForm.None) trait = FormTrait(form, trait);
                State.AddTrait(trait, 100f);
            }
            State.form = stage == GrowthStage.Adult ? (form == DragonForm.None ? CareLogic.FormFor(State, c) : form) : DragonForm.None;
            if (State.form != DragonForm.None && !State.codexForms.Contains(State.form)) State.codexForms.Add(State.form);
            State.hunger = c.maxHunger;
            State.injured = false;
            Save();
            StageChanged?.Invoke(State.stage);
        }

        static FoodTrait FormTrait(DragonForm form, FoodTrait fallback) => form switch
        {
            DragonForm.Hunter => FoodTrait.Meat,
            DragonForm.Tide => FoodTrait.Fish,
            DragonForm.Ember => FoodTrait.Fire,
            DragonForm.Grove => FoodTrait.Plant,
            _ => fallback
        };

        /// <summary>개발자 모드를 끝내고 백업해 둔 원래 드래곤으로 되돌린다</summary>
        public static void DevRestore()
        {
            if (!DevModeActive || Config == null) return;
            var json = PlayerPrefs.GetString(DevBackupKey);
            PlayerPrefs.DeleteKey(DevBackupKey);
            PlayerPrefs.Save();
            DragonState s = null;
            try { s = JsonUtility.FromJson<DragonState>(json); }
            catch (Exception) { /* 백업이 깨졌으면 지금 드래곤을 그대로 둔다 */ }
            if (s == null) return;
            Migrate(s);
            State = s;
            Tick();
            Save();
            StageChanged?.Invoke(State.stage);
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
            TribesChanged = null;
            LastTribeReport = null;
            Clock = () => DateTime.UtcNow;
        }

        /// <summary>테스트용: 메모리 상태만 비운다</summary>
        public static void UnloadForTests() => ResetStatics();
    }
}
