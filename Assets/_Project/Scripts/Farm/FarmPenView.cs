using System.Collections.Generic;
using DragonRecipe.Care;
using DragonRecipe.Food;
using UnityEngine;

namespace DragonRecipe.Farm
{
    /// <summary>
    /// 둥지 우리: 농장에 있는 동물을 울타리 안에 풀어놓고 돌아다니게 한다.
    /// 새로 들어온 동물은 톡 튀어나오고, 먹이거나 조합에 넣으면 줄어들며 사라진다.
    /// 우리를 넓히면(상점 확장 단계) 울타리가 커지고 꾸밈이 늘어난다.
    /// </summary>
    public class FarmPenView : MonoBehaviour
    {
        [Tooltip("우리 바닥 가운데. 동물은 이 높이에 선다")]
        public Transform area;
        public Material animalMaterial;
        [Tooltip("울타리·꾸밈 재질 (비우면 동물 재질을 쓴다). 색은 부분마다 따로 칠한다")]
        public Material penMaterial;

        [Header("확장 단계 (0 = 처음, 상점에서 넓힐 때마다 다음 단계)")]
        public List<PenLevel> levels = new List<PenLevel>
        {
            new PenLevel { half = new Vector2(2.6f, 2.1f), rails = 1, hay = true },
            new PenLevel { half = new Vector2(3.3f, 2.7f), rails = 2, hay = true, trough = true },
            new PenLevel { half = new Vector2(4.1f, 3.4f), rails = 2, paintedRails = true, gate = true, hay = true, trough = true },
            new PenLevel { half = new Vector2(5f, 4.1f), rails = 2, paintedRails = true, stoneBase = true, gate = true, hay = true,
                trough = true, shed = true, flowers = true, lanterns = true }
        };
        [Tooltip("동물이 울타리에서 떨어져 다니는 거리")]
        public float roamMargin = 0.6f;

        [Header("농장을 열었을 때 카메라 자리 (우리 크기에 맞춰 옮김)")]
        public Transform cameraSpot;
        [Tooltip("우리 가로 반지름 4일 때의 카메라 위치 (우리 가운데 기준). 우리가 크면 그만큼 멀어진다")]
        public Vector3 cameraOffset = new Vector3(6f, 7.5f, -12f);
        [Tooltip("우리를 화면 왼쪽에 두려고 오른쪽으로 돌리는 각도")]
        public float cameraYaw = 16f;
        [Tooltip("사냥터의 먹이보다 작게 (우리 크기에 맞춤)")]
        public float animalScale = 0.7f;
        public float walkSpeed = 0.9f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly Color RareColor = new Color(1f, 0.82f, 0.2f);
        static readonly Color SparkleColor = new Color(1f, 0.95f, 0.5f);

        class PenAnimal
        {
            public string uid;
            public Transform root;
            public Transform body;
            public Transform sparkle;
            public float height;
            public Vector3 target;
            public float wait;
            public float speed;
            public float pop;      // 0 → 1 나타남
            public bool leaving;
            public bool hops;
            public float phase;
        }

        readonly Dictionary<string, PenAnimal> _animals = new Dictionary<string, PenAnimal>();
        readonly List<string> _gone = new List<string>();
        MaterialPropertyBlock _block;
        bool _first = true;
        float _nextSync;
        int _builtLevel = -1;
        Transform _art;
        float _artPop = 1f;
        Vector2 _roam = new Vector2(2f, 1.5f);

        /// <summary>지금 우리 단계 (levels 범위로 자름)</summary>
        public PenLevel CurrentLevel => levels.Count == 0 ? null : levels[Mathf.Clamp(_builtLevel, 0, levels.Count - 1)];

        void OnEnable() => DragonCare.FarmChanged += Sync;
        void OnDisable() => DragonCare.FarmChanged -= Sync;

        void Start() => Sync();

