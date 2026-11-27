using System;
using UnityEngine;

namespace DragonRecipe.Flight
{
    /// <summary>
    /// FlightModel을 씬에 연결: 입력 → 계산 → 이동, 그리고 지면/장애물 충돌 감지.
    /// 드래곤 자체에는 일반 콜라이더가 없고, 이동 경로를 SphereCast로 검사한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DragonFlightController : MonoBehaviour
    {
        public DragonStageData stage;
        public DragonInput input;
        [Tooltip("충돌 검사 반경")]
        public float collisionRadius = 0.6f;
        [Tooltip("이 높이 아래로 떨어지면 맵 밖으로 추락한 것으로 처리")]
        public float killY = -20f;

        public FlightModel Model { get; private set; }
        /// <summary>지금 드래곤의 능력치 (힘·공격력 등. 비행 수치에 들어가는 값은 Model.Params에도 반영됨)</summary>
        public DragonStats Stats { get; private set; }
        public bool IsFlying { get; private set; }
        /// <summary>바로 아래 지면까지 거리 (m)</summary>
        public float Altitude { get; private set; }

        /// <summary>이륙 전 도움닫기 (둥지 → 절벽 끝). 비행 중에는 null</summary>
        public RunUpModel RunUp { get; private set; }

        public event Action<FlapOutcome> Flapped;
        public event Action<LaunchPlan> Launched;
        /// <summary>비행 중 지면에 살짝 닿아 미끄러지기 시작함 (아직 끝난 게 아님). (닿은 점, 지면 법선, 충격 세기 0~1)</summary>
        public event Action<Vector3, Vector3, float> TouchedGround;
        /// <summary>땅에 부딪혀 체력이 깎임 (피해량)</summary>
        public event Action<float> Damaged;

        /// <summary>지면에 닿아 미끄러지는 중</summary>
        public bool IsSkidding => IsFlying && _groundTime > 0f;
        /// <summary>미끄러진 시간 / 유예 시간 (0~1). 1이 되면 착지로 끝남</summary>
        public float SkidProgress => Model == null ? 0f : Mathf.Clamp01(_groundTime / Mathf.Max(0.01f, Model.Params.touchdownGrace));
        /// <summary>(종류, 충돌 시 속도)</summary>
        public event Action<ImpactType, Vector3> Impacted;

        /// <summary>공중에서 먹이를 먹는 중 (조종이 잠깐 굳음)</summary>
        public bool IsEating => IsFlying && Time.time < _eatUntil;
        float _eatUntil = -1f;

        /// <summary>먹기 시작: duration 동안 스틱과 날갯짓이 듣지 않는다</summary>
        public void BeginEat(float duration)
        {
            _eatUntil = Time.time + Mathf.Max(0f, duration);
            _flapRequested = false;
        }

        /// <summary>땅 밖에서 피해를 입는다 (걷어차임 등). 체력이 다하면 추락</summary>
        public void ApplyDamage(float damage)
        {
            if (!IsFlying || damage <= 0f) return;
            Model.TakeDamage(damage);
            Damaged?.Invoke(damage);
            if (Model.IsDead) EndFlight(ImpactType.Crashed, Model.Velocity);
        }

        Rigidbody _rb;
        bool _flapRequested;
        Vector3 _runStart;
        float _groundTime;
        float _airTime;
        const float GroundSkin = 0.15f;
        /// <summary>이만큼 더 떠올라야 "지면에서 떨어짐"으로 본다 (닿았다 떨어졌다 깜빡임 방지)</summary>
        const float LiftOffMargin = 0.3f;
        /// <summary>떨어져 있던 시간이 이보다 짧으면 다시 닿아도 새 충격으로 치지 않는다</summary>
        const float ReimpactDelay = 0.25f;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            // SetStage가 먼저 불렸으면 (FlightCareBridge) 그 값을 유지
            if (Model == null) Model = new FlightModel(stage ? stage.FlightWithStats() : new FlightParams());
            if (Stats == null) Stats = stage ? stage.stats.Clone() : new DragonStats();
            // [임시] 이미 만들어 둔 씬에도 흙 이펙트가 붙도록
            if (!GetComponent<GroundImpactFx>()) gameObject.AddComponent<GroundImpactFx>().controller = this;
        }

        /// <param name="flightOverride">굶주림 등이 반영된 수치. null이면 단계 기본값</param>
        /// <param name="stats">성체 모습 등이 반영된 능력치. null이면 단계 기본값</param>
        public void SetStage(DragonStageData data, FlightParams flightOverride = null, DragonStats stats = null)
        {
            stage = data;
            Stats = (stats ?? data.stats).Clone();
            float yaw = Model != null ? Model.Yaw : transform.eulerAngles.y;
            Model = new FlightModel(flightOverride ?? data.FlightWithStats(Stats));
            Model.Reset(transform.position, yaw, 0f); // 대기 중 위치/방향 유지
            transform.localScale = Vector3.one * data.bodyScale;
        }

        /// <summary>둥지에 세워 둔다 (비행 안 함)</summary>
        /// <param name="runUpDistance">둥지에서 절벽 끝까지 거리. 0이면 바로 도약 구간</param>
        public void Park(Vector3 position, float yaw, float runUpDistance = 0f)
        {
            IsFlying = false;
            _runStart = position;
            RunUp = new RunUpModel(runUpDistance);
            Model.Reset(position, yaw, 0f);
            _rb.position = position;
            _rb.rotation = Quaternion.Euler(0, yaw, 0);
            transform.SetPositionAndRotation(position, _rb.rotation);
            UpdateAltitude();
        }

