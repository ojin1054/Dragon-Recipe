using System;
using System.Collections.Generic;
using DragonRecipe.Farm;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using DragonRecipe.Tribes;
using UnityEngine;

namespace DragonRecipe.Upgrades
{
    /// <summary>업그레이드 트리의 큰 가지</summary>
    public enum UpgradeBranch
    {
        Flight,
        Skill,
        Nest,
        Tribe,
        /// <summary>먹이: 사냥 · 드는 힘</summary>
        Hunt
    }

    public enum UpgradeEffectKind
    {
        /// <summary>비행 수치 배율 (stat, value)</summary>
        FlightStat,
        /// <summary>구체 충전 수 + value</summary>
        OrbCharges,
        /// <summary>구체 재충전 시간 배율</summary>
        OrbCooldown,
        /// <summary>구체 폭발 반경 배율</summary>
        OrbBurst,
        /// <summary>브레스 거리 배율</summary>
        BreathRange,
        /// <summary>브레스 스태미나 소모 배율</summary>
        BreathCost,
        /// <summary>구체·브레스 피해 배율</summary>
        SkillDamage,
        /// <summary>구체·브레스 효과 지속 시간 배율</summary>
        SkillDuration,
        /// <summary>먹이 창고 칸 + value</summary>
        StorageSlots,
        /// <summary>우리 한 단계 확장 (살 때 한 번 적용)</summary>
        PenLevel,
        /// <summary>조합 슬롯 + value</summary>
        CombineSlots,
        /// <summary>허기가 줄어드는 속도 배율</summary>
        HungerDecay,
        /// <summary>동물이 드래곤을 알아채는 거리 배율</summary>
        PreyAwareness,
        /// <summary>추락해도 둥지에 돌아오면 낫는다</summary>
        HealOnReturn,
        /// <summary>그 종족이 value 시간마다 제물을 하나 바친다 (노드의 tribe)</summary>
        TimedOffering,
        /// <summary>그 종족의 제물 주기 배율 (노드의 tribe)</summary>
        OfferingInterval
    }

    [Serializable]
    public class UpgradeEffect
    {
        public UpgradeEffectKind kind;
        public BuffStat stat;
        public float value = 1f;
    }

    /// <summary>값: 창고에서 이 성질의 먹이를 count개 낸다</summary>
    [Serializable]
    public class UpgradeCost
    {
        public FoodTrait trait;
        public int count;
    }

    /// <summary>트리의 업그레이드 하나</summary>
    [Serializable]
    public class UpgradeNode
    {
        public string id;
        public string name;
        public string desc;
        public UpgradeBranch branch;
        /// <summary>UIIconSet의 필드 이름</summary>
        public string icon = "star";
        /// <summary>쓰지 않음: 화면 자리는 UpgradeLayout이 트리 모양에서 계산한다</summary>
        public Vector2 pos;
        /// <summary>종족 줄기의 단계 (1~3). 0이면 보통 노드</summary>
        public int tier;

        /// <summary>먼저 배워야 하는 업그레이드 (모두)</summary>
        public string[] requires = Array.Empty<string>();
        /// <summary>같은 묶음에서는 하나만 배울 수 있다 (갈림길)</summary>
        public string exclusiveGroup;

        public List<UpgradeCost> costs = new List<UpgradeCost>();
        /// <summary>성질과 상관없이 희귀 먹이를 이만큼 더 낸다</summary>
        public int rareCost;

        /// <summary>이 성장 단계부터 배울 수 있다</summary>
        public GrowthStage minStage = GrowthStage.Hatchling;
        /// <summary>종족 호감도를 채워야 배울 수 있고, 사이가 틀어지면 잠든다</summary>
        public bool tribeGate;
        public Tribe tribe;
        public Standing standing = Standing.Friendly;

        public List<UpgradeEffect> effects = new List<UpgradeEffect>();
    }

    /// <summary>배운 업그레이드 (DragonState에 저장. 새 알을 부화시켜도 이어진다)</summary>
    [Serializable]
    public class UpgradeState
    {
        public List<string> owned = new List<string>();
        /// <summary>종족별로 다음 정기 제물을 세기 시작한 시각 (UTC ticks, 0 = 아직 안 셈). Tribe 순서</summary>
        public List<long> offeringSince = new List<long>();

        public bool Has(string id) => owned.Contains(id);
    }

