using System;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using UnityEngine;

namespace DragonRecipe.Care
{
    public struct FeedOutcome
    {
        /// <summary>실제로 배를 채운 양</summary>
        public float absorbed;
        /// <summary>배가 불러 버려진 양 (먹이 창고는 다음 단계)</summary>
        public float wasted;
        public float xpGained;
        public bool stageChanged;
        public GrowthStage newStage;
        /// <summary>이번에 성체가 됐다면 정해진 모습</summary>
        public DragonForm newForm;
    }

    /// <summary>허기 감소, 먹이기, 성장, 굶주림 패널티 (순수 함수 → 테스트 가능).</summary>
    public static class CareLogic
    {
        public static bool IsStarving(DragonState s) => s.hunger <= 0.001f;

        public static bool IsHungry(DragonState s, CareSettings c)
            => s.hunger < c.maxHunger * c.hungryThreshold;

        /// <summary>마지막 기록 이후 흐른 실제 시간만큼 허기를 줄인다 (offlineCapHours 상한).</summary>
        public static void ApplyElapsed(DragonState s, CareSettings c, DateTime nowUtc)
        {
            long now = nowUtc.Ticks;
            if (s.lastTickUtc <= 0 || now <= s.lastTickUtc)
            {
                // 처음이거나 기기 시계가 뒤로 감 → 감소 없이 기준만 맞춤
                s.lastTickUtc = now;
                return;
            }

            double hours = TimeSpan.FromTicks(now - s.lastTickUtc).TotalHours;
            hours = Math.Min(hours, c.offlineCapHours);
            s.hunger = Mathf.Max(0f, s.hunger - (float)(hours * c.hungerDecayPerHour));
            s.lastTickUtc = now;
        }

        public static FeedOutcome Feed(DragonState s, CareSettings c, float satiety, FoodTrait trait)
        {
            var o = new FeedOutcome { newStage = s.stage };
            if (satiety <= 0f) return o;

            bool wasStarving = IsStarving(s);
            float room = Mathf.Max(0f, c.maxHunger - s.hunger);
            o.absorbed = Mathf.Min(room, satiety);
            o.wasted = satiety - o.absorbed;
            s.hunger += o.absorbed;

            // 굶주린 상태였다면 처음 일부는 회복에만 쓰임
            float xp = o.absorbed;
            if (wasStarving) xp = Mathf.Max(0f, xp - c.starvationRecovery);
            o.xpGained = xp;
            s.growthXp += xp;
            s.AddTrait(trait, o.absorbed);

            var stage = StageForXp(s.growthXp, c);
            if (stage > s.stage)
            {
                s.stage = stage;
                o.stageChanged = true;
                o.newStage = stage;
                if (stage == GrowthStage.Adult)
                {
                    // 성체가 되는 순간, 지금까지 먹은 먹이로 모습이 정해진다
                    s.form = FormFor(s, c);
                    if (!s.codexForms.Contains(s.form)) s.codexForms.Add(s.form);
                    o.newForm = s.form;
                }
            }
            return o;
        }

        public static bool HasRoom(DragonState s, CareSettings c) => s.hunger < c.maxHunger - 0.5f;

        // ───────────── 성체 모습 ─────────────

        /// <summary>성질별 비율 (0~1). 아무것도 안 먹었으면 0</summary>
        public static float TraitShare(DragonState s, FoodTrait t)
        {
            float total = s.TotalTraits;
            return total > 0f ? s.GetTrait(t) / total : 0f;
        }

        /// <summary>가장 많이 먹은 성질. 아무것도 안 먹었으면 false</summary>
        public static bool DominantTrait(DragonState s, out FoodTrait trait, out float share)
        {
            trait = FoodTrait.Meat;
            share = 0f;
            float total = s.TotalTraits;
            if (total <= 0f) return false;
            foreach (FoodTrait t in Enum.GetValues(typeof(FoodTrait)))
            {
                float sh = s.GetTrait(t) / total;
                if (sh > share)
                {
                    share = sh;
                    trait = t;
                }
            }
            return true;
        }

        public static DragonForm FormFor(DragonState s, CareSettings c)
        {
            if (!DominantTrait(s, out var trait, out float share) || share < c.formDominance) return DragonForm.Prism;
            return FormOf(trait);
        }

