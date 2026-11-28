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
        [Tooltip("모든 먹이 (먹이 창고 / 도감에서 id로 찾는다)")]
        public DragonRecipe.Food.FoodData[] foods;
        [Header("동물 농장")]
        public DragonRecipe.Farm.FarmSettings farm = new DragonRecipe.Farm.FarmSettings();
        public DragonRecipe.Farm.CombineTable combineTable;
        [Header("종족 (사람·오크·엘프)")]
        public DragonRecipe.Tribes.TribeSettings tribes = new DragonRecipe.Tribes.TribeSettings();

        public string nestScene = "Nest";
        public string flightScene = "FlightPrototype";
        [Tooltip("참전 중 비행 전용 전장")]
        public string warScene = "WarBattle";

        public DragonStageData StageData(GrowthStage stage)
        {
            int i = (int)stage;
            if (stages == null || stages.Length == 0) return null;
            return stages[Mathf.Clamp(i, 0, stages.Length - 1)];
        }

        public DragonRecipe.Food.FoodData FindFood(string id)
        {
            if (foods == null) return null;
            foreach (var f in foods)
                if (f && f.id == id) return f;
            return null;
        }
    }
}
