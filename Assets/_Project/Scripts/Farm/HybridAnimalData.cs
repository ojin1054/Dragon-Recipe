using System.Collections.Generic;
using DragonRecipe.Food;
using UnityEngine;

namespace DragonRecipe.Farm
{
    /// <summary>
    /// 조합으로만 얻는 동물. 농장에 두었다가 드래곤에게 먹이면 포만도·성질과 함께 버프를 준다.
    /// 다른 조합의 재료로도 쓸 수 있다 (재료로는 id로만 매칭).
    /// </summary>
    [CreateAssetMenu(menuName = "Dragon Recipe/Hybrid Animal", fileName = "Hybrid_")]
    public class HybridAnimalData : ScriptableObject
    {
        public string id = "hybrid";
        public string displayName = "조합 동물";
        public Sprite icon;
        [Tooltip("도감·조합 화면에서 아직 못 만든 동물의 실루엣")]
        public Sprite silhouette;

        [Header("먹었을 때")]
        [Tooltip("허기를 채우는 양. 일반 먹이보다 낮게 둬서 사냥을 대신하지 않게 한다")]
        public int satiety = 6;
        [Tooltip("먹으면 쌓이는 성질 (성체 모습 계산에 들어간다). 포만도를 이 비율로 나눠 담는다")]
        public List<TraitShare> traits = new List<TraitShare>();

        [Header("버프")]
        public List<BuffEffect> buffs = new List<BuffEffect>();
        [Tooltip("버프가 이어지는 비행 횟수")]
        [Min(1)] public int buffFlights = 3;
    }

    [System.Serializable]
    public class TraitShare
    {
        public FoodTrait trait;
        [Range(0f, 1f)] public float share = 0.5f;
    }
}
