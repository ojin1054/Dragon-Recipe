using System;
using System.Collections.Generic;
using DragonRecipe.Farm;
using UnityEngine;

namespace DragonRecipe.Tribes
{
    /// <summary>병사가 드래곤을 대하는 태도</summary>
    public enum Aggression
    {
        /// <summary>쏘지 않음</summary>
        None,
        /// <summary>마을 땅에 들어오면 쏨</summary>
        Territorial,
        /// <summary>사거리 안이면 쏨</summary>
        Full
    }

    /// <summary>종족 호감도·제물·축복·전쟁 계산 (순수 함수 → 테스트 가능). rand는 [0, 1) 난수</summary>
    public static class TribeLogic
    {
        public static readonly Tribe[] All = { Tribe.Human, Tribe.Orc, Tribe.Elf };

        /// <summary>예전 저장에 없던 종족 기록을 채운다</summary>
        public static void Ensure(TribeState state, TribeSettings s)
        {
            state.tribes ??= new List<TribeRecord>();
            state.altar ??= new List<Offering>();
            state.war ??= new WarState();
            foreach (var t in All)
                if (state.Get(t) == null)
                    state.tribes.Add(new TribeRecord { tribe = t, reputation = s.StartReputation(t) });
        }

        public static TribeState CreateNew(TribeSettings s)
        {
            var state = new TribeState();
            Ensure(state, s);
            return state;
        }

        public static float Reputation(TribeState state, Tribe t) => state.Get(t)?.reputation ?? 0f;

        public static Standing StandingOf(float rep, TribeSettings s)
        {
            if (rep <= s.hostileAt) return Standing.Hostile;
            if (rep <= s.waryAt) return Standing.Wary;
            if (rep >= s.reveredAt) return Standing.Revered;
            if (rep >= s.friendlyAt) return Standing.Friendly;
            return Standing.Neutral;
        }

        public static Standing StandingOf(TribeState state, Tribe t, TribeSettings s) => StandingOf(Reputation(state, t), s);

        /// <summary>호감도 바 (0~1): 최저 = 0, 최고 = 1</summary>
        public static float Normalized(float rep, TribeSettings s)
            => Mathf.InverseLerp(s.minReputation, s.maxReputation, rep);

        public static void Add(TribeState state, Tribe t, float delta, TribeSettings s)
        {
            var r = state.Get(t);
            if (r == null) return;
            r.reputation = Mathf.Clamp(r.reputation + delta, s.minReputation, s.maxReputation);
        }

        // ───────────── 드래곤의 행동 ─────────────

        /// <summary>
        /// 드래곤이 종족을 공격함.
        /// 사람 공격 → 사람 크게 하락, 오크 상승, 엘프 미약하게 상승.
        /// 오크 공격 → 오크 크게 하락, 사람 상승, 엘프 미약하게 상승.
        /// 엘프 공격 → 엘프 적대감 크게 상승.
        /// 참전 중이면 적 편 공격은 승률을 올리고, 편든 쪽 공격은 깎는다.
        /// </summary>
        public static void RegisterAttack(TribeState state, TribeSettings s, Tribe target)
        {
            var rec = state.Get(target);
            if (rec != null) rec.attacksReceived++;
            switch (target)
            {
                case Tribe.Human:
                    Add(state, Tribe.Human, -s.attackedLoss, s);
                    Add(state, Tribe.Orc, s.rivalGain, s);
                    Add(state, Tribe.Elf, s.elfSideGain, s);
                    break;
                case Tribe.Orc:
                    Add(state, Tribe.Orc, -s.attackedLoss, s);
                    Add(state, Tribe.Human, s.rivalGain, s);
                    Add(state, Tribe.Elf, s.elfSideGain, s);
                    break;
                case Tribe.Elf:
                    Add(state, Tribe.Elf, -s.elfAttackedLoss, s);
                    break;
            }

            var war = state.war;
            if (war.status == WarStatus.Joined)
            {
                if (target == war.Enemy(war.joined)) war.contribution += 1f;
                else if (target == war.joined) war.contribution = Mathf.Max(0f, war.contribution - 1f);
            }
        }

        /// <summary>엘프 땅에서 먹이를 사냥함: 엘프가 조금 싫어한다</summary>
        public static void RegisterElfLandHunt(TribeState state, TribeSettings s)
            => Add(state, Tribe.Elf, -s.elfLandHuntLoss, s);

