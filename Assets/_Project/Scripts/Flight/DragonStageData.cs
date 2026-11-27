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
        public GrowthStage stage = GrowthStage.Hatchling;
        public string displayName = "어린 드래곤";
        [Tooltip("드래곤 모델 배율")]
        public float bodyScale = 1f;
        public FlightParams flight = new FlightParams();
    }
}
