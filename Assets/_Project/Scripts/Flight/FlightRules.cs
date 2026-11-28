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
        public const float GroundNormalMinY = 0.5f;

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

        /// <summary>평평한 지면인지 (아니면 나무·바위 옆면 같은 벽)</summary>
        public static bool IsGround(Vector3 surfaceNormal) => surfaceNormal.y >= GroundNormalMinY;

        /// <summary>땅으로 파고드는 속도 (m/s) = 속도 × sin(닿는 각도). 각도가 가파르고 빠를수록 크다</summary>
        public static float IntoGroundSpeed(Vector3 velocity, Vector3 surfaceNormal)
            => Mathf.Max(0f, -Vector3.Dot(velocity, surfaceNormal.normalized));

        /// <summary>땅에 닿는 순간 받는 피해: 기본 피해 + 파고드는 속도에 비례한 추가 피해</summary>
        public static float GroundImpactDamage(Vector3 velocity, Vector3 surfaceNormal, FlightParams p)
            => p.touchDamage + p.impactDamagePerSpeed * Mathf.Max(0f, IntoGroundSpeed(velocity, surfaceNormal) - p.impactFreeSpeed);

        /// <summary>진행 방향과 지면이 이루는 각도 (deg). 0 = 스치듯 평행, 90 = 수직으로 내리꽂음</summary>
        public static float ImpactAngle(Vector3 velocity, Vector3 surfaceNormal)
        {
            if (velocity.sqrMagnitude < 0.0001f) return 0f;
            float into = -Vector3.Dot(velocity.normalized, surfaceNormal.normalized);
            return into <= 0f ? 0f : Mathf.Asin(Mathf.Clamp01(into)) * Mathf.Rad2Deg;
        }

        /// <summary>닿는 각도 → 충격 세기 (0~1)</summary>
        public static float TouchdownStrength(float impactAngle, FlightParams p)
            => Mathf.Clamp01(impactAngle / Mathf.Max(0.01f, p.groundImpactMaxAngle));

        /// <summary>닿는 순간 잃는 속도 비율 (0~groundImpactLoss)</summary>
        public static float TouchdownSpeedLoss(float impactAngle, FlightParams p)
            => TouchdownStrength(impactAngle, p) * p.groundImpactLoss;

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
