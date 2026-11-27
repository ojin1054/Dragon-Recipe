using System;
using System.Collections.Generic;
using DragonRecipe.Flight;
using UnityEngine;

namespace DragonRecipe.Farm
{
    /// <summary>조합 동물을 먹으면 붙는 버프가 바꾸는 비행 수치</summary>
    public enum BuffStat
    {
        /// <summary>최대 스태미나 (maxStamina)</summary>
        MaxStamina,
        /// <summary>스태미나 회복 속도 (staminaRegen)</summary>
        StaminaRegen,
        /// <summary>날갯짓 상승력 (flapLift)</summary>
        FlapLift,
        /// <summary>날갯짓 전진력과 최고 속도 (flapThrust, maxSpeed)</summary>
        Speed,
        /// <summary>활공 양력 (liftCoefficient)</summary>
        Glide,
        /// <summary>선회·기울기 속도 (turnRate, bankResponse)</summary>
        Agility,
        /// <summary>무거운 먹이를 드는 힘 (carryStrength)</summary>
        CarryStrength,
        /// <summary>최대 체력 (maxHealth)</summary>
        MaxHealth,
        /// <summary>땅에 닿거나 미끄러질 때 받는 피해 (배율이 1보다 작을수록 덜 다침)</summary>
        DamageTaken
    }

    /// <summary>버프 하나가 주는 효과. multiplier 1.2 = 20% 증가</summary>
    [Serializable]
    public class BuffEffect
    {
        public BuffStat stat;
        public float multiplier = 1f;
    }

    /// <summary>지금 드래곤에게 붙어 있는 버프 (저장됨)</summary>
    [Serializable]
    public class ActiveBuff
    {
        /// <summary>버프를 준 조합 동물 id. 같은 동물을 또 먹으면 새로 쌓이지 않고 남은 횟수만 채운다</summary>
        public string sourceId;
        public List<BuffEffect> effects = new List<BuffEffect>();
        /// <summary>남은 비행 횟수. 비행을 시작할 때 1씩 줄고 0이 되면 사라진다</summary>
        public int flightsLeft;
    }

    public static class FarmBuffs
    {
        /// <summary>버프를 반영한 비행 수치. 원본은 건드리지 않는다. 같은 수치에 걸린 버프끼리는 곱한다</summary>
        public static FlightParams Apply(FlightParams source, IEnumerable<ActiveBuff> buffs)
        {
            var p = source.Clone();
            if (buffs == null) return p;
            foreach (var b in buffs)
            {
                if (b == null || b.flightsLeft <= 0) continue;
                foreach (var e in b.effects) ApplyOne(p, e.stat, Mathf.Max(0f, e.multiplier));
            }
            return p;
        }

        static void ApplyOne(FlightParams p, BuffStat stat, float m)
        {
            switch (stat)
            {
                case BuffStat.MaxStamina: p.maxStamina *= m; break;
                case BuffStat.StaminaRegen: p.staminaRegen *= m; break;
                case BuffStat.FlapLift: p.flapLift *= m; break;
                case BuffStat.Speed:
                    p.flapThrust *= m;
                    p.maxSpeed *= m;
                    break;
                case BuffStat.Glide: p.liftCoefficient *= m; break;
                case BuffStat.Agility:
                    p.turnRate *= m;
                    p.bankResponse *= m;
                    break;
                case BuffStat.CarryStrength: p.carryStrength *= m; break;
                case BuffStat.MaxHealth: p.maxHealth *= m; break;
                case BuffStat.DamageTaken:
                    p.touchDamage *= m;
                    p.impactDamagePerSpeed *= m;
                    p.skidDamagePerSecond *= m;
                    break;
            }
        }
    }
}
