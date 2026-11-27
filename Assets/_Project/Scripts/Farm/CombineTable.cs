using System;
using System.Collections.Generic;
using DragonRecipe.Food;
using UnityEngine;

namespace DragonRecipe.Farm
{
    public enum RecipeInputKind
    {
        /// <summary>이 성질의 일반 동물이면 아무거나 (조합 동물은 해당 안 됨)</summary>
        Trait,
        /// <summary>이 id의 동물만 (일반 먹이 id 또는 조합 동물 id)</summary>
        Animal
    }

    [Serializable]
    public class RecipeInput
    {
        public RecipeInputKind kind;
        public FoodTrait trait;
        public string animalId;

        public bool Matches(FarmAnimal a)
        {
            if (a == null) return false;
            return kind == RecipeInputKind.Trait
                ? !a.hybrid && a.trait == trait
                : a.id == animalId;
        }

        /// <summary>id로 고르는 재료가 성질로 고르는 재료보다 우선한다</summary>
        public int Specificity => kind == RecipeInputKind.Animal ? 1 : 0;
    }

    /// <summary>조합표 한 줄: 두 재료 → 조합 동물. 재료 순서는 상관없다</summary>
    [Serializable]
    public class CombineRecipe
    {
        [Tooltip("저장·도감용 고유 id. 한 번 정하면 바꾸지 않는다")]
        public string id;
        public RecipeInput a = new RecipeInput();
        public RecipeInput b = new RecipeInput();
        public HybridAnimalData result;

        [Tooltip("기본 성공 확률")]
        [Range(0f, 1f)] public float successChance = 0.7f;
        [Tooltip("조합이 끝나기까지 걸리는 실제 시간 (분)")]
        [Min(0f)] public float durationMinutes = 30f;

        public bool Matches(FarmAnimal x, FarmAnimal y)
            => (a.Matches(x) && b.Matches(y)) || (a.Matches(y) && b.Matches(x));

        public int Specificity => a.Specificity + b.Specificity;
    }

    [CreateAssetMenu(menuName = "Dragon Recipe/Combine Table", fileName = "CombineTable")]
    public class CombineTable : ScriptableObject
    {
        public List<CombineRecipe> recipes = new List<CombineRecipe>();

        [Header("성공 확률 보정")]
        [Tooltip("희귀 재료 한 마리당 더해지는 성공 확률")]
        [Range(0f, 0.5f)] public float rareBonus = 0.1f;
        [Tooltip("같은 조합에 연속으로 실패할 때마다 더해지는 성공 확률 (성공하면 초기화)")]
        [Range(0f, 0.5f)] public float pityPerFail = 0.1f;
        [Tooltip("보정을 다 더해도 넘지 않는 성공 확률")]
        [Range(0f, 1f)] public float maxSuccessChance = 0.95f;

        [Header("실패")]
        [Tooltip("실패하면 재료 중 한 마리는 농장으로 돌아온다. 끄면 둘 다 사라진다")]
        public bool failReturnsOne = true;

        /// <summary>두 동물로 만들 수 있는 조합. 여러 개가 맞으면 id로 고른 재료가 많은 쪽</summary>
        public CombineRecipe Find(FarmAnimal x, FarmAnimal y)
        {
            CombineRecipe best = null;
            foreach (var r in recipes)
            {
                if (r == null || r.result == null || !r.Matches(x, y)) continue;
                if (best == null || r.Specificity > best.Specificity) best = r;
            }
            return best;
        }

        public CombineRecipe ById(string recipeId)
        {
            foreach (var r in recipes)
                if (r != null && r.id == recipeId) return r;
            return null;
        }

        public HybridAnimalData Hybrid(string hybridId)
        {
            foreach (var r in recipes)
                if (r?.result != null && r.result.id == hybridId) return r.result;
            return null;
        }

        /// <summary>희귀 재료와 연속 실패 보정을 더한 성공 확률</summary>
        public float SuccessChance(CombineRecipe r, FarmAnimal x, FarmAnimal y, int failStreak)
        {
            float p = r.successChance;
            if (x != null && x.rare) p += rareBonus;
            if (y != null && y.rare) p += rareBonus;
            p += Mathf.Max(0, failStreak) * pityPerFail;
            return Mathf.Clamp(p, 0f, Mathf.Max(r.successChance, maxSuccessChance));
        }
    }
}
