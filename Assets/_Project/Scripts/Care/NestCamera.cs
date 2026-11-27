using UnityEngine;

namespace DragonRecipe.Care
{
    /// <summary>둥지 카메라: 평소에는 드래곤을, 농장을 열면 우리 쪽을 부드럽게 비춘다</summary>
    public class NestCamera : MonoBehaviour
    {
        [Tooltip("농장을 열었을 때 카메라 자리 (위치 + 방향)")]
        public Transform farmView;
        [Tooltip("자리를 옮기는 데 걸리는 대략적인 시간 (초)")]
        public float moveTime = 0.5f;

        Vector3 _homePos;
        Quaternion _homeRot;
        bool _atFarm;
        Vector3 _vel;

        void Awake()
        {
            _homePos = transform.position;
            _homeRot = transform.rotation;
        }

        public void ShowFarm(bool on) => _atFarm = on && farmView;

        void LateUpdate()
        {
            var pos = _atFarm ? farmView.position : _homePos;
            var rot = _atFarm ? farmView.rotation : _homeRot;
            transform.position = Vector3.SmoothDamp(transform.position, pos, ref _vel, moveTime, Mathf.Infinity, Time.unscaledDeltaTime);
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 4f / Mathf.Max(0.05f, moveTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, k);
        }
    }
}
