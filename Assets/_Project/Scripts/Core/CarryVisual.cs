using System.Collections.Generic;
using UnityEngine;

namespace DragonRecipe.Core
{
    /// <summary>
    /// 잡은 먹이를 드래곤 등 위에 차곡차곡 쌓아 보여 준다.
    /// 높이 쌓일수록, 무거울수록 탑이 크게 출렁이고, 버둥거리는 먹이는 홱홱 몸부림친다.
    /// </summary>
    public class CarryVisual : MonoBehaviour
    {
        public FlightSession session;
        [Tooltip("먹이를 쌓는 곳 (비우면 드래곤의 'Model', 없으면 드래곤)")]
        public Transform anchor;
        [Tooltip("드래곤 등 위에 쌓기 (끄면 anchor 아래에 매단다)")]
        public bool stackOnBack = true;
        [Tooltip("등 위 첫 먹이가 놓이는 위치 (anchor 기준)")]
        public Vector3 backOffset = new Vector3(0f, 0.42f, -0.15f);
        public Material material;
        [Tooltip("쌓인 먹이 크기 배율 (원래 먹이 크기 기준)")]
        public float scale = 0.6f;
        [Tooltip("먹이 사이 틈 (m)")]
        public float gap = 0.03f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly Color RareColor = new Color(1f, 0.82f, 0.2f);

        class Stacked
        {
            public Transform t;
            public Vector3 basePos;
            public float yaw;
            public float height; // 탑에서 몇 번째 높이인지 (0 = 맨 아래)
            public bool struggles;
            public float phase;
        }

        readonly List<Stacked> _stack = new List<Stacked>();
        Transform _root;
        float _jolt; // 1 → 0
        float _eatPulse;
        float _lean; // 선회에 따라 탑이 바깥으로 기우는 정도 (deg)

        void Awake()
        {
            if (!material && session && session.spawner) material = session.spawner.preyMaterial;
        }

        Transform ResolveAnchor()
        {
            if (stackOnBack && session && session.dragon)
            {
                var model = session.dragon.transform.Find("Model");
                return model ? model : session.dragon.transform;
            }
            if (anchor) return anchor;
            return session && session.snatchZone ? session.snatchZone.transform : null;
        }

        void OnEnable()
        {
            if (!session) return;
            session.CarryChanged += Rebuild;
            session.Struggled += OnStruggled;
        }

        void OnDisable()
        {
            if (!session) return;
            session.CarryChanged -= Rebuild;
            session.Struggled -= OnStruggled;
        }

        void OnStruggled() => _jolt = 1f;

        void Rebuild()
        {
            if (_root)
            {
                // Destroy는 프레임 끝에 일어나므로, 같은 프레임에 새로 만든 것과 헷갈리지 않게 바로 숨긴다
                _root.name = "CarriedFood (old)";
                _root.gameObject.SetActive(false);
                Destroy(_root.gameObject);
            }
            _stack.Clear();
            var at = ResolveAnchor();
            if (!session || !at) return;

            _root = new GameObject("CarriedFood").transform;
            _root.SetParent(at, false);
            _root.localPosition = stackOnBack ? backOffset : Vector3.zero;

            int i = 0;
            float y = 0f;
            foreach (var c in session.Result.caught)
            {
                if (!c.Carried || !c.food) continue;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyImmediate(go.GetComponent<Collider>()); // 드래곤 SphereCast / 낚아채기에 걸리지 않게
                go.name = c.food.id;
                go.transform.SetParent(_root, false);
                var size = c.food.bodySize * scale * (c.rare ? Food.Prey.RareScale : 1f);
                go.transform.localScale = size;

                // 등 위: 아래에서부터 차곡차곡. 매달기: 옆으로 나란히 늘어뜨림
                Vector3 pos;
                if (stackOnBack)
                {
                    pos = new Vector3(Mathf.Sin(i * 2.4f) * 0.04f, y + size.y * 0.5f, Mathf.Cos(i * 1.7f) * 0.04f);
                    y += size.y + gap;
                }
                else
                {
                    pos = new Vector3(i * 0.55f, -size.y * 0.5f, 0f);
                }
                go.transform.localPosition = pos;

                var r = go.GetComponent<Renderer>();
                if (material) r.sharedMaterial = material;
                var block = new MaterialPropertyBlock();
                var color = c.rare ? Color.Lerp(c.food.color, RareColor, 0.7f) : c.food.color;
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                r.SetPropertyBlock(block);

                _stack.Add(new Stacked
                {
                    t = go.transform,
                    basePos = pos,
                    yaw = Mathf.Sin(i * 3.1f) * 25f, // 아무렇게나 얹은 듯 살짝 틀어짐
                    height = i,
                    struggles = c.food.struggles,
                    phase = i * 1.3f,
                });
                i++;
            }
        }

        void Update()
        {
            if (!_root || !session || !session.dragon) return;
            var m = session.dragon.Model;
            float dt = Time.deltaTime;
            _jolt = Mathf.Max(0f, _jolt - dt * 3f);

            // 선회하면 탑이 바깥쪽으로 늦게 따라 기운다
            float bank = m != null ? m.Bank : 0f;
            _lean = Mathf.Lerp(_lean, -bank * 0.25f, 4f * dt);
            float load = m != null ? Mathf.Clamp01(m.LoadFactor) : 0f;

            // 무거울수록, 위로 갈수록 크게 출렁임
            float sway = 2f + 5f * load;
            foreach (var s in _stack)
            {
                if (!s.t) continue;
                float h = s.height + 1f;
                float wobble = Mathf.Sin(Time.time * 3f + s.phase) * sway * h * 0.5f;
                float kick = s.struggles ? Mathf.Sin(Time.time * 30f + s.phase) * 35f * _jolt + Mathf.Sin(Time.time * 9f + s.phase) * 6f : 0f;
                if (stackOnBack)
                {
                    float roll = _lean * h * 0.5f + wobble;
                    // 기운 만큼 옆으로 밀려 탑이 휘어 보이게
                    float shift = Mathf.Sin(roll * Mathf.Deg2Rad) * s.basePos.y;
                    s.t.localPosition = s.basePos + new Vector3(-shift, 0f, 0f);
                    s.t.localRotation = Quaternion.Euler(wobble * 0.4f, s.yaw + kick, roll);
                }
                else
                {
                    s.t.localRotation = Quaternion.Euler(wobble * 0.5f, kick, bank * 0.4f + wobble);
                }
            }

            // 먹는 중: 탑이 꿈틀꿈틀
            _eatPulse = session.dragon.IsEating ? _eatPulse + dt * 12f : 0f;
            _root.localScale = Vector3.one * (1f + Mathf.Sin(_eatPulse) * 0.05f);
        }
    }
}
