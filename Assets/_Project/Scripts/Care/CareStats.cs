using DragonRecipe.Flight;

namespace DragonRecipe.Care
{
    /// <summary>지금 드래곤의 능력치 (힘·스태미나·체력·공격력·마나)</summary>
    public static class CareStats
    {
        /// <summary>성장 단계 기본값 + 성체 모습 보너스 (굶주림·버프 전)</summary>
        public static DragonStats Base()
        {
            var data = DragonCare.CurrentStageData;
            if (!data) return new DragonStats();
            return CareLogic.StatsFor(data.stats, DragonCare.State);
        }

        /// <summary>
        /// 실제 능력치. 힘·스태미나·체력은 굶주림과 버프까지 반영된 비행 값이다.
        /// </summary>
        public static DragonStats Current()
        {
            var s = Base();
            if (!DragonCare.CurrentStageData) return s;
            var p = DragonCare.CurrentFlightParams();
            s.strength = p.carryStrength;
            s.stamina = p.maxStamina;
            s.health = p.maxHealth;
            return s;
        }
    }
}
