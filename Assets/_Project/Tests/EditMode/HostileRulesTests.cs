using DragonRecipe.Hostile;
using NUnit.Framework;
using UnityEngine;

namespace DragonRecipe.Tests
{
    public class HostileRulesTests
    {
        [Test]
        public void Lead_StillTarget_AimsStraight()
        {
            Assert.IsTrue(HostileRules.LeadDirection(Vector3.zero, new Vector3(0, 0, 30), Vector3.zero, 45f, out var dir));
            Assert.AreEqual(1f, Vector3.Dot(dir, Vector3.forward), 1e-4f);
        }

        [Test]
        public void Lead_MovingTarget_ArrowMeetsTarget()
        {
            Vector3 from = Vector3.zero, target = new Vector3(0, 20, 40), vel = new Vector3(25, 0, 0);
            const float speed = 45f;
            Assert.IsTrue(HostileRules.LeadDirection(from, target, vel, speed, out var dir));
            Assert.Greater(dir.x, 0f, "움직이는 쪽으로 앞질러 쏜다");

            // 화살과 목표가 같은 순간 같은 자리에 있는지
            float best = float.MaxValue;
            for (float t = 0f; t < 3f; t += 0.001f)
                best = Mathf.Min(best, Vector3.Distance(from + dir * speed * t, target + vel * t));
            Assert.Less(best, 0.1f);
        }

        [Test]
        public void Lead_TargetFasterAndFleeing_ReturnsFalse()
        {
            Assert.IsFalse(HostileRules.LeadDirection(Vector3.zero, new Vector3(0, 0, 30), new Vector3(0, 0, 60), 45f, out _));
        }

        [Test]
        public void SegmentDistance_CatchesFastPassThrough()
        {
            // 한 프레임에 목표를 지나쳐도 선분으로 검사하면 잡힌다
            Assert.AreEqual(0.5f, HostileRules.SegmentDistance(new Vector3(0, 0, -5), new Vector3(0, 0, 5), new Vector3(0.5f, 0, 0)), 1e-4f);
            Assert.AreEqual(5f, HostileRules.SegmentDistance(Vector3.zero, new Vector3(0, 0, 1), new Vector3(0, 0, 6)), 1e-4f);
        }

        [Test]
        public void Steer_TurnsAtMostMaxDegrees()
        {
            var f = HostileRules.Steer(Vector3.forward, Vector3.right, 30f);
            Assert.AreEqual(30f, Vector3.Angle(Vector3.forward, f), 0.01f);
        }

        [Test]
        public void Monster_ChasesNearDragon_ButNotBeyondLeash()
        {
            var p = new HostileParams();
            Vector3 home = Vector3.zero;
            Assert.IsTrue(HostileRules.ShouldChase(home, home, new Vector3(0, 0, p.monsterAggroRadius - 1f), p));
            Assert.IsFalse(HostileRules.ShouldChase(home, home, new Vector3(0, 0, p.monsterAggroRadius + 1f), p));
            var farFromHome = new Vector3(p.monsterLeashRadius + 1f, 0, 0);
            Assert.IsFalse(HostileRules.ShouldChase(farFromHome, home, farFromHome + Vector3.forward, p));
            Assert.Greater(HostileRules.ChaseSpeed(1f, p), HostileRules.ChaseSpeed(p.monsterLungeRange + 5f, p), "가까우면 덤벼든다");
        }

        [Test]
        public void Griffins_SuspectBeforeTheyChase()
        {
            var p = new HostileParams();
            Vector3 home = Vector3.zero;
            var justOutside = new Vector3(0, 0, p.monsterAggroRadius + 5f);
            Assert.IsFalse(HostileRules.ShouldChase(home, home, justOutside, p));
            Assert.IsTrue(HostileRules.Suspects(home, home, justOutside, p), "쫓아올 거리 바로 밖에서는 의심한다");
            var far = new Vector3(0, 0, p.monsterAggroRadius * p.monsterSuspectScale + 5f);
            Assert.IsFalse(HostileRules.Suspects(home, home, far, p), "멀면 모른다");
        }

        [Test]
        public void Hunters_FrightenedOnlyByLowCloseDragon()
        {
            var p = new HostileParams();
            Assert.IsTrue(HostileRules.Frightened(Vector3.zero, new Vector3(3, 4, 0), p));
            Assert.IsFalse(HostileRules.Frightened(Vector3.zero, new Vector3(3, 30, 0), p), "높이 날면 안 무서워함");
            Assert.IsFalse(HostileRules.Frightened(Vector3.zero, new Vector3(30, 2, 0), p));
        }
    }
}
