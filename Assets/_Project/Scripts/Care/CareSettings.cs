using System;
using UnityEngine;

namespace DragonRecipe.Care
{
    /// <summary>허기 / 성장 수치. GameConfig 에셋에서 튜닝한다.</summary>
    [Serializable]
    public class CareSettings
    {
        [Header("허기")]
        public float maxHunger = 100f;
        public float startingHunger = 50f;
        [Tooltip("실제 시간 1시간당 줄어드는 포만도")]
        public float hungerDecayPerHour = 20f;
        [Tooltip("게임을 꺼 둔 동안 반영할 최대 시간. 이보다 오래 꺼 둬도 이만큼만 줄어든다")]
        public float offlineCapHours = 8f;
        [Tooltip("이 비율 아래면 '배고픔' 경고")]
        [Range(0f, 1f)] public float hungryThreshold = 0.3f;

        [Header("성장")]
        [Tooltip("어린 → 청소년에 필요한 누적 경험치")]
        public float xpToJuvenile = 150f;
        [Tooltip("청소년 → 성체에 필요한 누적 경험치")]
        public float xpToAdult = 450f;

        [Header("굶주림 패널티")]
        [Tooltip("굶주린 상태에서 먹으면, 처음 이만큼의 포만도는 회복에만 쓰이고 경험치가 되지 않는다")]
        public float starvationRecovery = 20f;
        [Tooltip("굶주린 채 비행하면 최대 스태미나 배율")]
        [Range(0.1f, 1f)] public float starvingStaminaMultiplier = 0.6f;
        [Tooltip("굶주린 채 비행하면 날갯짓 상승력 배율")]
        [Range(0.1f, 1f)] public float starvingFlapLiftMultiplier = 0.8f;
    }
}
