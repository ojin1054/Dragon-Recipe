using System.Collections.Generic;
using DragonRecipe.Care;
using DragonRecipe.Core;
using DragonRecipe.Food;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DragonRecipe.Tribes
{
    /// <summary>
    /// 종족이 기르는 가축과 목동.
    /// 종족 땅(마을 둘레 territoryRadius)마다 목동이 가축 무리를 몰고 다닌다. 울타리 없이 그 땅 안을 옮겨 다닌다.
    /// 가축은 몸에 종족 색 띠를 두른다 (야생 동물과 구분).
    /// 드래곤이 가축을 낚아채면 그 종족의 호감도가 떨어지고, 목동이 지팡이를 흔들며 화낸다.
    /// - 사람: 양치기(양), 소몰이(소)
    /// - 오크: 멧돼지 몰이꾼(멧돼지), 들소 몰이꾼(들소)
    /// - 엘프: 사슴지기(사슴), 달사슴지기(달사슴)
    /// 비행 씬에 스스로 붙는다. 전쟁 맵에는 세우지 않는다.
    /// </summary>
    public class TribeHerds : MonoBehaviour
    {
        [System.Serializable]
        public class HerdDef
        {
            public Tribe tribe;
            public string foodId;
            public int count = 3;
            [Tooltip("한 마리 낚아챌 때마다 깎이는 호감도")]
            public float reputationLoss = 4f;
            [Tooltip("처음 자리: 마을에서 구역 가운데 쪽을 0°로 한 방향 (deg)")]
            public float angle;
            [Tooltip("처음 자리: 마을에서 떨어진 거리 (종족 땅 반경 대비 비율)")]
            [Range(0f, 1f)] public float distance = 0.7f;
        }

        public FlightSession session;
        [Tooltip("무리가 모여 있는 반경")]
        public float herdRadius = 8f;
        [Tooltip("무리가 돌아다니는 땅의 반경. 0이면 종족 땅 반경(TribeSettings.territoryRadius)")]
        public float regionRadius = 0f;
        [Tooltip("마을 한가운데(집들)에는 들어가지 않는다")]
        public float villageKeepOut = 14f;
        [Tooltip("한 자리에 머무는 시간 (초, 최소~최대)")]
        public Vector2 grazeTime = new Vector2(10f, 22f);
        [Tooltip("무리가 옮겨 가는 속도의 상한 (m/s). 느린 가축은 제 걸음에 맞춘다")]
        public float roamSpeed = 1.1f;
        [Tooltip("가축을 빼앗긴 목동이 화내는 시간")]
        public float angryTime = 3.5f;
        public List<HerdDef> herds = new List<HerdDef>
        {
            new HerdDef { tribe = Tribe.Human, foodId = "sheep", count = 4, reputationLoss = 3f, angle = -55f },
            new HerdDef { tribe = Tribe.Human, foodId = "cow", count = 3, reputationLoss = 5f, angle = 55f },
            new HerdDef { tribe = Tribe.Orc, foodId = "boar", count = 3, reputationLoss = 4f, angle = -55f },
            new HerdDef { tribe = Tribe.Orc, foodId = "bison", count = 3, reputationLoss = 6f, angle = 55f },
            new HerdDef { tribe = Tribe.Elf, foodId = "deer", count = 3, reputationLoss = 4f, angle = -55f },
            new HerdDef { tribe = Tribe.Elf, foodId = "moon_deer", count = 3, reputationLoss = 6f, angle = 55f },
        };

        /// <summary>한 무리: 목동 + 가축</summary>
        public class Herd
        {
            public HerdDef def;
            /// <summary>무리가 지금 모여 있는 자리 (옮겨 다닌다)</summary>
            public Vector3 center;
            /// <summary>무리가 다니는 땅 (마을 가운데, 반경)</summary>
            public Vector3 regionCenter;
            public float regionRadius;
            /// <summary>지금 향하는 풀밭</summary>
            public Vector3 roamTarget;
            internal float nextRoam;
            internal float speed;
            public Transform herder;
            public Transform staff;
            public readonly List<Prey> animals = new List<Prey>();
            internal float angryUntil = -1f;
            internal Vector3 target;
            internal float nextWander;
            internal float phase;
            internal float groundY;
        }

        public static TribeHerds Instance { get; private set; }

        readonly List<Herd> _herds = new List<Herd>();
        bool _built;

        public IReadOnlyList<Herd> Herds => _herds;
        public bool IsBuilt => _built;
        /// <summary>이번 비행에서 빼앗은 가축 수</summary>
        public int Stolen { get; private set; }

        /// <summary>가축 띠·목동 옷에 쓰는 종족 색</summary>
        public static Color MarkColor(Tribe t) => t switch
        {
            Tribe.Orc => new Color(0.8f, 0.16f, 0.12f),
            Tribe.Elf => new Color(0.25f, 0.85f, 0.75f),
            _ => new Color(0.2f, 0.42f, 0.9f)
        };

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
            if (FindAnyObjectByType<TribeHerds>()) return;
            var session = FindAnyObjectByType<FlightSession>();
            if (!session) return;
            new GameObject("TribeHerds").AddComponent<TribeHerds>().session = session;
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (session)
            {
                session.Snatched -= OnSnatched;
                session.StateChanged -= OnStateChanged;
            }
        }

        void Start()
        {
            if (!session) session = FindAnyObjectByType<FlightSession>();
            if (!session) return;
            session.Snatched += OnSnatched;
            session.StateChanged += OnStateChanged;
        }

        void OnStateChanged(FlightSession.State s)
        {
            if (s != FlightSession.State.Ready) return;
            Stolen = 0;
            foreach (var h in _herds) h.angryUntil = -1f;
        }

        void Update()
        {
            if (!_built) TryBuild();
            if (!_built) return;
            float dt = Time.deltaTime;
            Vector3 dragon = session && session.dragon ? session.dragon.transform.position : Vector3.zero;
            foreach (var h in _herds)
            {
                UpdateRoam(h, dt);
                UpdateHerder(h, dt, dragon);
            }
        }

        // ───────── 세우기 ─────────

        void TryBuild()
        {
            var world = TribeWorld.Instance;
            if (!world || !session || !session.spawner) return; // 전쟁 맵에는 마을이 없어 아래에서 걸러진다
            var spawner = session.spawner;
            if (spawner.All.Count == 0) return; // 야생 먹이가 먼저 놓인 뒤에
            if (!world.TryGetVillageCenter(Tribe.Human, out _) && !world.TryGetVillageCenter(Tribe.Orc, out _)
                && !world.TryGetVillageCenter(Tribe.Elf, out _)) return;

            _built = true;
            foreach (var def in herds)
            {
                if (!world.TryGetVillageCenter(def.tribe, out var village)) continue;
                var food = FindFood(def.foodId, spawner);
                if (!food) continue;
                BuildHerd(world, spawner, def, food, village);
            }
        }

        static FoodData FindFood(string id, PreySpawner spawner)
        {
            var cfg = DragonCare.Config;
            var f = cfg ? cfg.FindFood(id) : null;
            if (f) return f;
            if (spawner.foods != null)
                foreach (var x in spawner.foods)
                    if (x && x.id == id) return x;
            return null;
        }

        void BuildHerd(TribeWorld world, PreySpawner spawner, HerdDef def, FoodData food, Vector3 village)
        {
            // 다니는 땅: 마을 둘레 종족 땅
            float region = regionRadius > 0f ? regionRadius : DragonCare.TribeSettings.territoryRadius;
            region = Mathf.Max(region, villageKeepOut + herdRadius + 4f);

            // 처음 자리: 마을에서 구역 가운데 쪽으로 비스듬히 (두 무리가 겹치지 않게 갈라 둔다)
            Vector3 inward = spawner.areaCenter - village;
            inward.y = 0f;
            inward = inward.sqrMagnitude > 1f ? inward.normalized : Vector3.forward;
            Vector3 dir = Quaternion.Euler(0f, def.angle, 0f) * inward;
            var herd = new Herd { def = def, regionCenter = village, regionRadius = region, phase = Random.Range(0f, 10f) };
            if (!GroundNear(spawner, herd, village + dir * (region * def.distance), 20f, out var center)) return;
            herd.center = herd.roamTarget = center;
            herd.groundY = center.y;
            herd.nextRoam = Time.time + Random.Range(grazeTime.x, grazeTime.y) * 0.5f;
            // 느린 가축(소)은 제 걸음보다 빨리 몰지 않는다
            herd.speed = Mathf.Min(roamSpeed, Mathf.Max(0.3f, food.wanderSpeed * 0.8f));
            var color = MarkColor(def.tribe);

            // 가축 (울타리 없이 무리 둘레에 모여 다닌다)
            int herdId = spawner.NewHerdId();
            for (int i = 0; i < def.count; i++)
            {
                var prey = spawner.SpawnOwned(food, def.tribe, center, herdRadius, herdId, color);
                if (prey) herd.animals.Add(prey);
            }

            // 목동
            herd.herder = BuildHerder(world, def.tribe, color, out herd.staff);
            herd.herder.SetParent(transform, false);
            herd.herder.position = center + new Vector3(herdRadius * 0.6f, 0f, 0f);
            herd.target = herd.herder.position;
            _herds.Add(herd);
        }

        Transform[] _ponds;

        /// <summary>연못(장식 원판 'Pond…') 위인지. 무리 둘레만큼 여유를 둔다</summary>
        bool OnPond(Vector3 p)
        {
            if (_ponds == null)
            {
                var list = new List<Transform>();
                var world = GameObject.Find("World");
                if (world)
                    foreach (Transform c in world.transform)
                        if (c.name.StartsWith("Pond")) list.Add(c);
                _ponds = list.ToArray();
            }
            foreach (var pond in _ponds)
            {
                if (!pond) continue;
                float rx = pond.lossyScale.x * 0.5f + herdRadius + 2f;
                float rz = pond.lossyScale.z * 0.5f + herdRadius + 2f;
                float dx = (p.x - pond.position.x) / rx, dz = (p.z - pond.position.z) / rz;
                if (dx * dx + dz * dz < 1f) return true;
            }
            return false;
        }

        /// <summary>무리가 설 수 있는 자리인지: 종족 땅 안, 마을 한가운데 밖</summary>
        bool InRegion(Herd h, Vector3 p)
        {
            Vector3 d = p - h.regionCenter;
            float dist = new Vector2(d.x, d.z).magnitude;
            return dist <= h.regionRadius - herdRadius * 0.5f && dist >= villageKeepOut;
        }

        bool GroundNear(PreySpawner spawner, Herd h, Vector3 wanted, float search, out Vector3 point)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                // 원하는 자리에서 시작해 점점 넓게 찾는다
                var off = attempt == 0 ? Vector2.zero : Random.insideUnitCircle * (search * Mathf.Min(1f, attempt / 30f + 0.2f));
                var p = spawner.ClampToArea(wanted + new Vector3(off.x, 0f, off.y));
                p.y = 200f;
                if (!Physics.Raycast(p, Vector3.down, out var hit, 400f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.95f || hit.point.y > 1f) continue;
                if (OnPond(hit.point) || !InRegion(h, hit.point)) continue;
                point = hit.point;
                return true;
            }
            point = default;
            return false;
        }

        /// <summary>목동: 종족 모습 + 종족 색 모자/어깨띠 + 긴 지팡이</summary>
        static Transform BuildHerder(TribeWorld world, Tribe tribe, Color color, out Transform staff)
        {
            var root = new GameObject($"{tribe}Herder").transform;
            var skin = TribeLook.Skin(tribe);
            float head;
            switch (tribe)
            {
                case Tribe.Orc:
                    world.MakePart(PrimitiveType.Capsule, "Body", root, new Vector3(0, 0.95f, 0), new Vector3(0.8f, 0.95f, 0.65f), new Color(0.4f, 0.3f, 0.22f));
                    world.MakePart(PrimitiveType.Sphere, "Head", root, new Vector3(0, 2.05f, 0.05f), Vector3.one * 0.5f, skin);
                    world.MakePart(PrimitiveType.Cube, "TuskL", root, new Vector3(-0.12f, 1.92f, 0.28f), new Vector3(0.06f, 0.14f, 0.06f), Color.white);
                    world.MakePart(PrimitiveType.Cube, "TuskR", root, new Vector3(0.12f, 1.92f, 0.28f), new Vector3(0.06f, 0.14f, 0.06f), Color.white);
                    head = 2.05f;
                    break;
                case Tribe.Elf:
                    world.MakePart(PrimitiveType.Capsule, "Body", root, new Vector3(0, 0.9f, 0), new Vector3(0.42f, 0.9f, 0.38f), new Color(0.6f, 0.78f, 0.6f));
                    world.MakePart(PrimitiveType.Sphere, "Head", root, new Vector3(0, 1.95f, 0), Vector3.one * 0.34f, skin);
                    world.MakePart(PrimitiveType.Cube, "EarL", root, new Vector3(-0.2f, 2.02f, 0), new Vector3(0.18f, 0.06f, 0.06f), skin)
                        .transform.localRotation = Quaternion.Euler(0, 0, -25f);
                    world.MakePart(PrimitiveType.Cube, "EarR", root, new Vector3(0.2f, 2.02f, 0), new Vector3(0.18f, 0.06f, 0.06f), skin)
                        .transform.localRotation = Quaternion.Euler(0, 0, 25f);
                    head = 1.95f;
                    break;
                default:
                    world.MakePart(PrimitiveType.Capsule, "Body", root, new Vector3(0, 0.75f, 0), new Vector3(0.55f, 0.75f, 0.45f), new Color(0.62f, 0.52f, 0.36f));
                    world.MakePart(PrimitiveType.Sphere, "Head", root, new Vector3(0, 1.7f, 0), Vector3.one * 0.38f, skin);
                    head = 1.7f;
                    break;
            }
            // 종족 색: 넓은 모자와 어깨띠 (가축 띠와 같은 색)
            world.MakePart(PrimitiveType.Cylinder, "HatBrim", root, new Vector3(0, head + 0.2f, 0), new Vector3(0.8f, 0.03f, 0.8f), color);
            world.MakePart(PrimitiveType.Cylinder, "HatTop", root, new Vector3(0, head + 0.3f, 0), new Vector3(0.36f, 0.1f, 0.36f), color);
            world.MakePart(PrimitiveType.Cube, "Sash", root, new Vector3(0, head - 0.55f, 0), new Vector3(0.62f, 0.14f, 0.5f), color);

            // 지팡이: 손 높이에서 위로 뻗고 끝이 굽었다
            staff = new GameObject("Staff").transform;
            staff.SetParent(root, false);
            staff.localPosition = new Vector3(0.42f, head - 0.6f, 0.1f);
            world.MakePart(PrimitiveType.Cube, "Shaft", staff, new Vector3(0, 0.5f, 0), new Vector3(0.08f, 2.3f, 0.08f), new Color(0.42f, 0.28f, 0.14f));
            world.MakePart(PrimitiveType.Cube, "Crook", staff, new Vector3(0, 1.65f, 0.14f), new Vector3(0.08f, 0.08f, 0.34f), new Color(0.42f, 0.28f, 0.14f));
            return root;
        }

        // ───────── 도둑질 ─────────

        void OnSnatched(Prey prey)
        {
            if (!prey || !prey.Owner.HasValue) return;
            var herd = HerdOf(prey);
            float loss = herd != null ? herd.def.reputationLoss : 4f;
            Stolen++;
            if (herd != null) herd.angryUntil = Time.time + angryTime;
            DragonCare.ReportLivestockTheft(prey.Owner.Value, loss);
        }

        public Herd HerdOf(Prey prey)
        {
            foreach (var h in _herds)
                if (h.animals.Contains(prey)) return h;
            return null;
        }

        /// <summary>이 목동이 지금 화내는 중인지</summary>
        public bool IsAngry(Herd h) => h != null && Time.time < h.angryUntil;

        // ───────── 무리 이동 ─────────

        /// <summary>한 자리에서 풀을 뜯다가, 종족 땅 안의 다른 자리로 천천히 옮겨 간다</summary>
        void UpdateRoam(Herd h, float dt)
        {
            if (Time.time >= h.nextRoam)
            {
                h.nextRoam = Time.time + Random.Range(grazeTime.x, grazeTime.y);
                var spawner = session ? session.spawner : null;
                // 종족 땅 어디든: 마을 가운데 기준으로 넓게 찾는다
                if (spawner && GroundNear(spawner, h, h.regionCenter + RandomRing(villageKeepOut, h.regionRadius), h.regionRadius, out var next))
                    h.roamTarget = next;
            }
            Vector3 to = h.roamTarget - h.center;
            to.y = 0f;
            float step = h.speed * dt;
            if (to.magnitude > step) h.center += to.normalized * step;
            else h.center = new Vector3(h.roamTarget.x, h.center.y, h.roamTarget.z);
            foreach (var a in h.animals)
                if (a) a.MoveHome(h.center);
        }

        static Vector3 RandomRing(float min, float max)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(Random.Range(min * min, max * max));
            return new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }

        /// <summary>디버그·테스트: 이 무리를 지금 바로 다른 자리로 보낸다</summary>
        public void RoamNow(Herd h)
        {
            if (h != null) h.nextRoam = 0f;
        }

        // ───────── 목동 움직임 ─────────

        void UpdateHerder(Herd h, float dt, Vector3 dragon)
        {
            if (!h.herder) return;
            Vector3 pos = h.herder.position;
            pos.y = h.groundY;
            bool angry = Time.time < h.angryUntil;
            float hop = 0f;

            if (angry)
            {
                // 드래곤 쪽을 보며 펄쩍 뛰고 지팡이를 흔든다
                Vector3 to = dragon - pos;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f)
                    h.herder.rotation = Quaternion.Slerp(h.herder.rotation, Quaternion.LookRotation(to), 10f * dt);
                float t = Time.time * 11f + h.phase;
                hop = Mathf.Abs(Mathf.Sin(t)) * 0.35f;
                if (h.staff) h.staff.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.3f) * 45f - 20f, 0f, Mathf.Cos(t) * 20f);
            }
            else
            {
                // 무리 곁을 느긋하게 거닌다 (무리가 옮겨 가면 따라간다)
                if (Time.time >= h.nextWander || (h.target - h.center).sqrMagnitude > herdRadius * herdRadius)
                {
                    var off = Random.insideUnitCircle * (herdRadius * 0.8f);
                    h.target = h.center + new Vector3(off.x, 0f, off.y);
                    h.nextWander = Time.time + Random.Range(3f, 7f);
                }
                Vector3 to = h.target - pos;
                to.y = 0f;
                if (to.magnitude > 0.3f)
                {
                    pos += to.normalized * (Mathf.Max(0.9f, h.speed * 1.4f) * dt);
                    h.herder.rotation = Quaternion.Slerp(h.herder.rotation, Quaternion.LookRotation(to), 5f * dt);
                    hop = Mathf.Abs(Mathf.Sin(Time.time * 5f + h.phase)) * 0.05f;
                }
                if (h.staff) h.staff.localRotation = Quaternion.Slerp(h.staff.localRotation, Quaternion.identity, 6f * dt);
            }
            h.herder.position = new Vector3(pos.x, h.groundY + hop, pos.z);
        }
    }
}
