using DragonRecipe.Flight;
using UnityEngine;

namespace DragonRecipe.CameraRig
{
    /// <summary>
    /// 3인칭 추적 카메라. 속도가 빠를수록 시야각이 넓어지고 살짝 뒤로 빠져 급강하 속도감을 준다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class FollowCamera : MonoBehaviour
    {
        public DragonFlightController target;
        public float distance = 9f;
        public float height = 3.5f;
        public float lookAhead = 6f;
        public float positionSmoothing = 5f;
        public float rotationSmoothing = 8f;

        [Header("속도감")]
        public float baseFov = 60f;
        public float maxFov = 82f;
        public float fovSpeedMin = 15f;
        public float fovSpeedMax = 38f;
        public float extraDistanceAtMaxSpeed = 3f;
        [Tooltip("추가 속도(연속 사냥 보상)가 붙어 있는 동안 더 넓어지는 시야각")]
        public float boostFov = 8f;

        Camera _cam;
        Vector3 _rigPos;
        Quaternion _rigRot = Quaternion.identity;
        bool _hasRig;
        float _fov;

        /// <summary>흔들림 (카메라 기준 로컬 위치). <see cref="SpeedJuice"/>가 매 프레임 정한다</summary>
        public Vector3 ShakeOffset { get; set; }
        /// <summary>흔들림 기울기 (deg)</summary>
        public float ShakeRoll { get; set; }
        /// <summary>시야각에 더하는 값 (속도 단계를 넘는 순간의 펀치 등)</summary>
        public float ExtraFov { get; set; }

        void Awake()
        {
            _cam = GetComponent<Camera>();
            if (!GetComponent<SpeedJuice>()) gameObject.AddComponent<SpeedJuice>();
        }

        void Start() => Snap();

        public void Snap()
        {
            if (!target) return;
            ComputeGoal(out var pos, out var rot, 0f);
            _rigPos = pos;
            _rigRot = rot;
            _hasRig = true;
            transform.SetPositionAndRotation(pos, rot);
        }

        void LateUpdate()
        {
            if (!target) return;
            float speed = target.IsFlying ? target.Model.Velocity.magnitude : 0f;
            float t = Mathf.InverseLerp(fovSpeedMin, fovSpeedMax, speed);

            ComputeGoal(out var pos, out var rot, t);
            float k = 1f - Mathf.Exp(-positionSmoothing * Time.deltaTime);
            float kr = 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime);
            if (!_hasRig)
            {
                _rigPos = transform.position;
                _rigRot = transform.rotation;
                _hasRig = true;
            }
            // 흔들림은 따라가기 계산과 따로 더한다 (흔들림이 다음 프레임 위치에 쌓이지 않게)
            _rigPos = Vector3.Lerp(_rigPos, pos, k);
            _rigRot = Quaternion.Slerp(_rigRot, rot, kr);
            transform.SetPositionAndRotation(_rigPos + _rigRot * ShakeOffset, _rigRot * Quaternion.Euler(0f, 0f, ShakeRoll));
            float boost = target.IsFlying && target.Model.IsBoosting ? boostFov : 0f;
            _fov = Mathf.Lerp(_fov <= 0f ? _cam.fieldOfView : _fov, Mathf.Lerp(baseFov, maxFov, t) + boost, k);
            _cam.fieldOfView = _fov + ExtraFov;
        }

        void ComputeGoal(out Vector3 pos, out Quaternion rot, float speedT)
        {
            Transform tr = target.transform;
            float yaw = target.Model != null ? target.Model.Yaw : tr.eulerAngles.y;
            Vector3 flatForward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            float scale = Mathf.Max(0.1f, tr.localScale.x); // 성장해서 커지면 카메라도 물러남
            float dist = (distance + extraDistanceAtMaxSpeed * speedT) * scale;

            // 공중제비: 기수가 높이 들릴수록 카메라도 같이 돌아 드래곤 등 뒤를 계속 따라간다 (뒤집히면 화면도 뒤집힘)
            Vector3 back = flatForward, up = Vector3.up;
            float loop = LoopFollow();
            if (loop > 0f)
            {
                var body = Quaternion.Euler(-target.Model.Pitch, yaw, 0f);
                back = Vector3.Slerp(flatForward, body * Vector3.forward, loop).normalized;
                up = Vector3.Slerp(Vector3.up, body * Vector3.up, loop).normalized;
            }

            pos = tr.position - back * dist + up * (height * scale);
            // 카메라가 땅속으로 들어가지 않게
            if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out var hit, 100f, ~0, QueryTriggerInteraction.Ignore)
                && hit.point.y < tr.position.y)
                pos.y = Mathf.Max(pos.y, hit.point.y + 1f);

            Vector3 look = tr.position + (target.IsFlying ? target.Model.Velocity.normalized : flatForward) * lookAhead;
            rot = Quaternion.LookRotation(look - pos, up);
        }

        /// <summary>
        /// 공중제비를 따라 도는 정도 (0 = 평소 수평 추적, 1 = 기체를 그대로 따라감).
        /// 평소 오르내리는 각도(maxPitchUp까지, 급강하)에서는 0이라 예전 느낌 그대로이고,
        /// 그보다 들어 올려 수직에 가까워질수록 1로, 뒤집혀 있는 동안은 계속 1이다.
        /// </summary>
        float LoopFollow()
        {
            if (!target.IsFlying || target.Model == null || !target.Model.Params.allowLoops) return 0f;
            float pitch = target.Model.Pitch;
            if (target.Model.IsInverted) return 1f;
            if (pitch >= 0f) return Mathf.InverseLerp(target.Model.Params.maxPitchUp, 80f, pitch);
            // 뒤집힌 채 내려오는 공중제비 후반: 수직 아래(-90)에서 평소 급강하 한계까지 풀어 준다
            return Mathf.InverseLerp(-target.Model.Params.maxPitchDown, -90f, pitch);
        }
    }
}
