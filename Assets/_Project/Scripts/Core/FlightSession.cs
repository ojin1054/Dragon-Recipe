using System;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using UnityEngine;

namespace DragonRecipe.Core
{
    /// <summary>
    /// 비행 한 판의 흐름: 둥지 대기 → (날갯짓으로) 이륙 → 비행 → 착지/추락 → 결과.
    /// </summary>
    public class FlightSession : MonoBehaviour
    {
        public enum State
        {
            Ready,
            Flying,
            Ended
        }

        public DragonFlightController dragon;
        public DragonInput input;
        public SnatchZone snatchZone;
        public PreySpawner spawner;
        [Tooltip("이륙 지점 (둥지). 이 오브젝트의 앞 방향으로 출발")]
        public Transform nest;
        [Tooltip("이 고도 아래에서 낚아채면 아슬아슬 보너스 계산")]
        public float nearMissAltitude = 3.5f;
        [Tooltip("아슬아슬 최대 보너스 (1 = 포만도 최대 2배)")]
        public float nearMissMaxBonus = 1f;

        public State Current { get; private set; } = State.Ready;
        public FlightResult Result { get; private set; } = new FlightResult();

        public event Action<State> StateChanged;
        /// <summary>화면 가운데 잠깐 뜨는 피드백 문구</summary>
        public event Action<string, Color> Feedback;

        static readonly Color Good = new Color(1f, 0.85f, 0.3f);
        static readonly Color Bad = new Color(1f, 0.45f, 0.4f);
        static readonly Color Info = new Color(0.85f, 0.95f, 1f);

        void OnEnable()
        {
            if (dragon)
            {
                dragon.Flapped += OnFlapped;
                dragon.Impacted += OnImpacted;
            }
            if (snatchZone) snatchZone.Snatched += OnSnatched;
        }

        void OnDisable()
        {
            if (dragon)
            {
                dragon.Flapped -= OnFlapped;
                dragon.Impacted -= OnImpacted;
            }
            if (snatchZone) snatchZone.Snatched -= OnSnatched;
        }

        void Start() => ResetFlight();

        void Update()
        {
            switch (Current)
            {
                case State.Ready:
                    if (input && input.ConsumeFlap()) Launch();
                    break;
                case State.Flying:
                    Result.airtime += Time.deltaTime;
                    Result.maxSpeed = Mathf.Max(Result.maxSpeed, dragon.Model.Velocity.magnitude);
                    break;
                case State.Ended:
                    input?.ConsumeFlap();
                    break;
            }
        }

        /// <summary>둥지로 돌아가 다음 비행 준비 (결과 화면의 "다시 날기" 버튼)</summary>
        public void ResetFlight()
        {
            Result = new FlightResult();
            if (snatchZone) snatchZone.Armed = false;
            if (nest) dragon.Park(nest.position, nest.eulerAngles.y);
            if (spawner) spawner.ResetAll();
            SetState(State.Ready);
        }

        void Launch()
        {
            dragon.Launch();
            if (snatchZone) snatchZone.Armed = true;
            SetState(State.Flying);
            Feedback?.Invoke(dragon.Model.Params.needsFlapping ? "리듬에 맞춰 날갯짓!" : "날아올라!", Info);
        }

        void OnFlapped(FlapOutcome outcome)
        {
            switch (outcome)
            {
                case FlapOutcome.Perfect: Feedback?.Invoke("완벽한 날갯짓!", Good); break;
                case FlapOutcome.Weak: Feedback?.Invoke("너무 서둘러요", Bad); break;
                case FlapOutcome.Failed: Feedback?.Invoke("지쳤어요… 활공해서 쉬세요", Bad); break;
            }
        }

        void OnSnatched(Prey prey)
        {
            if (Current != State.Flying) return;

            float speed = dragon.Model.Velocity.magnitude;
            float factor = FlightRules.NearMissFactor(dragon.Altitude, speed, dragon.Model.Params, nearMissAltitude);
            float mult = FlightRules.SnatchMultiplier(factor, nearMissMaxBonus);
            bool nearMiss = factor >= FlightRules.NearMissThreshold;

            Result.caught.Add(new CaughtFood { food = prey.Data, satiety = prey.Data.satiety * mult, nearMiss = nearMiss });
            if (nearMiss) Result.nearMissCount++;
            spawner.OnCaught(prey);

            Feedback?.Invoke(nearMiss
                    ? $"아슬아슬! {prey.Data.displayName} 낚아챔 x{mult:0.0}"
                    : $"{prey.Data.displayName} 낚아챔",
                nearMiss ? Good : Info);
        }

        void OnImpacted(ImpactType type, Vector3 velocity)
        {
            if (Current != State.Flying) return;

            if (snatchZone) snatchZone.Armed = false;
            Result.perfectFlapCount = dragon.Model.PerfectFlapCount;
            Result.reason = type == ImpactType.Landed
                ? FlightEndReason.Landed
                : dragon.transform.position.y < dragon.killY ? FlightEndReason.OutOfBounds : FlightEndReason.Crashed;

            if (Result.Injured)
            {
                // 쓰러진 모습
                dragon.SetRestPose(Quaternion.Euler(0, dragon.Model.Yaw, 80f));
                Feedback?.Invoke("쾅! 드래곤이 다쳤어요", Bad);
            }
            else
            {
                Feedback?.Invoke("사뿐히 착지!", Good);
            }

            SetState(State.Ended);
            GameEvents.RaiseFlightEnded(Result);
        }

        void SetState(State s)
        {
            Current = s;
            StateChanged?.Invoke(s);
        }
    }
}
