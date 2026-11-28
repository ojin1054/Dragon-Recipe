using System.Collections.Generic;
using DragonRecipe.Care;
using DragonRecipe.Farm;
using DragonRecipe.Food;
using DragonRecipe.Skills;
using DragonRecipe.Tribes;
using UnityEngine;

namespace DragonRecipe.Upgrades
{
    public enum UpgradeStatus
    {
        /// <summary>앞 단계를 아직 안 배움</summary>
        Locked,
        /// <summary>앞 단계는 배웠지만 성장 단계·종족 호감도가 모자람</summary>
        Gated,
        /// <summary>갈림길에서 다른 쪽을 골랐음</summary>
        Excluded,
        /// <summary>값만 치르면 배울 수 있음</summary>
        Available,
        /// <summary>배웠고 효과가 켜져 있음</summary>
        Owned,
        /// <summary>배웠지만 그 종족과 사이가 틀어져 효과가 잠듦</summary>
        Dormant
    }

    /// <summary>배운 업그레이드를 모두 더한 효과</summary>
    public class UpgradeBonus
    {
        public readonly List<BuffEffect> flight = new List<BuffEffect>();
        public int orbCharges;
        public float orbCooldown = 1f;
        public float orbBurst = 1f;
        public float breathRange = 1f;
        public float breathCost = 1f;
        public float skillDamage = 1f;
        public float skillDuration = 1f;
        public int storageSlots;
        public int combineSlots;
        public float hungerDecay = 1f;
        public float preyAwareness = 1f;
        public bool healOnReturn;
        /// <summary>종족별 정기 제물 주기 (시간, 0 = 없음). Tribe 순서</summary>
        public readonly float[] offeringHours = new float[3];

        /// <summary>비행 수치에 얹는 버프 한 묶음 (FarmBuffs.Apply에 넘긴다)</summary>
        public ActiveBuff AsBuff() => new ActiveBuff { sourceId = "upgrades", effects = flight, flightsLeft = 1 };

        /// <summary>스킬 수치에 반영한 사본. 바뀌는 게 없으면 원본 그대로</summary>
        public SkillSpec Apply(SkillSpec s)
        {
            if (s == null) return null;
            bool orb = s.kind == SkillKind.Orb;
            bool any = skillDamage != 1f || skillDuration != 1f ||
                       (orb ? orbCharges != 0 || orbCooldown != 1f || orbBurst != 1f : breathRange != 1f || breathCost != 1f);
            if (!any) return s;
            var c = s.Clone();
            c.damage *= skillDamage;
            c.effectDuration *= skillDuration;
            if (orb)
            {
                c.maxCharges = Mathf.Max(1, c.maxCharges + orbCharges);
                c.cooldown *= orbCooldown;
                c.burstRadius *= orbBurst;
            }
            else
            {
                c.range *= breathRange;
                c.staminaPerSecond *= breathCost;
            }
            return c;
        }
    }

    /// <summary>업그레이드 규칙 (순수 함수 → 테스트 가능)</summary>
    public static class UpgradeLogic
    {
        public static UpgradeStatus StatusOf(UpgradeNode n, DragonState s, TribeSettings ts, IReadOnlyList<UpgradeNode> all = null)
        {
            var up = s.upgrades;
            if (up.Has(n.id)) return IsDormant(n, s, ts) ? UpgradeStatus.Dormant : UpgradeStatus.Owned;

            if (!string.IsNullOrEmpty(n.exclusiveGroup))
                foreach (var other in all ?? UpgradeCatalog.All)
                    if (other != n && other.exclusiveGroup == n.exclusiveGroup && up.Has(other.id)) return UpgradeStatus.Excluded;

            foreach (var req in n.requires)
                if (!up.Has(req)) return UpgradeStatus.Locked;

            if (s.stage < n.minStage) return UpgradeStatus.Gated;
            if (n.tribeGate && TribeLogic.StandingOf(s.tribes, n.tribe, ts) < n.standing) return UpgradeStatus.Gated;
            return UpgradeStatus.Available;
        }

