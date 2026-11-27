using System;
using System.Collections.Generic;
using DragonRecipe.Farm;
using UnityEngine;

namespace DragonRecipe.Tribes
{
    /// <summary>종족마다 다른 병사 전투 방식</summary>
    [Serializable]
    public class TribeCombat
    {
        [Tooltip("이 거리 안에 드래곤이 들어오면 쏜다 (적대일 때)")]
        public float range = 55f;
        [Tooltip("시위를 당기는 시간. 길수록 피할 틈이 크다")]
        public float aimTime = 0.7f;
        public float reloadMin = 2f;
        public float reloadMax = 3.5f;
        public float projectileSpeed = 45f;
        public float damage = 6f;
        [Tooltip("조준 오차 (도)")]
        public float spread = 2.5f;
        [Tooltip("병사 수 (중립 기준). 경계 +1, 적대 +3")]
        public int soldiers = 3;
    }

    /// <summary>호감도 단계별로 주는 축복 (비행 수치 배율)</summary>
    [Serializable]
    public class TribeBlessing
    {
        public List<BuffEffect> friendly = new List<BuffEffect>();
        public List<BuffEffect> revered = new List<BuffEffect>();
    }

    [Serializable]
    public class TribeSettings
    {
        [Header("호감도 범위 / 단계")]
        public float minReputation = -100f;
        public float maxReputation = 100f;
        [Tooltip("이 값 이하 = 적대")]
        public float hostileAt = -60f;
        [Tooltip("이 값 이하 = 경계")]
        public float waryAt = -20f;
        [Tooltip("이 값 이상 = 우호")]
        public float friendlyAt = 20f;
        [Tooltip("이 값 이상 = 숭배")]
        public float reveredAt = 60f;

        [Header("처음 호감도")]
        public float startHuman = 0f;
        [Tooltip("오크는 처음부터 조금 경계한다")]
        public float startOrc = -25f;
        public float startElf = 10f;

        [Header("공격받았을 때")]
        [Tooltip("사람·오크가 공격받으면 잃는 호감도")]
        public float attackedLoss = 15f;
        [Tooltip("엘프가 공격받으면 잃는 호감도 (크게 적대)")]
        public float elfAttackedLoss = 40f;
        [Tooltip("사람 ↔ 오크: 상대가 공격받으면 오르는 호감도")]
        public float rivalGain = 10f;
        [Tooltip("사람·오크가 공격받으면 엘프가 미약하게 좋아함")]
        public float elfSideGain = 3f;
        [Tooltip("엘프 땅(마을 둘레)에서 먹이를 사냥하면 엘프가 잃는 호감도")]
        public float elfLandHuntLoss = 2f;

        [Header("시간이 지나면")]
        [Tooltip("비행 한 번마다 호감도가 0 쪽으로 돌아가는 양")]
        public float decayPerFlight = 2f;
        [Tooltip("엘프의 적대감은 잘 안 풀린다 (엘프 호감도가 음수일 때 돌아가는 양)")]
        public float elfGrudgeDecay = 0.4f;

        [Header("제물")]
        [Tooltip("우호일 때 비행 한 번마다 제물이 올 확률")]
        [Range(0f, 1f)] public float friendlyOfferingChance = 0.5f;
        [Tooltip("숭배일 때는 매번 오고, 이 확률로 하나 더")]
        [Range(0f, 1f)] public float reveredExtraChance = 0.5f;
        [Range(0f, 1f)] public float reveredRareChance = 0.25f;
        [Tooltip("엘프는 제물보다 축복을 주는 종족이라 제물 확률에 이 배율")]
        [Range(0f, 1f)] public float elfOfferingScale = 0.5f;
        public string humanOffering = "sheep";
        public string orcOffering = "boar";
        public string elfOffering = "golden_melon";
        [Tooltip("창고가 가득 차 제단에 쌓아 둘 수 있는 제물 수")]
        public int altarCapacity = 6;

        [Header("축복")]
        public TribeBlessing humanBlessing = new TribeBlessing
        {
            friendly = new List<BuffEffect>(),
            revered = new List<BuffEffect> { new BuffEffect { stat = BuffStat.DamageTaken, multiplier = 0.85f } }
        };
        public TribeBlessing orcBlessing = new TribeBlessing
        {
            friendly = new List<BuffEffect> { new BuffEffect { stat = BuffStat.CarryStrength, multiplier = 1.15f } },
            revered = new List<BuffEffect>
            {
                new BuffEffect { stat = BuffStat.CarryStrength, multiplier = 1.3f },
                new BuffEffect { stat = BuffStat.MaxHealth, multiplier = 1.15f }
            }
        };
        public TribeBlessing elfBlessing = new TribeBlessing
        {
            friendly = new List<BuffEffect> { new BuffEffect { stat = BuffStat.Glide, multiplier = 1.08f } },
            revered = new List<BuffEffect>
            {
                new BuffEffect { stat = BuffStat.Glide, multiplier = 1.12f },
                new BuffEffect { stat = BuffStat.StaminaRegen, multiplier = 1.25f }
            }
        };

