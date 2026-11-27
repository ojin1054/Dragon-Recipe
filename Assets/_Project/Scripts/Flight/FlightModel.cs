using UnityEngine;

namespace DragonRecipe.Flight
{
    public enum FlapOutcome
    {
        /// <summary>스태미나 부족으로 실패</summary>
        Failed,
        /// <summary>너무 빨리 연타해서 효율이 떨어짐</summary>
        Weak,
        Normal,
        /// <summary>리듬에 맞춘 날갯짓</summary>
        Perfect
    }

    /// <summary>
    /// 드래곤 비행 계산 (MonoBehaviour 없음 → 테스트 가능).
    /// - 기수를 내리면 중력이 속도로 바뀌어 빨라지고, 올리면 속도를 잃는다.
    /// - 양력은 속도² 에 비례. 어린 드래곤은 양력이 모자라 날갯짓으로 보충해야 한다.
    /// </summary>
    public class FlightModel
    {
        public FlightParams Params { get; private set; }

        public Vector3 Position;
        /// <summary>수평 진행 방향 (deg)</summary>
        public float Yaw;
        /// <summary>기수 각도 (deg, + = 위)</summary>
        public float Pitch;
        /// <summary>시각용 기울기 (deg, + = 오른쪽으로 기움)</summary>
        public float Bank;
        /// <summary>기수 방향 속도 (m/s)</summary>
        public float Speed;
        /// <summary>양력 부족/날갯짓으로 생기는 추가 수직 속도 (m/s)</summary>
        public float VerticalSpeed;
        public float Stamina;
        public float Time { get; private set; }
        public int PerfectFlapCount { get; private set; }

        float _lastFlapTime = float.NegativeInfinity;

        const float StallNoseDownPitch = -30f;
        const float StallNoseDropRate = 45f;

        public FlightModel(FlightParams p)
        {
            Params = p;
            Stamina = p.maxStamina;
        }

        public Vector3 Forward => Quaternion.Euler(-Pitch, Yaw, 0f) * Vector3.forward;
        public Vector3 Velocity => Forward * Speed + Vector3.up * VerticalSpeed;
        public Quaternion Rotation => Quaternion.Euler(-Pitch, Yaw, -Bank);
        public bool IsStalling => Speed < Params.stallSpeed;
        public float TimeSinceLastFlap => Time - _lastFlapTime;

        public void Reset(Vector3 position, float yaw, float speed)
        {
            Position = position;
            Yaw = yaw;
            Pitch = 0f;
            Bank = 0f;
            Speed = speed;
            VerticalSpeed = 0f;
            Stamina = Params.maxStamina;
            Time = 0f;
            PerfectFlapCount = 0;
            _lastFlapTime = float.NegativeInfinity;
        }

        /// <summary>현재 속도에서 중력을 상쇄하는 비율 (0~1)</summary>
        public float LiftFraction(float speed)
        {
            float ratio = speed / Mathf.Max(0.01f, Params.liftReferenceSpeed);
            float lift = ratio * ratio * Params.liftCoefficient;
            if (speed < Params.stallSpeed)
                lift *= Mathf.Clamp01(speed / Params.stallSpeed) * 0.5f; // 실속: 양력 급감
            return Mathf.Clamp01(lift);
        }

        /// <summary>마지막 날갯짓 이후 시간으로 효율을 판정</summary>
        public static FlapOutcome JudgeFlapTiming(float timeSinceLast, FlightParams p, out float efficiency)
        {
            if (timeSinceLast < p.spamInterval)
            {
                efficiency = p.spamPenalty;
                return FlapOutcome.Weak;
            }
            if (Mathf.Abs(timeSinceLast - p.idealFlapInterval) <= p.perfectWindow)
            {
                efficiency = p.perfectBonus;
                return FlapOutcome.Perfect;
            }
            efficiency = 1f;
            return FlapOutcome.Normal;
        }

        public FlapOutcome TryFlap()
        {
            if (Stamina < Params.flapStaminaCost)
                return FlapOutcome.Failed;

            var outcome = JudgeFlapTiming(TimeSinceLastFlap, Params, out float eff);
            Stamina -= Params.flapStaminaCost;
            _lastFlapTime = Time;

            VerticalSpeed = Mathf.Min(VerticalSpeed + Params.flapLift * eff, Params.maxRiseSpeed);
            Speed = Mathf.Min(Speed + Params.flapThrust * eff, Params.maxSpeed);
            if (outcome == FlapOutcome.Perfect) PerfectFlapCount++;
            return outcome;
        }

        /// <param name="stick">x = 선회, y = 기수 (+ 위)</param>
        public void Step(float dt, Vector2 stick)
        {
            var p = Params;
            stick = Vector2.ClampMagnitude(stick, 1f);

            // 기수: 스틱은 기수를 "돌리는" 입력. 손을 떼면 그 각도를 그대로 유지한다 (자동 수평 복귀 없음)
            Pitch += stick.y * p.pitchResponse * dt;
            if (IsStalling && Pitch > StallNoseDownPitch)
            {
                // 실속하면 기수가 떨어져 속도를 회복하려 한다 (유일한 자동 개입)
                float stall = 1f - Mathf.Clamp01(Speed / p.stallSpeed);
                Pitch = Mathf.MoveTowards(Pitch, StallNoseDownPitch, (StallNoseDropRate * (stall + 0.3f)) * dt);
            }
            Pitch = Mathf.Clamp(Pitch, -p.maxPitchDown, p.maxPitchUp);

            // 선회
            Yaw += stick.x * p.turnRate * dt;
            Bank = Mathf.MoveTowards(Bank, stick.x * p.maxBank, 120f * dt);

            // 속도: 내려가면 가속, 올라가면 감속
            float sinPitch = Mathf.Sin(Pitch * Mathf.Deg2Rad);
            Speed += (-p.gravity * sinPitch - p.drag * Speed * Speed) * dt;
            Speed = Mathf.Clamp(Speed, p.minSpeed, p.maxSpeed);

            // 수직: 양력이 모자란 만큼 가라앉는다
            float lift = LiftFraction(Speed);
            VerticalSpeed += -p.gravity * (1f - lift) * dt;
            VerticalSpeed -= VerticalSpeed * p.verticalDamping * dt;
            VerticalSpeed = Mathf.Clamp(VerticalSpeed, -p.maxSinkSpeed, p.maxRiseSpeed);

            Position += Velocity * dt;

            // 스태미나 회복
            if (Time - _lastFlapTime >= p.staminaRegenDelay)
                Stamina = Mathf.Min(p.maxStamina, Stamina + p.staminaRegen * dt);

            Time += dt;
        }
    }
}