        /// <summary>종족 업그레이드는 그 종족이 경계·적대가 되면 잠든다 (다시 사이가 풀리면 깨어남)</summary>
        public static bool IsDormant(UpgradeNode n, DragonState s, TribeSettings ts)
            => n.tribeGate && TribeLogic.StandingOf(s.tribes, n.tribe, ts) < (n.standing < Standing.Neutral ? n.standing : Standing.Neutral);

        public static int CountTrait(List<StoredFood> storage, FoodTrait t)
        {
            int n = 0;
            foreach (var f in storage)
                if (f.trait == t) n++;
            return n;
        }

        public static int CountRare(List<StoredFood> storage)
        {
            int n = 0;
            foreach (var f in storage)
                if (f.rare) n++;
            return n;
        }

        /// <summary>
        /// 값으로 낼 창고 칸 번호들. 못 내면 null.
        /// 성질 값은 흔한 먹이부터(포만도 낮은 것 먼저), 희귀 값은 남은 희귀 먹이에서 낸다.
        /// </summary>
        public static List<int> PickPayment(UpgradeNode n, List<StoredFood> storage)
        {
            var picked = new List<int>();
            foreach (var cost in n.costs)
            {
                for (int k = 0; k < cost.count; k++)
                {
                    int best = -1;
                    for (int i = 0; i < storage.Count; i++)
                    {
                        var f = storage[i];
                        if (f.trait != cost.trait || picked.Contains(i)) continue;
                        if (best < 0 || Cheaper(f, storage[best])) best = i;
                    }
                    if (best < 0) return null;
                    picked.Add(best);
                }
            }
            for (int k = 0; k < n.rareCost; k++)
            {
                int best = -1;
                for (int i = 0; i < storage.Count; i++)
                {
                    var f = storage[i];
                    if (!f.rare || picked.Contains(i)) continue;
                    if (best < 0 || f.satiety < storage[best].satiety) best = i;
                }
                if (best < 0) return null;
                picked.Add(best);
            }
            return picked;
        }

        static bool Cheaper(StoredFood a, StoredFood b)
        {
            if (a.rare != b.rare) return !a.rare;
            return a.satiety < b.satiety;
        }

        public static bool CanAfford(UpgradeNode n, List<StoredFood> storage) => PickPayment(n, storage) != null;

        public static bool CanBuy(UpgradeNode n, DragonState s, TribeSettings ts)
            => StatusOf(n, s, ts) == UpgradeStatus.Available && CanAfford(n, s.storage);

        /// <summary>값을 치르고 배운다. 우리 확장·조합 슬롯은 이때 농장에 바로 반영</summary>
        public static bool Buy(UpgradeNode n, DragonState s, TribeSettings ts)
        {
            if (n == null || StatusOf(n, s, ts) != UpgradeStatus.Available) return false;
            var pay = PickPayment(n, s.storage);
            if (pay == null) return false;
            pay.Sort();
            for (int i = pay.Count - 1; i >= 0; i--) s.storage.RemoveAt(pay[i]);
            s.upgrades.owned.Add(n.id);
            foreach (var e in n.effects)
            {
                if (e.kind == UpgradeEffectKind.PenLevel) s.farm.penUpgradeLevel += Mathf.RoundToInt(e.value);
                else if (e.kind == UpgradeEffectKind.CombineSlots) s.farm.bonusCombineSlots += Mathf.RoundToInt(e.value);
            }
            return true;
        }

