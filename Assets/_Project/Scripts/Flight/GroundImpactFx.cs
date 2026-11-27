using System.Collections.Generic;
using UnityEngine;

namespace DragonRecipe.Flight
{
    /// <summary>
    /// [임시 이펙트] 비행 중 지면에 닿으면 튀는 흙덩이,
    /// 미끄러지는 동안에는 뒤로 흩날리는 흙먼지를 만든다.
    /// 에셋 없이 프리미티브로만 만든 자리표시용이라, 나중에 파티클/데칼로 바꿀 것.
    /// </summary>
    public class GroundImpactFx : MonoBehaviour
    {
        public DragonFlightController controller;

        [Header("흙덩이")]
        public Color dirtColor = new Color(0.45f, 0.32f, 0.2f);
        public float clodLifetime = 0.9f;
        public float clodGravity = 15f;
        [Tooltip("미끄러지는 동안 흙먼지를 뿌리는 간격 (초)")]
        public float skidDustInterval = 0.06f;

        class Piece
        {
            public Transform t;
            public Vector3 baseScale;
            public Vector3 velocity;
            public Vector3 spin;
            public float groundY;
            public float life;
            public float age;
            public bool ballistic;
        }

        static Material _dirtMat;
        readonly List<Piece> _pieces = new List<Piece>();
        float _dustTimer;

        void Awake()
        {
            if (!controller) controller = GetComponent<DragonFlightController>();
        }

        void OnEnable()
        {
            if (controller) controller.TouchedGround += OnTouchedGround;
        }

        void OnDisable()
        {
            if (controller) controller.TouchedGround -= OnTouchedGround;
        }

        void OnDestroy()
        {
            foreach (var p in _pieces)
                if (p.t) Destroy(p.t.gameObject);
            _pieces.Clear();
        }

        void OnTouchedGround(Vector3 point, Vector3 normal, float strength)
        {
            // 아주 얕게 스쳐도 흙은 조금 튄다
            float s = Mathf.Lerp(0.15f, 1f, Mathf.Clamp01(strength));
            Vector3 forward = Vector3.ProjectOnPlane(controller.Model.Forward, normal);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(transform.forward, normal);
            forward.Normalize();

            SpawnClods(point, normal, forward, Mathf.RoundToInt(Mathf.Lerp(5f, 18f, s)), Mathf.Lerp(2f, 7f, s), Mathf.Lerp(0.12f, 0.32f, s));
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // 미끄러지는 동안 뒤로 흙먼지
            if (controller && controller.IsSkidding && dt > 0f)
            {
                _dustTimer -= dt;
                if (_dustTimer <= 0f)
                {
                    _dustTimer = skidDustInterval;
                    Vector3 ground = controller.Model.Position + Vector3.down * controller.Altitude;
                    Vector3 back = -Vector3.ProjectOnPlane(controller.Model.Forward, Vector3.up).normalized;
                    float speed = Mathf.Clamp(controller.Model.Speed * 0.15f, 1f, 4f);
                    SpawnClods(ground, Vector3.up, back, 2, speed, 0.14f);
                }
            }

            for (int i = _pieces.Count - 1; i >= 0; i--)
            {
                var p = _pieces[i];
                if (!p.t)
                {
                    _pieces.RemoveAt(i);
                    continue;
                }
                p.age += dt;
                if (p.age >= p.life)
                {
                    Destroy(p.t.gameObject);
                    _pieces.RemoveAt(i);
                    continue;
                }

                if (p.ballistic)
                {
                    p.velocity += Vector3.down * clodGravity * dt;
                    Vector3 pos = p.t.position + p.velocity * dt;
                    if (pos.y < p.groundY)
                    {
                        pos.y = p.groundY;
                        p.velocity = Vector3.zero;
                        p.spin = Vector3.zero;
                    }
                    p.t.position = pos;
                    p.t.Rotate(p.spin * dt, Space.Self);
                }

                // 수명 마지막 30%에 줄어들며 사라짐
                float fade = Mathf.Clamp01((p.life - p.age) / (p.life * 0.3f));
                p.t.localScale = p.baseScale * fade;
            }
        }

        // ───────── 생성 ─────────

        void SpawnClods(Vector3 point, Vector3 normal, Vector3 forward, int count, float speed, float size)
        {
            var up = normal.normalized;
            for (int i = 0; i < count; i++)
            {
                Vector3 side = Vector3.ProjectOnPlane(Random.onUnitSphere, up).normalized;
                Vector3 v = forward * speed * Random.Range(0.2f, 0.7f)
                            + side * speed * Random.Range(0.2f, 0.6f)
                            + up * speed * Random.Range(0.6f, 1.2f);
                float sz = size * Random.Range(0.6f, 1.3f);
                var go = MakePiece(PrimitiveType.Cube, DirtMat, point + up * 0.1f, Random.rotation, Vector3.one * sz);
                var p = Add(go, clodLifetime * Random.Range(0.7f, 1.2f), true);
                p.velocity = v;
                p.spin = Random.insideUnitSphere * 540f;
                p.groundY = point.y + sz * 0.5f;
            }
        }

        Piece Add(GameObject go, float life, bool ballistic)
        {
            var p = new Piece { t = go.transform, baseScale = go.transform.localScale, life = life, ballistic = ballistic };
            _pieces.Add(p);
            return p;
        }

        static GameObject MakePiece(PrimitiveType type, Material mat, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = "GroundFx";
            // 같은 프레임의 충돌 검사(SphereCast/Raycast)에 걸리지 않게 콜라이더를 즉시 제거
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        Material DirtMat => _dirtMat ? _dirtMat : _dirtMat = MakeMaterial(dirtColor);

        static Material MakeMaterial(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
            m.color = c;
            return m;
        }
    }
}