        [Header("전쟁")]
        [Tooltip("전쟁이 없을 때 비행 한 번마다 새 전쟁이 일어날 확률")]
        [Range(0f, 1f)] public float warStartChance = 0.2f;
        [Tooltip("전쟁이 끝나고 다음 전쟁까지 최소 비행 횟수")]
        public int warCooldown = 4;
        [Tooltip("전쟁이 이어지는 비행 횟수")]
        public int warDuration = 3;
        [Tooltip("사람-오크 / 사람-엘프 / 오크-엘프 전쟁이 일어날 비율")]
        public float warWeightHumanOrc = 3f;
        public float warWeightHumanElf = 1f;
        public float warWeightOrcElf = 1f;
        [Tooltip("이 단계 이상인 편만 드래곤에게 참여를 요청한다")]
        public Standing requestFrom = Standing.Friendly;
        [Tooltip("요청을 거절하면 요청한 편이 잃는 호감도")]
        public float declineLoss = 5f;
        [Tooltip("참전하는 순간 적 편이 잃는 호감도")]
        public float joinEnemyLoss = 10f;
        [Range(0f, 1f)] public float baseWinChance = 0.5f;
        [Tooltip("참전 중 적 편을 한 번 공격할 때마다 오르는 승률")]
        public float winChancePerAttack = 0.04f;
        [Tooltip("적이 편든 쪽 마을까지 뚫고 들어올 때마다 깎이는 승률")]
        public float breachPenalty = 0.08f;
        [Range(0f, 1f)] public float minWinChance = 0.05f;
        [Range(0f, 1f)] public float maxWinChance = 0.95f;
        [Tooltip("적이 마을까지 뚫고 들어오면 편든 쪽이 잃는 호감도 (지켜 주지 못함)")]
        public float breachRepLoss = 2f;

        [Header("방어전 (참전 중 비행)")]
        [Tooltip("비행 한 번에 몰려오는 적 무리 수")]
        public int raidWaves = 3;
        [Tooltip("이륙 뒤 첫 무리가 나타날 때까지 (초)")]
        public float raidFirstDelay = 6f;
        public float raidInterval = 25f;
        [Tooltip("첫 무리 병사 수, 무리마다 늘어나는 수")]
        public int raidBaseSize = 3;
        public int raidGrowth = 2;
        [Tooltip("편든 쪽 마을에서 이만큼 떨어진 곳(적 마을 쪽)에서 나타난다")]
        public float raidSpawnDistance = 110f;
        public float raidSpeed = 3.2f;
        [Tooltip("마을 가운데에서 이 반경 안에 들어오면 뚫린 것")]
        public float breachRadius = 14f;
        [Tooltip("방어전 침입자를 쓰러뜨릴 때 호감도 변화 배율 (승률에는 한 명당 그대로 반영)")]
        [Range(0f, 1f)] public float raiderRepScale = 0.25f;
        [Tooltip("비행 한 번 동안 마을이 버티는 횟수 (방어 게이지 칸 수)")]
        public int villageDefense = 5;
        [Tooltip("편든 쪽 병사도 다가오는 적을 쏜다. 재장전 시간 배율 (클수록 덜 돕는다)")]
        public float allyReloadScale = 2.2f;
        [Tooltip("망루 높이 (참전하면 여기서 이륙)")]
        public float watchtowerHeight = 16f;
        [Tooltip("이기면 편든 쪽이 오르는 호감도")]
        public float winGain = 25f;
        [Tooltip("지면 편든 쪽이 잃는 호감도")]
        public float loseLoss = 10f;
        [Tooltip("이기면 받는 제물 수 (편든 종족의 제물)")]
        public int winOfferings = 3;
        [Range(0f, 1f)] public float winRareChance = 0.5f;
        [Tooltip("이기면 붙는 승전 축복이 유지되는 비행 횟수")]
        public int victoryFlights = 3;
        public List<BuffEffect> victoryBlessing = new List<BuffEffect>
        {
            new BuffEffect { stat = BuffStat.Speed, multiplier = 1.1f },
            new BuffEffect { stat = BuffStat.MaxStamina, multiplier = 1.15f }
        };
        [Tooltip("진 종족은 이 비행 횟수 동안 병력이 줄어든다")]
        public int weakenedFlights = 3;

        [Header("병사")]
        [Tooltip("경계 단계에서 이 반경(마을 둘레) 안에 들어와야 쏜다")]
        public float territoryRadius = 45f;
        public TribeCombat humanCombat = new TribeCombat();
        [Tooltip("오크: 무거운 창. 느리고 아프다")]
        public TribeCombat orcCombat = new TribeCombat
        {
            range = 40f, aimTime = 1f, reloadMin = 3f, reloadMax = 4.5f, projectileSpeed = 32f, damage = 14f, spread = 4f, soldiers = 4
        };
        [Tooltip("엘프: 저격. 멀리서 정확하지만 시위를 오래 당긴다")]
        public TribeCombat elfCombat = new TribeCombat
        {
            range = 85f, aimTime = 1.2f, reloadMin = 3f, reloadMax = 5f, projectileSpeed = 70f, damage = 9f, spread = 0.8f, soldiers = 2
        };

        public TribeCombat Combat(Tribe t) => t switch
        {
            Tribe.Orc => orcCombat,
            Tribe.Elf => elfCombat,
            _ => humanCombat
        };

        public TribeBlessing Blessing(Tribe t) => t switch
        {
            Tribe.Orc => orcBlessing,
            Tribe.Elf => elfBlessing,
            _ => humanBlessing
        };

        public string OfferingFood(Tribe t) => t switch
        {
            Tribe.Orc => orcOffering,
            Tribe.Elf => elfOffering,
            _ => humanOffering
        };

        public float StartReputation(Tribe t) => t switch
        {
            Tribe.Orc => startOrc,
            Tribe.Elf => startElf,
            _ => startHuman
        };
    }
}