        public static DragonForm FormOf(FoodTrait t) => t switch
        {
            FoodTrait.Meat => DragonForm.Hunter,
            FoodTrait.Fish => DragonForm.Tide,
            FoodTrait.Fire => DragonForm.Ember,
            _ => DragonForm.Grove
        };

        // ───────────── 먹이 창고 ─────────────

        /// <summary>창고에 넣는다. 가득 차 있으면 false (버려짐)</summary>
        public static bool Store(DragonState s, CareSettings c, string id, float satiety, FoodTrait trait, bool rare)
        {
            if (s.storage.Count >= c.storageCapacity) return false;
            s.storage.Add(new StoredFood { id = id, satiety = satiety, trait = trait, rare = rare });
            return true;
        }

        /// <summary>창고에서 꺼내 먹인다. 배가 꽉 찼거나 번호가 틀리면 먹이지 않는다</summary>
        public static bool FeedFromStorage(DragonState s, CareSettings c, int index, out FeedOutcome outcome)
        {
            outcome = new FeedOutcome { newStage = s.stage };
            if (index < 0 || index >= s.storage.Count || !HasRoom(s, c)) return false;
            var f = s.storage[index];
            s.storage.RemoveAt(index);
            outcome = Feed(s, c, f.satiety, f.trait);
            return true;
        }

        // ───────────── 도감 ─────────────

        public static void RecordCatch(DragonState s, string foodId, bool rare)
        {
            if (string.IsNullOrEmpty(foodId)) return;
            var entry = s.Codex(foodId);
            if (entry == null)
            {
                entry = new CodexFood { id = foodId };
                s.codexFoods.Add(entry);
            }
            entry.caught++;
            if (rare) entry.rareCaught++;
        }

        /// <summary>도감에서 성질 아이콘이 보이기 시작하는 포획 수</summary>
        public const int CodexTraitReveal = 3;

        /// <summary>
        /// 성체가 된 뒤 새 알을 부화시킨다. 도감과 비행 횟수, 세대, 농장이 이어진다 (농장 버프는 사라짐).
        /// </summary>
        public static DragonState HatchNewEgg(DragonState old, CareSettings c, DateTime nowUtc)
        {
            var s = DragonState.CreateNew(c, nowUtc);
            s.generation = old.generation + 1;
            s.totalFlights = old.totalFlights;
            s.codexFoods = old.codexFoods;
            s.codexForms = old.codexForms;
            s.farm = old.farm ?? new DragonRecipe.Farm.FarmState();
            DragonRecipe.Farm.FarmLogic.OnNewEgg(s.farm);
            return s;
        }

        public static GrowthStage StageForXp(float xp, CareSettings c)
        {
            if (xp >= c.xpToAdult) return GrowthStage.Adult;
            if (xp >= c.xpToJuvenile) return GrowthStage.Juvenile;
            return GrowthStage.Hatchling;
        }

        /// <summary>현재 단계 안에서의 성장 진행도 (0~1). 성체는 1.</summary>
        public static float StageProgress(DragonState s, CareSettings c)
        {
            switch (s.stage)
            {
                case GrowthStage.Hatchling: return Mathf.Clamp01(s.growthXp / c.xpToJuvenile);
                case GrowthStage.Juvenile:
                    return Mathf.Clamp01((s.growthXp - c.xpToJuvenile) / Mathf.Max(1f, c.xpToAdult - c.xpToJuvenile));
                default: return 1f;
            }
        }

        /// <summary>현재 상태(굶주림)를 반영한 비행 수치. 원본은 건드리지 않는다.</summary>
        public static FlightParams FlightParamsFor(FlightParams stageParams, DragonState s, CareSettings c)
        {
            var p = stageParams.Clone();
            if (IsStarving(s))
            {
                p.maxStamina *= c.starvingStaminaMultiplier;
                p.flapLift *= c.starvingFlapLiftMultiplier;
            }
            return p;
        }

        public static string StageName(GrowthStage stage) => stage switch
        {
            GrowthStage.Hatchling => "어린 드래곤",
            GrowthStage.Juvenile => "청소년 드래곤",
            _ => "성체 드래곤"
        };
    }
}