        // ───────────── 단계별 효과 ─────────────

        /// <summary>병사가 드래곤을 쏘는지. 참전한 전쟁의 적 편은 단계와 상관없이 쏜다</summary>
        public static Aggression AggressionOf(TribeState state, Tribe t, TribeSettings s)
        {
            var war = state.war;
            if (war.status == WarStatus.Joined && war.Enemy(war.joined) == t) return Aggression.Full;
            return StandingOf(state, t, s) switch
            {
                Standing.Hostile => Aggression.Full,
                Standing.Wary => Aggression.Territorial,
                _ => Aggression.None
            };
        }

        /// <summary>마을의 병사 수: 적대할수록 많고, 전쟁에 지면 절반</summary>
        public static int SoldierCount(TribeState state, Tribe t, TribeSettings s)
        {
            int n = s.Combat(t).soldiers;
            switch (StandingOf(state, t, s))
            {
                case Standing.Hostile: n += 3; break;
                case Standing.Wary: n += 1; break;
            }
            var rec = state.Get(t);
            if (rec != null && rec.weakenedFlights > 0) n = Mathf.Max(1, n / 2);
            return n;
        }

        /// <summary>지금 받는 축복 전부 (우호·숭배 종족 + 승전). FarmBuffs.Apply에 그대로 넘길 수 있다</summary>
        public static List<ActiveBuff> Blessings(TribeState state, TribeSettings s)
        {
            var list = new List<ActiveBuff>();
            foreach (var t in All)
            {
                var b = s.Blessing(t);
                var effects = StandingOf(state, t, s) switch
                {
                    Standing.Revered => b.revered,
                    Standing.Friendly => b.friendly,
                    _ => null
                };
                if (effects != null && effects.Count > 0)
                    list.Add(new ActiveBuff { sourceId = $"tribe_{t}", effects = effects, flightsLeft = 1 });
            }
            if (state.victoryFlightsLeft > 0 && s.victoryBlessing.Count > 0)
                list.Add(new ActiveBuff { sourceId = "tribe_victory", effects = s.victoryBlessing, flightsLeft = state.victoryFlightsLeft });
            return list;
        }

        /// <summary>
        /// 비행 한 번이 끝난 뒤: 호감도가 원래 자리로 조금 돌아가고, 우호 종족이 제물을 보내고,
        /// 전쟁이 진행되거나 새로 일어난다.
        /// </summary>
        public static TribeReport AfterFlight(TribeState state, TribeSettings s, Func<float> rand)
        {
            Ensure(state, s);
            var report = new TribeReport();
            foreach (var t in All) report.repBefore[(int)t] = Reputation(state, t);

            // 시간이 지나면 처음 마음으로 돌아간다. 엘프의 원한은 오래간다
            foreach (var r in state.tribes)
            {
                float home = s.StartReputation(r.tribe);
                float step = r.tribe == Tribe.Elf && r.reputation < home ? s.elfGrudgeDecay : s.decayPerFlight;
                r.reputation = Mathf.MoveTowards(r.reputation, home, step);
                if (r.weakenedFlights > 0) r.weakenedFlights--;
            }
            if (state.victoryFlightsLeft > 0) state.victoryFlightsLeft--;

            // 제물 (단계는 감쇠 전 호감도로 판정: 이번 비행의 결과를 바로 돌려준다)
            foreach (var t in All)
            {
                var standing = StandingOf(report.repBefore[(int)t], s);
                float scale = t == Tribe.Elf ? s.elfOfferingScale : 1f;
                int count = 0;
                bool rareChance = false;
                if (standing == Standing.Revered)
                {
                    if (rand() < scale) count++;
                    if (rand() < s.reveredExtraChance * scale) count++;
                    rareChance = true;
                }
                else if (standing == Standing.Friendly && rand() < s.friendlyOfferingChance * scale)
                {
                    count++;
                }
                for (int i = 0; i < count && state.altar.Count < s.altarCapacity; i++)
                {
                    var o = new Offering { from = t, foodId = s.OfferingFood(t), rare = rareChance && rand() < s.reveredRareChance };
                    state.altar.Add(o);
                    report.offerings.Add(o);
                }
            }

            StepWar(state, s, rand, report);

            foreach (var t in All) report.repAfter[(int)t] = Reputation(state, t);
            return report;
        }

