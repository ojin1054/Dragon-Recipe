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

        Camera _cam;

        void Awake() => _cam = GetComponent<Camera>();

        void Start() => Snap();

        public void Snap()
        {
            if (!target) return;
            ComputeGoal(out var pos, out var rot, 0f);
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
            transform.position = Vector3.Lerp(transform.position, pos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, kr);
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, Mathf.Lerp(baseFov, maxFov, t), k);
        }

        void ComputeGoal(out Vector3 pos, out Quaternion rot, float speedT)
        {
            Transform tr = target.transform;
            float yaw = target.Model != null ? target.Model.Yaw : tr.eulerAngles.y;
            Vector3 flatForward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            float scale = Mathf.Max(0.1f, tr.localScale.x); // 성장해서 커지면 카메라도 물러남
            float dist = (distance + extraDistanceAtMaxSpeed * speedT) * scale;

            pos = tr.position - flatForward * dist + Vector3.up * (height * scale);
            // 카메라가 땅속으로 들어가지 않게
            if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out var hit, 100f, ~0, QueryTriggerInteraction.Ignore)
                && hit.point.y < tr.position.y)
                pos.y = Mathf.Max(pos.y, hit.point.y + 1f);

            Vector3 look = tr.position + (target.IsFlying ? target.Model.Velocity.normalized : flatForward) * lookAhead;
            rot = Quaternion.LookRotation(look - pos, Vector3.up);
        }
    }
}
