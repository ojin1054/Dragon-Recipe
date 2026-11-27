using System;
using DragonRecipe.Farm;
using DragonRecipe.Flight;
using DragonRecipe.Tribes;
using NUnit.Framework;

namespace DragonRecipe.Tests
{
    public class TribeLogicTests
    {
        static TribeSettings Settings() => new TribeSettings
        {
            startHuman = 0f, startOrc = 0f, startElf = 0f,
            warStartChance = 0f
        };

        static Func<float> Always(float v) => () => v;

        static float Rep(TribeState s, Tribe t) => TribeLogic.Reputation(s, t);

        // ───────────── 공격 ─────────────

        [Test]
        public void AttackHuman_OrcUp_ElfSlightlyUp_HumanDown()
        {
            var s = Settings();
            var st = TribeLogic.CreateNew(s);
            TribeLogic.RegisterAttack(st, s, Tribe.Human);
            Assert.AreEqual(-s.attackedLoss, Rep(st, Tribe.Human), 1e-4f);
            Assert.AreEqual(s.rivalGain, Rep(st, Tribe.Orc), 1e-4f);
            Assert.AreEqual(s.elfSideGain, Rep(st, Tribe.Elf), 1e-4f);
            Assert.Less(s.elfSideGain, s.rivalGain, "엘프는 미약하게");
        }

        [Test]
        public void AttackOrc_HumanUp_ElfSlightlyUp_OrcDown()
        {
            var s = Settings();
            var st = TribeLogic.CreateNew(s);
            TribeLogic.RegisterAttack(st, s, Tribe.Orc);
            Assert.AreEqual(-s.attackedLoss, Rep(st, Tribe.Orc), 1e-4f);
            Assert.AreEqual(s.rivalGain, Rep(st, Tribe.Human), 1e-4f);
            Assert.AreEqual(s.elfSideGain, Rep(st, Tribe.Elf), 1e-4f);
        }

        [Test]
        public void AttackElf_ElfHostilityRisesSharply_OthersUnchanged()
        {
            var s = Settings();
            var st = TribeLogic.CreateNew(s);
            TribeLogic.RegisterAttack(st, s, Tribe.Elf);
            Assert.AreEqual(-s.elfAttackedLoss, Rep(st, Tribe.Elf), 1e-4f);
            Assert.Greater(s.elfAttackedLoss, s.attackedLoss);
            Assert.AreEqual(0f, Rep(st, Tribe.Human), 1e-4f);
            Assert.AreEqual(0f, Rep(st, Tribe.Orc), 1e-4f);
            TribeLogic.RegisterAttack(st, s, Tribe.Elf);
            Assert.AreEqual(Standing.Hostile, TribeLogic.StandingOf(st, Tribe.Elf, s), "두 번이면 적대");
        }

        [Test]
        public void AlternatingAttacks_CannotRaiseBothHumansAndOrcs()
        {
            var s = Settings();
            var st = TribeLogic.CreateNew(s);
            for (int i = 0; i < 5; i++)
            {
                TribeLogic.RegisterAttack(st, s, Tribe.Human);
                TribeLogic.RegisterAttack(st, s, Tribe.Orc);
            }
            Assert.Less(Rep(st, Tribe.Human), 0f);
            Assert.Less(Rep(st, Tribe.Orc), 0f);
        }

        [Test]
        public void Reputation_IsClamped()
        {
            var s = Settings();
            var st = TribeLogic.CreateNew(s);
            for (int i = 0; i < 20; i++) TribeLogic.RegisterAttack(st, s, Tribe.Elf);
            Assert.AreEqual(s.minReputation, Rep(st, Tribe.Elf), 1e-4f);
        }

        // ───────────── 단계 ─────────────

        [Test]
        public void Standings_FollowThresholds()
        {
            var s = Settings();
            Assert.AreEqual(Standing.Hostile, TribeLogic.StandingOf(s.hostileAt, s));
            Assert.AreEqual(Standing.Wary, TribeLogic.StandingOf(s.waryAt, s));
            Assert.AreEqual(Standing.Neutral, TribeLogic.StandingOf(0f, s));
            Assert.AreEqual(Standing.Friendly, TribeLogic.StandingOf(s.friendlyAt, s));
            Assert.AreEqual(Standing.Revered, TribeLogic.StandingOf(s.reveredAt, s));
        }

        [Test]
        public void Aggression_HostileFull_WaryTerritorial_NeutralNone()
        {
            var s = Settings();
            var st = TribeLogic.CreateNew(s);
            st.Get(Tribe.Human).reputation = -80f;
            st.Get(Tribe.Orc).reputation = -30f;
            st.Get(Tribe.Elf).reputation = 50f;
            Assert.AreEqual(Aggression.Full, TribeLogic.AggressionOf(st, Tribe.Human, s));
            Assert.AreEqual(Aggression.Territorial, TribeLogic.AggressionOf(st, Tribe.Orc, s));
            Assert.AreEqual(Aggression.None, TribeLogic.AggressionOf(st, Tribe.Elf, s));
            Assert.Greater(TribeLogic.SoldierCount(st, Tribe.Human, s), s.humanCombat.soldiers, "적대하면 병사가 늘어난다");
        }