    /// <summary>업그레이드 트리 전체. 수치는 여기서 고친다</summary>
    public static class UpgradeCatalog
    {
        public const string SkillFinalGroup = "skill_final";

        static List<UpgradeNode> _all;
        public static IReadOnlyList<UpgradeNode> All => _all ??= Build();

        public static UpgradeNode Find(string id)
        {
            foreach (var n in All)
                if (n.id == id) return n;
            return null;
        }

        const FoodTrait Meat = FoodTrait.Meat, Fish = FoodTrait.Fish, Fire = FoodTrait.Fire, Plant = FoodTrait.Plant;

        static List<UpgradeNode> Build() => new List<UpgradeNode>
        {
            // ───────── 비행 ─────────
            N("f_glide", "넓은 날개", "활공 양력 +12%", UpgradeBranch.Flight, "wind", -560, 0)
                .Cost(Fish, 2).Stat(BuffStat.Glide, 1.12f),
            N("f_stamina1", "깊은 숨", "최대 스태미나 +15%", UpgradeBranch.Flight, "stamina", -680, 1, "f_glide")
                .Cost(Meat, 2).Stat(BuffStat.MaxStamina, 1.15f),
            N("f_speed", "빠른 날개", "속도 +10%", UpgradeBranch.Flight, "wing", -440, 1, "f_glide")
                .Cost(Fire, 2).Stat(BuffStat.Speed, 1.1f),
            N("f_stamina2", "더 깊은 숨", "최대 스태미나 +15%", UpgradeBranch.Flight, "stamina", -740, 2, "f_stamina1")
                .Cost(Meat, 3).Cost(Plant, 1).Stat(BuffStat.MaxStamina, 1.15f),
            N("f_regen", "고른 호흡", "스태미나 회복 +25%", UpgradeBranch.Flight, "sprout", -620, 2, "f_stamina1")
                .Cost(Plant, 3).Stat(BuffStat.StaminaRegen, 1.25f),
            N("f_agility", "날렵한 꼬리", "선회 +15%", UpgradeBranch.Flight, "target", -440, 2, "f_speed")
                .Cost(Fish, 3).Stat(BuffStat.Agility, 1.15f),
            N("f_tough", "단단한 비늘", "땅에 닿을 때 피해 -20%", UpgradeBranch.Flight, "bandage", -680, 3, "f_stamina2")
                .Cost(Meat, 3).Cost(Fire, 2).Stat(BuffStat.DamageTaken, 0.8f),
            N("f_health", "큰 심장", "최대 체력 +20%", UpgradeBranch.Flight, "heart", -440, 3, "f_agility")
                .Cost(Meat, 4).Rare(1).Stat(BuffStat.MaxHealth, 1.2f),

            // ───────── 스킬 ─────────
            N("s_cooldown", "빠른 장전", "구체가 25% 빨리 찬다", UpgradeBranch.Skill, "clock", -120, 0)
                .Stage(GrowthStage.Juvenile).Cost(Fire, 2).Fx(UpgradeEffectKind.OrbCooldown, 0.75f),
            N("s_charge", "세 번째 구체", "구체 충전 +1", UpgradeBranch.Skill, "sparkle", -200, 1, "s_cooldown")
                .Stage(GrowthStage.Juvenile).Cost(Fire, 2).Cost(Meat, 2).Fx(UpgradeEffectKind.OrbCharges, 1f),
            N("s_burst", "큰 폭발", "구체 폭발 범위 +30%", UpgradeBranch.Skill, "boom", -40, 1, "s_cooldown")
                .Stage(GrowthStage.Juvenile).Cost(Fish, 2).Cost(Plant, 2).Fx(UpgradeEffectKind.OrbBurst, 1.3f),
            N("s_breath_range", "긴 숨결", "브레스 거리 +25%", UpgradeBranch.Skill, "flame", -200, 2, "s_charge")
                .Stage(GrowthStage.Adult).Cost(Fire, 3).Cost(Fish, 2).Fx(UpgradeEffectKind.BreathRange, 1.25f),
            N("s_breath_cost", "아끼는 숨결", "브레스 스태미나 소모 -25%", UpgradeBranch.Skill, "wind", -40, 2, "s_burst")
                .Stage(GrowthStage.Adult).Cost(Plant, 3).Cost(Fish, 2).Fx(UpgradeEffectKind.BreathCost, 0.75f),
            // 갈림길: 둘 중 하나만
            N("s_power", "맹렬함", "스킬 피해 +40%", UpgradeBranch.Skill, "claw", -200, 3, "s_breath_range")
                .Stage(GrowthStage.Adult).Exclusive(SkillFinalGroup).Cost(Fire, 3).Rare(1).Fx(UpgradeEffectKind.SkillDamage, 1.4f),
            N("s_control", "끈질김", "기절·잠·화상이 40% 오래 간다", UpgradeBranch.Skill, "star", -40, 3, "s_breath_cost")
                .Stage(GrowthStage.Adult).Exclusive(SkillFinalGroup).Cost(Plant, 3).Rare(1).Fx(UpgradeEffectKind.SkillDuration, 1.4f),

            // ───────── 둥지 ─────────
            N("n_storage1", "넓은 창고", "먹이 창고 +4칸", UpgradeBranch.Nest, "basket", 300, 0)
                .Cost(Plant, 2).Fx(UpgradeEffectKind.StorageSlots, 4f),
            N("n_pen1", "우리 넓히기", "우리가 한 단계 넓어진다", UpgradeBranch.Nest, "fence", 200, 1, "n_storage1")
                .Cost(Meat, 2).Cost(Plant, 1).Fx(UpgradeEffectKind.PenLevel, 1f),
            N("n_storage2", "더 넓은 창고", "먹이 창고 +4칸", UpgradeBranch.Nest, "basket", 400, 1, "n_storage1")
                .Cost(Plant, 2).Cost(Fish, 2).Fx(UpgradeEffectKind.StorageSlots, 4f),
            N("n_pen2", "튼튼한 울타리", "우리가 한 단계 넓어진다", UpgradeBranch.Nest, "fence", 140, 2, "n_pen1")
                .Cost(Meat, 3).Cost(Plant, 2).Fx(UpgradeEffectKind.PenLevel, 1f),
            N("n_combine", "두 번째 조합터", "조합을 하나 더 돌린다", UpgradeBranch.Nest, "potion", 260, 2, "n_pen1")
                .Cost(Fire, 2).Cost(Fish, 2).Fx(UpgradeEffectKind.CombineSlots, 1f),
            N("n_hunger", "든든한 배", "배가 20% 천천히 꺼진다", UpgradeBranch.Nest, "meat", 400, 2, "n_storage2")
                .Cost(Meat, 2).Cost(Fish, 2).Fx(UpgradeEffectKind.HungerDecay, 0.8f),
            N("n_pen3", "큰 목장", "우리가 한 단계 넓어진다", UpgradeBranch.Nest, "fence", 140, 3, "n_pen2")
                .Cost(Meat, 4).Cost(Plant, 3).Fx(UpgradeEffectKind.PenLevel, 1f),
            N("n_heal", "약초 둥지", "추락해도 둥지에 돌아오면 낫는다", UpgradeBranch.Nest, "leaf", 400, 3, "n_hunger")
                .Cost(Plant, 4).Rare(1).Fx(UpgradeEffectKind.HealOnReturn, 1f),

            // ───────── 먹이 ─────────
            N("h_carry1", "굳센 발톱", "드는 힘 +10%", UpgradeBranch.Hunt, "claw", 0, 0)
                .Cost(Meat, 2).Stat(BuffStat.CarryStrength, 1.1f),
            N("h_quiet1", "낮은 그림자", "동물이 10% 늦게 알아챈다", UpgradeBranch.Hunt, "target", 0, 0, "h_carry1")
                .Cost(Plant, 2).Fx(UpgradeEffectKind.PreyAwareness, 0.9f),
            N("h_carry2", "억센 다리", "드는 힘 +10%", UpgradeBranch.Hunt, "strength", 0, 0, "h_carry1")
                .Cost(Meat, 3).Stat(BuffStat.CarryStrength, 1.1f),
            N("h_belly", "아껴 먹기", "배가 10% 천천히 꺼진다", UpgradeBranch.Hunt, "meat", 0, 0, "h_carry1")
                .Cost(Fish, 2).Fx(UpgradeEffectKind.HungerDecay, 0.9f),
            N("h_quiet2", "바람 아래로", "동물이 10% 늦게 알아챈다", UpgradeBranch.Hunt, "wind", 0, 0, "h_quiet1")
                .Cost(Plant, 3).Cost(Fish, 1).Fx(UpgradeEffectKind.PreyAwareness, 0.9f),
            N("h_lift", "힘찬 날갯짓", "날갯짓 상승력 +10%", UpgradeBranch.Hunt, "wing", 0, 0, "h_carry2")
                .Cost(Meat, 3).Cost(Fire, 1).Stat(BuffStat.FlapLift, 1.1f),
            N("h_carry3", "무쇠 발톱", "드는 힘 +15%", UpgradeBranch.Hunt, "claw", 0, 0, "h_carry2")
                .Cost(Meat, 4).Rare(1).Stat(BuffStat.CarryStrength, 1.15f),

            // ───────── 종족 ─────────
            // 종족마다 줄기 하나: 스킬 · 스킬 · 1단(제물) · 스킬 · 스킬 · 2단(시설) · 스킬 · 스킬 · 3단(드래곤 스킬), 사이사이 곁가지.
            // 단계 노드는 호감도(중립 / 우호 / 숭배)를 채워야 배우고, 줄기 전체가 그 종족과 사이가 틀어지면 잠든다.

            // 사람: 갑옷 · 곳간
            T("th_a1", "덧댄 가죽", "땅에 닿을 때 피해 -5%", Tribe.Human, "bandage").Cost(Meat, 1).Stat(BuffStat.DamageTaken, 0.95f),
            T("th_a2", "곡식 자루", "먹이 창고 +2칸", Tribe.Human, "basket", "th_a1").Cost(Plant, 2).Fx(UpgradeEffectKind.StorageSlots, 2f),
            T("th_s1", "건초 더미", "배가 5% 천천히 꺼진다", Tribe.Human, "sprout", "th_a1").Cost(Plant, 2).Fx(UpgradeEffectKind.HungerDecay, 0.95f),
            T("th_t1", "사람의 제물", "사람들이 4시간마다 양을 바친다", Tribe.Human, "meat", "th_a2").Tier(1).Cost(Meat, 2).Cost(Plant, 1).Fx(UpgradeEffectKind.TimedOffering, 4f),
            T("th_b1", "넉넉한 제물", "사람의 제물이 25% 자주 온다", Tribe.Human, "clock", "th_t1").Cost(Meat, 2).Cost(Fish, 1).Fx(UpgradeEffectKind.OfferingInterval, 0.75f),
            T("th_s2", "큰 광주리", "먹이 창고 +2칸", Tribe.Human, "basket", "th_t1").Cost(Plant, 2).Cost(Fish, 1).Fx(UpgradeEffectKind.StorageSlots, 2f),
            T("th_b2", "대장장이의 손길", "땅에 닿을 때 피해 -8%", Tribe.Human, "bandage", "th_b1").Cost(Fire, 2).Cost(Meat, 1).Stat(BuffStat.DamageTaken, 0.92f),
            T("th_t2", "곡물 창고", "사람들이 지어 준 곳간. 먹이 창고 +6칸", Tribe.Human, "basket", "th_b2").Tier(2).Cost(Meat, 3).Cost(Plant, 3).Fx(UpgradeEffectKind.StorageSlots, 6f),
            T("th_c1", "쇠 덧판", "땅에 닿을 때 피해 -10%", Tribe.Human, "bandage", "th_t2").Cost(Fire, 3).Cost(Meat, 2).Stat(BuffStat.DamageTaken, 0.9f),
            T("th_s3", "지하 저장고", "먹이 창고 +4칸", Tribe.Human, "basket", "th_t2").Cost(Plant, 3).Cost(Fish, 2).Fx(UpgradeEffectKind.StorageSlots, 4f),
            T("th_s4", "소금 절임", "배가 8% 천천히 꺼진다", Tribe.Human, "meat", "th_t2").Cost(Fish, 3).Fx(UpgradeEffectKind.HungerDecay, 0.92f),
            T("th_c2", "튼튼한 대들보", "최대 체력 +10%", Tribe.Human, "heart", "th_c1").Cost(Meat, 3).Cost(Plant, 2).Stat(BuffStat.MaxHealth, 1.1f),
            T("th_t3", "사람의 갑옷", "드래곤이 갑옷을 입는다. 땅에 닿을 때 피해 -20%", Tribe.Human, "bandage", "th_c2").Tier(3).Cost(Fire, 3).Cost(Meat, 3).Rare(1).Stat(BuffStat.DamageTaken, 0.8f),

            // 오크: 힘 · 훈련장
            T("to_a1", "거친 발톱", "드는 힘 +5%", Tribe.Orc, "claw").Cost(Meat, 1).Stat(BuffStat.CarryStrength, 1.05f),
            T("to_a2", "두꺼운 목", "드는 힘 +5%", Tribe.Orc, "strength", "to_a1").Cost(Meat, 2).Stat(BuffStat.CarryStrength, 1.05f),
            T("to_s1", "질긴 힘줄", "최대 스태미나 +5%", Tribe.Orc, "stamina", "to_a1").Cost(Meat, 2).Stat(BuffStat.MaxStamina, 1.05f),
            T("to_t1", "오크의 제물", "오크들이 6시간마다 멧돼지를 바친다", Tribe.Orc, "meat", "to_a2").Tier(1).Cost(Meat, 3).Fx(UpgradeEffectKind.TimedOffering, 6f),
            T("to_b1", "넉넉한 제물", "오크의 제물이 25% 자주 온다", Tribe.Orc, "clock", "to_t1").Cost(Meat, 2).Cost(Fire, 1).Fx(UpgradeEffectKind.OfferingInterval, 0.75f),
            T("to_s2", "사냥 잔치", "배가 5% 천천히 꺼진다", Tribe.Orc, "meat", "to_t1").Cost(Meat, 3).Fx(UpgradeEffectKind.HungerDecay, 0.95f),
            T("to_b2", "바위 들기", "드는 힘 +8%", Tribe.Orc, "strength", "to_b1").Cost(Meat, 3).Cost(Fire, 1).Stat(BuffStat.CarryStrength, 1.08f),
            T("to_t2", "훈련장", "오크들이 지어 준 훈련장. 드는 힘 +15%", Tribe.Orc, "strength", "to_b2").Tier(2).Cost(Meat, 4).Cost(Fire, 2).Stat(BuffStat.CarryStrength, 1.15f),
            T("to_c1", "무거운 날개", "날갯짓 상승력 +8%", Tribe.Orc, "wing", "to_t2").Cost(Meat, 3).Cost(Fire, 2).Stat(BuffStat.FlapLift, 1.08f),
            T("to_s3", "모래 주머니", "최대 스태미나 +8%", Tribe.Orc, "stamina", "to_t2").Cost(Meat, 3).Cost(Plant, 1).Stat(BuffStat.MaxStamina, 1.08f),
            T("to_c2", "굳은 어깨", "드는 힘 +10%", Tribe.Orc, "strength", "to_c1").Cost(Meat, 4).Stat(BuffStat.CarryStrength, 1.1f),
            T("to_s4", "통나무 끌기", "날갯짓 상승력 +5%", Tribe.Orc, "wing", "to_c1").Cost(Meat, 2).Cost(Plant, 2).Stat(BuffStat.FlapLift, 1.05f),
            T("to_t3", "오크의 완력", "드는 힘 +30%", Tribe.Orc, "claw", "to_c2").Tier(3).Cost(Meat, 5).Rare(1).Stat(BuffStat.CarryStrength, 1.3f),

            // 엘프: 바람 · 샘
            T("te_a1", "가벼운 깃", "활공 양력 +4%", Tribe.Elf, "wing").Cost(Plant, 1).Stat(BuffStat.Glide, 1.04f),
            T("te_a2", "숲의 발걸음", "동물이 8% 늦게 알아챈다", Tribe.Elf, "leaf", "te_a1").Cost(Plant, 2).Fx(UpgradeEffectKind.PreyAwareness, 0.92f),
            T("te_s1", "이슬 한 모금", "스태미나 회복 +6%", Tribe.Elf, "sprout", "te_a1").Cost(Plant, 2).Stat(BuffStat.StaminaRegen, 1.06f),
            T("te_t1", "엘프의 제물", "엘프들이 12시간마다 황금 멜론을 바친다", Tribe.Elf, "leaf", "te_a2").Tier(1).Cost(Plant, 3).Fx(UpgradeEffectKind.TimedOffering, 12f),
            T("te_b1", "넉넉한 제물", "엘프의 제물이 25% 자주 온다", Tribe.Elf, "clock", "te_t1").Cost(Plant, 2).Cost(Fish, 1).Fx(UpgradeEffectKind.OfferingInterval, 0.75f),
            T("te_s2", "고요한 날개", "동물이 10% 늦게 알아챈다", Tribe.Elf, "target", "te_t1").Cost(Plant, 3).Fx(UpgradeEffectKind.PreyAwareness, 0.9f),
            T("te_b2", "바람 읽기", "활공 양력 +6%", Tribe.Elf, "wind", "te_b1").Cost(Fish, 2).Cost(Plant, 1).Stat(BuffStat.Glide, 1.06f),
            T("te_t2", "치유의 샘", "엘프들이 틔워 준 샘. 스태미나 회복 +20%, 최대 스태미나 +10%", Tribe.Elf, "potion", "te_b2").Tier(2).Cost(Plant, 4).Cost(Fish, 2)
                .Stat(BuffStat.StaminaRegen, 1.2f).Stat(BuffStat.MaxStamina, 1.1f),
            T("te_c1", "긴 활공", "활공 양력 +6%", Tribe.Elf, "wind", "te_t2").Cost(Fish, 3).Cost(Plant, 2).Stat(BuffStat.Glide, 1.06f),
            T("te_s3", "맑은 숨", "스태미나 회복 +10%", Tribe.Elf, "sprout", "te_t2").Cost(Plant, 3).Cost(Fish, 1).Stat(BuffStat.StaminaRegen, 1.1f),
            T("te_s4", "달빛 물", "최대 스태미나 +8%", Tribe.Elf, "stamina", "te_t2").Cost(Plant, 2).Cost(Fish, 2).Stat(BuffStat.MaxStamina, 1.08f),
            T("te_c2", "매끈한 비늘", "속도 +5%", Tribe.Elf, "wing", "te_c1").Cost(Fish, 3).Cost(Fire, 1).Stat(BuffStat.Speed, 1.05f),
            T("te_t3", "바람 타기", "활공 양력 +15%, 속도 +8%", Tribe.Elf, "wind", "te_c2").Tier(3).Cost(Plant, 4).Cost(Fish, 3).Rare(1)
                .Stat(BuffStat.Glide, 1.15f).Stat(BuffStat.Speed, 1.08f),
        };