        // ───────────── 전쟁 ─────────────

        static void StepWar(TribeState state, TribeSettings s, Func<float> rand, TribeReport report)
        {
            var war = state.war;
            if (war.Active)
            {
                war.flightsLeft--;
                if (war.flightsLeft <= 0) ResolveWar(state, s, rand, report);
                return;
            }

            if (war.cooldown > 0)
            {
                war.cooldown--;
                return;
            }
            if (rand() >= s.warStartChance) return;
            StartWar(state, s, PickWarPair(s, rand()));
            report.warStarted = true;
        }

        static (Tribe, Tribe) PickWarPair(TribeSettings s, float r)
        {
            float total = Mathf.Max(0.0001f, s.warWeightHumanOrc + s.warWeightHumanElf + s.warWeightOrcElf);
            r *= total;
            if (r < s.warWeightHumanOrc) return (Tribe.Human, Tribe.Orc);
            if (r < s.warWeightHumanOrc + s.warWeightHumanElf) return (Tribe.Human, Tribe.Elf);
            return (Tribe.Orc, Tribe.Elf);
        }

        /// <summary>전쟁을 일으킨다. 드래곤과 사이가 좋은 편은 참여를 요청한다</summary>
        public static void StartWar(TribeState state, TribeSettings s, (Tribe a, Tribe b) sides)
        {
            var war = state.war;
            war.sideA = sides.a;
            war.sideB = sides.b;
            war.requestA = StandingOf(state, sides.a, s) >= s.requestFrom;
            war.requestB = StandingOf(state, sides.b, s) >= s.requestFrom;
            war.status = war.requestA || war.requestB ? WarStatus.Requested : WarStatus.Bystander;
            war.flightsLeft = Mathf.Max(1, s.warDuration);
            war.contribution = 0f;
        }

        /// <summary>요청한 편으로 참전한다. 적 편은 바로 드래곤을 미워하고 공격한다</summary>
        public static bool JoinWar(TribeState state, TribeSettings s, Tribe side)
        {
            var war = state.war;
            if (war.status != WarStatus.Requested || !war.Requested(side)) return false;
            war.status = WarStatus.Joined;
            war.joined = side;
            Add(state, war.Enemy(side), -s.joinEnemyLoss, s);
            return true;
        }

        /// <summary>참여를 거절한다. 요청한 편이 조금 서운해한다</summary>
        public static bool DeclineWar(TribeState state, TribeSettings s)
        {
            var war = state.war;
            if (war.status != WarStatus.Requested) return false;
            if (war.requestA) Add(state, war.sideA, -s.declineLoss, s);
            if (war.requestB) Add(state, war.sideB, -s.declineLoss, s);
            war.status = WarStatus.Declined;
            return true;
        }

        public static float WinChance(WarState war, TribeSettings s)
            => Mathf.Clamp(s.baseWinChance + war.contribution * s.winChancePerAttack, 0f, s.maxWinChance);

        static void ResolveWar(TribeState state, TribeSettings s, Func<float> rand, TribeReport report)
        {
            var war = state.war;
            report.warEnded = true;
            Tribe winner;
            if (war.status == WarStatus.Joined)
            {
                report.warJoined = true;
                bool won = rand() < WinChance(war, s);
                report.warWon = won;
                winner = won ? war.joined : war.Enemy(war.joined);
                if (won)
                {
                    Add(state, war.joined, s.winGain, s);
                    // 승전 보상은 제단이 가득 차도 받는다
                    for (int i = 0; i < s.winOfferings; i++)
                    {
                        var o = new Offering { from = war.joined, foodId = s.OfferingFood(war.joined), rare = rand() < s.winRareChance };
                        state.altar.Add(o);
                        report.offerings.Add(o);
                    }
                    state.victoryFlightsLeft = s.victoryFlights;
                    state.victoryFrom = war.joined;
                }
                else
                {
                    Add(state, war.joined, -s.loseLoss, s);
                }
            }
            else
            {
                winner = rand() < 0.5f ? war.sideA : war.sideB;
            }

            var loser = war.Enemy(winner);
            var lost = state.Get(loser);
            if (lost != null) lost.weakenedFlights = s.weakenedFlights;
            report.warWinner = winner;
            report.warLoser = loser;
            state.war = new WarState { cooldown = s.warCooldown };
        }
    }
}