        [Test]
        public void Blessings_ComeFromFriendlyAndReveredTribes_AndChangeFlight()
        {
            var s = Settings();
            var st = TribeLogic.CreateNew(s);
            Assert.AreEqual(0, TribeLogic.Blessings(st, s).Count);

            st.Get(Tribe.Elf).reputation = 30f; // 우호: 활공
            st.Get(Tribe.Orc).reputation = 80f; // 숭배: 힘·체력
            var p = FarmBuffs.Apply(new FlightParams(), TribeLogic.Blessings(st, s));
            var basic = new FlightParams();
            Assert.Greater(p.liftCoefficient, basic.liftCoefficient);
            Assert.Greater(p.carryStrength, basic.carryStrength);
            Assert.Greater(p.maxHealth, basic.maxHealth);
        }

        // ───────────── 비행 뒤 ─────────────

        [Test]
        public void AfterFlight_DecaysTowardStart_ElfGrudgeLasts()
        {
            var s = Settings();
            var st = TribeLogic.CreateNew(s);
            st.Get(Tribe.Human).reputation = -30f;
            st.Get(Tribe.Elf).reputation = -30f;
            TribeLogic.AfterFlight(st, s, Always(0.99f));
            Assert.AreEqual(-30f + s.decayPerFlight, Rep(st, Tribe.Human), 1e-4f);
            Assert.AreEqual(-30f + s.elfGrudgeDecay, Rep(st, Tribe.Elf), 1e-4f);
        }

        [Test]
        public void AfterFlight_ReveredAlwaysSendsOfferings_NeutralNever()
        {
            var s = Settings();
            var st = TribeLogic.CreateNew(s);
            st.Get(Tribe.Human).reputation = 80f;
            var r = TribeLogic.AfterFlight(st, s, Always(0f));
            Assert.GreaterOrEqual(r.offerings.Count, 1);
            foreach (var o in r.offerings)
            {
                Assert.AreEqual(Tribe.Human, o.from);
                Assert.AreEqual(s.humanOffering, o.foodId);
            }
            Assert.AreEqual(r.offerings.Count, st.altar.Count);
        }

        [Test]
        public void Altar_DoesNotOverflow()
        {
            var s = Settings();
            var st = TribeLogic.CreateNew(s);
            st.Get(Tribe.Human).reputation = 100f;
            st.Get(Tribe.Orc).reputation = 100f;
            for (int i = 0; i < 10; i++) TribeLogic.AfterFlight(st, s, Always(0f));
            Assert.LessOrEqual(st.altar.Count, s.altarCapacity);
        }

        // ───────────── 전쟁 ─────────────

        [Test]
        public void War_FriendlySideRequests_JoinThenWin_GivesRewards()
        {
            var s = Settings();
            var st = TribeLogic.CreateNew(s);
            st.Get(Tribe.Human).reputation = 30f;
            TribeLogic.StartWar(st, s, (Tribe.Human, Tribe.Orc));
            Assert.AreEqual(WarStatus.Requested, st.war.status);
            Assert.IsTrue(st.war.requestA);
            Assert.IsFalse(st.war.requestB, "중립인 오크는 요청하지 않는다");

            Assert.IsFalse(TribeLogic.JoinWar(st, s, Tribe.Orc), "요청하지 않은 편에는 못 붙는다");
            Assert.IsTrue(TribeLogic.JoinWar(st, s, Tribe.Human));
            Assert.AreEqual(Aggression.Full, TribeLogic.AggressionOf(st, Tribe.Orc, s), "참전하면 적 편이 공격한다");

            TribeLogic.RegisterAttack(st, s, Tribe.Orc);
            Assert.AreEqual(1f, st.war.contribution, 1e-4f);
            Assert.Greater(TribeLogic.WinChance(st.war, s), s.baseWinChance);

            TribeReport r = null;
            for (int i = 0; i < s.warDuration; i++) r = TribeLogic.AfterFlight(st, s, Always(0f));
            Assert.IsTrue(r.warEnded);
            Assert.IsTrue(r.warJoined);
            Assert.IsTrue(r.warWon);
            Assert.AreEqual(Tribe.Human, r.warWinner);
            Assert.AreEqual(s.victoryFlights, st.victoryFlightsLeft);
            Assert.GreaterOrEqual(st.altar.Count, s.winOfferings);
            Assert.AreEqual(s.weakenedFlights, st.Get(Tribe.Orc).weakenedFlights, "진 쪽은 병력이 준다");
            Assert.IsFalse(st.war.Active);
            Assert.AreEqual(s.warCooldown, st.war.cooldown);
        }