        static UpgradeNode N(string id, string name, string desc, UpgradeBranch branch, string icon, float x, int row, params string[] requires)
            => new UpgradeNode { id = id, name = name, desc = desc, branch = branch, icon = icon, pos = new Vector2(x, row), requires = requires };

        /// <summary>종족 줄기의 노드. 단계가 아닌 노드도 그 종족이 적대만 아니면 배울 수 있고, 사이가 틀어지면 잠든다</summary>
        static UpgradeNode T(string id, string name, string desc, Tribe tribe, string icon, params string[] requires)
            => new UpgradeNode
            {
                id = id, name = name, desc = desc, branch = UpgradeBranch.Tribe, icon = icon, requires = requires,
                tribeGate = true, tribe = tribe, standing = Standing.Wary
            };

        /// <summary>종족 단계: 1단 중립, 2단 우호, 3단 숭배</summary>
        static UpgradeNode Tier(this UpgradeNode n, int tier)
        {
            n.tier = tier;
            n.standing = tier >= 3 ? Standing.Revered : tier == 2 ? Standing.Friendly : Standing.Neutral;
            return n;
        }

        static UpgradeNode Cost(this UpgradeNode n, FoodTrait t, int count)
        {
            n.costs.Add(new UpgradeCost { trait = t, count = count });
            return n;
        }

        static UpgradeNode Rare(this UpgradeNode n, int count)
        {
            n.rareCost = count;
            return n;
        }

        static UpgradeNode Stage(this UpgradeNode n, GrowthStage stage)
        {
            n.minStage = stage;
            return n;
        }

        static UpgradeNode Gate(this UpgradeNode n, Tribe tribe, Standing standing)
        {
            n.tribeGate = true;
            n.tribe = tribe;
            n.standing = standing;
            return n;
        }

        static UpgradeNode Exclusive(this UpgradeNode n, string group)
        {
            n.exclusiveGroup = group;
            return n;
        }

        static UpgradeNode Stat(this UpgradeNode n, BuffStat stat, float mult)
        {
            n.effects.Add(new UpgradeEffect { kind = UpgradeEffectKind.FlightStat, stat = stat, value = mult });
            return n;
        }

        static UpgradeNode Fx(this UpgradeNode n, UpgradeEffectKind kind, float value)
        {
            n.effects.Add(new UpgradeEffect { kind = kind, value = value });
            return n;
        }
    }
}
