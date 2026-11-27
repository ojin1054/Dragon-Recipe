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
        /// <summary>아슬아슬 / 연속 사냥 / 희귀 보너스가 적용된 포만도</summary>
        public float satiety;
        public bool nearMiss;
        /// <summary>희귀 개체 (황금빛)</summary>
        public bool rare;
        /// <summary>몇 번째 연속 사냥이었는지 (1 = 단독)</summary>
        public int combo;
        /// <summary>비행 중에 먹어 버림 → 비행이 끝나면 바로 배를 채운다</summary>
        public bool eaten;
        /// <summary>추락하면서 떨어뜨림 → 잃어버림</summary>
        public bool dropped;

        /// <summary>아직 발톱에 매달고 있는지 (끝까지 들고 오면 먹이 창고로)</summary>
        public bool Carried => !eaten && !dropped;
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
        /// <summary>가장 길게 이어 간 연속 사냥</summary>
        public int maxCombo;

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

        public int CarriedCount
        {
            get
            {
                int n = 0;
                foreach (var c in caught)
                    if (c.Carried) n++;
                return n;
            }
        }
    }

    /// <summary>사냥 수치 (FlightSession에서 튜닝)</summary>
    [Serializable]
    public class HuntSettings
    {
        [UnityEngine.Header("연속 사냥")]
        [UnityEngine.Tooltip("이 시간(초) 안에 다음 먹이를 잡으면 연속 사냥")]
        public float comboWindow = 3f;
        [UnityEngine.Tooltip("연속 사냥 한 단계마다 돌려받는 스태미나 (2연속 = 1단계)")]
        public float comboStaminaPerStep = 15f;
        public float comboStaminaMax = 60f;
        [UnityEngine.Tooltip("연속 사냥 한 단계마다 잠깐 붙는 추가 속도 (m/s). 상한 없이 이어 갈수록 계속 커지고, 최고 속도도 그만큼 잠깐 넘을 수 있다")]
        public float comboBoostPerStep = 4f;
        [UnityEngine.Tooltip("추가 속도가 붙어 있는 시간 (초)")]
        public float comboBoostTime = 1.5f;

        [UnityEngine.Header("공중에서 먹기")]
        [UnityEngine.Tooltip("먹는 동안 조종이 굳는 시간 (초)")]
        public float eatFreezeTime = 0.7f;
        [UnityEngine.Tooltip("먹이 포만도 1당 회복하는 스태미나")]
        public float eatStaminaPerSatiety = 1.5f;

        [UnityEngine.Header("추락")]
        [UnityEngine.Tooltip("추락하면 들고 있던 먹이 중 이 비율을 떨어뜨린다 (반올림)")]
        [UnityEngine.Range(0f, 1f)] public float crashDropFraction = 0.5f;

        [UnityEngine.Header("버둥거리는 먹이 (멧돼지 등)")]
        [UnityEngine.Tooltip("매달린 먹이가 버둥거리는 평균 간격 (초)")]
        public float struggleInterval = 0.9f;
        [UnityEngine.Tooltip("버둥거릴 때 몸이 기울어지는 각도 (deg)")]
        public float struggleBank = 16f;
        public float strugglePitch = 5f;
        [UnityEngine.Tooltip("버둥거릴 때마다 줄어드는 스태미나")]
        public float struggleStamina = 3f;
        [UnityEngine.Tooltip("아슬아슬하지 않게(느리게/높게) 잡으면 걷어차여 받는 피해")]
        public float kickDamage = 12f;

        [UnityEngine.Header("희귀 개체")]
        public float rareSatietyMultiplier = 2f;
        public float rareWeightMultiplier = 1.3f;
    }

    /// <summary>사냥 계산 (순수 함수 → 테스트 가능)</summary>
    public static class HuntRules
    {
        /// <summary>연속 사냥으로 돌려받는 스태미나 (단독 사냥은 0)</summary>
        /// <param name="combo">1 = 단독, 2 = 두 번째 연속 …</param>
        public static float ComboStamina(int combo, HuntSettings h)
            => UnityEngine.Mathf.Min(h.comboStaminaMax, h.comboStaminaPerStep * UnityEngine.Mathf.Max(0, combo - 1));

        /// <summary>연속 사냥으로 잠깐 붙는 추가 속도 (단독 사냥은 0, 상한 없음)</summary>
        public static float ComboBoost(int combo, HuntSettings h)
            => h.comboBoostPerStep * UnityEngine.Mathf.Max(0, combo - 1);

        /// <summary>추락할 때 떨어뜨리는 먹이 수 (반올림: 1개 들고 있으면 1개 다 잃음)</summary>
        public static int DropCount(int carried, float fraction)
        {
            if (carried <= 0) return 0;
            int n = UnityEngine.Mathf.FloorToInt(carried * UnityEngine.Mathf.Clamp01(fraction) + 0.5f);
            return UnityEngine.Mathf.Clamp(n, 0, carried);
        }

        public static float CarryWeight(CaughtFood c, HuntSettings h)
            => c.food ? c.food.carryWeight * (c.rare ? h.rareWeightMultiplier : 1f) : 0f;

        public static float Payload(IEnumerable<CaughtFood> caught, HuntSettings h)
        {
            float w = 0f;
            foreach (var c in caught)
                if (c.Carried) w += CarryWeight(c, h);
            return w;
        }
    }

    /// <summary>시스템 간 느슨한 연결용 전역 이벤트.</summary>
    public static class GameEvents
    {
        public static event Action<FlightResult> FlightEnded;
        public static void RaiseFlightEnded(FlightResult r) => FlightEnded?.Invoke(r);
    }
}
