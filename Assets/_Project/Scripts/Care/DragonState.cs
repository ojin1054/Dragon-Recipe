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

    /// <summary>성체 모습. 어떤 성질의 먹이를 많이 먹었는지로 정해진다</summary>
    public enum DragonForm
    {
        /// <summary>아직 성체가 아님</summary>
        None,
        /// <summary>고기를 많이 먹음: 큰 뿔과 등 가시</summary>
        Hunter,
        /// <summary>물고기를 많이 먹음: 등지느러미와 물빛 비늘</summary>
        Tide,
        /// <summary>불 먹이를 많이 먹음: 불꽃 갈기</summary>
        Ember,
        /// <summary>식물을 많이 먹음: 잎사귀 날개</summary>
        Grove,
        /// <summary>골고루 먹음: 무지갯빛 수정</summary>
        Prism
    }

    /// <summary>먹이 창고에 보관된 먹이 한 개</summary>
    [Serializable]
    public class StoredFood
    {
        public string id;
        /// <summary>아슬아슬 / 연속 사냥 / 희귀 보너스가 적용된 포만도</summary>
        public float satiety;
        public FoodTrait trait;
        public bool rare;
    }

    /// <summary>도감: 먹이 한 종류의 기록</summary>
    [Serializable]
    public class CodexFood
    {
        public string id;
        public int caught;
        public int rareCaught;
    }

    /// <summary>
    /// 저장되는 드래곤 상태. JsonUtility로 직렬화하므로 Dictionary 대신 List를 쓴다.
    /// </summary>
    [Serializable]
    public class DragonState
    {
        public const int CurrentVersion = 3;

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

        /// <summary>성체가 되는 순간 정해지는 모습</summary>
        public DragonForm form = DragonForm.None;
        /// <summary>몇 번째로 키우는 드래곤인지 (새 알을 부화시킬 때마다 +1)</summary>
        public int generation = 1;

        /// <summary>둥지로 가져온 먹이</summary>
        public List<StoredFood> storage = new List<StoredFood>();

        // 도감은 새 알을 부화시켜도 이어진다
        public List<CodexFood> codexFoods = new List<CodexFood>();
        public List<DragonForm> codexForms = new List<DragonForm>();

        /// <summary>둥지 동물 농장. 새 알을 부화시켜도 이어진다 (버프만 사라짐)</summary>
        public DragonRecipe.Farm.FarmState farm = new DragonRecipe.Farm.FarmState();

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

        public float TotalTraits
        {
            get
            {
                float sum = 0f;
                foreach (var x in traits) sum += x.amount;
                return sum;
            }
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

        public CodexFood Codex(string foodId)
        {
            foreach (var c in codexFoods)
                if (c.id == foodId) return c;
            return null;
        }

        public bool HasForm(DragonForm f) => codexForms.Contains(f);
    }
}
