using System.Collections.Generic;
using DragonRecipe.Care;
using DragonRecipe.Core;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using DragonRecipe.Hostile;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DragonRecipe.Tribes
{
    /// <summary>
    /// [임시] 비행 맵의 세 종족 마을: 집과 깃발, 돌아다니는 주민, 병사, 전쟁터.
    /// - 깃발 색 = 호감도 단계 (빨강 적대 · 주황 경계 · 회색 중립 · 초록 우호 · 금색 숭배). 호감도가 바뀌면 번쩍인다.
    /// - 병사: 적대면 사거리 안에서, 경계면 마을 땅에 들어왔을 때 쏜다. 시위를 당기는 동안 무기 끝이 빨개진다.
    ///   사람 = 활, 오크 = 무겁고 아픈 창, 엘프 = 멀리서 정확한 저격.
    /// - 주민: 드래곤이 낮게 덮치면 흩어진다. 우호 이상이면 드래곤이 지나갈 때 펄쩍펄쩍 반긴다.
    /// - 전쟁 중이면 두 마을 사이에 전쟁터가 생기고 양쪽 병사가 서로 쏜다.
    /// 공격·스킬은 Hit / AttackArea / Stun / Knockback / ClearProjectilesIn으로.
    /// 프리미티브만 쓰고 콜라이더가 없어 드래곤 충돌 판정에 걸리지 않는다. 비행 씬에 스스로 붙는다.
    /// </summary>
    public class TribeWorld : MonoBehaviour
    {
        public static TribeWorld Instance { get; private set; }

        public FlightSession session;
        [Tooltip("배치 구역을 따라갈 먹이 스포너 (비우면 session.spawner)")]
        public PreySpawner area;

        [Header("마을 자리 (구역 가운데 기준, x/z)")]
        public Vector2 humanVillage = new Vector2(-95f, -40f);
        public Vector2 orcVillage = new Vector2(100f, 20f);
        public Vector2 elfVillage = new Vector2(0f, 120f);
        public int residentsPerVillage = 5;
        [Tooltip("전쟁터에 나가는 한 편의 병사 수")]
        public int warPartySize = 4;

        [Header("움직임")]
        public float wanderRadius = 12f;
        public float walkSpeed = 1.1f;
        public float fleeSpeed = 5.5f;
        public float fleeTime = 3f;
        [Tooltip("드래곤이 이만큼 가까이 낮게 오면 겁먹고 흩어진다")]
        public float frightRadius = 9f;
        [Tooltip("우호 이상일 때 드래곤이 이 거리 안을 지나가면 반긴다")]
        public float cheerRadius = 35f;
        [Tooltip("공격받아 쓰러져 있는 시간")]
        public float downTime = 6f;
        [Tooltip("엘프 땅: 엘프 마을에서 이 반경 안에서 사냥하면 엘프가 싫어한다")]
        public float elfLandRadius = 70f;

        class Village
        {
            public Tribe tribe;
            public Vector3 center;
            public Renderer flag;
            public float flash;
            public float shownRep;
        }

        class Projectile
        {
            public Transform t;
            public Vector3 velocity;
            public float age;
            public float damage;
            public bool stuck;
            public bool hurtsDragon;
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly Color TipIdle = new Color(0.55f, 0.55f, 0.6f);
        static readonly Color TipAim = new Color(1f, 0.12f, 0.08f);
        static Material _mat, _trailMat;

        readonly Village[] _villages = new Village[3];
        readonly List<TribeMember> _members = new List<TribeMember>();
        readonly List<Projectile> _projectiles = new List<Projectile>();
        Transform _membersRoot;
        MaterialPropertyBlock _block;
        bool _built;

        public IReadOnlyList<TribeMember> Members => _members;

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
            if (FindAnyObjectByType<TribeWorld>()) return;
            var session = FindAnyObjectByType<FlightSession>();
            if (!session) return;
            new GameObject("Tribes").AddComponent<TribeWorld>().session = session;
        }

        // ───────── 수명 ─────────

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (session)
            {
                session.StateChanged -= OnStateChanged;
                session.Caught -= OnCaught;
            }
            DragonCare.TribesChanged -= OnTribesChanged;
        }

        void Start()
        {
            if (!session) session = FindAnyObjectByType<FlightSession>();
            if (!session) return;
            if (!area) area = session.spawner;
            session.StateChanged += OnStateChanged;
            session.Caught += OnCaught;
            DragonCare.TribesChanged += OnTribesChanged;
            Build();
        }

        static TribeState State => DragonCare.IsLoaded ? DragonCare.Tribes : null;
        static TribeSettings Settings => DragonCare.TribeSettings;

        void OnStateChanged(FlightSession.State s)
        {
            // 다시 날기: 바뀐 호감도·전쟁에 맞춰 주민과 병사를 새로 세운다
            if (s == FlightSession.State.Ready && _built) Populate();
        }

        void OnTribesChanged()
        {
            var st = State;
            if (st == null) return;
            foreach (var v in _villages)
            {
                if (v == null) continue;
                float rep = TribeLogic.Reputation(st, v.tribe);
                if (!Mathf.Approximately(rep, v.shownRep)) v.flash = 1f;
                v.shownRep = rep;
            }
        }

        /// <summary>엘프 땅에서 먹이를 잡으면 엘프가 싫어한다</summary>
        void OnCaught(int combo)
        {
            var elf = _villages[(int)Tribe.Elf];
            if (elf == null || !session.dragon) return;
            Vector3 d = session.dragon.Model.Position - elf.center;
            if (new Vector2(d.x, d.z).magnitude <= elfLandRadius) DragonCare.ReportElfLandHunt();
        }

        // ───────── 공격·스킬용 API ─────────

        /// <summary>범위 안의 (쓰러지지 않은) 주민·병사</summary>
        public List<TribeMember> MembersIn(Vector3 center, float radius)
        {
            var list = new List<TribeMember>();
            float r2 = radius * radius;
            foreach (var m in _members)
                if (m && !m.IsDown && (m.transform.position - center).sqrMagnitude <= r2) list.Add(m);
            return list;
        }

        /// <summary>
        /// 드래곤이 주민·병사 하나를 공격함: 쓰러뜨리고 호감도에 반영한다.
        /// 이미 쓰러져 있으면 아무 일 없이 false
        /// </summary>
        public bool Hit(TribeMember m)
        {
            if (!KnockDown(m)) return false;
            DragonCare.ReportTribeAttack(m.Tribe);
            return true;
        }

        /// <summary>범위 공격: 범위 안 주민·병사를 모두 쓰러뜨리고, 맞힌 종족마다 한 번씩 호감도에 반영. 맞힌 수</summary>
        public int AttackArea(Vector3 center, float radius)
        {
            int hit = 0;
            bool human = false, orc = false, elf = false;
            foreach (var m in MembersIn(center, radius))
            {
                if (!KnockDown(m)) continue;
                hit++;
                switch (m.Tribe)
                {
                    case Tribe.Human: human = true; break;
                    case Tribe.Orc: orc = true; break;
                    default: elf = true; break;
                }
            }
            if (human) DragonCare.ReportTribeAttack(Tribe.Human);
            if (orc) DragonCare.ReportTribeAttack(Tribe.Orc);
            if (elf) DragonCare.ReportTribeAttack(Tribe.Elf);
            return hit;
        }

        /// <summary>기절·둔화: seconds 동안 움직이지도 쏘지도 못한다 (호감도에는 반영 안 함)</summary>
        public void Stun(TribeMember m, float seconds)
        {
            if (!m) return;
            m.stunUntil = Mathf.Max(m.stunUntil, Time.time + seconds);
            m.aim = 0f;
        }

        /// <summary>밀어내기 (m/s). 땅 위에서 미끄러지다 멈춘다</summary>
        public void Knockback(TribeMember m, Vector3 push)
        {
            if (!m) return;
            push.y = 0f;
            m.push += push;
        }

        /// <summary>범위 안에 날아가는 화살·창을 지운다. 지운 수</summary>
        public int ClearProjectilesIn(Vector3 center, float radius)
        {
            int n = 0;
            float r2 = radius * radius;
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                var p = _projectiles[i];
                if (!p.t || p.stuck || (p.t.position - center).sqrMagnitude > r2) continue;
                Destroy(p.t.gameObject);
                _projectiles.RemoveAt(i);
                n++;
            }
            return n;
        }

        /// <summary>그 자리가 어느 종족 땅인지 (마을 둘레 territoryRadius 안). 아니면 false</summary>
        public bool TerritoryAt(Vector3 point, out Tribe tribe)
        {
            tribe = Tribe.Human;
            float best = float.MaxValue;
            float r = Settings.territoryRadius;
            foreach (var v in _villages)
            {
                if (v == null) continue;
                Vector3 d = point - v.center;
                float dist = new Vector2(d.x, d.z).magnitude;
                if (dist <= r && dist < best)
                {
                    best = dist;
                    tribe = v.tribe;
                }
            }
            return best < float.MaxValue;
        }

        bool KnockDown(TribeMember m)
        {
            if (!m || m.IsDown) return false;
            m.downUntil = Time.time + downTime;
            m.aim = 0f;
            m.fleeUntil = -1f;
            return true;
        }

        // ───────── 매 프레임 ─────────

        void Update()
        {
            if (!_built || !session || GamePause.IsPaused) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var st = State;
            if (st == null) return;

            var dragon = session.dragon;
            bool flying = session.Current == FlightSession.State.Flying && dragon && dragon.IsFlying;
            Vector3 target = dragon ? dragon.Model.Position : Vector3.zero;
            Vector3 targetVel = dragon ? dragon.Model.Velocity : Vector3.zero;
            float hitRadius = dragon ? dragon.collisionRadius * dragon.transform.localScale.x + 0.5f : 1f;

            foreach (var m in _members)
                if (m) UpdateMember(m, dt, st, flying, target, targetVel);
            UpdateProjectiles(dt, flying, dragon, target, hitRadius);
            UpdateFlags(dt, st);
        }

        void UpdateMember(TribeMember m, float dt, TribeState st, bool flying, Vector3 target, Vector3 targetVel)
        {
            var s = Settings;
            var tr = m.transform;
            Vector3 pos = tr.position;

            // 밀려남은 쓰러져 있어도 적용
            if (m.push.sqrMagnitude > 0.01f)
            {
                pos += m.push * dt;
                m.push = Vector3.MoveTowards(m.push, Vector3.zero, 12f * dt);
            }

            if (m.IsDown)
            {
                SetPose(m, pos, Quaternion.Euler(85f, tr.eulerAngles.y, 0f), 0f);
                Tint(m.tip, TipIdle);
                return;
            }
            if (m.IsStunned)
            {
                SetPose(m, pos, Quaternion.Euler(0f, tr.eulerAngles.y + 360f * dt, 0f), 0f); // 빙글빙글
                Tint(m.tip, TipIdle);
                return;
            }

            Vector3 toTarget = target - pos;
            float flat = new Vector2(toTarget.x, toTarget.z).magnitude;
            var standing = TribeLogic.StandingOf(st, m.Tribe, s);
            var aggression = TribeLogic.AggressionOf(st, m.Tribe, s);

            // 낮게 덮치면 겁먹고 흩어진다 (적대 병사는 버티고 쏜다)
            bool brave = m.IsSoldier && aggression == Aggression.Full;
            if (flying && !brave && flat <= frightRadius && toTarget.y <= frightRadius && Time.time >= m.fleeUntil)
            {
                Vector3 away = pos - target;
                away.y = 0f;
                var rnd = Random.insideUnitCircle.normalized;
                m.fleeDir = away.sqrMagnitude > 0.01f ? away.normalized : new Vector3(rnd.x, 0f, rnd.y);
                m.fleeUntil = Time.time + fleeTime;
                m.aim = 0f;
            }

            Vector3 move = Vector3.zero;
            float speed = 0f;
            Vector3 face = tr.forward;
            float hop = 0f;

            bool canShoot = flying && m.IsSoldier && CanShoot(m, aggression, toTarget);
            if (Time.time < m.fleeUntil)
            {
                move = m.fleeDir;
                speed = fleeSpeed;
                face = move;
            }
            else if (canShoot)
            {
                face = new Vector3(toTarget.x, 0f, toTarget.z);
                AimAndFire(m, dt, target, targetVel, true);
            }
            else
            {
                if (m.IsWarParty)
                {
                    // 전쟁터: 마주 본 적 편을 향해 쏜다 (드래곤은 다치지 않는 화살)
                    face = m.warFacing;
                    hop = Mathf.Abs(Mathf.Sin(Time.time * 6f + m.phase)) * 0.15f;
                    if (m.IsSoldier) AimAndFire(m, dt, pos + m.warFacing * 14f + Vector3.up * 1.2f, Vector3.zero, false);
                }
                else
                {
                    m.aim = 0f;
                    if (Time.time >= m.nextWander)
                    {
                        var off = Random.insideUnitCircle * wanderRadius;
                        m.wanderTarget = m.home + new Vector3(off.x, 0f, off.y);
                        m.nextWander = Time.time + Random.Range(3f, 7f);
                    }
                    Vector3 to = m.wanderTarget - pos;
                    to.y = 0f;
                    if (to.magnitude > 0.5f)
                    {
                        move = to.normalized;
                        speed = walkSpeed;
                        face = move;
                    }
                }

                // 우호 이상: 드래곤이 지나가면 펄쩍펄쩍 반긴다
                if (flying && standing >= Standing.Friendly && toTarget.magnitude <= cheerRadius)
                {
                    face = new Vector3(toTarget.x, 0f, toTarget.z);
                    hop = Mathf.Abs(Mathf.Sin(Time.time * 9f + m.phase)) * (standing == Standing.Revered ? 0.8f : 0.5f);
                    speed = 0f;
                }
            }

            if (speed > 0f) pos += move * speed * dt;
            if (area) pos = area.ClampToArea(pos);
            var rot = face.sqrMagnitude > 0.001f
                ? Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(face), 6f * dt)
                : tr.rotation;
            SetPose(m, pos, rot, hop);

            // 시위를 당기는 동안 무기가 들리고 끝이 빨개진다
            var combat = s.Combat(m.Tribe);
            float draw = m.aim > 0f ? 1f - m.aim / Mathf.Max(0.01f, combat.aimTime) : 0f;
            if (m.weapon)
            {
                float pitch = 60f;
                if (draw > 0f)
                {
                    Vector3 aimAt = canShoot ? toTarget : m.warFacing * 14f + Vector3.up * 1.2f;
                    pitch = -Mathf.Clamp(Mathf.Atan2(aimAt.y, new Vector2(aimAt.x, aimAt.z).magnitude) * Mathf.Rad2Deg, -10f, 80f);
                }
                m.weapon.localRotation = Quaternion.Slerp(m.weapon.localRotation, Quaternion.Euler(pitch, 0f, 0f), 10f * dt);
            }
            Tint(m.tip, Color.Lerp(TipIdle, TipAim, draw));
        }

        bool CanShoot(TribeMember m, Aggression aggression, Vector3 toTarget)
        {
            var combat = Settings.Combat(m.Tribe);
            if (toTarget.sqrMagnitude > combat.range * combat.range) return false;
            switch (aggression)
            {
                case Aggression.Full: return true;
                case Aggression.Territorial:
                    Vector3 fromVillage = toTarget + m.transform.position - m.villageCenter;
                    return new Vector2(fromVillage.x, fromVillage.z).magnitude <= Settings.territoryRadius;
                default: return false;
            }
        }

        void SetPose(TribeMember m, Vector3 pos, Quaternion rot, float hop)
        {
            pos.y = m.groundY + hop;
            m.transform.SetPositionAndRotation(pos, rot);
        }

        void AimAndFire(TribeMember m, float dt, Vector3 target, Vector3 targetVel, bool atDragon)
        {
            var combat = Settings.Combat(m.Tribe);
            if (m.aim > 0f)
            {
                m.aim -= dt;
                if (m.aim > 0f) return;
                Fire(m, combat, target, targetVel, atDragon);
                m.reload = Random.Range(combat.reloadMin, combat.reloadMax) * (atDragon ? 1f : 1.5f);
                return;
            }
            m.reload -= dt;
            if (m.reload <= 0f) m.aim = combat.aimTime;
        }

        void Fire(TribeMember m, TribeCombat combat, Vector3 target, Vector3 targetVel, bool atDragon)
        {
            Vector3 from = m.tip ? m.tip.transform.position : m.transform.position + Vector3.up * 1.4f;
            HostileRules.LeadDirection(from, target, targetVel, combat.projectileSpeed, out var dir);
            float spread = combat.spread * (atDragon ? 1f : 3f);
            dir = Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0f) * dir;

            bool spear = m.Tribe == Tribe.Orc;
            var go = Part(PrimitiveType.Cube, spear ? "Spear" : "Arrow", null, Vector3.zero,
                spear ? new Vector3(0.12f, 0.12f, 2f) : new Vector3(0.07f, 0.07f, 1.1f),
                spear ? new Color(0.3f, 0.2f, 0.12f) : new Color(0.35f, 0.25f, 0.15f));
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(dir));
            go.transform.SetParent(transform, true);
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = TrailMat;
            trail.time = 0.25f;
            trail.widthMultiplier = spear ? 0.2f : 0.12f;
            var c = m.Tribe == Tribe.Elf ? new Color(0.75f, 1f, 0.8f) : new Color(1f, 0.9f, 0.8f);
            trail.startColor = new Color(c.r, c.g, c.b, 0.8f);
            trail.endColor = new Color(c.r, c.g, c.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _projectiles.Add(new Projectile { t = go.transform, velocity = dir * combat.projectileSpeed, damage = combat.damage, hurtsDragon = atDragon });
        }

        void UpdateProjectiles(float dt, bool flying, DragonFlightController dragon, Vector3 target, float hitRadius)
        {
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                var p = _projectiles[i];
                if (!p.t)
                {
                    _projectiles.RemoveAt(i);
                    continue;
                }
                p.age += dt;
                if (p.age >= 3.5f)
                {
                    Destroy(p.t.gameObject);
                    _projectiles.RemoveAt(i);
                    continue;
                }
                if (p.stuck) continue;

                Vector3 from = p.t.position;
                Vector3 to = from + p.velocity * dt;
                if (p.hurtsDragon && flying && HostileRules.SegmentDistance(from, to, target) <= hitRadius)
                {
                    dragon.ApplyDamage(p.damage);
                    if (dragon.IsFlying) dragon.Model.Jolt(Random.Range(-8f, 8f) * (p.damage / 6f), Random.Range(-4f, 2f), 0f);
                    Destroy(p.t.gameObject);
                    _projectiles.RemoveAt(i);
                    continue;
                }
                if (Physics.Linecast(from, to, out var hit, ~0, QueryTriggerInteraction.Ignore))
                {
                    p.t.position = hit.point;
                    p.stuck = true;
                    p.age = Mathf.Max(p.age, 2f);
                    continue;
                }
                p.t.position = to;
            }
        }

        void UpdateFlags(float dt, TribeState st)
        {
            foreach (var v in _villages)
            {
                if (v == null || !v.flag) continue;
                var c = TribeLook.Of(TribeLogic.StandingOf(st, v.tribe, Settings));
                if (v.flash > 0f)
                {
                    v.flash = Mathf.Max(0f, v.flash - dt * 1.5f);
                    c = Color.Lerp(c, Color.white, Mathf.Abs(Mathf.Sin(v.flash * 12f)) * v.flash);
                }
                Tint(v.flag, c);
                // 펄럭임
                v.flag.transform.parent.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 3f + (int)v.tribe) * 12f, 0f);
            }
        }

        // ───────── 배치 ─────────

        void Build()
        {
            if (_built || State == null) return;
            _built = true;
            Vector3 c = area ? area.areaCenter : new Vector3(0, 0, 200);
            BuildVillage(Tribe.Human, c + new Vector3(humanVillage.x, 0, humanVillage.y));
            BuildVillage(Tribe.Orc, c + new Vector3(orcVillage.x, 0, orcVillage.y));
            BuildVillage(Tribe.Elf, c + new Vector3(elfVillage.x, 0, elfVillage.y));
            foreach (var v in _villages)
                if (v != null) v.shownRep = TribeLogic.Reputation(State, v.tribe);
            Populate();
        }

        /// <summary>주민·병사·전쟁터를 지금 호감도와 전쟁 상태에 맞게 새로 세운다</summary>
        void Populate()
        {
            var st = State;
            if (st == null) return;
            foreach (var p in _projectiles)
                if (p.t) Destroy(p.t.gameObject);
            _projectiles.Clear();
            if (_membersRoot) Destroy(_membersRoot.gameObject);
            _members.Clear();
            _membersRoot = new GameObject("Members").transform;
            _membersRoot.SetParent(transform, false);

            var s = Settings;
            foreach (var v in _villages)
            {
                if (v == null) continue;
                int soldiers = TribeLogic.SoldierCount(st, v.tribe, s);
                for (int i = 0; i < residentsPerVillage; i++) Spawn(v, Around(v.center, 9f), false, false);
                for (int i = 0; i < soldiers; i++) Spawn(v, Around(v.center, 14f), true, false);
            }

            // 전쟁터: 두 마을 사이 한가운데서 마주 본다
            var war = st.war;
            if (war.Active)
            {
                var a = _villages[(int)war.sideA];
                var b = _villages[(int)war.sideB];
                if (a != null && b != null)
                {
                    Vector3 mid = (a.center + b.center) * 0.5f;
                    Vector3 axis = b.center - a.center;
                    axis.y = 0f;
                    axis = axis.sqrMagnitude > 0.01f ? axis.normalized : Vector3.right;
                    Vector3 side = Vector3.Cross(Vector3.up, axis);
                    for (int i = 0; i < warPartySize; i++)
                    {
                        float lateral = (i - (warPartySize - 1) * 0.5f) * 2.5f;
                        var ma = Spawn(a, mid - axis * 7f + side * lateral, true, true);
                        if (ma) ma.warFacing = axis;
                        var mb = Spawn(b, mid + axis * 7f + side * lateral, true, true);
                        if (mb) mb.warFacing = -axis;
                    }
                }
            }
        }

        static Vector3 Around(Vector3 center, float radius)
        {
            var off = Random.insideUnitCircle * radius;
            return center + new Vector3(off.x, 0f, off.y);
        }

        bool GroundAt(Vector3 near, float searchRadius, out Vector3 point)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var off = attempt == 0 ? Vector2.zero : Random.insideUnitCircle * searchRadius;
                var p = new Vector3(near.x + off.x, 200f, near.z + off.y);
                if (area) p = area.ClampToArea(p);
                p.y = 200f;
                if (!Physics.Raycast(p, Vector3.down, out var hit, 400f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.95f || hit.point.y > 1f) continue; // 나무/바위/절벽 위는 제외
                point = hit.point;
                return true;
            }
            point = new Vector3(near.x, 0f, near.z);
            return false;
        }

        void BuildVillage(Tribe tribe, Vector3 wanted)
        {
            GroundAt(wanted, 40f, out var center);
            var v = new Village { tribe = tribe, center = center };
            _villages[(int)tribe] = v;

            var root = new GameObject($"Village_{tribe}").transform;
            root.SetParent(transform, false);
            root.position = center;

            // 깃발 (색 = 호감도 단계)
            Part(PrimitiveType.Cylinder, "BannerPole", root, new Vector3(0, 4f, 0), new Vector3(0.25f, 4f, 0.25f), new Color(0.35f, 0.25f, 0.15f));
            var flagPivot = new GameObject("FlagPivot").transform;
            flagPivot.SetParent(root, false);
            flagPivot.localPosition = new Vector3(0, 7.2f, 0);
            v.flag = Part(PrimitiveType.Cube, "Flag", flagPivot, new Vector3(1.4f, 0, 0), new Vector3(2.8f, 1.6f, 0.08f), TribeLook.NeutralColor)
                .GetComponent<Renderer>();

            var rng = new System.Random(100 + (int)tribe);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            for (int i = 0; i < 6; i++)
            {
                float ang = i / 6f * Mathf.PI * 2f + R(-0.3f, 0.3f);
                float dist = R(11f, 17f);
                var at = new Vector3(Mathf.Cos(ang) * dist, 0, Mathf.Sin(ang) * dist);
                var yaw = Quaternion.Euler(0, -ang * Mathf.Rad2Deg + 90f, 0);
                var house = new GameObject($"House_{i}").transform;
                house.SetParent(root, false);
                house.localPosition = at;
                house.localRotation = yaw;
                switch (tribe)
                {
                    case Tribe.Human: HumanHouse(house); break;
                    case Tribe.Orc: OrcHut(house, R(0f, 360f)); break;
                    default: ElfTree(house, R(0.85f, 1.25f)); break;
                }
            }

            if (tribe == Tribe.Human)
            {
                // 울타리
                for (int i = 0; i < 16; i++)
                {
                    float ang = i / 16f * Mathf.PI * 2f;
                    Part(PrimitiveType.Cube, "Fence", root, new Vector3(Mathf.Cos(ang) * 22f, 0.5f, Mathf.Sin(ang) * 22f),
                        new Vector3(0.2f, 1f, 0.2f), new Color(0.55f, 0.4f, 0.25f));
                }
            }
            else if (tribe == Tribe.Orc)
            {
                // 토템
                Part(PrimitiveType.Cylinder, "Totem", root, new Vector3(4f, 2.5f, 3f), new Vector3(0.9f, 2.5f, 0.9f), new Color(0.3f, 0.22f, 0.15f));
                Part(PrimitiveType.Sphere, "TotemSkull", root, new Vector3(4f, 5.4f, 3f), Vector3.one * 1.1f, new Color(0.9f, 0.88f, 0.8f));
            }
            else
            {
                // 엘프 숲: 마을 둘레 큰 나무들
                for (int i = 0; i < 10; i++)
                {
                    float ang = R(0f, Mathf.PI * 2f);
                    float dist = R(24f, 40f);
                    var t = new GameObject("ForestTree").transform;
                    t.SetParent(root, false);
                    t.localPosition = new Vector3(Mathf.Cos(ang) * dist, 0, Mathf.Sin(ang) * dist);
                    ElfTree(t, R(1.2f, 1.8f), house: false);
                }
            }
        }

        void HumanHouse(Transform t)
        {
            Part(PrimitiveType.Cube, "Walls", t, new Vector3(0, 1.4f, 0), new Vector3(4f, 2.8f, 3.4f), new Color(0.85f, 0.78f, 0.62f));
            var roof = Part(PrimitiveType.Cube, "Roof", t, new Vector3(0, 3.3f, 0), new Vector3(2.9f, 2.9f, 3.8f), new Color(0.6f, 0.25f, 0.18f));
            roof.transform.localRotation = Quaternion.Euler(0, 0, 45f);
            Part(PrimitiveType.Cube, "Door", t, new Vector3(0, 0.8f, 1.72f), new Vector3(0.9f, 1.6f, 0.05f), new Color(0.4f, 0.27f, 0.15f));
        }

        void OrcHut(Transform t, float spin)
        {
            Part(PrimitiveType.Cylinder, "Hut", t, new Vector3(0, 1.2f, 0), new Vector3(4f, 1.2f, 4f), new Color(0.35f, 0.27f, 0.2f));
            var cone = Part(PrimitiveType.Cube, "HideRoof", t, new Vector3(0, 3f, 0), new Vector3(3f, 1.8f, 3f), new Color(0.45f, 0.33f, 0.22f));
            cone.transform.localRotation = Quaternion.Euler(45f, spin, 45f);
            for (int i = 0; i < 3; i++)
            {
                var spike = Part(PrimitiveType.Cube, "Spike", t, new Vector3(-1.5f + i * 1.5f, 3.6f, 0), new Vector3(0.15f, 2f, 0.15f), new Color(0.9f, 0.88f, 0.8f));
                spike.transform.localRotation = Quaternion.Euler(0, 0, -20f + i * 20f);
            }
        }

        void ElfTree(Transform t, float scale, bool house = true)
        {
            Part(PrimitiveType.Cylinder, "Trunk", t, new Vector3(0, 4f * scale, 0), new Vector3(0.9f, 4f * scale, 0.9f) * 1f, new Color(0.45f, 0.36f, 0.25f));
            Part(PrimitiveType.Sphere, "Canopy", t, new Vector3(0, 8.5f * scale, 0), new Vector3(6f, 3.5f, 6f) * scale, new Color(0.3f, 0.6f, 0.35f));
            if (!house) return;
            // 나무 위 집
            Part(PrimitiveType.Cylinder, "TreeHouse", t, new Vector3(0, 5.5f * scale, 0), new Vector3(2.6f, 0.8f, 2.6f), new Color(0.82f, 0.85f, 0.7f));
        }

        TribeMember Spawn(Village v, Vector3 wanted, bool soldier, bool warParty)
        {
            GroundAt(wanted, 6f, out var at);
            var root = new GameObject($"{v.tribe}{(soldier ? "Soldier" : "Resident")}").transform;
            root.SetParent(_membersRoot, false);
            root.SetPositionAndRotation(at, Quaternion.Euler(0, Random.Range(0f, 360f), 0));

            var m = root.gameObject.AddComponent<TribeMember>();
            m.Tribe = v.tribe;
            m.IsSoldier = soldier;
            m.IsWarParty = warParty;
            m.home = at;
            m.groundY = at.y;
            m.villageCenter = v.center;
            m.phase = Random.Range(0f, 10f);
            m.reload = Random.Range(0.5f, Settings.Combat(v.tribe).reloadMax);
            m.wanderTarget = at;

            var skin = TribeLook.Skin(v.tribe);
            switch (v.tribe)
            {
                case Tribe.Orc:
                    Part(PrimitiveType.Capsule, "Body", root, new Vector3(0, 0.95f, 0), new Vector3(0.8f, 0.95f, 0.65f),
                        soldier ? new Color(0.25f, 0.2f, 0.18f) : new Color(0.45f, 0.35f, 0.25f));
                    Part(PrimitiveType.Sphere, "Head", root, new Vector3(0, 2.05f, 0.05f), Vector3.one * 0.5f, skin);
                    Part(PrimitiveType.Cube, "TuskL", root, new Vector3(-0.12f, 1.92f, 0.28f), new Vector3(0.06f, 0.14f, 0.06f), Color.white);
                    Part(PrimitiveType.Cube, "TuskR", root, new Vector3(0.12f, 1.92f, 0.28f), new Vector3(0.06f, 0.14f, 0.06f), Color.white);
                    break;
                case Tribe.Elf:
                    Part(PrimitiveType.Capsule, "Body", root, new Vector3(0, 0.9f, 0), new Vector3(0.42f, 0.9f, 0.38f),
                        soldier ? new Color(0.2f, 0.45f, 0.3f) : new Color(0.55f, 0.75f, 0.55f));
                    Part(PrimitiveType.Sphere, "Head", root, new Vector3(0, 1.95f, 0), Vector3.one * 0.34f, skin);
                    Part(PrimitiveType.Cube, "EarL", root, new Vector3(-0.2f, 2.02f, 0), new Vector3(0.18f, 0.06f, 0.06f), skin)
                        .transform.localRotation = Quaternion.Euler(0, 0, -25f);
                    Part(PrimitiveType.Cube, "EarR", root, new Vector3(0.2f, 2.02f, 0), new Vector3(0.18f, 0.06f, 0.06f), skin)
                        .transform.localRotation = Quaternion.Euler(0, 0, 25f);
                    Part(PrimitiveType.Sphere, "Hair", root, new Vector3(0, 2.05f, -0.06f), new Vector3(0.36f, 0.3f, 0.36f), new Color(0.98f, 0.86f, 0.45f));
                    break;
                default:
                    var tunics = new[] { new Color(0.55f, 0.2f, 0.18f), new Color(0.25f, 0.35f, 0.6f), new Color(0.5f, 0.4f, 0.25f) };
                    Part(PrimitiveType.Capsule, "Body", root, new Vector3(0, 0.75f, 0), new Vector3(0.55f, 0.75f, 0.45f),
                        soldier ? new Color(0.45f, 0.47f, 0.52f) : tunics[Random.Range(0, tunics.Length)]);
                    Part(PrimitiveType.Sphere, "Head", root, new Vector3(0, 1.7f, 0), Vector3.one * 0.38f, skin);
                    if (soldier) Part(PrimitiveType.Sphere, "Helmet", root, new Vector3(0, 1.8f, 0), new Vector3(0.42f, 0.25f, 0.42f), new Color(0.6f, 0.62f, 0.66f));
                    break;
            }

            if (soldier)
            {
                var weapon = new GameObject("Weapon").transform;
                weapon.SetParent(root, false);
                float h = v.tribe == Tribe.Orc ? 1.6f : v.tribe == Tribe.Elf ? 1.5f : 1.3f;
                weapon.localPosition = new Vector3(0.35f, h, 0.15f);
                weapon.localRotation = Quaternion.Euler(60f, 0, 0);
                if (v.tribe == Tribe.Orc)
                {
                    Part(PrimitiveType.Cube, "Shaft", weapon, new Vector3(0, 0, 0.6f), new Vector3(0.1f, 0.1f, 2f), new Color(0.3f, 0.2f, 0.12f));
                    m.tip = Part(PrimitiveType.Cube, "SpearTip", weapon, new Vector3(0, 0, 1.7f), new Vector3(0.2f, 0.06f, 0.35f), TipIdle).GetComponent<Renderer>();
                }
                else
                {
                    float len = v.tribe == Tribe.Elf ? 1.5f : 1.1f;
                    Part(PrimitiveType.Cube, "Limb", weapon, new Vector3(0, 0, 0.35f), new Vector3(0.06f, len, 0.06f),
                        v.tribe == Tribe.Elf ? new Color(0.85f, 0.8f, 0.6f) : new Color(0.4f, 0.27f, 0.12f));
                    m.tip = Part(PrimitiveType.Cube, "ArrowTip", weapon, new Vector3(0, 0, 0.75f), new Vector3(0.12f, 0.12f, 0.25f), TipIdle).GetComponent<Renderer>();
                }
                m.weapon = weapon;
            }
            _members.Add(m);
            return m;
        }

        // ───────── 프리미티브 ─────────

        GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            DestroyImmediate(go.GetComponent<Collider>()); // 드래곤 SphereCast·배치 Raycast에 걸리지 않게 즉시 제거
            if (parent) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = Mat;
            Tint(r, color);
            return go;
        }

        void Tint(Renderer r, Color c)
        {
            if (!r) return;
            _block ??= new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, c);
            _block.SetColor(ColorId, c);
            r.SetPropertyBlock(_block);
        }

        static Material Mat
        {
            get
            {
                if (_mat) return _mat;
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) shader = Shader.Find("Standard");
                _mat = new Material(shader) { name = "TribeTemp" };
                if (_mat.HasProperty("_Smoothness")) _mat.SetFloat("_Smoothness", 0.1f);
                return _mat;
            }
        }

        static Material TrailMat
        {
            get
            {
                if (_trailMat) return _trailMat;
                var shader = Shader.Find("Sprites/Default");
                if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
                _trailMat = new Material(shader) { name = "TribeTrailTemp" };
                return _trailMat;
            }
        }
    }
}
