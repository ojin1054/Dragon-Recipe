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
        public bool IsFlying { get; private set; }
        /// <summary>바로 아래 지면까지 거리 (m)</summary>
        public float Altitude { get; private set; }

        /// <summary>이륙 전 도움닫기 (둥지 → 절벽 끝). 비행 중에는 null</summary>
        public RunUpModel RunUp { get; private set; }

        public event Action<FlapOutcome> Flapped;
        public event Action<LaunchPlan> Launched;
        /// <summary>(종류, 충돌 시 속도)</summary>
        public event Action<ImpactType, Vector3> Impacted;

        Rigidbody _rb;
        bool _flapRequested;
        Vector3 _runStart;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            // SetStage가 먼저 불렸으면 (FlightCareBridge) 그 값을 유지
            if (Model == null) Model = new FlightModel(stage ? stage.flight : new FlightParams());
        }

        /// <param name="flightOverride">굶주림 등이 반영된 수치. null이면 단계 기본값</param>
        public void SetStage(DragonStageData data, FlightParams flightOverride = null)
        {
            stage = data;
            float yaw = Model != null ? Model.Yaw : transform.eulerAngles.y;
            Model = new FlightModel(flightOverride ?? data.flight);
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
            IsFlying = true;
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

            if (_flapRequested)
            {
                _flapRequested = false;
                Flapped?.Invoke(Model.TryFlap());
            }

            float dt = Time.fixedDeltaTime;
            Vector3 from = Model.Position;
            Model.Step(dt, input ? input.Move : Vector2.zero);
            Vector3 to = Model.Position;

            Vector3 delta = to - from;
            float dist = delta.magnitude;
            if (dist > 0.0001f &&
                Physics.SphereCast(from, collisionRadius, delta / dist, out var hit, dist,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                Vector3 velocity = Model.Velocity;
                Model.Position = hit.point + hit.normal * collisionRadius;
                _rb.MovePosition(Model.Position);
                IsFlying = false;
                var type = FlightRules.ClassifyImpact(velocity, hit.normal, Model.Params);
                if (type == ImpactType.Landed)
                    _rb.MoveRotation(Quaternion.Euler(0, Model.Yaw, 0));
                Impacted?.Invoke(type, velocity);
                return;
            }

            _rb.MovePosition(to);
            _rb.MoveRotation(Model.Rotation);
            UpdateAltitude();

            if (to.y < killY)
            {
                IsFlying = false;
                Impacted?.Invoke(ImpactType.Crashed, Model.Velocity);
            }
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
