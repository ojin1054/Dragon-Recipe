using System.Collections.Generic;
using UnityEngine;

namespace DragonRecipe.Core
{
    /// <summary>
    /// 발톱에 매단 먹이를 드래곤 아래에 매달아 보여 준다.
    /// 무거울수록 크게 흔들리고, 버둥거리는 먹이는 홱홱 몸부림친다.
    /// </summary>
    public class CarryVisual : MonoBehaviour
    {
        public FlightSession session;
        [Tooltip("먹이를 매다는 위치 (비우면 낚아채기 영역)")]
        public Transform anchor;
        public Material material;
        [Tooltip("매달린 먹이 크기 배율 (원래 먹이 크기 기준)")]
        public float scale = 0.6f;
        public float spacing = 0.55f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly Color RareColor = new Color(1f, 0.82f, 0.2f);

        class Hung
        {
            public Transform t;
            public bool struggles;
            public float phase;
        }

        readonly List<Hung> _hung = new List<Hung>();
        Transform _root;
        float _jolt; // 1 → 0
        float _eatPulse;

        void Awake()
        {
            if (!anchor && session && session.snatchZone) anchor = session.snatchZone.transform;
            if (!material && session && session.spawner) material = session.spawner.preyMaterial;
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
            _hung.Clear();
            if (!session || !anchor) return;

            _root = new GameObject("CarriedFood").transform;
            _root.SetParent(anchor, false);
            int i = 0;
            int count = session.Result.CarriedCount;
            foreach (var c in session.Result.caught)
            {
                if (!c.Carried || !c.food) continue;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyImmediate(go.GetComponent<Collider>()); // 드래곤 SphereCast / 낚아채기에 걸리지 않게
                go.name = c.food.id;
                go.transform.SetParent(_root, false);
                float x = (i - (count - 1) * 0.5f) * spacing;
                go.transform.localPosition = new Vector3(x, -c.food.bodySize.y * scale * 0.5f, 0f);
                go.transform.localScale = c.food.bodySize * scale * (c.rare ? Food.Prey.RareScale : 1f);
                var r = go.GetComponent<Renderer>();
                if (material) r.sharedMaterial = material;
                var block = new MaterialPropertyBlock();
                var color = c.rare ? Color.Lerp(c.food.color, RareColor, 0.7f) : c.food.color;
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                r.SetPropertyBlock(block);
                _hung.Add(new Hung { t = go.transform, struggles = c.food.struggles, phase = i * 1.3f });
                i++;
            }
        }

        void Update()
        {
            if (!_root || !session || !session.dragon) return;
            var m = session.dragon.Model;
            _jolt = Mathf.Max(0f, _jolt - Time.deltaTime * 3f);

            // 무거울수록 축 늘어져 크게 흔들림
            float sway = 6f + 14f * Mathf.Clamp01(m.LoadFactor);
            float bank = m.Bank;
            foreach (var h in _hung)
            {
                if (!h.t) continue;
                float wobble = Mathf.Sin(Time.time * 3f + h.phase) * sway;
                float kick = h.struggles ? Mathf.Sin(Time.time * 30f + h.phase) * 35f * _jolt + Mathf.Sin(Time.time * 9f + h.phase) * 6f : 0f;
                h.t.localRotation = Quaternion.Euler(wobble * 0.5f, kick, bank * 0.4f + wobble);
            }

            // 먹는 중: 맨 앞 먹이가 쪼그라든다
            _eatPulse = session.dragon.IsEating ? _eatPulse + Time.deltaTime * 12f : 0f;
            _root.localScale = Vector3.one * (1f + Mathf.Sin(_eatPulse) * 0.05f);
        }
    }
}
