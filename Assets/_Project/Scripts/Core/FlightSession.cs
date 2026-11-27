using System;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using UnityEngine;

namespace DragonRecipe.Core
{
    /// <summary>
    /// 비행 한 판의 흐름: 둥지 대기 → 도움닫기(날갯짓 연타로 달리고, 도약 구간에서 웅크리기 버튼을 꾹 눌렀다가 떼면 도약)
    /// → 비행 → 착지/추락 → 결과.
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
        [Tooltip("이륙 지점 (둥지). 이 오브젝트의 앞 방향으로 달려 나간다")]
        public Transform nest;
        [Tooltip("절벽 끝 (도약 지점). 비우면 둥지 바로 앞이 도약 구간")]
        public Transform launchEdge;
        [Tooltip("이 고도 아래에서 낚아채면 아슬아슬 보너스 계산")]
        public float nearMissAltitude = 3.5f;
        [Tooltip("아슬아슬 최대 보너스 (1 = 포만도 최대 2배)")]
        public float nearMissMaxBonus = 1f;
        public HuntSettings hunt = new HuntSettings();

        public State Current { get; private set; } = State.Ready;
        public FlightResult Result { get; private set; } = new FlightResult();

        public event Action<State> StateChanged;
        /// <summary>화면 가운데 잠깐 뜨는 피드백 문구</summary>
        public event Action<string, Color> Feedback;
        /// <summary>발톱에 매단 먹이가 바뀜 (잡기 / 먹기 / 떨어뜨리기)</summary>
        public event Action CarryChanged;
        /// <summary>먹이를 잡음 (연속 사냥 횟수, 1 = 단독)</summary>
        public event Action<int> Caught;
        /// <summary>매달린 먹이가 버둥거림</summary>
        public event Action Struggled;

        /// <summary>지금 이어지고 있는 연속 사냥 (0 = 없음)</summary>
        public int CurrentCombo { get; private set; }
        /// <summary>마지막으로 먹이를 잡은 시각 (Time.time)</summary>
        public float LastCatchTime { get; private set; } = -999f;

        float _nextStruggle;

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
            if (GamePause.IsPaused) return;