        [Test]
        public void War_JoinThenLose_AllyDisappointed()
        {
            var s = Settings();
            var st = TribeLogic.CreateNew(s);
            st.Get(Tribe.Orc).reputation = 30f;
            TribeLogic.StartWar(st, s, (Tribe.Human, Tribe.Orc));
            TribeLogic.JoinWar(st, s, Tribe.Orc);
            float before = Rep(st, Tribe.Orc);
            TribeReport r = null;
            for (int i = 0; i < s.warDuration; i++) r = TribeLogic.AfterFlight(st, s, Always(0.99f));
            Assert.IsFalse(r.warWon);
            Assert.Less(Rep(st, Tribe.Orc), before);
            Assert.AreEqual(0, st.victoryFlightsLeft);
        }

        [Test]
        public void War_Decline_RequesterLosesALittle()
        {
            var s = Settings();
            var st = TribeLogic.CreateNew(s);
            st.Get(Tribe.Elf).reputation = 30f;
            TribeLogic.StartWar(st, s, (Tribe.Orc, Tribe.Elf));
            Assert.IsTrue(TribeLogic.DeclineWar(st, s));
            Assert.AreEqual(30f - s.declineLoss, Rep(st, Tribe.Elf), 1e-4f);
            Assert.AreEqual(WarStatus.Declined, st.war.status);
        }

        [Test]
        public void War_NoFriendlySide_DragonIsBystander()
        {
            var s = Settings();
            var st = TribeLogic.CreateNew(s);
            TribeLogic.StartWar(st, s, (Tribe.Human, Tribe.Orc));
            Assert.AreEqual(WarStatus.Bystander, st.war.status);
        }

        // ───────────── 방어전 ─────────────

        static TribeState JoinedHumanVsOrc(TribeSettings s)
        {
            var st = TribeLogic.CreateNew(s);
            st.Get(Tribe.Human).reputation = 30f;
            TribeLogic.StartWar(st, s, (Tribe.Human, Tribe.Orc));
            TribeLogic.JoinWar(st, s, Tribe.Human);
            return st;
        }

        [Test]
        public void Breach_LowersWinChance_AndDisappointsAlly()
        {
            var s = Settings();
            var st = JoinedHumanVsOrc(s);
            float chance = TribeLogic.WinChance(st.war, s);
            float rep = Rep(st, Tribe.Human);
            TribeLogic.RegisterBreach(st, s);
            Assert.AreEqual(1, st.war.breaches);
            Assert.AreEqual(chance - s.breachPenalty, TribeLogic.WinChance(st.war, s), 1e-4f);
            Assert.AreEqual(rep - s.breachRepLoss, Rep(st, Tribe.Human), 1e-4f);
        }

        [Test]
        public void WinChance_StaysWithinMinMax()
        {
            var s = Settings();
            var st = JoinedHumanVsOrc(s);
            for (int i = 0; i < 50; i++) TribeLogic.RegisterBreach(st, s);
            Assert.AreEqual(s.minWinChance, TribeLogic.WinChance(st.war, s), 1e-4f);
            st.war.breaches = 0;
            st.war.contribution = 1000f;
            Assert.AreEqual(s.maxWinChance, TribeLogic.WinChance(st.war, s), 1e-4f);
        }

        [Test]
        public void RaiderKills_CountFullyForWar_ButMoveReputationLess()
        {
            var s = Settings();
            var st = JoinedHumanVsOrc(s);
            float human = Rep(st, Tribe.Human);
            TribeLogic.RegisterAttack(st, s, Tribe.Orc, s.raiderRepScale, 3f);
            Assert.AreEqual(3f, st.war.contribution, 1e-4f, "쓰러뜨린 수만큼 승률");
            Assert.AreEqual(human + s.rivalGain * s.raiderRepScale, Rep(st, Tribe.Human), 1e-4f, "호감도는 작게");
        }

        [Test]
        public void Breach_WithoutJoinedWar_DoesNothing()
        {
            var s = Settings();
            var st = TribeLogic.CreateNew(s);
            TribeLogic.RegisterBreach(st, s);
            Assert.AreEqual(0, st.war.breaches);
            Assert.AreEqual(0f, Rep(st, Tribe.Human), 1e-4f);
        }

        [Test]
        public void RaidWaves_GrowEachTime()
        {
            var s = Settings();
            Assert.AreEqual(s.raidBaseSize, TribeLogic.RaidSize(0, s));
            Assert.Greater(TribeLogic.RaidSize(2, s), TribeLogic.RaidSize(1, s));
        }

        [Test]
        public void War_StartsByChance_AfterCooldown()
        {
            var s = Settings();
            s.warStartChance = 1f;
            var st = TribeLogic.CreateNew(s);
            st.war.cooldown = 1;
            Assert.IsFalse(TribeLogic.AfterFlight(st, s, Always(0f)).warStarted, "쿨다운 중");
            Assert.IsTrue(TribeLogic.AfterFlight(st, s, Always(0f)).warStarted);
            Assert.IsTrue(st.war.Active);
        }
    }
}
