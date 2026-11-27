using System;
using System.Collections.Generic;
using DragonRecipe.Food;

namespace DragonRecipe.Core
{
    public enum FlightEndReason
    {
        /// <summary>안전하게 착지</summary>
        Landed,
        /// <summary>지면/장애물에 박음</summary>
        Crashed,
        /// <summary>맵 밖으로 추락</summary>
        OutOfBounds
    }

    [Serializable]
    public struct CaughtFood
    {
        public FoodData food;
        /// <summary>아슬아슬 보너스가 적용된 포만도</summary>
        public float satiety;
        public bool nearMiss;
    }

    /// <summary>
    /// 비행 한 번의 결과. 다음 단계(허기/먹이 창고/진화/부상 패널티)가 이 데이터를 받아 처리한다.
    /// </summary>
    [Serializable]
    public class FlightResult
    {
        public float airtime;
        public FlightEndReason reason;
        public List<CaughtFood> caught = new List<CaughtFood>();
        public int nearMissCount;
        public int perfectFlapCount;
        public float maxSpeed;

        public bool Injured => reason != FlightEndReason.Landed;

        public float TotalSatiety
        {
            get
            {
                float s = 0f;
                foreach (var c in caught) s += c.satiety;
                return s;
            }
        }
    }

    /// <summary>시스템 간 느슨한 연결용 전역 이벤트.</summary>
    public static class GameEvents
    {
        public static event Action<FlightResult> FlightEnded;
        public static void RaiseFlightEnded(FlightResult r) => FlightEnded?.Invoke(r);
    }
}