            switch (Current)
            {
                case State.Ready:
                    input?.ConsumeEat();
                    UpdateRunUp();
                    break;
                case State.Flying:
                    Result.airtime += Time.deltaTime;
                    Result.maxSpeed = Mathf.Max(Result.maxSpeed, dragon.Model.Velocity.magnitude);
                    if (CurrentCombo > 0 && Time.time - LastCatchTime > hunt.comboWindow) CurrentCombo = 0;
                    if (input && input.ConsumeEat()) EatInAir();
                    UpdateStruggle();
                    break;
                case State.Ended:
                    input?.ConsumeFlap();
                    input?.ConsumeEat();
                    break;
            }
        }

        /// <summary>둥지로 돌아가 다음 비행 준비 (결과 화면의 "다시 날기" 버튼)</summary>
        public void ResetFlight()
        {
            Result = new FlightResult();
            CurrentCombo = 0;
            LastCatchTime = -999f;
            if (snatchZone) snatchZone.Armed = false;
            if (nest) dragon.Park(nest.position, nest.eulerAngles.y, RunUpDistance());
            if (spawner) spawner.ResetAll();
            SetState(State.Ready);
            CarryChanged?.Invoke();
        }

        /// <summary>둥지에서 절벽 끝까지 (둥지 앞 방향 기준) 거리</summary>
        float RunUpDistance()
        {
            if (!nest || !launchEdge) return 0f;
            return Mathf.Max(0f, Vector3.Dot(launchEdge.position - nest.position, nest.forward));
        }

        /// <summary>날갯짓 연타 = 달리기, 도약 구간에서 웅크리기 버튼 누르기 = 웅크리기, 떼기 = 도약</summary>
        void UpdateRunUp()
        {
            var run = dragon.RunUp;
            if (run == null) return;
            if (run.Overran)
            {
                Launch(); // 떼지 못하고 절벽을 넘어감
                return;
            }

            var p = dragon.Model.Params;
            if (!input) return;
            if (!run.IsCrouching)
            {
                if (input.ConsumeFlap()) run.Tap(p);
                // 구간 밖에서 미리 누르고 있으면 구간에 들어서는 순간 웅크린다
                if (input.CrouchHeld) run.TryCrouch(p);
            }
            else
            {
                input.ConsumeFlap(); // 웅크린 뒤의 날갯짓은 무시
                if (!input.CrouchHeld) Launch(); // 웅크리기 버튼을 떼는 순간 도약
            }
        }

        void Launch()
        {
            var plan = dragon.Launch();
            if (snatchZone) snatchZone.Armed = true;
            SetState(State.Flying);

            switch (plan.grade)
            {
                case LaunchGrade.Perfect: Feedback?.Invoke("완벽한 도약!", Good); break;
                case LaunchGrade.Good: Feedback?.Invoke("좋은 도약!", Info); break;
                case LaunchGrade.Stumble: Feedback?.Invoke("앗, 발을 헛디뎠어요!", Bad); break;
                default:
                    Feedback?.Invoke(plan.depth < 1f ? "덜 웅크렸어요" : "너무 일찍 뛰었어요", Bad);
                    break;
            }
        }

        void OnFlapped(FlapOutcome outcome)
        {
            switch (outcome)
            {
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

            // 연속 사냥: 짧은 간격으로 이어 잡을수록 포만도 배율이 붙는다
            CurrentCombo = Time.time - LastCatchTime <= hunt.comboWindow ? CurrentCombo + 1 : 1;
            LastCatchTime = Time.time;
            Result.maxCombo = Mathf.Max(Result.maxCombo, CurrentCombo);
            bool rare = prey.IsRare;

            Result.caught.Add(new CaughtFood
            {
                food = prey.Data,
                satiety = prey.Data.satiety * mult * (rare ? hunt.rareSatietyMultiplier : 1f),
                nearMiss = nearMiss,
                rare = rare,
                combo = CurrentCombo
            });
            if (nearMiss) Result.nearMissCount++;
            spawner.OnCaught(prey);
            UpdatePayload();
            // 연속 사냥 보상: 스태미나가 차오르고 잠깐 속도가 붙어 다음 먹이로 이어 가기 쉬워진다
            if (CurrentCombo >= 2)
            {
                dragon.Model.Eat(HuntRules.ComboStamina(CurrentCombo, hunt));
                dragon.Model.Boost(HuntRules.ComboBoost(CurrentCombo, hunt), hunt.comboBoostTime);
            }
            Caught?.Invoke(CurrentCombo);
            CarryChanged?.Invoke();

            _nextStruggle = Time.time + hunt.struggleInterval;

            Feedback?.Invoke(nearMiss
                    ? $"아슬아슬! {prey.Data.displayName} 낚아챔 x{mult:0.0}"
                    : $"{prey.Data.displayName} 낚아챔",
                nearMiss ? Good : Info);

            // 버둥거리는 먹이를 어설프게(느리게 / 높게) 잡으면 걷어차인다
            if (prey.Data.struggles && !nearMiss) dragon.ApplyDamage(hunt.kickDamage);
        }

        void OnImpacted(ImpactType type, Vector3 velocity)
        {
            if (Current != State.Flying) return;

            if (snatchZone) snatchZone.Armed = false;
            Result.perfectFlapCount = dragon.Model.PerfectFlapCount;
            Result.reason = type == ImpactType.Landed
                ? FlightEndReason.Landed
                : dragon.transform.position.y < dragon.killY ? FlightEndReason.OutOfBounds : FlightEndReason.Crashed;
            if (Result.Injured) DropCarried();

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

        /// <summary>마지막에 잡은 먹이를 공중에서 먹는다: 스태미나 회복, 대신 잠깐 조종이 굳는다</summary>
        public bool EatInAir()
        {
            if (Current != State.Flying || dragon.IsEating) return false;
            for (int i = Result.caught.Count - 1; i >= 0; i--)
            {
                var c = Result.caught[i];
                if (!c.Carried) continue;
                c.eaten = true;
                Result.caught[i] = c;
                dragon.BeginEat(hunt.eatFreezeTime);
                dragon.Model.Eat(c.satiety * hunt.eatStaminaPerSatiety);
                UpdatePayload();
                CarryChanged?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>추락: 들고 있던 먹이 일부를 떨어뜨린다 (나중에 잡은 것부터)</summary>
        void DropCarried()
        {
            int drop = HuntRules.DropCount(Result.CarriedCount, hunt.crashDropFraction);
            for (int i = Result.caught.Count - 1; i >= 0 && drop > 0; i--)
            {
                var c = Result.caught[i];
                if (!c.Carried) continue;
                c.dropped = true;
                Result.caught[i] = c;
                drop--;
            }
            UpdatePayload();
            CarryChanged?.Invoke();
        }

        void UpdatePayload()
        {
            if (dragon && dragon.Model != null) dragon.Model.Payload = HuntRules.Payload(Result.caught, hunt);
        }

        /// <summary>버둥거리는 먹이를 매달고 있으면 가끔 몸이 홱 흔들린다</summary>
        void UpdateStruggle()
        {
            int strugglers = 0;
            foreach (var c in Result.caught)
                if (c.Carried && c.food && c.food.struggles) strugglers++;
            if (strugglers == 0 || Time.time < _nextStruggle) return;

            _nextStruggle = Time.time + hunt.struggleInterval * UnityEngine.Random.Range(0.7f, 1.3f);
            float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            float k = Mathf.Sqrt(strugglers);
            dragon.Model.Jolt(side * hunt.struggleBank * k, UnityEngine.Random.Range(-1f, 1f) * hunt.strugglePitch * k, hunt.struggleStamina * k);
            Struggled?.Invoke();
        }

        void SetState(State s)
        {
            Current = s;
            StateChanged?.Invoke(s);
        }
    }
}
