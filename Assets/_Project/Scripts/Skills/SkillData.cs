using System;
using DragonRecipe.Care;
using DragonRecipe.Food;
using UnityEngine;

namespace DragonRecipe.Skills
{
    /// <summary>청소년: 한 번 누르면 구체 하나, 성체: 누르고 있는 동안 브레스</summary>
    public enum SkillKind
    {
        None,
        Orb,
        Breath
    }

    /// <summary>브레스가 닿는 모양</summary>
    public enum SkillShape
    {
        /// <summary>날아가다 터지는 구체</summary>
        Projectile,
        /// <summary>입 앞 부채꼴</summary>
        Cone,
        /// <summary>길고 가는 줄기</summary>
        Line,
        /// <summary>드래곤 둘레 고리 (포효)</summary>
        Ring
    }

    /// <summary>맞은 대상에게 생기는 일 (여러 개를 겹칠 수 있다)</summary>
    [Flags]
    public enum SkillEffect
    {
        None = 0,
        /// <summary>기절: 그 자리에 멈춤 (별이 돈다)</summary>
        Stun = 1 << 0,
        /// <summary>덩굴 속박: 그 자리에 멈춤</summary>
        Bind = 1 << 1,
        /// <summary>밀려남</summary>
        Push = 1 << 2,
        /// <summary>불이 붙어 계속 피해</summary>
        Burn = 1 << 3,
        /// <summary>느려짐</summary>
        Slow = 1 << 4,
        /// <summary>잠듦: 멈춤 (기절보다 길다)</summary>
        Sleep = 1 << 5,
        /// <summary>무리가 흩어짐</summary>
        Scatter = 1 << 6,
        /// <summary>익힘: 잡으면 포만도가 더 크다</summary>
        Cook = 1 << 7,
        /// <summary>날아가던 화살을 떨어뜨림</summary>
        ClearArrows = 1 << 8
    }

    /// <summary>스킬 하나의 수치</summary>
    [Serializable]
    public class SkillSpec
    {
        public string id = "skill";
        public SkillKind kind = SkillKind.Orb;
        public SkillShape shape = SkillShape.Projectile;
        public Color color = Color.white;
        [Tooltip("먹이에게 생기는 일")]
        public SkillEffect preyEffects;
        [Tooltip("적에게 생기는 일")]
        public SkillEffect enemyEffects;

        [Header("구체")]
        public float staminaCost = 16f;
        [Tooltip("충전 하나가 차는 시간 (초)")]
        public float cooldown = 1.4f;
        public int maxCharges = 2;
        public float projectileSpeed = 42f;
        [Tooltip("아래로 떨어지는 힘 (0 = 곧게)")]
        public float gravity = 6f;
        [Tooltip("구체 크기 (시각 + 맞는 판정)")]
        public float projectileSize = 0.7f;
        [Tooltip("터질 때 퍼지는 반경")]
        public float burstRadius = 3f;
        public float lifetime = 2.5f;

        [Header("브레스")]
        public float staminaPerSecond = 24f;
        [Tooltip("이만큼 스태미나가 있어야 뿜기 시작")]
        public float minStamina = 12f;
        public float range = 16f;
        [Tooltip("부채꼴 반각 (도) 또는 줄기 반폭")]
        public float width = 25f;

        [Header("효과")]
        [Tooltip("기절·속박·잠·흩어짐·화상·둔화가 이어지는 시간")]
        public float effectDuration = 2.5f;
        [Tooltip("적 피해 (구체는 한 방, 브레스는 초당)")]
        public float damage = 1f;
        public float pushForce = 14f;
    }

    /// <summary>모든 스킬 수치. 구체는 성질 순서(고기·물고기·불·식물), 브레스는 성체 모습별</summary>
    [Serializable]
    public class SkillBook
    {
        public SkillSpec rockOrb = new SkillSpec
        {
            id = "orb_rock", color = new Color(0.62f, 0.5f, 0.4f),
            preyEffects = SkillEffect.Stun, enemyEffects = SkillEffect.Stun,
            projectileSpeed = 36f, gravity = 10f, projectileSize = 0.85f, damage = 3f, effectDuration = 3f, burstRadius = 2.6f
        };

