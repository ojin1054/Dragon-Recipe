using UnityEngine;

namespace DragonRecipe.Food
{
    /// <summary>먹이 성질. 나중에 어떤 성질을 많이 먹었는지로 성체 외형이 갈린다.</summary>
    public enum FoodTrait
    {
        Meat,
        Fish,
        Fire,
        Plant
    }

    [CreateAssetMenu(menuName = "Dragon Recipe/Food", fileName = "Food_")]
    public class FoodData : ScriptableObject
    {
        public string id = "food";
        public string displayName = "먹이";
        [Tooltip("허기를 채우는 양")]
        public int satiety = 10;
        public FoodTrait trait = FoodTrait.Meat;

        [Header("외형 (프리미티브 placeholder)")]
        public Color color = Color.white;
        public Vector3 bodySize = new Vector3(0.8f, 0.6f, 1.2f);

        [Header("행동")]
        public float wanderSpeed = 1f;
        [Tooltip("드래곤이 가까이 오면 도망가는지")]
        public bool flees;
        public float fleeSpeed = 5f;
        public float fleeRadius = 25f;

        [Header("스폰")]
        [Tooltip("스폰 가중치 (클수록 자주 등장)")]
        public float spawnWeight = 1f;
    }
}
