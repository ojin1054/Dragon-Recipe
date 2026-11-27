using UnityEngine;

namespace DragonRecipe.Flight
{
    public enum LaunchGrade
    {
        /// <summary>떼지 못하고 절벽 끝을 넘어감 — 고꾸라지며 출발</summary>
        Stumble,
        /// <summary>너무 일찍 뛰었거나 덜 웅크림</summary>
        Weak,
        Good,
        /// <summary>끝까지 웅크린 채 절벽 끝에서 뜀</summary>
        Perfect
    }

    /// <summary>도약 결과. 달리기 = 파워(출발 속도), 웅크리기 = 높이.</summary>
    public struct LaunchPlan
    {
        /// <summary>달리기 속도 비율 (0~1) → 출발 속도</summary>
        public float power;
        /// <summary>웅크리기 판정 (0~1) → 상승 속도와 기수 각도</summary>
        public float height;
        /// <summary>웅크린 깊이 (0~1)</summary>
        public float depth;
        /// <summary>뗀 순간 절벽 끝까지 남은 거리 (m, 음수 = 넘어감)</summary>
        public float remaining;
        public LaunchGrade grade;
    }

    /// <summary>
    /// 이륙 도움닫기 (MonoBehaviour 없음 → 테스트 가능).
    /// 탭할 때마다 절벽 쪽으로 가속하고, 절벽 끝 근처(도약 구간)에서 꾹 눌러 웅크렸다가
    /// 떼는 위치와 웅크린 깊이로 높이를 판정한다.
    /// 수치는 매 호출마다 받는다 (성장/굶주림으로 FlightParams가 바뀔 수 있어서).
    /// </summary>
    public class RunUpModel
    {
        /// <summary>출발점에서 절벽 끝까지 거리 (m)</summary>
        public float EdgeDistance { get; }
        public float Distance { get; private set; }
        public float RunSpeed { get; private set; }
        public bool Started { get; private set; }
        public bool IsCrouching { get; private set; }
        public float CrouchTime { get; private set; }

        public RunUpModel(float edgeDistance)
        {
            EdgeDistance = Mathf.Max(0f, edgeDistance);
        }

        public float Remaining => EdgeDistance - Distance;
        public bool Overran => Remaining < 0f;
        public bool InJumpZone(FlightParams p) => Remaining <= p.jumpZoneLength;
        public float PowerFraction(FlightParams p) => Mathf.Clamp01(RunSpeed / Mathf.Max(0.01f, p.runMaxSpeed));
        public float CrouchDepth(FlightParams p) => Mathf.Clamp01(CrouchTime / Mathf.Max(0.01f, p.crouchFullTime));

        /// <summary>달리기 탭: 속도가 붙는다 (웅크린 뒤에는 무시)</summary>
        public void Tap(FlightParams p)
        {
            if (IsCrouching) return;
            Started = true;
            RunSpeed = Mathf.Min(RunSpeed + p.runTapAccel, p.runMaxSpeed);
        }

        /// <summary>도약 구간 안에서만 웅크릴 수 있다</summary>
        public bool TryCrouch(FlightParams p)
        {
            if (IsCrouching) return true;
            if (!InJumpZone(p)) return false;
            Started = true;
            IsCrouching = true;
            CrouchTime = 0f;
            return true;
        }

        public void Step(float dt, FlightParams p)
        {
            if (!Started) return;
            // 탭을 멈추면 서서히, 웅크리면 미끄러지며 더 빨리 느려진다
            float decel = IsCrouching ? p.crouchDecel : p.runFriction;
            RunSpeed = Mathf.Max(0f, RunSpeed - decel * dt);
            Distance += RunSpeed * dt;
            if (IsCrouching) CrouchTime += dt;
        }

        public LaunchPlan Release(FlightParams p)
            => Evaluate(RunSpeed, Remaining, IsCrouching ? CrouchTime : 0f, p);

        /// <param name="runSpeed">뗀 순간의 달리기 속도</param>
        /// <param name="remaining">뗀 순간 절벽 끝까지 남은 거리 (음수 = 넘어감)</param>
        /// <param name="crouchTime">웅크리고 있던 시간</param>
        public static LaunchPlan Evaluate(float runSpeed, float remaining, float crouchTime, FlightParams p)
        {
            var plan = new LaunchPlan
            {
                power = Mathf.Clamp01(runSpeed / Mathf.Max(0.01f, p.runMaxSpeed)),
                depth = Mathf.Clamp01(crouchTime / Mathf.Max(0.01f, p.crouchFullTime)),
                remaining = remaining
            };

            if (remaining < 0f)
            {
                plan.grade = LaunchGrade.Stumble;
                plan.height = 0f;
                return plan;
            }

            // 위치 점수: 완벽 거리 안이면 1, 도약 구간 시작점이면 0.2
            float position = remaining <= p.perfectJumpDistance
                ? 1f
                : Mathf.Lerp(1f, 0.2f, Mathf.InverseLerp(p.perfectJumpDistance, Mathf.Max(p.perfectJumpDistance + 0.01f, p.jumpZoneLength), remaining));
            plan.height = position * plan.depth;

            if (remaining <= p.perfectJumpDistance && plan.depth >= 1f) plan.grade = LaunchGrade.Perfect;
            else if (plan.height >= p.goodJumpThreshold) plan.grade = LaunchGrade.Good;
            else plan.grade = LaunchGrade.Weak;
            return plan;
        }
    }
}
