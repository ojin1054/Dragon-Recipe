using System;
using System.Collections.Generic;
using DragonRecipe.Care;
using DragonRecipe.Flight;
using DragonRecipe.Food;

namespace DragonRecipe.Farm
{
    public enum CombineBlock
    {
        None,
        /// <summary>그런 동물이 우리에 없음</summary>
        MissingAnimal,
        /// <summary>같은 동물을 두 번 고름</summary>
        SameAnimal,
        /// <summary>조합 칸이 모두 사용 중</summary>
        NoSlot,
        /// <summary>두 동물로 만들 수 있는 조합이 없음</summary>
        NoRecipe
    }

    /// <summary>끝난 조합 하나를 거둔 결과</summary>
    public struct CombineResult
    {
        public string recipeId;
        public bool success;
        /// <summary>처음으로 성공한 조합 (도감에 새로 열림)</summary>
        public bool firstDiscovery;
        /// <summary>성공: 새 조합 동물 / 실패: 돌아온 재료 (없으면 null)</summary>
        public FarmAnimal animal;
    }

    /// <summary>농장 동물을 먹인 결과</summary>
    public struct FarmFeedOutcome
    {
        public FeedOutcome feed;
        /// <summary>새로 붙거나 횟수가 채워진 버프 (일반 동물이면 null)</summary>
        public ActiveBuff buff;
        /// <summary>버프 상한에 걸려 빠진 버프</summary>
        public ActiveBuff removedBuff;
    }

    /// <summary>농장 우리, 조합, 버프 (순수 함수 → 테스트 가능).</summary>
    public static class FarmLogic
    {
        static readonly Random SharedRandom = new Random();

        // ───────────── 우리 ─────────────

        public static int PenCapacity(FarmState f, FarmSettings s) => s.PenCapacity(f.penUpgradeLevel);

        public static bool HasPenRoom(FarmState f, FarmSettings s) => f.animals.Count < PenCapacity(f, s);

        public static FarmAnimal Find(FarmState f, string uid)
        {
            foreach (var a in f.animals)
                if (a.uid == uid) return a;
            return null;
        }

        /// <summary>잡은 동물을 우리에 넣는다. 가득 차 있으면 null</summary>
        public static FarmAnimal AddAnimal(FarmState f, FarmSettings s, string foodId, FoodTrait trait, float satiety, bool rare)
        {
            if (!HasPenRoom(f, s) || string.IsNullOrEmpty(foodId)) return null;
            var a = new FarmAnimal { uid = NewUid(), id = foodId, trait = trait, satiety = satiety, rare = rare };
            f.animals.Add(a);
            return a;
        }

        public static FarmAnimal AddAnimal(FarmState f, FarmSettings s, FoodData food, float satiety, bool rare)
            => food == null ? null : AddAnimal(f, s, food.id, food.trait, satiety, rare);

        /// <summary>다음에 살 수 있는 우리 확장. 다 샀으면 null</summary>
        public static PenUpgrade NextPenUpgrade(FarmState f, FarmSettings s)
            => f.penUpgradeLevel < s.penUpgrades.Count ? s.penUpgrades[f.penUpgradeLevel] : null;

        /// <summary>우리를 한 단계 넓힌다. 값을 치르는 건 상점 쪽에서 한다</summary>
        public static bool UpgradePen(FarmState f, FarmSettings s)
        {
            if (NextPenUpgrade(f, s) == null) return false;
            f.penUpgradeLevel++;
            return true;
        }

        // ───────────── 조합 ─────────────

        public static bool HasCombineSlot(FarmState f, FarmSettings s) => f.jobs.Count < s.combineSlots + f.bonusCombineSlots;

        public static CombineBlock CanCombine(FarmState f, FarmSettings s, CombineTable t, string uidA, string uidB,
            out CombineRecipe recipe)
        {
            recipe = null;
            var a = Find(f, uidA);
            var b = Find(f, uidB);
            if (a == null || b == null) return CombineBlock.MissingAnimal;
            if (a == b) return CombineBlock.SameAnimal;
            if (!HasCombineSlot(f, s)) return CombineBlock.NoSlot;
            recipe = t.Find(a, b);
            return recipe == null ? CombineBlock.NoRecipe : CombineBlock.None;
        }

        /// <summary>현재 보정을 반영한 성공 확률. 조합이 없으면 0</summary>
        public static float SuccessChance(FarmState f, CombineTable t, FarmAnimal a, FarmAnimal b)
        {
            var r = t.Find(a, b);
            if (r == null) return 0f;
            return t.SuccessChance(r, a, b, f.Record(r.id)?.failStreak ?? 0);
        }

