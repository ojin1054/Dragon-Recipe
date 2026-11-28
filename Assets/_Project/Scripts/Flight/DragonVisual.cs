using UnityEngine;

namespace DragonRecipe.Flight
{
    /// <summary>프리미티브 드래곤의 날개 애니메이션 (날갯짓 / 활공 / 접기).</summary>
    public class DragonVisual : MonoBehaviour
    {
        public DragonFlightController controller;
        public Transform leftWing;
        public Transform rightWing;
        public Transform tail;
        [Tooltip("달리기 흔들림/웅크리기에 쓰는 몸통 (비우면 자식 'Model')")]
        public Transform body;

        [Header("각도 (deg, + = 위로)")]
        public float glideAngle = 5f;
        public float foldedAngle = 70f;
        public float flapUpAngle = 55f;
        public float flapDownAngle = -40f;
        public float flapDuration = 0.35f;

        float _flapT = 1f;
        float _flapStrength = 1f;
        float _current;
        Vector3 _bodyBase;
        float _crouch;
        float _chew;

        void Awake()
        {
            if (!body) body = transform.Find("Model");
            if (body) _bodyBase = body.localPosition;
        }

        void OnEnable()
        {
            if (!controller) return;
            controller.Flapped += OnFlapped;
            controller.Launched += OnLaunched;
        }

        void OnDisable()
        {
            if (!controller) return;
            controller.Flapped -= OnFlapped;
            controller.Launched -= OnLaunched;
        }

        void OnLaunched(LaunchPlan plan)
        {
            _flapT = 0f;
            // 높이 뛸수록 크게 내려친다
            _flapStrength = plan.grade == LaunchGrade.Perfect ? 1.5f
                : plan.grade == LaunchGrade.Stumble ? 0.4f
                : Mathf.Lerp(0.8f, 1.3f, plan.height);
        }

        void OnFlapped(FlapOutcome outcome)
        {
            if (outcome == FlapOutcome.Failed)
            {
                _flapT = 0f;
                _flapStrength = 0.25f; // 힘없는 날갯짓
                return;
            }
            _flapT = 0f;
            _flapStrength = outcome == FlapOutcome.Perfect ? 1.2f : 1f;
        }

        void Update()
        {
            float target;
            bool flying = controller && controller.IsFlying;
            var run = !flying && controller ? controller.RunUp : null;
            var p = controller ? controller.Model?.Params : null;
            float bob = 0f;
            float crouchTarget = 0f;
            if (run != null && p != null && run.IsCrouching)
            {
                // 웅크릴수록 몸을 낮추고 날개를 치켜든다. 끝까지 웅크리면 부르르 떨림
                float d = run.CrouchDepth(p);
                crouchTarget = d;
                target = Mathf.Lerp(foldedAngle, flapUpAngle + 15f, d);
                if (d >= 1f) target += Mathf.Sin(Time.time * 60f) * 4f;
            }
            else if (run != null && p != null && run.Started)
            {
                // 달리기: 빠를수록 크게 들썩이고 날개를 반쯤 펴서 퍼덕인다
                float f = run.PowerFraction(p);
                float stride = Time.time * Mathf.Lerp(6f, 16f, f);
                bob = Mathf.Abs(Mathf.Sin(stride)) * 0.18f * f;
                target = Mathf.Lerp(foldedAngle, glideAngle + 20f, f) + Mathf.Sin(stride * 2f) * 8f * f;
            }
            else if (!flying)
            {
                target = foldedAngle;
            }
            else if (_flapT < 1f)
            {
                _flapT += Time.deltaTime / flapDuration;
                float s = Mathf.Sin(Mathf.Clamp01(_flapT) * Mathf.PI * 2f); // 위 → 아래 → 원위치
                float amp = s >= 0 ? flapUpAngle - glideAngle : glideAngle - flapDownAngle;
                target = glideAngle + s * amp * _flapStrength;
                _current = target; // 날갯짓은 즉시 반영
            }
            else if (controller.Model != null && controller.Model.IsBoosting)
            {
                target = Mathf.Lerp(glideAngle, foldedAngle, 0.4f); // 추가 속도: 날개를 반쯤 접고 쏜살같이
            }
            else
            {
                target = glideAngle + Mathf.Sin(Time.time * 3f) * 3f; // 활공 중 살짝 흔들림
            }

            // 공중에서 먹는 중: 날개를 펴고 고개를 숙였다 들었다 (와구와구)
            bool eating = controller && controller.IsEating;
            if (eating) target = glideAngle + 10f;
            _chew = eating ? _chew + Time.deltaTime * 14f : 0f;

            _current = Mathf.Lerp(_current, target, 12f * Time.deltaTime);
            _crouch = Mathf.Lerp(_crouch, crouchTarget, 15f * Time.deltaTime);
            if (body && controller)
            {
                body.localPosition = _bodyBase + Vector3.up * (bob - 0.35f * _crouch);
                body.localRotation = eating ? Quaternion.Euler(12f + Mathf.Sin(_chew) * 10f, 0f, 0f)
                    : Quaternion.Slerp(body.localRotation, Quaternion.identity, 10f * Time.deltaTime);
            }
            if (leftWing) leftWing.localRotation = Quaternion.Euler(0, 0, _current);
            if (rightWing) rightWing.localRotation = Quaternion.Euler(0, 0, -_current);
            if (tail) tail.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 4f) * 6f, Mathf.Sin(Time.time * 2.3f) * 10f, 0);
        }
    }
}
