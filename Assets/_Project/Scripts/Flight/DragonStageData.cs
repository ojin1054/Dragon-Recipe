using UnityEngine;

namespace DragonRecipe.Flight
{
    public enum GrowthStage
    {
        Hatchling,
        Juvenile,
        Adult
    }

    /// <summary>성장 단계 하나의 데이터 (비행 수치 + 크기).</summary>
    [CreateAssetMenu(menuName = "Dragon Recipe/Dragon Stage", fileName = "Stage_")]
    public class DragonStageData : ScriptableObject
    {
        /// <summary>씬 빌더가 새 필드 기본값을 채웠는지 (기존 에셋 이전용)</summary>
        [HideInInspector] public int schema;

        public GrowthStage stage = GrowthStage.Hatchling;
        public string displayName = "어린 드래곤";
        [Tooltip("드래곤 모델 배율")]
        public float bodyScale = 1f;
        [Tooltip("동물이 드래곤을 알아채는 거리 배율. 자랄수록 위화감이 줄어 작아진다")]
        [Range(0.1f, 1.5f)] public float preyAwarenessScale = 1f;
        [Tooltip("이 단계의 기본 능력치 (힘·스태미나·체력·공격력·마나). 성체 모습에 따라 더해진다")]
        public DragonStats stats = new DragonStats();
        public FlightParams flight = new FlightParams();

        /// <summary>기본 능력치(힘·스태미나·체력)가 들어간 비행 수치</summary>
        public FlightParams FlightWithStats(DragonStats s = null) => (s ?? stats).ApplyTo(flight);
    }
}
