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

        [Header("각도 (deg, + = 위로)")]
        public float glideAngle = 5f;
        public float foldedAngle = 70f;
        public float flapUpAngle = 55f;
        public float flapDownAngle = -40f;
        public float flapDuration = 0.35f;

        float _flapT = 1f;
        float _flapStrength = 1f;
        float _current;

        void OnEnable()
        {
            if (controller) controller.Flapped += OnFlapped;
        }

        void OnDisable()
        {
            if (controller) controller.Flapped -= OnFlapped;
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
            _flapStrength = outcome == FlapOutcome.Weak ? 0.5f : outcome == FlapOutcome.Perfect ? 1.2f : 1f;
        }

        void Update()
        {
            float target;
            bool flying = controller && controller.IsFlying;
            if (!flying)
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
            else
            {
                target = glideAngle + Mathf.Sin(Time.time * 3f) * 3f; // 활공 중 살짝 흔들림
            }

            _current = Mathf.Lerp(_current, target, 12f * Time.deltaTime);
            if (leftWing) leftWing.localRotation = Quaternion.Euler(0, 0, _current);
            if (rightWing) rightWing.localRotation = Quaternion.Euler(0, 0, -_current);
            if (tail) tail.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 4f) * 6f, Mathf.Sin(Time.time * 2.3f) * 10f, 0);
        }
    }
}
