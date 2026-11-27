using DragonRecipe.Flight;
using UnityEngine;

namespace DragonRecipe.Care
{
    /// <summary>둥지에서 쉬는 드래곤: 단계에 맞는 크기, 숨쉬기, 배고프면 축 처짐.</summary>
    public class NestDragonView : MonoBehaviour
    {
        public Transform model;
        public float breathSpeed = 1.6f;
        public float breathAmount = 0.03f;
        [Tooltip("크기가 바뀔 때 부드럽게 변하는 속도")]
        public float growSmoothing = 3f;

        float _scale = -1f;

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
            model.localScale = new Vector3(1f, breath, 1f);
            // 굶주리면 고개를 숙인 듯 앞으로 기울어짐
            float droop = starving ? 18f : CareLogic.IsHungry(DragonCare.State, DragonCare.Config.care) ? 8f : 0f;
            model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(droop, 0, 0), 3f * Time.deltaTime);
        }
    }
}
