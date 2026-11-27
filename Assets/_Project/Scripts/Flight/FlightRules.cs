using UnityEngine;

namespace DragonRecipe.Flight
{
    public enum ImpactType
    {
        /// <summary>부드럽게 내려앉음 — 부상 없음</summary>
        Landed,
        /// <summary>지면/장애물에 박음 — 부상</summary>
        Crashed
    }

    /// <summary>충돌 판정과 낚아채기 보너스 계산 (순수 함수).</summary>
    public static class FlightRules
    {
        /// <summary>이 기울기보다 가파른 면(나무, 바위 옆면)은 무조건 충돌</summary>
        public const float GroundNormalMinY = 0.7f;

        public static ImpactType ClassifyImpact(Vector3 velocity, Vector3 surfaceNormal, FlightParams p)
        {
            if (surfaceNormal.y < GroundNormalMinY)
                return ImpactType.Crashed;

            float descent = -velocity.y;
            float horizontal = new Vector2(velocity.x, velocity.z).magnitude;
            return descent <= p.safeLandingSink && horizontal <= p.safeLandingSpeed
                ? ImpactType.Landed
                : ImpactType.Crashed;
        }

        /// <summary>
        /// 아슬아슬 계수 (0~1): 지면에 가까울수록, 빠를수록 1에 가깝다.
        /// </summary>
        public static float NearMissFactor(float altitude, float speed, FlightParams p, float nearMissAltitude)
        {
            float lowness = 1f - Mathf.Clamp01(altitude / Mathf.Max(0.01f, nearMissAltitude));
            float fastness = Mathf.InverseLerp(p.stallSpeed, p.maxSpeed * 0.75f, speed);
            return lowness * fastness;
        }

        /// <summary>포만도 배율: 1 ~ 1 + maxBonus</summary>
        public static float SnatchMultiplier(float nearMissFactor, float maxBonus = 1f)
            => 1f + Mathf.Clamp01(nearMissFactor) * maxBonus;

        public const float NearMissThreshold = 0.35f;
    }
}