        /// <summary>지금 켜져 있는 업그레이드를 모두 더한다 (잠든 종족 업그레이드는 빠짐)</summary>
        public static UpgradeBonus BonusOf(DragonState s, TribeSettings ts, IReadOnlyList<UpgradeNode> all = null)
        {
            var b = new UpgradeBonus();
            if (s == null || s.upgrades == null) return b;
            var interval = new[] { 1f, 1f, 1f };
            foreach (var n in all ?? UpgradeCatalog.All)
            {
                if (!s.upgrades.Has(n.id) || IsDormant(n, s, ts)) continue;
                foreach (var e in n.effects)
                {
                    switch (e.kind)
                    {
                        case UpgradeEffectKind.FlightStat: b.flight.Add(new BuffEffect { stat = e.stat, multiplier = e.value }); break;
                        case UpgradeEffectKind.OrbCharges: b.orbCharges += Mathf.RoundToInt(e.value); break;
                        case UpgradeEffectKind.OrbCooldown: b.orbCooldown *= e.value; break;
                        case UpgradeEffectKind.OrbBurst: b.orbBurst *= e.value; break;
                        case UpgradeEffectKind.BreathRange: b.breathRange *= e.value; break;
                        case UpgradeEffectKind.BreathCost: b.breathCost *= e.value; break;
                        case UpgradeEffectKind.SkillDamage: b.skillDamage *= e.value; break;
                        case UpgradeEffectKind.SkillDuration: b.skillDuration *= e.value; break;
                        case UpgradeEffectKind.StorageSlots: b.storageSlots += Mathf.RoundToInt(e.value); break;
                        case UpgradeEffectKind.HungerDecay: b.hungerDecay *= e.value; break;
                        case UpgradeEffectKind.PreyAwareness: b.preyAwareness *= e.value; break;
                        case UpgradeEffectKind.HealOnReturn: b.healOnReturn = true; break;
                        case UpgradeEffectKind.TimedOffering: b.offeringHours[(int)n.tribe] = e.value; break;
                        case UpgradeEffectKind.OfferingInterval: interval[(int)n.tribe] *= e.value; break;
                        // PenLevel, CombineSlots: 살 때 농장 상태에 이미 반영됨
                    }
                }
            }
            for (int i = 0; i < interval.Length; i++) b.offeringHours[i] *= interval[i];
            return b;
        }

        /// <summary>
        /// 정기 제물: 주기가 지난 만큼 그 종족의 제물을 제단에 올린다 (게임을 꺼 둔 동안도 쌓임). 올린 개수를 돌려준다.
        /// 제단이 가득 차면 거기서 멈추고, 자리가 날 때부터 다시 센다.
        /// </summary>
        public static int TickOfferings(DragonState s, TribeSettings ts, UpgradeBonus bonus, System.DateTime nowUtc)
        {
            if (s == null || s.upgrades == null || s.tribes == null || bonus == null) return 0;
            var since = s.upgrades.offeringSince ??= new List<long>();
            while (since.Count < bonus.offeringHours.Length) since.Add(0);
            s.tribes.altar ??= new List<Offering>();
            int added = 0;
            for (int i = 0; i < bonus.offeringHours.Length; i++)
            {
                float hours = bonus.offeringHours[i];
                if (hours <= 0f)
                {
                    since[i] = 0; // 꺼져 있는 동안은 세지 않는다 (다시 켜지면 그때부터)
                    continue;
                }
                if (since[i] <= 0 || since[i] > nowUtc.Ticks)
                {
                    since[i] = nowUtc.Ticks;
                    continue;
                }
                long step = System.TimeSpan.FromHours(hours).Ticks;
                if (step <= 0) continue;
                while (nowUtc.Ticks - since[i] >= step)
                {
                    if (s.tribes.altar.Count >= ts.altarCapacity)
                    {
                        since[i] = nowUtc.Ticks;
                        break;
                    }
                    var t = (Tribe)i;
                    s.tribes.altar.Add(new Offering { from = t, foodId = ts.OfferingFood(t), rare = false });
                    since[i] += step;
                    added++;
                }
            }
            return added;
        }

        /// <summary>지금 바로 배울 수 있는 업그레이드 수 (여는 버튼의 알림 점)</summary>
        public static int BuyableCount(DragonState s, TribeSettings ts)
        {
            int n = 0;
            foreach (var node in UpgradeCatalog.All)
                if (CanBuy(node, s, ts)) n++;
            return n;
        }
    }
}
