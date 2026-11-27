using System;
using UnityEngine;

namespace DragonRecipe.Flight
{
    /// <summary>
    /// 드래곤 능력치. 성장 단계마다 기본값이 있고, 성체 모습(먹어 온 먹이)에 따라 더해진다.
    /// 비행에 쓰이는 값(힘·스태미나·체력)은 <see cref="ApplyTo"/>로 비행 수치에 들어간다.
    /// </summary>
    [Serializable]
    public class DragonStats
    {
        [Tooltip("들 수 있는 동물 힘의 합. 매단 동물 힘의 합이 이보다 크면 비행이 흔들리고, 너무 크면 더 낚지 못한다")]
        public float strength = 20f;
        [Tooltip("최대 스태미나 (날갯짓)")]
        public float stamina = 100f;
        [Tooltip("최대 체력. 0이 되면 추락")]
        public float health = 100f;
        [Tooltip("공격력. 동물 힘보다 높으면 버둥거림이 약해지고 걷어차이지 않는다")]
        public float attack = 8f;
        [Tooltip("마나 (아직 쓰는 곳 없음: 나중에 브레스 등)")]
        public float mana = 0f;

        public DragonStats Clone() => (DragonStats)MemberwiseClone();

        public static DragonStats operator +(DragonStats a, DragonStats b) => new DragonStats
        {
            strength = a.strength + b.strength,
            stamina = a.stamina + b.stamina,
            health = a.health + b.health,
            attack = a.attack + b.attack,
            mana = a.mana + b.mana,
        };

        /// <summary>힘 → 들 수 있는 무게, 스태미나 → 최대 스태미나, 체력 → 최대 체력</summary>
        public FlightParams ApplyTo(FlightParams flight)
        {
            var p = flight.Clone();
            p.carryStrength = Mathf.Max(0.01f, strength);
            p.maxStamina = Mathf.Max(1f, stamina);
            p.maxHealth = Mathf.Max(1f, health);
            return p;
        }
    }
}
