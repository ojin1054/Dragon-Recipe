using DragonRecipe.UI;
using NUnit.Framework;

namespace DragonRecipe.Tests
{
    public class AwarenessMarksTests
    {
        const float Scale = 1.6f;

        [Test]
        public void FleeingPrey_SuspectsFirst_ThenAlerts()
        {
            Assert.AreEqual(AwarenessMarks.Level.None, AwarenessMarks.PreyLevel(true, 1f, false, 25f, 50f, 5f, Scale));
            Assert.AreEqual(AwarenessMarks.Level.Suspicious, AwarenessMarks.PreyLevel(true, 1f, false, 25f, 35f, 5f, Scale));
            Assert.AreEqual(AwarenessMarks.Level.Alerted, AwarenessMarks.PreyLevel(true, 1f, false, 25f, 20f, 5f, Scale));
            Assert.AreEqual(AwarenessMarks.Level.Alerted, AwarenessMarks.PreyLevel(true, 1f, true, 25f, 80f, 5f, Scale), "흩어지는 중");
        }

        [Test]
        public void CalmPrey_OnlySuspects_AndPlantsShowNothing()
        {
            Assert.AreEqual(AwarenessMarks.Level.Suspicious, AwarenessMarks.PreyLevel(false, 0.8f, false, 25f, 10f, 5f, Scale));
            Assert.AreEqual(AwarenessMarks.Level.None, AwarenessMarks.PreyLevel(false, 0.8f, false, 25f, 30f, 5f, Scale));
            Assert.AreEqual(AwarenessMarks.Level.None, AwarenessMarks.PreyLevel(false, 0f, false, 25f, 1f, 1f, Scale), "식물");
        }

        [Test]
        public void HighAbove_IsNotNoticed()
        {
            Assert.AreEqual(AwarenessMarks.Level.None, AwarenessMarks.PreyLevel(true, 1f, false, 25f, 5f, 60f, Scale));
        }

        [Test]
        public void Members_AlertWhenFleeingOrAiming_SuspectWhenNear()
        {
            Assert.AreEqual(AwarenessMarks.Level.Alerted, AwarenessMarks.MemberLevel(true, false, false, 99f, 99f, 20f, 0f, 140f));
            Assert.AreEqual(AwarenessMarks.Level.Alerted, AwarenessMarks.MemberLevel(false, true, true, 99f, 99f, 20f, 55f, 140f));
            Assert.AreEqual(AwarenessMarks.Level.Suspicious, AwarenessMarks.MemberLevel(false, false, false, 15f, 10f, 20f, 0f, 18f));
            Assert.AreEqual(AwarenessMarks.Level.Suspicious, AwarenessMarks.MemberLevel(false, false, true, 40f, 30f, 20f, 55f, 50f), "병사는 사거리 안이면 의심");
            Assert.AreEqual(AwarenessMarks.Level.None, AwarenessMarks.MemberLevel(false, false, false, 40f, 30f, 20f, 0f, 50f));
        }
    }
}
