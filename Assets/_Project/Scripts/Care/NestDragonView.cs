using DragonRecipe.Flight;
using UnityEngine;

namespace DragonRecipe.Care
{
    /// <summary>둥지에서 쉬는 드래곤: 단계에 맞는 크기, 숨쉬기, 배고프면 축 처짐, 먹이를 받으면 와구와구.</summary>
    public class NestDragonView : MonoBehaviour
    {
        public Transform model;
        public float breathSpeed = 1.6f;
        public float breathAmount = 0.03f;
        [Tooltip("크기가 바뀔 때 부드럽게 변하는 속도")]
        public float growSmoothing = 3f;
        [Tooltip("먹이를 받았을 때 와구와구 하는 시간 (초)")]
        public float chompDuration = 0.8f;

        float _scale = -1f;
        float _chomp; // 1 → 0

        void OnEnable()
        {
            DragonCare.Fed += OnFed;
            DragonCare.Hatched += OnHatched;
        }

        void OnDisable()
        {
            DragonCare.Fed -= OnFed;
            DragonCare.Hatched -= OnHatched;
        }

        void OnFed(FeedOutcome o) => _chomp = 1f;

        /// <summary>새 알: 작은 크기에서 다시 시작 (부드럽게 줄어들지 않고 바로)</summary>
        void OnHatched() => _scale = -1f;

        void Update()
        {
            if (!DragonCare.IsLoaded) return;
            var data = DragonCare.CurrentStageData;
            float target = data ? data.bodyScale : 1f;
            _scale = _scale < 0 ? target : Mathf.Lerp(_scale, target, 1f - Mathf.Exp(-growSmoothing * Time.deltaTime));
            transform.localScale = Vector3.one * _scale;

            if (!model) return;
            bool starving = CareLogic.IsStarving(DragonCare.State);
            float breath = 1f + Mathf.Sin(Time.time * (starving ? breathSpeed * 0.5f : breathSpeed)) * breathAmount;

            // 굶주리면 고개를 숙인 듯 앞으로 기울어짐
            float droop = starving ? 18f : CareLogic.IsHungry(DragonCare.State, DragonCare.Config.care) ? 8f : 0f;
            if (_chomp > 0f)
            {
                _chomp = Mathf.Max(0f, _chomp - Time.deltaTime / Mathf.Max(0.05f, chompDuration));
                droop = 14f + Mathf.Sin((1f - _chomp) * Mathf.PI * 6f) * 12f; // 고개를 콕콕
                breath += Mathf.Sin(_chomp * Mathf.PI) * 0.08f;
            }
            model.localScale = new Vector3(1f, breath, 1f);
            model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(droop, 0, 0), (_chomp > 0f ? 20f : 3f) * Time.deltaTime);
        }
    }
}
