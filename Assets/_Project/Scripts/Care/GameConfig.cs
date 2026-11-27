using DragonRecipe.Flight;
using UnityEngine;

namespace DragonRecipe.Care
{
    /// <summary>게임 전체 설정: 성장 단계 데이터, 허기/성장 수치, 씬 이름.</summary>
    [CreateAssetMenu(menuName = "Dragon Recipe/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        public CareSettings care = new CareSettings();
        [Tooltip("GrowthStage 순서대로 (어린, 청소년, 성체)")]
        public DragonStageData[] stages;
        public string nestScene = "Nest";
        public string flightScene = "FlightPrototype";

        public DragonStageData StageData(GrowthStage stage)
        {
            int i = (int)stage;
            if (stages == null || stages.Length == 0) return null;
            return stages[Mathf.Clamp(i, 0, stages.Length - 1)];
        }
    }
}
