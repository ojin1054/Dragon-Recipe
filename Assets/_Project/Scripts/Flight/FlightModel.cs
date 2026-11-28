using UnityEngine;

namespace DragonRecipe.Flight
{
    public enum FlapOutcome
    {
        /// <summary>스태미나 부족으로 실패</summary>
        Failed,
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
        public float Health;
        public float Time { get; private set; }
        public int PerfectFlapCount { get; private set; }
        /// <summary>발톱에 매단 먹이 무게 합</summary>
        public float Payload;
        /// <summary>잠깐 붙은 추가 속도 (연속 사냥 보상). 남은 시간 동안 최고 속도를 그만큼 넘을 수 있다</summary>
        public float BoostSpeed { get; private set; }
        public float BoostTimeLeft { get; private set; }
        public bool IsBoosting => BoostTimeLeft > 0f;

        /// <summary>매단 무게 / 드는 힘 (0 = 빈 발톱)</summary>
        public float LoadFactor => Mathf.Max(0f, Payload) / Mathf.Max(0.01f, Params.carryStrength);
        public float LiftLoadMultiplier => 1f - Mathf.Min(0.8f, LoadFactor * Params.payloadLiftPenalty);
        public float FlapLoadMultiplier => 1f - Mathf.Min(0.7f, LoadFactor * Params.payloadFlapPenalty);
        public float TurnLoadMultiplier => 1f - Mathf.Min(0.6f, LoadFactor * Params.payloadTurnPenalty);

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
        public bool IsDead => Health <= 0f;
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
            Health = Params.maxHealth;
            Time = 0f;
            PerfectFlapCount = 0;
            Payload = 0f;
            BoostSpeed = 0f;
            BoostTimeLeft = 0f;
            _lastFlapTime = float.NegativeInfinity;
        }

        /// <summary>잠깐 속도를 붙인다: 바로 extraSpeed만큼 빨라지고, duration 동안은 최고 속도 제한도 그만큼 풀린다</summary>
        public void Boost(float extraSpeed, float duration)
        {
            if (extraSpeed <= 0f || duration <= 0f) return;
            BoostSpeed = Mathf.Max(IsBoosting ? BoostSpeed : 0f, extraSpeed);
            BoostTimeLeft = Mathf.Max(BoostTimeLeft, duration);
            Speed = Mathf.Min(Speed + extraSpeed, Params.maxSpeed + BoostSpeed);
        }

        /// <summary>매달린 먹이가 버둥거려 몸이 흔들린다</summary>
        public void Jolt(float bankDelta, float pitchDelta, float staminaCost)
        {
            Bank = Mathf.Clamp(Bank + bankDelta, -Params.maxBank, Params.maxBank);
            Pitch = Mathf.Clamp(Pitch + pitchDelta, -Params.maxPitchDown, Params.maxPitchUp);
            Stamina = Mathf.Max(0f, Stamina - staminaCost);
        }

        /// <summary>공중에서 먹이를 먹어 스태미나를 회복한다</summary>
        public void Eat(float staminaGain)
        {
            Stamina = Mathf.Min(Params.maxStamina, Stamina + Mathf.Max(0f, staminaGain));
        }

        /// <summary>땅 밖에서 입은 피해 (걷어차임 등)</summary>
        public void TakeDamage(float damage)
        {
            Health = Mathf.Max(0f, Health - Mathf.Max(0f, damage));
        }

