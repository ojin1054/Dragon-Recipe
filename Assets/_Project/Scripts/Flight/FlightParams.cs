using System;
using UnityEngine;

namespace DragonRecipe.Flight
{
    /// <summary>
    /// 성장 단계별 비행 수치. 순수 데이터라 EditMode 테스트에서 직접 만들어 쓸 수 있다.
    /// </summary>
    [Serializable]
    public class FlightParams
    {
        [Header("중력 / 양력")]
        [Tooltip("게임용 중력 가속도 (m/s²)")]
        public float gravity = 12f;
        [Tooltip("기준 속도에서 중력을 얼마나 상쇄하는지 (1 = 완전히 상쇄)")]
        public float liftCoefficient = 0.55f;
        [Tooltip("이 속도에서 양력 = liftCoefficient. 양력은 속도의 제곱에 비례")]
        public float liftReferenceSpeed = 14f;
        [Tooltip("수직 속도 감쇠 (클수록 낙하 종단속도가 작아짐)")]
        public float verticalDamping = 1.2f;
        public float maxSinkSpeed = 14f;
        public float maxRiseSpeed = 7f;

        [Header("속도")]
        [Tooltip("공기 저항 계수 (v² 비례)")]
        public float drag = 0.007f;
        public float minSpeed = 3f;
        [Tooltip("이 속도 아래로 떨어지면 실속: 양력이 급감하고 기수가 내려간다")]
        public float stallSpeed = 8f;
        public float maxSpeed = 40f;

        [Header("이륙 (힘 모으기)")]
        [Tooltip("힘을 모으지 않고 바로 뗐을 때의 출발 속도")]
        public float launchSpeed = 12f;
        [Tooltip("힘을 가득 모았을 때의 출발 속도")]
        public float launchMaxSpeed = 24f;
        [Tooltip("바로 뗐을 때 뛰어오르는 수직 속도")]
        public float launchRise = 2f;
        [Tooltip("가득 모았을 때 뛰어오르는 수직 속도 (maxRiseSpeed를 넘지 않음)")]
        public float launchMaxRise = 7f;
        [Tooltip("가득 모았을 때 출발 기수 각도 (deg). 속도를 높이로 바꿔 준다")]
        public float launchMaxPitch = 20f;
        [Tooltip("게이지가 0 → 가득까지 차는 시간 (초). 계속 누르고 있으면 다시 줄어든다")]
        public float launchChargeTime = 0.9f;
        [Tooltip("게이지가 (1 - 이 값) 이상일 때 떼면 '완벽한 도약'")]
        public float launchPerfectWindow = 0.12f;
        [Tooltip("완벽한 도약일 때 출발 속도/상승 배율")]
        public float launchPerfectBonus = 1.2f;

        [Header("조종")]
        public float maxPitchUp = 35f;
        public float maxPitchDown = 70f;
        [Tooltip("스틱을 끝까지 밀었을 때 기수가 돌아가는 속도 (deg/s). 손을 떼면 그 각도를 유지")]
        public float pitchResponse = 60f;
        [Tooltip("최대 기울기일 때의 선회 속도 (deg/s)")]
        public float turnRate = 70f;
        public float maxBank = 45f;
        [Tooltip("스틱을 끝까지 밀었을 때 몸이 기울어지는 속도 (deg/s). 손을 떼면 그 기울기를 유지")]
        public float bankResponse = 90f;

        [Header("날갯짓")]
        [Tooltip("이 단계는 플레이어의 날갯짓 개입 없이는 고도를 유지할 수 없음 (UI 안내용)")]
        public bool needsFlapping = true;
        [Tooltip("날갯짓 한 번에 더해지는 상승 속도 (m/s)")]
        public float flapLift = 4f;
        [Tooltip("날갯짓 한 번에 더해지는 전진 속도 (m/s)")]
        public float flapThrust = 1.5f;
        public float flapStaminaCost = 8f;
        public float maxStamina = 100f;
        [Tooltip("초당 스태미나 회복량")]
        public float staminaRegen = 6f;
        [Tooltip("마지막 날갯짓 후 회복이 시작되기까지의 시간")]
        public float staminaRegenDelay = 0.5f;

        [Header("날갯짓 리듬")]
        [Tooltip("가장 효율적인 날갯짓 간격 (초)")]
        public float idealFlapInterval = 0.6f;
        [Tooltip("이상적 간격 ± 이 값 안이면 '완벽한 날갯짓'")]
        public float perfectWindow = 0.2f;
        [Tooltip("이 간격보다 빨리 누르면 연타로 판정")]
        public float spamInterval = 0.25f;
        public float perfectBonus = 1.3f;
        public float spamPenalty = 0.35f;

        [Header("착지 / 충돌")]
        [Tooltip("이 하강 속도 이하로 지면에 닿으면 안전 착지")]
        public float safeLandingSink = 4.5f;
        [Tooltip("이 수평 속도 이하로 지면에 닿으면 안전 착지")]
        public float safeLandingSpeed = 17f;

        public FlightParams Clone() => (FlightParams)MemberwiseClone();
    }
}