        void Update()
        {
            // 이벤트를 놓쳐도(초기화·새 알 등) 맞춰지도록 가끔 다시 확인
            if (Time.unscaledTime >= _nextSync)
            {
                _nextSync = Time.unscaledTime + 0.5f;
                Sync();
            }

            float dt = Time.deltaTime;
            if (_art && _artPop < 1f)
            {
                _artPop = Mathf.Min(1f, _artPop + dt * 2.2f);
                _art.localScale = Vector3.one * Pop(_artPop);
            }
            _gone.Clear();
            foreach (var a in _animals.Values)
            {
                if (a.leaving)
                {
                    a.pop -= dt * 3f;
                    if (a.pop <= 0f) _gone.Add(a.uid);
                }
                else if (a.pop < 1f) a.pop = Mathf.Min(1f, a.pop + dt * 2.5f);

                a.root.localScale = Vector3.one * Pop(Mathf.Clamp01(a.pop)) * animalScale;
                if (!a.leaving) Wander(a, dt);
                if (a.sparkle) a.sparkle.Rotate(0f, 120f * dt, 0f, Space.World);
            }
            foreach (var uid in _gone)
            {
                Destroy(_animals[uid].root.gameObject);
                _animals.Remove(uid);
            }
        }

        /// <summary>튀어나올 때 살짝 커졌다 돌아오는 크기</summary>
        static float Pop(float t) => t >= 1f ? 1f : Mathf.Sin(t * Mathf.PI * 0.5f) * (1f + 0.25f * Mathf.Sin(t * Mathf.PI));

        void Wander(PenAnimal a, float dt)
        {
            var center = Center;
            if (a.wait > 0f)
            {
                a.wait -= dt;
                SetHeight(a, 0f);
                return;
            }
            var pos = a.root.position;
            var to = a.target - pos;
            to.y = 0f;
            if (to.magnitude < 0.15f)
            {
                a.wait = Random.Range(0.8f, 3.5f);
                a.target = RandomPoint(center);
                return;
            }
            var dir = to.normalized;
            pos += dir * (a.speed * dt);
            pos.y = center.y;
            a.root.position = pos;
            a.root.rotation = Quaternion.Slerp(a.root.rotation, Quaternion.LookRotation(dir, Vector3.up), dt * 6f);

            a.phase += dt * a.speed * 6f;
            SetHeight(a, a.hops ? Mathf.Abs(Mathf.Sin(a.phase)) * 0.35f : Mathf.Abs(Mathf.Sin(a.phase)) * 0.06f);
        }

        static void SetHeight(PenAnimal a, float bob)
        {
            if (a.body) a.body.localPosition = new Vector3(0f, a.height + bob, 0f);
        }

        Vector3 Center => area ? area.position : transform.position;

        Vector3 RandomPoint(Vector3 center)
        {
            var right = area ? area.right : Vector3.right;
            var fwd = area ? area.forward : Vector3.forward;
            return center + right * Random.Range(-_roam.x, _roam.x) + fwd * Random.Range(-_roam.y, _roam.y);
        }

        // ───────────── 우리 짓기 ─────────────

        void BuildPen(int level)
        {
            if (levels.Count == 0) return;
            bool grew = _builtLevel >= 0 && level > _builtLevel;
            _builtLevel = level;
            var lv = CurrentLevel;
            if (_art) Destroy(_art.gameObject);
            _art = FarmPenArt.Build(area ? area : transform, lv, penMaterial ? penMaterial : animalMaterial, 1234 + level);
            _roam = new Vector2(Mathf.Max(0.5f, lv.half.x - roamMargin), Mathf.Max(0.5f, lv.half.y - roamMargin));
            // 넓어지는 순간: 울타리가 톡 튀어나오며 새 모습으로
            _artPop = grew ? 0f : 1f;
            _art.localScale = Vector3.one * (grew ? 0.01f : 1f);
            PlaceCamera(lv);
        }

        void PlaceCamera(PenLevel lv)
        {
            if (!cameraSpot) return;
            var center = Center;
            float k = Mathf.Max(0.75f, lv.half.x / 4f);
            cameraSpot.position = center + cameraOffset * k;
            cameraSpot.rotation = Quaternion.Euler(0f, cameraYaw, 0f) * Quaternion.LookRotation(center - cameraSpot.position, Vector3.up);
        }

        // ───────────── 농장 상태 맞추기 ─────────────

