using System.Collections.Generic;
using DragonRecipe.Core;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DragonRecipe.Hostile
{
    /// <summary>
    /// [임시] 비행 구역에 드래곤을 공격하는 적을 배치한다.
    /// - 사냥꾼 무리: 땅에 모여 있다가 드래곤이 다가오면 시위를 당겨(활이 들리고 화살촉이 빨개짐) 앞질러 쏜다.
    ///   드래곤이 낮게 덮치면 흩어져 달아난다.
    /// - 날개 몬스터: 자기 자리 상공을 맴돌다 드래곤이 다가오면 눈이 빨개지며 쫓아와 문다. 물고 나면 잠깐 물러난다.
    /// 프리미티브로만 만든 자리표시용이고 콜라이더가 없어 드래곤 충돌 판정에 걸리지 않는다.
    /// 씬에 없으면 비행 씬(FlightSession)이 열릴 때 스스로 붙는다.
    /// </summary>
    public partial class HostileSpawner : MonoBehaviour
    {
        public FlightSession session;
        [Tooltip("배치 구역을 따라갈 먹이 스포너 (비우면 session.spawner)")]
        public PreySpawner area;
        public HostileParams settings = new HostileParams();

        class Hunter
        {
            public Transform root;
            public Transform bow;
            public Renderer tip;
            public Vector3 home;
            public float groundY;
            public float reload;
            public float aim; // > 0: 시위 당기는 중
            public float fleeUntil;
            public Vector3 fleeDir;
        }

        class Monster
        {
            public Transform root;
            public Transform wingL, wingR;
            public Renderer[] eyes;
            public Vector3 home;
            public Vector3 forward;
            public float retreatUntil;
            public float circlePhase;
            public bool chasing;
        }

        class Arrow
        {
            public Transform t;
            public Vector3 velocity;
            public float age;
            public bool stuck;
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly Color TipIdle = new Color(0.55f, 0.55f, 0.6f);
        static readonly Color TipAim = new Color(1f, 0.15f, 0.1f);
        static readonly Color EyeIdle = new Color(1f, 0.85f, 0.3f);
        static readonly Color EyeAngry = new Color(1f, 0.05f, 0.05f);
        static readonly Color[] Tunics =
        {
            new Color(0.55f, 0.2f, 0.18f), new Color(0.25f, 0.35f, 0.6f),
            new Color(0.3f, 0.45f, 0.25f), new Color(0.5f, 0.4f, 0.25f)
        };

        static Material _mat;
        static Material _trailMat;

        readonly List<Hunter> _hunters = new List<Hunter>();
        readonly List<Monster> _monsters = new List<Monster>();
        readonly List<Arrow> _arrows = new List<Arrow>();
        MaterialPropertyBlock _block;
        bool _built;

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
            if (FindAnyObjectByType<HostileSpawner>()) return;
            var session = FindAnyObjectByType<FlightSession>();
            if (!session) return;
            var go = new GameObject("Hostiles");
            go.AddComponent<HostileSpawner>().session = session;
        }

        // ───────── 수명 ─────────

        void Start()
        {
            if (!session) session = FindAnyObjectByType<FlightSession>();
            if (!session) return;
            if (!area) area = session.spawner;
            session.StateChanged += OnStateChanged;
            Build();
        }

        void OnDestroy()
        {
            if (session) session.StateChanged -= OnStateChanged;
        }

        void OnStateChanged(FlightSession.State s)
        {
            if (s == FlightSession.State.Ready) ResetAll(); // 다시 날기: 처음 자리로
        }

        /// <summary>모든 적을 처음 자리로 되돌리고 날아가던 화살을 지운다</summary>
        public void ResetAll()
        {
            if (!_built) return;
            foreach (var a in _arrows)
                if (a.t) Destroy(a.t.gameObject);
            _arrows.Clear();
            ResetSkillStatus();
            foreach (var h in _hunters)
            {
                h.root.position = new Vector3(h.home.x, h.groundY, h.home.z);
                h.aim = 0f;
                h.fleeUntil = -1f;
                h.reload = Random.Range(0f, settings.hunterReloadMax);
            }
            foreach (var m in _monsters)
            {
                m.root.position = m.home;
                m.forward = Vector3.forward;
                m.retreatUntil = -1f;
                m.chasing = false;
            }
        }

        void Update()
        {
            if (!_built || !session || GamePause.IsPaused) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            var dragon = session.dragon;
            bool active = session.Current == FlightSession.State.Flying && dragon && dragon.IsFlying;
            Vector3 target = dragon ? dragon.Model.Position : Vector3.zero;
            Vector3 targetVel = dragon ? dragon.Model.Velocity : Vector3.zero;
            float hitRadius = dragon ? dragon.collisionRadius * dragon.transform.localScale.x + 0.5f : 1f;

            foreach (var h in _hunters)
            {
                float hd = dt;
                if (!SkillHoldHunter(h, ref hd)) UpdateHunter(h, hd, active, target, targetVel); // 스킬 기절·잠·쓰러짐·둔화
            }
            foreach (var m in _monsters)
            {
                float md = dt;
                if (!SkillHoldMonster(m, ref md)) UpdateMonster(m, md, active, dragon, target, hitRadius);
            }
            UpdateArrows(dt, active, dragon, target, hitRadius);
        }

        // ───────── 사냥꾼 ─────────

        void UpdateHunter(Hunter h, float dt, bool active, Vector3 target, Vector3 targetVel)
        {
            var p = settings;
            Vector3 pos = h.root.position;

            if (active && HostileRules.Frightened(pos, target, p) && Time.time >= h.fleeUntil)
            {
                // 낮게 덮치면 겁먹고 흩어진다
                Vector3 away = pos - target;
                away.y = 0f;
                var rnd = Random.insideUnitCircle.normalized;
                h.fleeDir = away.sqrMagnitude > 0.01f ? away.normalized : new Vector3(rnd.x, 0f, rnd.y);
                h.fleeUntil = Time.time + p.hunterFleeTime;
                h.aim = 0f;
            }

            Vector3 move = Vector3.zero;
            float speed = 0f;
            Vector3 face = h.root.forward;
            bool fleeing = Time.time < h.fleeUntil;
            Vector3 toTarget = target - pos;

            if (fleeing)
            {
                move = h.fleeDir;
                speed = p.hunterFleeSpeed;
                face = move;
            }
            else if (active && toTarget.sqrMagnitude <= p.hunterRange * p.hunterRange)
            {
                face = new Vector3(toTarget.x, 0f, toTarget.z);
                if (h.aim > 0f)
                {
                    h.aim -= dt;
                    if (h.aim <= 0f)
                    {
                        Fire(h, target, targetVel);
                        h.reload = Random.Range(p.hunterReloadMin, p.hunterReloadMax);
                    }
                }
                else
                {
                    h.reload -= dt;
                    if (h.reload <= 0f) h.aim = p.hunterAimTime;
                }
            }
            else
            {
                h.aim = 0f;
                // 무리 자리로 천천히 돌아간다
                Vector3 back = h.home - pos;
                back.y = 0f;
                if (back.magnitude > 1f)
                {
                    move = back.normalized;
                    speed = p.hunterWalkSpeed;
                    face = move;
                }
            }

            if (speed > 0f)
            {
                pos += move * speed * dt;
                if (area) pos = area.ClampToArea(pos);
                pos.y = h.groundY;
                h.root.position = pos;
            }
            if (face.sqrMagnitude > 0.001f)
                h.root.rotation = Quaternion.Slerp(h.root.rotation, Quaternion.LookRotation(face), 6f * dt);

            // 시위를 당기는 동안 활이 들리고 화살촉이 빨개진다
            float draw = h.aim > 0f ? 1f - h.aim / Mathf.Max(0.01f, p.hunterAimTime) : 0f;
            if (h.bow)
            {
                float pitch = draw > 0f && active ? -Mathf.Clamp(Mathf.Atan2(toTarget.y, new Vector2(toTarget.x, toTarget.z).magnitude) * Mathf.Rad2Deg, -10f, 80f) : 60f;
                h.bow.localRotation = Quaternion.Slerp(h.bow.localRotation, Quaternion.Euler(pitch, 0f, 0f), 10f * dt);
            }
            if (h.tip) SetColor(h.tip, Color.Lerp(TipIdle, TipAim, draw));
        }

        void Fire(Hunter h, Vector3 target, Vector3 targetVel)
        {
            var p = settings;
            Vector3 from = h.bow ? h.bow.position : h.root.position + Vector3.up * 1.4f;
            HostileRules.LeadDirection(from, target, targetVel, p.arrowSpeed, out var dir);
            dir = Quaternion.Euler(Random.Range(-p.arrowSpread, p.arrowSpread), Random.Range(-p.arrowSpread, p.arrowSpread), 0f) * dir;

            var go = Part(PrimitiveType.Cube, "Arrow", null, Vector3.zero, new Vector3(0.07f, 0.07f, 1.1f), new Color(0.35f, 0.25f, 0.15f));
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(dir));
            go.transform.SetParent(transform, true);
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = TrailMat;
            trail.time = 0.25f;
            trail.widthMultiplier = 0.12f;
            trail.startColor = new Color(1f, 0.9f, 0.8f, 0.8f);
            trail.endColor = new Color(1f, 0.9f, 0.8f, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _arrows.Add(new Arrow { t = go.transform, velocity = dir * p.arrowSpeed });
        }

        void UpdateArrows(float dt, bool active, DragonFlightController dragon, Vector3 target, float hitRadius)
        {
            for (int i = _arrows.Count - 1; i >= 0; i--)
            {
                var a = _arrows[i];
                if (!a.t)
                {
                    _arrows.RemoveAt(i);
                    continue;
                }
                a.age += dt;
                if (a.age >= settings.arrowLifetime)
                {
                    Destroy(a.t.gameObject);
                    _arrows.RemoveAt(i);
                    continue;
                }
                if (a.stuck) continue;

                Vector3 from = a.t.position;
                Vector3 to = from + a.velocity * dt;
                if (active && HostileRules.SegmentDistance(from, to, target) <= hitRadius)
                {
                    // 명중: 체력 감소 + 몸이 움찔 (체력 바가 흔들리는 건 HUD가 처리)
                    dragon.ApplyDamage(settings.arrowDamage);
                    if (dragon.IsFlying) dragon.Model.Jolt(Random.Range(-8f, 8f), Random.Range(-4f, 2f), 0f);
                    Destroy(a.t.gameObject);
                    _arrows.RemoveAt(i);
                    continue;
                }
                if (Physics.Linecast(from, to, out var hit, ~0, QueryTriggerInteraction.Ignore))
                {
                    // 땅이나 나무에 꽂힘
                    a.t.position = hit.point;
                    a.stuck = true;
                    a.age = Mathf.Max(a.age, settings.arrowLifetime - 1.5f);
                    continue;
                }
                a.t.position = to;
            }
        }

        // ───────── 날개 몬스터 ─────────

        void UpdateMonster(Monster m, float dt, bool active, DragonFlightController dragon, Vector3 target, float hitRadius)
        {
            var p = settings;
            Vector3 pos = m.root.position;
            bool retreating = Time.time < m.retreatUntil;
            m.chasing = active && !retreating && HostileRules.ShouldChase(pos, m.home, target, p);

            Vector3 desired;
            float speed;
            if (m.chasing)
            {
                Vector3 to = target - pos;
                desired = to;
                speed = HostileRules.ChaseSpeed(to.magnitude, p);
                if (to.magnitude <= p.biteRange + hitRadius)
                {
                    dragon.ApplyDamage(p.biteDamage);
                    if (dragon.IsFlying) dragon.Model.Jolt(Random.value < 0.5f ? -20f : 20f, -6f, 0f);
                    m.retreatUntil = Time.time + p.monsterRetreatTime;
                }
            }
            else if (retreating)
            {
                // 물고 나서 위로 빠져나간다
                desired = (pos - target).normalized + Vector3.up;
                speed = p.monsterChaseSpeed;
            }
            else
            {
                // 자기 자리 상공을 맴돈다
                m.circlePhase += dt * p.monsterIdleSpeed / Mathf.Max(1f, p.monsterCircleRadius);
                Vector3 ring = m.home + new Vector3(Mathf.Cos(m.circlePhase), 0f, Mathf.Sin(m.circlePhase)) * p.monsterCircleRadius;
                desired = ring - pos;
                speed = Vector3.Distance(pos, m.home) > p.monsterCircleRadius * 2f ? p.monsterChaseSpeed : p.monsterIdleSpeed;
            }

            m.forward = HostileRules.Steer(m.forward, desired, p.monsterTurnRate * dt);
            pos += m.forward * speed * dt;
            // 땅 속으로 파고들지 않게
            if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out var ground, 100f, ~0, QueryTriggerInteraction.Ignore))
                pos.y = Mathf.Max(pos.y, ground.point.y + 1.5f);
            m.root.position = pos;
            m.root.rotation = Quaternion.LookRotation(m.forward);

            // 날갯짓: 쫓을 때 더 빠르게
            float flap = Mathf.Sin(Time.time * (m.chasing ? 18f : 9f) + m.circlePhase) * 40f;
            if (m.wingL) m.wingL.localRotation = Quaternion.Euler(0f, 0f, flap);
            if (m.wingR) m.wingR.localRotation = Quaternion.Euler(0f, 0f, -flap);
            var eye = m.chasing ? EyeAngry : EyeIdle;
            foreach (var e in m.eyes) SetColor(e, eye);
        }

        // ───────── 배치 ─────────

        void Build()
        {
            if (_built) return;
            _built = true;
            var p = settings;
            for (int g = 0; g < p.hunterGroups; g++)
            {
                if (!FindGround(out var center)) continue;
                int size = Random.Range(p.hunterGroupMin, p.hunterGroupMax + 1);
                MakeCamp(center);
                for (int i = 0; i < size; i++)
                {
                    var off = Random.insideUnitCircle * p.hunterGroupSpread;
                    var at = center + new Vector3(off.x, 0f, off.y);
                    _hunters.Add(MakeHunter(at, center.y));
                }
            }
            for (int i = 0; i < p.monsterCount; i++)
            {
                if (!FindGround(out var g)) continue;
                _monsters.Add(MakeMonster(g + Vector3.up * p.monsterHoverHeight));
            }
            ResetAll();
        }

        bool FindGround(out Vector3 point)
        {
            Vector3 center = area ? area.areaCenter : new Vector3(0, 0, 200);
            Vector2 half = (area ? area.areaSize : new Vector2(300, 340)) * 0.5f;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                var p = new Vector3(center.x + Random.Range(-half.x, half.x), 200f, center.z + Random.Range(-half.y, half.y));
                if (new Vector2(p.x, p.z).magnitude < settings.keepOutRadius) continue;
                if (!Physics.Raycast(p, Vector3.down, out var hit, 400f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.95f || hit.point.y > 1f) continue; // 나무/바위/절벽 위는 제외
                point = hit.point;
                return true;
            }
            point = default;
            return false;
        }

        void MakeCamp(Vector3 at)
        {
            var camp = new GameObject("HunterCamp").transform;
            camp.SetParent(transform, false);
            camp.position = at;
            // 모닥불과 천막
            Part(PrimitiveType.Cube, "Fire", camp, new Vector3(0, 0.25f, 0), new Vector3(0.6f, 0.5f, 0.6f), new Color(1f, 0.5f, 0.1f))
                .transform.localRotation = Quaternion.Euler(0, 45f, 0);
            var tent = Part(PrimitiveType.Cube, "Tent", camp, new Vector3(3f, 0.9f, 2f), new Vector3(2.2f, 2.2f, 3f), new Color(0.75f, 0.68f, 0.5f));
            tent.transform.localRotation = Quaternion.Euler(0, 20f, 45f);
        }

        Hunter MakeHunter(Vector3 at, float groundY)
        {
            var root = new GameObject("Hunter").transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(new Vector3(at.x, groundY, at.z), Quaternion.Euler(0, Random.Range(0f, 360f), 0));
            var tunic = Tunics[Random.Range(0, Tunics.Length)];
            Part(PrimitiveType.Capsule, "Body", root, new Vector3(0, 0.75f, 0), new Vector3(0.55f, 0.75f, 0.45f), tunic);
            Part(PrimitiveType.Sphere, "Head", root, new Vector3(0, 1.7f, 0), Vector3.one * 0.38f, new Color(0.93f, 0.75f, 0.6f));

            // 활 (팔 위치를 축으로 들어 올린다)
            var bow = new GameObject("Bow").transform;
            bow.SetParent(root, false);
            bow.localPosition = new Vector3(0.3f, 1.3f, 0.15f);
            bow.localRotation = Quaternion.Euler(60f, 0, 0);
            Part(PrimitiveType.Cube, "Limb", bow, new Vector3(0, 0, 0.35f), new Vector3(0.06f, 1.1f, 0.06f), new Color(0.4f, 0.27f, 0.12f));
            var tip = Part(PrimitiveType.Cube, "ArrowTip", bow, new Vector3(0, 0, 0.75f), new Vector3(0.12f, 0.12f, 0.25f), TipIdle);

            return new Hunter { root = root, bow = bow, tip = tip.GetComponent<Renderer>(), home = root.position, groundY = groundY };
        }

        Monster MakeMonster(Vector3 home)
        {
            var root = new GameObject("WingedMonster").transform;
            root.SetParent(transform, false);
            root.position = home;
            var body = new Color(0.28f, 0.16f, 0.32f);
            Part(PrimitiveType.Sphere, "Body", root, Vector3.zero, new Vector3(1.2f, 1f, 1.6f), body);
            Part(PrimitiveType.Sphere, "Head", root, new Vector3(0, 0.25f, 0.95f), Vector3.one * 0.8f, body);
            var eyes = new[]
            {
                Part(PrimitiveType.Sphere, "EyeL", root, new Vector3(-0.2f, 0.4f, 1.3f), Vector3.one * 0.2f, EyeIdle).GetComponent<Renderer>(),
                Part(PrimitiveType.Sphere, "EyeR", root, new Vector3(0.2f, 0.4f, 1.3f), Vector3.one * 0.2f, EyeIdle).GetComponent<Renderer>()
            };
            var wingColor = new Color(0.2f, 0.1f, 0.22f);
            var wingL = new GameObject("WingL").transform;
            wingL.SetParent(root, false);
            wingL.localPosition = new Vector3(-0.5f, 0.2f, 0);
            Part(PrimitiveType.Cube, "Membrane", wingL, new Vector3(-1.2f, 0, 0), new Vector3(2.4f, 0.08f, 1.2f), wingColor);
            var wingR = new GameObject("WingR").transform;
            wingR.SetParent(root, false);
            wingR.localPosition = new Vector3(0.5f, 0.2f, 0);
            Part(PrimitiveType.Cube, "Membrane", wingR, new Vector3(1.2f, 0, 0), new Vector3(2.4f, 0.08f, 1.2f), wingColor);

            return new Monster
            {
                root = root, wingL = wingL, wingR = wingR, eyes = eyes, home = home,
                forward = Vector3.forward, circlePhase = Random.Range(0f, Mathf.PI * 2f)
            };
        }

        GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            // 즉시 제거: 드래곤 SphereCast·배치 Raycast에 걸리지 않게
            DestroyImmediate(go.GetComponent<Collider>());
            if (parent) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = Mat;
            SetColor(r, color);
            return go;
        }

        void SetColor(Renderer r, Color c)
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
                _mat = new Material(shader) { name = "HostileTemp" };
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
                _trailMat = new Material(shader) { name = "ArrowTrailTemp" };
                return _trailMat;
            }
        }
    }
}