        /// <summary>
        /// 두 동물을 우리에서 꺼내 조합을 시작한다. 성공 여부와 실패 시 돌아올 재료는 지금 정해 저장한다.
        /// 시작할 수 없으면 null
        /// </summary>
        public static CombineJob StartCombine(FarmState f, FarmSettings s, CombineTable t, string uidA, string uidB,
            DateTime nowUtc, Random rng = null)
        {
            if (CanCombine(f, s, t, uidA, uidB, out var recipe) != CombineBlock.None) return null;
            rng ??= SharedRandom;

            var a = Find(f, uidA);
            var b = Find(f, uidB);
            float chance = t.SuccessChance(recipe, a, b, f.Record(recipe.id)?.failStreak ?? 0);
            bool success = rng.NextDouble() < chance;
            int returned = -1;
            if (!success && t.failReturnsOne) returned = rng.Next(2);

            f.animals.Remove(a);
            f.animals.Remove(b);
            var job = new CombineJob
            {
                recipeId = recipe.id,
                inputA = a,
                inputB = b,
                startUtcTicks = nowUtc.Ticks,
                readyUtcTicks = nowUtc.Ticks + TimeSpan.FromMinutes(recipe.durationMinutes).Ticks,
                success = success,
                returnedInput = returned
            };
            f.jobs.Add(job);
            return job;
        }

        /// <summary>
        /// 끝난 조합을 모두 거둔다. 우리에 자리가 없으면 그 조합은 칸에 남아 기다린다.
        /// 조합표에서 사라진 조합은 재료를 둘 다 돌려준다 (자리가 날 때까지 기다림).
        /// </summary>
        public static List<CombineResult> CollectReady(FarmState f, FarmSettings s, CombineTable t, DateTime nowUtc)
        {
            var results = new List<CombineResult>();
            for (int i = 0; i < f.jobs.Count; i++)
            {
                var job = f.jobs[i];
                if (!job.IsReady(nowUtc)) continue;

                var recipe = t.ById(job.recipeId);
                if (recipe == null || recipe.result == null)
                {
                    if (PenCapacity(f, s) - f.animals.Count < 2) continue;
                    f.animals.Add(job.inputA);
                    f.animals.Add(job.inputB);
                    f.jobs.RemoveAt(i--);
                    continue;
                }

                FarmAnimal back = job.success ? NewHybrid(recipe.result)
                    : job.returnedInput == 0 ? job.inputA
                    : job.returnedInput == 1 ? job.inputB
                    : null;
                if (back != null && !HasPenRoom(f, s)) continue;
                if (back != null) f.animals.Add(back);
                f.jobs.RemoveAt(i--);

                var rec = f.Record(recipe.id);
                if (rec == null)
                {
                    rec = new RecipeRecord { recipeId = recipe.id };
                    f.recipes.Add(rec);
                }
                bool first = job.success && rec.successes == 0;
                if (job.success)
                {
                    rec.successes++;
                    rec.failStreak = 0;
                }
                else rec.failStreak++;

                results.Add(new CombineResult
                {
                    recipeId = recipe.id,
                    success = job.success,
                    firstDiscovery = first,
                    animal = back
                });
            }
            return results;
        }

        public static FarmAnimal NewHybrid(HybridAnimalData h) => new FarmAnimal
        {
            uid = NewUid(),
            id = h.id,
            hybrid = true,
            trait = MainTrait(h),
            satiety = h.satiety
        };

        /// <summary>조합 동물에서 비율이 가장 큰 성질 (아이콘 표시용)</summary>
        public static FoodTrait MainTrait(HybridAnimalData h)
        {
            var best = FoodTrait.Meat;
            float share = -1f;
            foreach (var t in h.traits)
            {
                if (t.share <= share) continue;
                share = t.share;
                best = t.trait;
            }
            return best;
        }

        // ───────────── 먹이기 ─────────────

