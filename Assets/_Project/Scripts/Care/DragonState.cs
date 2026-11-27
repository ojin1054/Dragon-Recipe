using System;
using System.Collections.Generic;
using DragonRecipe.Flight;
using DragonRecipe.Food;

namespace DragonRecipe.Care
{
    [Serializable]
    public class TraitAmount
    {
        public FoodTrait trait;
        /// <summary>이 성질의 먹이로 흡수한 포만도 누적 (성체 외형 분기용)</summary>
        public float amount;
    }

    /// <summary>
    /// 저장되는 드래곤 상태. JsonUtility로 직렬화하므로 Dictionary 대신 List를 쓴다.
    /// </summary>
    [Serializable]
    public class DragonState
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string dragonName = "새끼 드래곤";
        /// <summary>현재 포만도 (0 = 굶주림)</summary>
        public float hunger;
        /// <summary>누적 성장 경험치</summary>
        public float growthXp;
        public GrowthStage stage = GrowthStage.Hatchling;
        /// <summary>마지막으로 시간 경과를 반영한 시각 (UTC ticks)</summary>
        public long lastTickUtc;
        /// <summary>추락으로 다친 상태. 치료는 상점 단계에서 연결</summary>
        public bool injured;
        public int totalFlights;
        public List<TraitAmount> traits = new List<TraitAmount>();

        public static DragonState CreateNew(CareSettings s, DateTime nowUtc) => new DragonState
        {
            hunger = s.startingHunger,
            lastTickUtc = nowUtc.Ticks
        };

        public float GetTrait(FoodTrait t)
        {
            foreach (var x in traits)
                if (x.trait == t) return x.amount;
            return 0f;
        }

        public void AddTrait(FoodTrait t, float amount)
        {
            foreach (var x in traits)
            {
                if (x.trait != t) continue;
                x.amount += amount;
                return;
            }
            traits.Add(new TraitAmount { trait = t, amount = amount });
        }
    }
}