        void Sync()
        {
            if (!DragonCare.IsLoaded || DragonCare.Farm == null) return;
            if (DragonCare.Farm.penUpgradeLevel != _builtLevel) BuildPen(DragonCare.Farm.penUpgradeLevel);
            var animals = DragonCare.Farm.animals;
            var alive = new HashSet<string>();
            foreach (var a in animals)
            {
                alive.Add(a.uid);
                if (_animals.TryGetValue(a.uid, out var existing))
                {
                    existing.leaving = false;
                    continue;
                }
                _animals[a.uid] = Spawn(a, popIn: !_first);
            }
            foreach (var p in _animals.Values)
                if (!alive.Contains(p.uid)) p.leaving = true;
            _first = false;
        }

        PenAnimal Spawn(FarmAnimal a, bool popIn)
        {
            var food = a.hybrid ? null : DragonCare.FindFood(a.id);
            var hybrid = a.hybrid && DragonCare.CombineTable ? DragonCare.CombineTable.Hybrid(a.id) : null;

            Vector3 size = food ? food.bodySize : new Vector3(0.9f, 0.7f, 1.2f);
            Color bodyColor, headColor;
            if (a.hybrid)
            {
                var t1 = hybrid != null && hybrid.traits.Count > 0 ? hybrid.traits[0].trait : a.trait;
                var t2 = hybrid != null && hybrid.traits.Count > 0 ? hybrid.traits[hybrid.traits.Count - 1].trait : a.trait;
                bodyColor = TraitColor(t1);
                headColor = t1 == t2 ? Color.Lerp(TraitColor(t2), Color.white, 0.35f) : TraitColor(t2);
                size *= 1.1f;
            }
            else
            {
                bodyColor = food ? food.color : TraitColor(a.trait);
                if (a.rare) bodyColor = Color.Lerp(bodyColor, RareColor, 0.7f);
                headColor = bodyColor;
            }

            var root = new GameObject($"PenAnimal_{a.id}").transform;
            root.SetParent(transform, false);
            root.position = RandomPoint(Center);
            root.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var body = new GameObject("Body").transform;
            body.SetParent(root, false);
            Part(body, Vector3.zero, size, bodyColor);
            float head = Mathf.Min(size.x, size.y) * 0.7f;
            Part(body, new Vector3(0, size.y * 0.35f, size.z * 0.5f + head * 0.3f), Vector3.one * head, headColor);

            Transform sparkle = null;
            if (a.hybrid || a.rare)
            {
                sparkle = Part(body, new Vector3(0, size.y + 0.5f, 0), Vector3.one * 0.28f, SparkleColor).transform;
                sparkle.localRotation = Quaternion.Euler(45f, 0f, 45f);
            }

            return new PenAnimal
            {
                uid = a.uid,
                root = root,
                body = body,
                sparkle = sparkle,
                height = size.y * 0.5f,
                target = RandomPoint(Center),
                wait = Random.Range(0f, 2f),
                speed = walkSpeed * Random.Range(0.7f, 1.2f) * (food ? Mathf.Clamp(food.wanderSpeed, 0.5f, 1.5f) : 1f),
                pop = popIn ? 0f : 1f,
                hops = a.hybrid,
                phase = Random.Range(0f, 6f)
            };
        }

        Renderer Part(Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = scale;
            var r = part.GetComponent<Renderer>();
            if (animalMaterial) r.sharedMaterial = animalMaterial;
            _block ??= new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            r.SetPropertyBlock(_block);
            return r;
        }

        public static Color TraitColor(FoodTrait t) => t switch
        {
            FoodTrait.Meat => new Color(0.82f, 0.55f, 0.45f),
            FoodTrait.Fish => new Color(0.45f, 0.68f, 0.95f),
            FoodTrait.Fire => new Color(1f, 0.45f, 0.2f),
            _ => new Color(0.45f, 0.82f, 0.4f)
        };

        void OnDrawGizmosSelected()
        {
            var c = Center;
            Gizmos.color = Color.yellow;
            foreach (var lv in levels)
                Gizmos.DrawWireCube(c, new Vector3(lv.half.x * 2f, 0.1f, lv.half.y * 2f));
        }
    }
}
