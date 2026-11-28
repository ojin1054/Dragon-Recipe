using System;
using System.Collections.Generic;
using DragonRecipe.Food;
using UnityEngine;

namespace DragonRecipe.Farm
{
    /// <summary>농장 수치. GameConfig 에셋에서 튜닝한다</summary>
    [Serializable]
    public class FarmSettings
    {
        [Header("우리")]
        [Tooltip("처음 키울 수 있는 동물 수")]
        [Min(1)] public int basePenCapacity = 4;
        [Tooltip("상점에서 사는 우리 확장. 순서대로 산다")]
        public List<PenUpgrade> penUpgrades = new List<PenUpgrade>
        {
            new PenUpgrade { cost = 100, addCapacity = 2 },
            new PenUpgrade { cost = 250, addCapacity = 2 },
            new PenUpgrade { cost = 500, addCapacity = 4 }
        };

        [Header("조합")]
        [Tooltip("동시에 돌릴 수 있는 조합 수")]
        [Min(1)] public int combineSlots = 1;

        [Header("버프")]
        [Tooltip("한 번에 붙어 있을 수 있는 버프 수. 넘치면 남은 횟수가 가장 적은 버프가 빠진다")]
        [Min(1)] public int maxActiveBuffs = 3;

        public int PenCapacity(int upgradeLevel)
        {
            int cap = basePenCapacity;
            int n = Mathf.Min(upgradeLevel, penUpgrades.Count);
            for (int i = 0; i < n; i++) cap += penUpgrades[i].addCapacity;
            return cap;
        }
    }

    [Serializable]
    public class PenUpgrade
    {
        [Tooltip("상점 가격 (화폐는 상점 단계에서 정함)")]
        public int cost;
        public int addCapacity;
    }

    /// <summary>농장에 있는 동물 한 마리</summary>
    [Serializable]
    public class FarmAnimal
    {
        /// <summary>같은 종 여러 마리를 구분하는 값</summary>
        public string uid;
        /// <summary>FoodData.id 또는 HybridAnimalData.id</summary>
        public string id;
        public bool hybrid;
        /// <summary>일반 동물의 성질 (조합 동물은 HybridAnimalData.traits를 쓴다)</summary>
        public FoodTrait trait;
        public bool rare;
        /// <summary>일반 동물을 먹였을 때 채우는 포만도 (잡을 때 보너스 포함)</summary>
        public float satiety;
    }

    /// <summary>진행 중인 조합 한 건</summary>
    [Serializable]
    public class CombineJob
    {
        public string recipeId;
        public FarmAnimal inputA;
        public FarmAnimal inputB;
        public long startUtcTicks;
        public long readyUtcTicks;
        /// <summary>시작할 때 미리 정해 둔다 (껐다 켜서 결과를 바꾸지 못하게)</summary>
        public bool success;
        /// <summary>실패했을 때 돌아오는 재료 (0 = A, 1 = B, -1 = 없음). 시작할 때 정한다</summary>
        public int returnedInput = -1;

        public bool IsReady(DateTime nowUtc) => nowUtc.Ticks >= readyUtcTicks;

        /// <summary>남은 시간 비율 (0 = 막 시작, 1 = 완료). 게이지용</summary>
        public float Progress(DateTime nowUtc)
        {
            long total = readyUtcTicks - startUtcTicks;
            if (total <= 0) return 1f;
            return Mathf.Clamp01((float)((double)(nowUtc.Ticks - startUtcTicks) / total));
        }
    }

    /// <summary>조합 하나에 대한 기록 (도감 + 연속 실패 보정)</summary>
    [Serializable]
    public class RecipeRecord
    {
        public string recipeId;
        public int successes;
        public int failStreak;
    }

    /// <summary>
    /// 저장되는 농장 상태. 둥지에 속하므로 새 알을 부화시켜도 이어진다.
    /// 진행 중인 버프는 지금 드래곤의 것이라 새 알을 부화시키면 비운다.
    /// </summary>
    [Serializable]
    public class FarmState
    {
        public List<FarmAnimal> animals = new List<FarmAnimal>();
        /// <summary>상점에서 산 우리 확장 수</summary>
        public int penUpgradeLevel;
        /// <summary>업그레이드로 늘어난 조합 슬롯 수</summary>
        public int bonusCombineSlots;
        public List<CombineJob> jobs = new List<CombineJob>();
        public List<ActiveBuff> buffs = new List<ActiveBuff>();
        public List<RecipeRecord> recipes = new List<RecipeRecord>();

        public RecipeRecord Record(string recipeId)
        {
            foreach (var r in recipes)
                if (r.recipeId == recipeId) return r;
            return null;
        }

        public bool Discovered(string recipeId) => Record(recipeId)?.successes > 0;
    }
}
