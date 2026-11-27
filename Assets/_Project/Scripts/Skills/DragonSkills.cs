using System;
using System.Collections.Generic;
using DragonRecipe.Care;
using DragonRecipe.Core;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DragonRecipe.Skills
{
    /// <summary>
    /// 드래곤 속성 스킬 두 칸.
    /// - 구체: 청소년부터. 가장 많이 먹은 성질의 구체를 한 발씩 (R / 패드 RB / 화면 버튼). 성체도 그대로 이어 받는다.
    /// - 브레스: 성체만. 누르고 있는 동안 모습에 맞는 브레스 (F / 패드 LB / 화면 버튼).
    /// 둘 다 스태미나를 쓴다.
    /// 씬에 없으면 비행 씬이 열릴 때 드래곤에 스스로 붙는다.
    /// </summary>
    public class DragonSkills : MonoBehaviour
    {
        public FlightSession session;
        public DragonFlightController dragon;
        public SkillBook book = new SkillBook();

        [Header("구체 자동 조준")]
        [Tooltip("이 각도(도) 안의 먹이·적을 겨눈다")]
        public float aimAngle = 28f;
        public float aimRange = 70f;
        [Tooltip("겨눌 대상이 없을 때 앞보다 이만큼 아래로 쏜다 (도)")]
        public float freeAimDown = 8f;

        [Header("브레스")]
        [Tooltip("브레스가 범위 안을 맞히는 간격 (초)")]
        public float breathTick = 0.15f;
        [Tooltip("앞보다 이만큼 아래로 뿜는다 (도) — 땅 위 먹이를 맞히기 쉽게")]
        public float breathDown = 14f;
        [Tooltip("익힘·흩어짐이 무리 쪽으로 퍼지는 배율")]
        public float scatterSpread = 2.5f;

        /// <summary>구체 칸 스킬 (없으면 null)</summary>
        public SkillSpec Orb { get; private set; }
        /// <summary>브레스 칸 스킬 (성체가 아니면 null)</summary>
        public SkillSpec Breath { get; private set; }
        /// <summary>구체 충전 (구체가 없으면 null)</summary>
        public SkillCharges Charges { get; private set; }
        public bool IsBreathing { get; private set; }

        /// <summary>쓸 수 있는 스킬이 바뀜 (성장, 모습 결정, 먹은 성질 변화)</summary>
        public event Action SkillChanged;
        /// <summary>구체를 쐈거나 브레스를 뿜기 시작함</summary>
        public event Action<SkillSpec> Used;

        InputAction _orbAction;
        InputAction _breathAction;
        bool _pressed;
        float _tickTimer;
        float _puffTimer;
        float _ringTimer;
        readonly List<Transform> _hits = new List<Transform>();
        readonly List<Vector3> _aimPoints = new List<Vector3>();

        // ───────── 자동 부착 ─────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

        static void Attach()
        {
            if (FindAnyObjectByType<DragonSkills>()) return;
            var session = FindAnyObjectByType<FlightSession>();
            if (!session || !session.dragon) return;
            var s = session.dragon.gameObject.AddComponent<DragonSkills>();
            s.session = session;
            s.dragon = session.dragon;
        }

        // ───────── 수명 ─────────

        void Awake()
        {
            _orbAction = new InputAction("SkillOrb", InputActionType.Button);
            _orbAction.AddBinding("<Keyboard>/r");
            _orbAction.AddBinding("<Gamepad>/rightShoulder");
            _orbAction.performed += _ =>
            {
                if (!GamePause.IsPaused) _pressed = true;
            };
            _breathAction = new InputAction("SkillBreath", InputActionType.Button);
            _breathAction.AddBinding("<Keyboard>/f");
            _breathAction.AddBinding("<Gamepad>/leftShoulder");
        }

        void OnEnable()
        {
            _orbAction.Enable();
            _breathAction.Enable();
            DragonCare.StageChanged += OnStageChanged;
            DragonCare.Fed += OnFed;
        }

        void OnDisable()
        {
            _orbAction.Disable();
            _breathAction.Disable();
            _pressed = false;
            DragonCare.StageChanged -= OnStageChanged;
            DragonCare.Fed -= OnFed;
            StopBreath();
        }

        void OnDestroy()
        {
            _orbAction.Dispose();
            _breathAction.Dispose();
        }

        void Start()
        {
            if (!session) session = FindAnyObjectByType<FlightSession>();
            if (!dragon && session) dragon = session.dragon;
            if (!dragon) dragon = GetComponent<DragonFlightController>();
            if (session) session.StateChanged += OnStateChanged;
            // 종족 마을 주민·병사도 스킬을 맞게
            var tribes = Tribes.TribeWorld.Instance ? Tribes.TribeWorld.Instance : FindAnyObjectByType<Tribes.TribeWorld>();
            TribeSkillTarget.EnsureFor(tribes);
            Refresh();
        }

        void OnStateChanged(FlightSession.State s)
        {
            if (s != FlightSession.State.Flying) StopBreath();
            if (s == FlightSession.State.Ready) Refresh(true);
        }

        void OnStageChanged(GrowthStage _) => Refresh();
        void OnFed(FeedOutcome _) => Refresh();

        /// <summary>돌봄 상태에서 지금 스킬을 다시 고른다. refill이면 구체 충전도 가득 채운다</summary>
        public void Refresh(bool refill = false)
        {
            var state = DragonCare.IsLoaded ? DragonCare.State : null;
            var orb = SkillRules.PickOrb(book, state);
            var breath = SkillRules.PickBreath(book, state);
            if (orb == Orb && breath == Breath)
            {
                if (refill && orb != null) Charges = new SkillCharges(orb.maxCharges, orb.cooldown);
                return;
            }
            if (orb != Orb) Charges = orb != null ? new SkillCharges(orb.maxCharges, orb.cooldown) : null;
            Orb = orb;
            Breath = breath;
            StopBreath();
            SkillChanged?.Invoke();
        }

        bool CanAct =>
            session && session.Current == FlightSession.State.Flying && dragon && dragon.IsFlying && !dragon.IsEating;

        void Update()
        {
            bool pressed = _pressed;
            _pressed = false;
            if (GamePause.IsPaused) return;
            float dt = Time.deltaTime;
            Charges?.Tick(dt);

            if (!CanAct)
            {
                StopBreath();
                return;
            }

            // 구체 (브레스를 뿜는 중에도 쏠 수 있다)
            if (Orb != null && pressed && SkillRules.CanFireOrb(Orb, Charges, dragon.Model.Stamina) && Charges.TryUse())
            {
                dragon.Model.Stamina -= Orb.staminaCost;
                FireOrb(Orb);
                Used?.Invoke(Orb);
            }

            // 브레스
            if (Breath != null && _breathAction.IsPressed() && SkillRules.CanBreathe(Breath, dragon.Model.Stamina, IsBreathing))
            {
                if (!IsBreathing)
                {
                    IsBreathing = true;
                    _tickTimer = 0f;
                    _ringTimer = 0f;
                    Used?.Invoke(Breath);
                }
                dragon.Model.Stamina -= SkillRules.BreathDrain(Breath, dt, dragon.Model.Stamina);
                Breathe(Breath, dt);
            }
            else StopBreath();
        }

        void StopBreath() => IsBreathing = false;

        // ───────── 위치 ─────────

        float Scale => dragon ? dragon.transform.localScale.x : 1f;

        Vector3 Mouth
        {
            get
            {
                var t = dragon.transform;
                return t.position + t.forward * (1.3f * Scale) + t.up * (0.2f * Scale);
            }
        }

        Vector3 DownTilted(float degrees)
        {
            var t = dragon.transform;
            var flatRight = Vector3.Cross(Vector3.up, t.forward);
            if (flatRight.sqrMagnitude < 1e-4f) flatRight = t.right;
            return Quaternion.AngleAxis(degrees, flatRight.normalized) * t.forward;
        }

        PreySpawner Spawner => session ? session.spawner : null;

        // ───────── 구체 ─────────

        void FireOrb(SkillSpec s)
        {
            Vector3 from = Mouth;
            Vector3 fwd = dragon.transform.forward;

            // 앞쪽 먹이·적 중 가장 가운데 있는 것을 겨눈다
            _aimPoints.Clear();
            if (Spawner)
                foreach (var p in Spawner.All)
                    if (p && p.IsAvailable && !p.IsHeld) _aimPoints.Add(p.transform.position);
            foreach (var t in SkillTargets.All) t.CollectAimPoints(_aimPoints);
            int pick = SkillRules.PickAimTarget(from, fwd, _aimPoints, aimRange, aimAngle);

            Vector3 vel = pick >= 0
                ? SkillRules.LaunchVelocity(from, _aimPoints[pick], s.projectileSpeed, s.gravity)
                : DownTilted(freeAimDown) * s.projectileSpeed;

            var go = SkillFx.Shape(PrimitiveType.Sphere, "SkillOrb_" + s.id, null, from, Vector3.one * s.projectileSize, s.color);
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = SkillFx.Mat;
            trail.time = 0.2f;
            trail.widthMultiplier = s.projectileSize * 0.7f;
            trail.startColor = s.color;
            trail.endColor = new Color(s.color.r, s.color.g, s.color.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var orb = go.AddComponent<SkillOrb>();
            orb.Init(this, s, vel);

            // 입에서 터지는 작은 섬광
            SkillFx.Puff(from, vel * 0.1f, s.projectileSize * 0.6f, s.projectileSize * 1.6f, 0.15f, Color.Lerp(s.color, Color.white, 0.5f));
        }

        /// <summary>날아가는 구체(from→to)가 먹이·적·땅에 닿았는지</summary>
        internal bool OrbCollides(SkillSpec s, Vector3 from, Vector3 to, out Vector3 point)
        {
            float r = s.projectileSize * 0.5f;
            point = to;
            float best = float.MaxValue;
            bool hit = false;

            foreach (var t in SkillTargets.All)
            {
                if (!t.ProjectileHit(from, to, r, out var p)) continue;
                float d = (p - from).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    point = p;
                    hit = true;
                }
            }
            if (Spawner)
            {
                foreach (var prey in Spawner.All)
                {
                    if (!prey || !prey.IsAvailable) continue;
                    var p = prey.transform.position;
                    float size = prey.Data ? Mathf.Max(prey.Data.bodySize.x, prey.Data.bodySize.z) * 0.5f * prey.transform.localScale.x : 0.6f;
                    if (SkillRules.SegmentDistance(from, to, p) > r + size + 0.3f) continue;
                    float d = (p - from).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        point = p;
                        hit = true;
                    }
                }
            }
            // 땅·나무·바위 (드래곤 자신은 빼고)
            var dir = to - from;
            float len = dir.magnitude;
            if (len > 1e-4f)
            {
                foreach (var h in Physics.RaycastAll(from, dir / len, len, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (dragon && h.transform.IsChildOf(dragon.transform)) continue;
                    float d = (h.point - from).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        point = h.point;
                        hit = true;
                    }
                }
            }
            return hit;
        }

        /// <summary>구체가 터짐: 반경 안 먹이·적에게 효과</summary>
        internal void Burst(SkillSpec s, Vector3 at, Vector3 travelDir)
        {
            var area = SkillArea.Sphere(at, s.burstRadius);
            HitPrey(s, area, at);
            HitEnemies(s, area, travelDir, 1f);
            SkillFx.Burst(at, s.color, s.burstRadius);
        }

        // ───────── 브레스 ─────────

        void Breathe(SkillSpec s, float dt)
        {
            Vector3 origin = s.shape == SkillShape.Ring ? dragon.transform.position : Mouth;
            Vector3 dir = DownTilted(breathDown);
            Vector3 carry = dragon.Model.Velocity;

            // 연출
            _puffTimer -= dt;
            if (s.shape == SkillShape.Ring)
            {
                _ringTimer -= dt;
                if (_ringTimer <= 0f)
                {
                    _ringTimer = 0.4f;
                    SkillFx.Ring(origin, s.color, s.range);
                }
            }
            else
            {
                while (_puffTimer <= 0f)
                {
                    _puffTimer += s.shape == SkillShape.Line ? 0.015f : 0.025f;
                    EmitPuff(s, dir, carry);
                }
            }

            // 판정
            _tickTimer -= dt;
            if (_tickTimer > 0f) return;
            _tickTimer += breathTick;
            var area = SkillArea.Of(s, origin, dir);
            HitPrey(s, area, origin);
            HitEnemies(s, area, dir, breathTick);
        }

        void EmitPuff(SkillSpec s, Vector3 dir, Vector3 carry)
        {
            Vector3 at = Mouth;
            Color c = s.id == book.rainbowBreath.id
                ? Color.HSVToRGB(Mathf.Repeat(Time.time * 0.8f + UnityEngine.Random.value * 0.2f, 1f), 0.6f, 1f)
                : SkillFx.Vary(s.color);
            if (s.shape == SkillShape.Line)
            {
                // 곧게 뻗는 물줄기
                var v = dir * (s.range / 0.35f) + UnityEngine.Random.insideUnitSphere * 1.5f + carry;
                SkillFx.Puff(at, v, 0.35f * Scale, s.width * 0.9f, 0.35f, c);
            }
            else
            {
                // 부채꼴로 퍼지는 입자
                var spread = Quaternion.AngleAxis(UnityEngine.Random.Range(-s.width, s.width), dragon.transform.up)
                             * Quaternion.AngleAxis(UnityEngine.Random.Range(-s.width, s.width) * 0.6f, dragon.transform.right);
                float life = 0.5f;
                var v = spread * dir * (s.range / life * 1.3f) + carry;
                float end = s.range * Mathf.Tan(s.width * Mathf.Deg2Rad) * 0.6f;
                SkillFx.Puff(at, v, 0.3f * Scale, Mathf.Max(0.8f, end), life, c);
            }
        }

        // ───────── 효과 적용 ─────────

        void HitPrey(SkillSpec s, SkillArea area, Vector3 from)
        {
            if (!Spawner) return;
            var e = s.preyEffects;
            float hold = SkillRules.PreyHold(s);
            bool scatter = (e & SkillEffect.Scatter) != 0;
            // 흩어짐은 범위보다 넓게 (무리 전체가 놀라 달아난다)
            var wide = SkillArea.Sphere(area.Center, area.Radius * scatterSpread);

            foreach (var prey in Spawner.All)
            {
                if (!prey || !prey.IsAvailable) continue;
                var p = prey.transform.position;
                if (area.Contains(p, 0.6f))
                {
                    if ((e & SkillEffect.Cook) != 0) prey.Cook();
                    if (hold > 0f)
                    {
                        prey.Hold(hold);
                        SkillFx.Mark(prey.transform, HoldMark(e), hold);
                    }
                    else if (scatter) prey.Scatter(from, s.effectDuration);
                }
                else if (scatter && hold <= 0f && wide.Contains(p))
                    prey.Scatter(from, s.effectDuration);
            }
        }

        static SkillEffect HoldMark(SkillEffect e) =>
            (e & SkillEffect.Sleep) != 0 ? SkillEffect.Sleep : (e & SkillEffect.Bind) != 0 ? SkillEffect.Bind : SkillEffect.Stun;

        void HitEnemies(SkillSpec s, SkillArea area, Vector3 pushDir, float scale)
        {
            var hit = SkillRules.EnemyHit(s, pushDir, scale);
            foreach (var t in SkillTargets.All)
            {
                _hits.Clear();
                t.ApplySkill(area, hit, _hits);
                foreach (var h in _hits)
                {
                    if (hit.stun > 0f) SkillFx.Mark(h, SkillEffect.Stun, hit.stun);
                    if (hit.sleep > 0f) SkillFx.Mark(h, SkillEffect.Sleep, hit.sleep);
                    if (hit.burn > 0f) SkillFx.Mark(h, SkillEffect.Burn, hit.burn);
                    if (hit.slow > 0f) SkillFx.Mark(h, SkillEffect.Slow, hit.slow);
                }
            }
        }
    }

    /// <summary>날아가는 구체 하나</summary>
    public class SkillOrb : MonoBehaviour
    {
        DragonSkills _owner;
        SkillSpec _spec;
        Vector3 _vel;
        float _age;

        public void Init(DragonSkills owner, SkillSpec spec, Vector3 velocity)
        {
            _owner = owner;
            _spec = spec;
            _vel = velocity;
        }

        void Update()
        {
            if (GamePause.IsPaused) return;
            if (!_owner || _spec == null)
            {
                Destroy(gameObject);
                return;
            }
            float dt = Time.deltaTime;
            _age += dt;
            Vector3 from = transform.position;
            _vel += Vector3.down * (_spec.gravity * dt);
            Vector3 to = from + _vel * dt;

            if (_owner.OrbCollides(_spec, from, to, out var point))
            {
                _owner.Burst(_spec, point, _vel);
                Destroy(gameObject);
                return;
            }
            if (_age >= _spec.lifetime)
            {
                // 아무것도 못 맞히고 사그라든다
                SkillFx.Puff(to, Vector3.zero, _spec.projectileSize, _spec.projectileSize * 1.5f, 0.2f, _spec.color);
                Destroy(gameObject);
                return;
            }
            transform.position = to;
            transform.Rotate(360f * dt, 540f * dt, 0f, Space.Self);
        }
    }
}