        public SkillSpec waterOrb = new SkillSpec
        {
            id = "orb_water", color = new Color(0.35f, 0.7f, 1f),
            preyEffects = SkillEffect.Stun, enemyEffects = SkillEffect.Push | SkillEffect.ClearArrows,
            projectileSpeed = 60f, gravity = 0f, projectileSize = 0.6f, damage = 1f, effectDuration = 1.2f, burstRadius = 3.5f,
            cooldown = 1f, staminaCost = 12f
        };

        public SkillSpec fireOrb = new SkillSpec
        {
            id = "orb_fire", color = new Color(1f, 0.45f, 0.15f),
            preyEffects = SkillEffect.Scatter | SkillEffect.Cook, enemyEffects = SkillEffect.Burn,
            projectileSpeed = 44f, gravity = 4f, damage = 1f, effectDuration = 3f, burstRadius = 4f
        };

        public SkillSpec seedOrb = new SkillSpec
        {
            id = "orb_seed", color = new Color(0.45f, 0.8f, 0.3f),
            preyEffects = SkillEffect.Bind, enemyEffects = SkillEffect.Slow,
            projectileSpeed = 40f, gravity = 7f, damage = 1f, effectDuration = 4f, burstRadius = 4.5f
        };

        public SkillSpec fireBreath = new SkillSpec
        {
            id = "breath_fire", kind = SkillKind.Breath, shape = SkillShape.Cone, color = new Color(1f, 0.45f, 0.12f),
            preyEffects = SkillEffect.Cook | SkillEffect.Scatter, enemyEffects = SkillEffect.Burn,
            range = 45f, width = 32f, damage = 2.5f, effectDuration = 3.5f
        };

        public SkillSpec waterJet = new SkillSpec
        {
            id = "breath_water", kind = SkillKind.Breath, shape = SkillShape.Line, color = new Color(0.35f, 0.72f, 1f),
            preyEffects = SkillEffect.Stun, enemyEffects = SkillEffect.Push | SkillEffect.ClearArrows,
            range = 75f, width = 3f, damage = 1.2f, effectDuration = 1.5f, pushForce = 30f, staminaPerSecond = 20f
        };

        public SkillSpec pollenMist = new SkillSpec
        {
            id = "breath_pollen", kind = SkillKind.Breath, shape = SkillShape.Cone, color = new Color(1f, 0.92f, 0.5f),
            preyEffects = SkillEffect.Sleep, enemyEffects = SkillEffect.Sleep,
            range = 38f, width = 48f, damage = 0f, effectDuration = 6f, staminaPerSecond = 22f
        };

        public SkillSpec roar = new SkillSpec
        {
            id = "breath_roar", kind = SkillKind.Breath, shape = SkillShape.Ring, color = new Color(0.95f, 0.85f, 0.75f),
            preyEffects = SkillEffect.Stun, enemyEffects = SkillEffect.Stun | SkillEffect.ClearArrows,
            range = 24f, damage = 1f, effectDuration = 3f, staminaPerSecond = 30f, minStamina = 20f
        };

        public SkillSpec rainbowBreath = new SkillSpec
        {
            id = "breath_rainbow", kind = SkillKind.Breath, shape = SkillShape.Cone, color = new Color(0.85f, 0.7f, 1f),
            preyEffects = SkillEffect.Cook | SkillEffect.Stun,
            enemyEffects = SkillEffect.Burn | SkillEffect.Push | SkillEffect.Slow | SkillEffect.ClearArrows,
            range = 40f, width = 34f, damage = 1.4f, effectDuration = 2f, pushForce = 12f
        };

        public SkillSpec Orb(FoodTrait t) => t switch
        {
            FoodTrait.Meat => rockOrb,
            FoodTrait.Fish => waterOrb,
            FoodTrait.Fire => fireOrb,
            _ => seedOrb
        };

        public SkillSpec Breath(DragonForm f) => f switch
        {
            DragonForm.Hunter => roar,
            DragonForm.Tide => waterJet,
            DragonForm.Ember => fireBreath,
            DragonForm.Grove => pollenMist,
            _ => rainbowBreath
        };
    }

    /// <summary>적에게 한 번 맞힐 때 넘기는 값 (브레스는 한 틱 분량)</summary>
    public struct SkillHit
    {
        public float damage;
        public float stun;
        public float sleep;
        public float slow;
        public float burn;
        /// <summary>밀어내는 속도 (방향 × 세기). 0이면 밀지 않음</summary>
        public Vector3 push;
        public bool clearArrows;
    }
}