        /// <summary>도움닫기를 끝내고 도약한다 (판정은 RunUp 상태로 계산)</summary>
        public LaunchPlan Launch()
        {
            var plan = RunUp != null
                ? RunUp.Release(Model.Params)
                : RunUpModel.Evaluate(0f, 0f, 0f, Model.Params);
            Model.Launch(_rb.position, Model.Yaw, plan);
            RunUp = null;
            _groundTime = 0f;
            _airTime = ReimpactDelay;
            IsFlying = true;
            _eatUntil = -1f;
            input?.ConsumeFlap();
            Launched?.Invoke(plan);
            return plan;
        }

        void Update()
        {
            // 비행 중일 때만 날갯짓을 가져간다 (대기 중 입력은 FlightSession이 이륙에 사용)
            if (IsFlying && input != null && input.ConsumeFlap())
                _flapRequested = true;
        }

        void FixedUpdate()
        {
            if (!IsFlying)
            {
                _flapRequested = false;
                StepRunUp(Time.fixedDeltaTime);
                return;
            }

            bool eating = IsEating;
            if (_flapRequested)
            {
                _flapRequested = false;
                if (!eating) Flapped?.Invoke(Model.TryFlap());
            }

            float dt = Time.fixedDeltaTime;
            Vector3 from = Model.Position;
            Model.Step(dt, input && !eating ? input.Move : Vector2.zero);
            Vector3 to = Model.Position;

            Vector3 delta = to - from;
            float dist = delta.magnitude;
            bool touching = false;
            Vector3 touchPoint = default, touchNormal = Vector3.up, touchVelocity = Model.Velocity;
            if (dist > 0.0001f &&
                Physics.SphereCast(from, collisionRadius, delta / dist, out var hit, dist,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                Vector3 velocity = Model.Velocity;
                if (!FlightRules.IsGround(hit.normal))
                {
                    // 나무·바위 옆면 같은 벽에 박음 → 바로 추락
                    Model.Position = hit.point + hit.normal * collisionRadius;
                    _rb.MovePosition(Model.Position);
                    EndFlight(ImpactType.Crashed, velocity);
                    return;
                }

                // 지면에 닿음: 지면 위에 올려놓고 미끄러진다 (세게 닿으면 체력이 크게 깎임)
                to.y = Mathf.Max(to.y, hit.point.y + collisionRadius);
                Model.Position = to;
                touching = true;
                touchPoint = hit.point;
                touchNormal = hit.normal;
                touchVelocity = velocity;
            }

            _rb.MovePosition(to);
            _rb.MoveRotation(Model.Rotation);
            UpdateAltitude();

            // 지면에 붙어 있는 동안 유예 시간을 센다. 날갯짓 등으로 충분히 떠오르면 초기화
            if (!touching && Altitude <= collisionRadius + GroundSkin)
            {
                touching = true;
                touchPoint = Model.Position + Vector3.down * Altitude;
            }
            if (touching)
            {
                if (_groundTime <= 0f && _airTime >= ReimpactDelay)
                {
                    // 새로 닿음: 각도·속도만큼 피해 + 감속 + 흙 이펙트
                    float damage = Model.TakeGroundImpact(touchVelocity, touchNormal);
                    float strength = Model.Touchdown(touchVelocity, touchNormal);
                    TouchedGround?.Invoke(touchPoint, touchNormal, strength);
                    Damaged?.Invoke(damage);
                }
                _airTime = 0f;
                Model.Skid(dt); // 미끄러지는 동안에도 조금씩 깎인다
                if (Model.IsDead)
                {
                    EndFlight(ImpactType.Crashed, touchVelocity);
                    return;
                }
                _groundTime += dt;
                if (_groundTime >= Model.Params.touchdownGrace)
                {
                    EndFlight(ImpactType.Landed, Model.Velocity);
                    return;
                }
            }
            else
            {
                _airTime += dt;
                if (Altitude > collisionRadius + GroundSkin + LiftOffMargin) _groundTime = 0f;
            }

            if (to.y < killY)
            {
                IsFlying = false;
                Impacted?.Invoke(ImpactType.Crashed, Model.Velocity);
            }
        }

        void EndFlight(ImpactType type, Vector3 velocity)
        {
            IsFlying = false;
            _groundTime = 0f;
            if (type == ImpactType.Landed)
                _rb.MoveRotation(Quaternion.Euler(0, Model.Yaw, 0));
            Impacted?.Invoke(type, velocity);
        }

        /// <summary>둥지에서 절벽 끝 방향으로 달린다 (땅 위, 높이 고정)</summary>
        void StepRunUp(float dt)
        {
            if (RunUp == null || !RunUp.Started) return;
            RunUp.Step(dt, Model.Params);
            Vector3 forward = Quaternion.Euler(0, Model.Yaw, 0) * Vector3.forward;
            Vector3 pos = _runStart + forward * RunUp.Distance;
            Model.Position = pos;
            _rb.MovePosition(pos);
        }

        /// <summary>비행 중이 아닐 때 자세를 바꾼다 (예: 추락 후 쓰러진 모습)</summary>
        public void SetRestPose(Quaternion rotation)
        {
            if (IsFlying) return;
            _rb.rotation = rotation;
            transform.rotation = rotation;
        }

        void UpdateAltitude()
        {
            Altitude = Physics.Raycast(Model.Position, Vector3.down, out var hit, 1000f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.distance
                : Model.Position.y;
        }
    }
}