        /// <summary>
        /// 우리의 동물을 드래곤에게 먹인다. 배가 꽉 찼거나 없는 동물이면 먹이지 않는다.
        /// 조합 동물은 포만도를 성질 비율대로 나눠 쌓고 버프를 붙인다.
        /// </summary>
        public static bool Feed(FarmState f, FarmSettings s, CombineTable t, DragonState dragon, CareSettings care,
            string uid, out FarmFeedOutcome outcome)
        {
            outcome = new FarmFeedOutcome { feed = new FeedOutcome { newStage = dragon.stage } };
            var a = Find(f, uid);
            if (a == null || !CareLogic.HasRoom(dragon, care)) return false;

            if (!a.hybrid)
            {
                f.animals.Remove(a);
                outcome.feed = CareLogic.Feed(dragon, care, a.satiety, a.trait);
                return true;
            }

            var h = t.Hybrid(a.id);
            if (h == null) return false;
            f.animals.Remove(a);
            outcome.feed = FeedShares(dragon, care, a.satiety, h);
            outcome.buff = AddBuff(f, s, h, out outcome.removedBuff);
            return true;
        }

        static FeedOutcome FeedShares(DragonState dragon, CareSettings care, float satiety, HybridAnimalData h)
        {
            float total = 0f;
            foreach (var t in h.traits) total += Math.Max(0f, t.share);
            if (total <= 0f) return CareLogic.Feed(dragon, care, satiety, MainTrait(h));

            var sum = new FeedOutcome { newStage = dragon.stage };
            foreach (var t in h.traits)
            {
                if (t.share <= 0f) continue;
                var o = CareLogic.Feed(dragon, care, satiety * t.share / total, t.trait);
                sum.absorbed += o.absorbed;
                sum.wasted += o.wasted;
                sum.xpGained += o.xpGained;
                if (!o.stageChanged) continue;
                sum.stageChanged = true;
                sum.newStage = o.newStage;
                if (o.newForm != DragonForm.None) sum.newForm = o.newForm;
            }
            return sum;
        }

        // ───────────── 버프 ─────────────

        /// <summary>
        /// 조합 동물의 버프를 붙인다. 같은 동물의 버프가 있으면 남은 횟수만 채운다.
        /// 상한을 넘으면 남은 횟수가 가장 적은 버프를 뺀다.
        /// </summary>
        public static ActiveBuff AddBuff(FarmState f, FarmSettings s, HybridAnimalData h, out ActiveBuff removed)
        {
            removed = null;
            if (h.buffs == null || h.buffs.Count == 0) return null;

            foreach (var b in f.buffs)
            {
                if (b.sourceId != h.id) continue;
                b.flightsLeft = Math.Max(b.flightsLeft, h.buffFlights);
                return b;
            }

            while (f.buffs.Count >= Math.Max(1, s.maxActiveBuffs))
            {
                int min = 0;
                for (int i = 1; i < f.buffs.Count; i++)
                    if (f.buffs[i].flightsLeft < f.buffs[min].flightsLeft) min = i;
                removed = f.buffs[min];
                f.buffs.RemoveAt(min);
            }

            var buff = new ActiveBuff { sourceId = h.id, flightsLeft = h.buffFlights };
            foreach (var e in h.buffs) buff.effects.Add(new BuffEffect { stat = e.stat, multiplier = e.multiplier });
            f.buffs.Add(buff);
            return buff;
        }

        /// <summary>
        /// 비행을 시작할 때 부른다. 지금 붙은 버프를 반영한 비행 수치를 돌려주고 버프 횟수를 1씩 줄인다.
        /// </summary>
        public static FlightParams BeginFlight(FarmState f, FlightParams source)
        {
            var p = FarmBuffs.Apply(source, f.buffs);
            CountDownBuffs(f);
            return p;
        }

        /// <summary>비행 한 번이 끝났을 때: 버프 남은 횟수를 1씩 줄이고 다 쓴 버프를 뺀다</summary>
        public static void CountDownBuffs(FarmState f)
        {
            for (int i = f.buffs.Count - 1; i >= 0; i--)
            {
                f.buffs[i].flightsLeft--;
                if (f.buffs[i].flightsLeft <= 0) f.buffs.RemoveAt(i);
            }
        }

        /// <summary>디버그: 진행 중인 조합을 그 시간만큼 앞당긴다</summary>
        public static void DebugAdvance(FarmState f, TimeSpan time)
        {
            foreach (var j in f.jobs)
            {
                j.startUtcTicks -= time.Ticks;
                j.readyUtcTicks -= time.Ticks;
            }
        }

        /// <summary>새 알을 부화시킬 때. 농장은 이어지고 버프만 사라진다</summary>
        public static void OnNewEgg(FarmState f) => f.buffs.Clear();

        static string NewUid() => Guid.NewGuid().ToString("N");
    }
}