        /// <summary>
        /// 절벽에서 도약한다. 달리기 파워 → 출발 속도, 웅크리기 판정 → 상승 속도와 기수 각도.
        /// </summary>
        public void Launch(Vector3 position, float yaw, LaunchPlan plan)
        {
            var p = Params;
            float speed = Mathf.Min(Mathf.Lerp(p.launchSpeed, p.launchMaxSpeed, Mathf.Clamp01(plan.power)), p.maxSpeed);
            Reset(position, yaw, speed);

            if (plan.grade == LaunchGrade.Stumble)
            {
                // 발을 헛디뎌 고꾸라짐: 속도는 남지만 높이는 없다
                Pitch = Mathf.Clamp(p.stumblePitch, -p.maxPitchDown, p.maxPitchUp);
                VerticalSpeed = 0f;
                return;
            }

            float h = Mathf.Clamp01(plan.height);
            float bonus = plan.grade == LaunchGrade.Perfect ? p.launchPerfectBonus : 1f;
            Pitch = Mathf.Clamp(Mathf.Lerp(0f, p.launchMaxPitch, h) * bonus, -p.maxPitchDown, p.maxPitchUp);
            VerticalSpeed = Mathf.Min(Mathf.Lerp(p.launchRise, p.launchMaxRise, h), p.maxRiseSpeed);
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

            VerticalSpeed = Mathf.Min(VerticalSpeed + Params.flapLift * eff * FlapLoadMultiplier, Params.maxRiseSpeed);
            // 추가 속도가 붙어 있으면 날갯짓이 그 속도를 깎지 않는다
            float cap = Params.maxSpeed + (IsBoosting ? BoostSpeed : 0f);
            Speed = Mathf.Max(Speed, Mathf.Min(Speed + Params.flapThrust * eff, cap));
            if (outcome == FlapOutcome.Perfect) PerfectFlapCount++;
            return outcome;
        }

        /// <summary>지면에 닿는 순간의 피해. 받은 피해량을 돌려준다</summary>
        public float TakeGroundImpact(Vector3 velocity, Vector3 surfaceNormal)
        {
            float damage = FlightRules.GroundImpactDamage(velocity, surfaceNormal, Params);
            Health = Mathf.Max(0f, Health - damage);
            return damage;
        }

        /// <summary>지면에 닿는 순간: 닿는 각도가 클수록 속도를 크게 잃는다. 충격 세기(0~1)를 돌려준다</summary>
        public float Touchdown(Vector3 velocity, Vector3 surfaceNormal)
        {
            float angle = FlightRules.ImpactAngle(velocity, surfaceNormal);
            Speed = Mathf.Max(Params.minSpeed, Speed * (1f - FlightRules.TouchdownSpeedLoss(angle, Params)));
            return FlightRules.TouchdownStrength(angle, Params);
        }

        /// <summary>지면에 닿아 미끄러지는 중: 파고들지 않게 하고 마찰로 감속</summary>
        public void Skid(float dt)
        {
            if (VerticalSpeed < 0f) VerticalSpeed = 0f;
            if (Pitch < 0f) Pitch = 0f;
            Speed = Mathf.Max(Params.minSpeed, Speed - Params.groundFriction * dt);
            Health = Mathf.Max(0f, Health - Params.skidDamagePerSecond * dt);
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

            // 선회: 스틱 좌우는 몸을 "기울이는" 입력. 손을 떼면 기울기를 유지하고,
            // 기울어진 만큼 계속 선회한다 (수평으로 돌리려면 반대로 밀어야 함)
            // 무거운 먹이를 매달면 몸이 둔해진다
            float turnLoad = TurnLoadMultiplier;
            Bank += stick.x * p.bankResponse * turnLoad * dt;
            Bank = Mathf.Clamp(Bank, -p.maxBank, p.maxBank);
            Yaw += (Bank / Mathf.Max(1f, p.maxBank)) * p.turnRate * turnLoad * dt;

            // 속도: 내려가면 가속, 올라가면 감속
            float sinPitch = Mathf.Sin(Pitch * Mathf.Deg2Rad);
            Speed += (-p.gravity * sinPitch - p.drag * Speed * Speed) * dt;
            // 추가 속도가 붙어 있는 동안은 최고 속도를 넘을 수 있다 (끝나면 공기 저항으로 서서히 줄어듦)
            if (BoostTimeLeft > 0f)
            {
                BoostTimeLeft = Mathf.Max(0f, BoostTimeLeft - dt);
                Speed = Mathf.Clamp(Speed, p.minSpeed, p.maxSpeed + BoostSpeed);
            }
            else if (Speed > p.maxSpeed)
            {
                Speed = Mathf.Max(p.maxSpeed, Speed - p.maxSpeed * 0.5f * dt);
            }
            else
            {
                Speed = Mathf.Max(Speed, p.minSpeed);
            }

            // 수직: 양력이 모자란 만큼 가라앉는다
            float lift = LiftFraction(Speed) * LiftLoadMultiplier;
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
