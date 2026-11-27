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
            }
            return o;
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
