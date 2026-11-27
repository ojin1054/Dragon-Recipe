using UnityEngine;
using UnityEngine.Serialization;

namespace DragonRecipe.Food
{
    /// <summary>먹이 성질. 어떤 성질을 많이 먹었는지로 성체 외형이 갈린다.</summary>
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
        /// <summary>씬 빌더가 새 필드 기본값을 채웠는지 (기존 에셋 이전용)</summary>
        [HideInInspector] public int schema;

        public string id = "food";
        public string displayName = "먹이";
        [Tooltip("허기를 채우는 양")]
        public int satiety = 10;
        public FoodTrait trait = FoodTrait.Meat;

        [Tooltip("결과 화면 등에 쓰는 아이콘")]
        public Sprite icon;

        [Header("외형 (프리미티브 placeholder)")]
        public Color color = Color.white;
        public Vector3 bodySize = new Vector3(0.8f, 0.6f, 1.2f);

        [Header("행동")]
        public float wanderSpeed = 1f;
        [Tooltip("드래곤이 가까이 오면 도망가는지")]
        public bool flees;
        public float fleeSpeed = 5f;
        [Tooltip("드래곤을 알아채는 거리. 드래곤이 자랄수록 성장 단계의 배율만큼 좁아진다")]
        public float fleeRadius = 25f;

        [Header("무리")]
        [Tooltip("한 무리의 최소 / 최대 마릿수 (1 = 혼자 다님)")]
        public int herdMin = 1;
        public int herdMax = 1;
        [Tooltip("무리 중 하나가 잡히면 나머지가 흩어지는 속도")]
        public float scatterSpeed = 5f;

        [Header("사냥")]
        [Tooltip("동물의 힘. 매단 동물 힘의 합이 드래곤 힘보다 크면 비행이 흔들리고, 무거울수록 둔해진다")]
        [FormerlySerializedAs("carryWeight")]
        public float strength = 10f;
        [Tooltip("매달린 채 버둥거려 드래곤을 흔든다. 제대로(아슬아슬하게) 못 잡으면 걷어차인다")]
        public bool struggles;

        [Header("스폰")]
        [Tooltip("스폰 가중치 (클수록 자주 등장)")]
        public float spawnWeight = 1f;
    }
}
