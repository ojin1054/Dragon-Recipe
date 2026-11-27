using System.Collections.Generic;
using DragonRecipe.Core;
using UnityEngine;

namespace DragonRecipe.Skills
{
    /// <summary>
    /// [임시] 스킬 연출: 도형으로 만든 브레스 입자, 터지는 구, 포효 고리, 맞은 대상 머리 위 표시.
    /// 나중에 파티클·VFX로 바꿀 자리.
    /// </summary>
    public static class SkillFx
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static Material _mat;
        static MaterialPropertyBlock _block;

        public static Material Mat
        {
            get
            {
                if (_mat) return _mat;
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (!shader) shader = Shader.Find("Unlit/Color");
                if (!shader) shader = Shader.Find("Universal Render Pipeline/Lit");
                _mat = new Material(shader) { name = "SkillFxTemp" };
                return _mat;
            }
        }

        public static GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Color c)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var t = go.transform;
            if (parent) t.SetParent(parent, false);
            t.localPosition = pos;
            t.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = Mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            SetColor(r, c);
            return go;
        }

        public static void SetColor(Renderer r, Color c)
        {
            if (!r) return;
            _block ??= new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, c);
            _block.SetColor(ColorId, c);
            r.SetPropertyBlock(_block);
        }

        /// <summary>스킬 색에서 조금씩 벗어난 색 (입자마다 다르게)</summary>
        public static Color Vary(Color c, float amount = 0.15f) =>
            new Color(Mathf.Clamp01(c.r + Random.Range(-amount, amount)), Mathf.Clamp01(c.g + Random.Range(-amount, amount)),
                Mathf.Clamp01(c.b + Random.Range(-amount, amount)));

        /// <summary>날아가며 커졌다가 줄어 사라지는 입자 하나</summary>
        public static void Puff(Vector3 at, Vector3 velocity, float startSize, float endSize, float life, Color c,
            PrimitiveType type = PrimitiveType.Sphere)
        {
            var go = Shape(type, "SkillPuff", null, at, Vector3.one * startSize, c);
            go.transform.rotation = Random.rotation;
            var p = go.AddComponent<FxPuff>();
            p.velocity = velocity;
            p.startSize = startSize;
            p.endSize = endSize;
            p.life = life;
        }

        /// <summary>구체가 터질 때: 커지는 구 + 튀는 조각</summary>
        public static void Burst(Vector3 at, Color c, float radius)
        {
            Puff(at, Vector3.zero, radius * 0.3f, radius * 1.6f, 0.3f, Color.Lerp(c, Color.white, 0.3f));
            for (int i = 0; i < 10; i++)
                Puff(at, Random.onUnitSphere * radius * 4f + Vector3.up * 3f, 0.35f, 0.05f, 0.5f, Vary(c), PrimitiveType.Cube);
        }

        /// <summary>포효: 드래곤 둘레로 퍼지는 고리</summary>
        public static void Ring(Vector3 at, Color c, float radius, float time = 0.45f)
        {
            var root = new GameObject("SkillRing");
            root.transform.position = at;
            var ring = root.AddComponent<FxRing>();
            ring.radius = radius;
            ring.time = time;
            const int n = 20;
            ring.bits = new Transform[n];
            for (int i = 0; i < n; i++)
                ring.bits[i] = Shape(PrimitiveType.Cube, "Bit", root.transform, Vector3.zero, new Vector3(0.6f, 0.25f, 1.1f), Vary(c, 0.08f)).transform;
        }

        /// <summary>
        /// 맞은 대상 위에 상태 표시를 붙인다 (기절 별, 덩굴, 잠, 불꽃, 둔화).
        /// 같은 대상에 같은 표시가 있으면 시간만 늘린다.
        /// </summary>
        public static void Mark(Transform target, SkillEffect kind, float duration)
        {
            if (!target || duration <= 0f) return;
            if (FxMarker.TryExtend(target, kind, duration)) return;
            var go = new GameObject("SkillMark_" + kind);
            var m = go.AddComponent<FxMarker>();
            m.Init(target, kind, duration);
        }
    }

    /// <summary>날아가며 크기가 변하고 시간이 다 되면 사라지는 입자</summary>
    public class FxPuff : MonoBehaviour
    {
        public Vector3 velocity;
        public float startSize = 0.3f;
        public float endSize = 1f;
        public float life = 0.5f;
        float _age;

        void Update()
        {
            if (GamePause.IsPaused) return;
            _age += Time.deltaTime;
            float t = _age / Mathf.Max(0.01f, life);
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            transform.position += velocity * Time.deltaTime;
            velocity *= Mathf.Exp(-1.5f * Time.deltaTime);
            // 끝 무렵에 빠르게 줄어 사라진다
            float size = Mathf.Lerp(startSize, endSize, Mathf.Sqrt(t));
            if (t > 0.75f) size *= 1f - (t - 0.75f) / 0.25f;
            transform.localScale = Vector3.one * Mathf.Max(0.01f, size);
        }
    }

    /// <summary>바깥으로 퍼지는 조각 고리</summary>
    public class FxRing : MonoBehaviour
    {
        public Transform[] bits;
        public float radius = 10f;
        public float time = 0.45f;
        float _age;

        void Update()
        {
            if (GamePause.IsPaused) return;
            _age += Time.deltaTime;
            float t = _age / Mathf.Max(0.01f, time);
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            float r = Mathf.Lerp(0.8f, radius, 1f - (1f - t) * (1f - t));
            float s = 1f - t * t;
            for (int i = 0; i < bits.Length; i++)
            {
                float a = i / (float)bits.Length * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                bits[i].localPosition = dir * r;
                bits[i].localRotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, dir));
                bits[i].localScale = new Vector3(0.6f, 0.25f, 1.1f + r * 0.15f) * s;
            }
        }
    }

    /// <summary>대상을 따라다니는 상태 표시. 대상이 꺼지면 같이 사라진다</summary>
    public class FxMarker : MonoBehaviour
    {
        static readonly List<FxMarker> _live = new List<FxMarker>();

        Transform _target;
        SkillEffect _kind;
        float _until;
        float _height = 1.5f;
        float _girth = 0.6f;
        float _bottom;
        Transform[] _bits;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _live.Clear();

        public static bool TryExtend(Transform target, SkillEffect kind, float duration)
        {
            foreach (var m in _live)
            {
                if (m && m._target == target && m._kind == kind)
                {
                    m._until = Mathf.Max(m._until, Time.time + duration);
                    return true;
                }
            }
            return false;
        }

        public void Init(Transform target, SkillEffect kind, float duration)
        {
            _target = target;
            _kind = kind;
            _until = Time.time + duration;
            _live.Add(this);

            // 대상 몸집에 맞춰 높이·둘레를 정한다
            var rs = target.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                _height = b.max.y - target.position.y;
                _bottom = b.min.y - target.position.y;
                _girth = Mathf.Max(b.extents.x, b.extents.z);
            }

            var (color, count, size, type) = kind switch
            {
                SkillEffect.Stun => (new Color(1f, 0.92f, 0.3f), 3, 0.22f, PrimitiveType.Cube),
                SkillEffect.Bind => (new Color(0.3f, 0.7f, 0.25f), 6, 0.2f, PrimitiveType.Cube),
                SkillEffect.Sleep => (new Color(0.7f, 0.85f, 1f), 3, 0.2f, PrimitiveType.Cube),
                SkillEffect.Burn => (new Color(1f, 0.5f, 0.1f), 4, 0.25f, PrimitiveType.Cube),
                _ => (new Color(0.55f, 0.75f, 0.45f), 4, 0.16f, PrimitiveType.Sphere) // 둔화
            };
            _bits = new Transform[count];
            for (int i = 0; i < count; i++)
                _bits[i] = SkillFx.Shape(type, "Bit", transform, Vector3.zero, Vector3.one * size, SkillFx.Vary(color, 0.06f)).transform;
            Update();
        }

        void OnDestroy() => _live.Remove(this);

        void Update()
        {
            if (!_target || !_target.gameObject.activeInHierarchy || Time.time >= _until)
            {
                Destroy(gameObject);
                return;
            }
            transform.position = _target.position;
            float t = Time.time;
            int n = _bits.Length;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                Vector3 p;
                switch (_kind)
                {
                    case SkillEffect.Stun: // 머리 위를 빙글빙글
                        a += t * 5f;
                        p = new Vector3(Mathf.Cos(a) * 0.45f, _height + 0.35f, Mathf.Sin(a) * 0.45f);
                        _bits[i].localRotation = Quaternion.Euler(45f, t * 300f, 45f);
                        break;
                    case SkillEffect.Bind: // 발밑을 감는 덩굴
                        a += t * 0.8f;
                        p = new Vector3(Mathf.Cos(a) * (_girth + 0.15f), _bottom + 0.12f + (i % 2) * 0.22f, Mathf.Sin(a) * (_girth + 0.15f));
                        break;
                    case SkillEffect.Sleep: // 머리 위로 천천히 떠오르는 조각
                    {
                        float k = Mathf.Repeat(t * 0.6f + i / (float)n, 1f);
                        p = new Vector3(0.3f + k * 0.4f, _height + 0.2f + k * 0.9f, 0f);
                        _bits[i].localScale = Vector3.one * (0.12f + k * 0.18f);
                        break;
                    }
                    case SkillEffect.Burn: // 몸에서 깜빡이며 솟는 불씨
                    {
                        float k = Mathf.Repeat(t * 2.2f + i / (float)n, 1f);
                        p = new Vector3(Mathf.Cos(a) * _girth * 0.6f, Mathf.Lerp(_bottom, _height, 0.2f + k * 0.9f), Mathf.Sin(a) * _girth * 0.6f);
                        _bits[i].localScale = Vector3.one * (0.3f * (1f - k) + 0.05f);
                        break;
                    }
                    default: // 둔화: 몸 둘레를 느리게 도는 방울
                        a += t * 1.2f;
                        p = new Vector3(Mathf.Cos(a) * (_girth + 0.25f), Mathf.Lerp(_bottom, _height, 0.5f), Mathf.Sin(a) * (_girth + 0.25f));
                        break;
                }
                _bits[i].localPosition = p;
            }
        }
    }
}
