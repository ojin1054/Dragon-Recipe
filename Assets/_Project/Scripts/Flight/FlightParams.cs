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

        [Header("이륙: 달리기 (= 파워 → 출발 속도)")]
        [Tooltip("탭 한 번에 붙는 달리기 속도 (m/s)")]
        public float runTapAccel = 1.5f;
        public float runMaxSpeed = 11f;
        [Tooltip("탭을 멈추면 줄어드는 달리기 속도 (m/s²)")]
        public float runFriction = 3f;
        [Tooltip("달리기 파워 0일 때 출발 속도")]
        public float launchSpeed = 12f;
        [Tooltip("최고 속도로 달려 뛰었을 때 출발 속도")]
        public float launchMaxSpeed = 24f;

        [Header("이륙: 웅크리기 (= 높이)")]
        [Tooltip("절벽 끝에서 이 거리 안(도약 구간)에서만 웅크릴 수 있다 (m)")]
        public float jumpZoneLength = 10f;
        [Tooltip("절벽 끝까지 이 거리 안에서 떼면 위치 만점 (m)")]
        public float perfectJumpDistance = 2.5f;
        [Tooltip("이 시간 동안 웅크려야 끝까지 웅크린 것 (초)")]
        public float crouchFullTime = 0.45f;
        [Tooltip("웅크린 채 미끄러지며 줄어드는 달리기 속도 (m/s²)")]
        public float crouchDecel = 5f;
        [Tooltip("높이 점수가 이 이상이면 '좋은 도약'")]
        public float goodJumpThreshold = 0.6f;
        [Tooltip("높이 점수 0일 때 뛰어오르는 수직 속도")]
        public float launchRise = 2f;
        [Tooltip("높이 점수 1일 때 뛰어오르는 수직 속도 (maxRiseSpeed를 넘지 않음)")]
        public float launchMaxRise = 7f;
        [Tooltip("높이 점수 1일 때 출발 기수 각도 (deg). 속도를 높이로 바꿔 준다")]
        public float launchMaxPitch = 22f;
        [Tooltip("완벽한 도약일 때 기수 각도 배율")]
        public float launchPerfectBonus = 1.25f;
        [Tooltip("발을 헛디뎌 떨어질 때 기수 각도 (deg, 음수 = 아래)")]
        public float stumblePitch = -15f;

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

        [Header("먹이 매달기 (무게)")]
        [Tooltip("드래곤의 힘 (DragonStats.strength가 들어감). 매단 동물 힘의 합이 이만큼이면 '무게 1배'")]
        public float carryStrength = 20f;
        [Tooltip("무게 1배일 때 잃는 양력 비율 (최대 80%)")]
        public float payloadLiftPenalty = 0.3f;
        [Tooltip("무게 1배일 때 잃는 날갯짓 상승력 비율 (최대 70%)")]
        public float payloadFlapPenalty = 0.25f;
        [Tooltip("무게 1배일 때 둔해지는 선회·기울기 비율 (최대 60%)")]
        public float payloadTurnPenalty = 0.3f;

        [Header("체력")]
        [Tooltip("비행을 시작할 때 체력. 0이 되면 추락(부상)으로 끝난다")]
        public float maxHealth = 100f;
        [Tooltip("땅에 닿을 때마다 받는 기본 피해")]
        public float touchDamage = 2f;
        [Tooltip("땅으로 파고드는 속도 1 m/s당 추가 피해. 가파르고 빠르게 닿을수록 커진다")]
        public float impactDamagePerSpeed = 6f;
        [Tooltip("땅으로 파고드는 속도가 이 이하면 추가 피해 없음 (m/s)")]
        public float impactFreeSpeed = 1f;
        [Tooltip("땅에서 미끄러지는 동안 초당 피해")]
        public float skidDamagePerSecond = 3f;

        [Header("착지 / 충돌")]
        [Tooltip("이 하강 속도 이하로 지면에 닿으면 끝나지 않고 미끄러진다 (넘으면 추락)")]
        public float safeLandingSink = 4.5f;
        [Tooltip("이 수평 속도 이하로 지면에 닿으면 안전 착지")]
        public float safeLandingSpeed = 17f;
        [Tooltip("지면에 닿은 채 이 시간이 지나면 착지로 끝난다. 그 전에 날아오르면 계속 비행 (초)")]
        public float touchdownGrace = 1f;
        [Tooltip("지면에서 미끄러질 때 줄어드는 속도 (m/s²)")]
        public float groundFriction = 6f;
        [Tooltip("지면에 닿는 순간 잃는 속도 비율의 최대값 (닿는 각도가 groundImpactMaxAngle 이상일 때)")]
        public float groundImpactLoss = 0.7f;
        [Tooltip("이 각도(deg) 이상으로 내리꽂듯 닿으면 감속이 최대. 스치듯 얕게 닿을수록 덜 감속")]
        public float groundImpactMaxAngle = 20f;

        public FlightParams Clone() => (FlightParams)MemberwiseClone();
    }
}
