using UnityEngine;

namespace DragonRecipe.Food
{
    /// <summary>
    /// 지상에서 배회하다가 드래곤을 알아채면 도망가는 먹이.
    /// 무리 중 하나가 잡히면 나머지가 흩어진다. 가끔 황금빛 희귀 개체로 나온다.
    /// </summary>
    public class Prey : MonoBehaviour
    {
        public FoodData Data { get; private set; }
        public bool IsAvailable { get; private set; }
        /// <summary>황금빛 희귀 개체 (포만도 2배, 도감에 별)</summary>
        public bool IsRare { get; private set; }
        /// <summary>같은 무리 번호 (-1 = 혼자 다님)</summary>
        public int HerdId { get; set; } = -1;
        /// <summary>지금 흩어져 달아나는 중</summary>
        public bool IsScattering => Time.time < _scatterUntil;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly Color RareColor = new Color(1f, 0.82f, 0.2f);
        public const float RareScale = 1.25f;

        Transform _threat;
        PreySpawner _spawner;
        Renderer[] _parts;
        GameObject _sparkle;
        MaterialPropertyBlock _block;
        Vector3 _wanderDir;
        float _nextTurn;
        float _groundY;
        float _scale = 1f;
        Vector3 _scatterFrom;
        float _scatterUntil = -1f;

        public void Init(FoodData data, PreySpawner spawner, Transform threat, Renderer[] parts = null, GameObject sparkle = null)
        {
            Data = data;
            _spawner = spawner;
            _threat = threat;
            _parts = parts;
            _sparkle = sparkle;
        }

        public void Place(Vector3 groundPoint, bool rare = false)
        {
            IsRare = rare;
            _scale = rare ? RareScale : 1f;
            transform.localScale = Vector3.one * _scale;
            ApplyLook();

            _groundY = groundPoint.y;
            transform.position = new Vector3(groundPoint.x, _groundY + HalfHeight, groundPoint.z);
            transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            _wanderDir = transform.forward;
            _nextTurn = 0f;
            _scatterUntil = -1f;
            IsAvailable = true;
            gameObject.SetActive(true);
        }

        float HalfHeight => Data.bodySize.y * 0.5f * _scale;

        void ApplyLook()
        {
            if (_sparkle) _sparkle.SetActive(IsRare);
            if (_parts == null || Data == null) return;
            _block ??= new MaterialPropertyBlock();
            var c = IsRare ? Color.Lerp(Data.color, RareColor, 0.7f) : Data.color;
            _block.SetColor(BaseColorId, c);
            _block.SetColor(ColorId, c);
            foreach (var r in _parts)
                if (r) r.SetPropertyBlock(_block);
        }

        public void MarkCaught()
        {
            IsAvailable = false;
            gameObject.SetActive(false);
        }

        /// <summary>from에서 멀어지는 쪽으로 duration 동안 달아난다 (무리 중 하나가 잡혔을 때)</summary>
        public void Scatter(Vector3 from, float duration)
        {
            if (!IsAvailable) return;
            _scatterFrom = from;
            _scatterUntil = Time.time + duration;
        }

        /// <summary>드래곤을 알아채는 거리 (성장 단계 배율 반영)</summary>
        public float AwarenessRadius => Data ? Data.fleeRadius * (_spawner ? _spawner.awarenessScale : 1f) : 0f;

        void Update()
        {
            if (!IsAvailable || Data == null) return;
            if (_sparkle && _sparkle.activeSelf) _sparkle.transform.Rotate(0f, 180f * Time.deltaTime, 0f, Space.World);

            Vector3 move;
            float speed;
            Vector3 toThreat = _threat ? _threat.position - transform.position : Vector3.positiveInfinity;
            Vector3 flatToThreat = new Vector3(toThreat.x, 0, toThreat.z);
            float aware = AwarenessRadius;

            if (IsScattering)
            {
                Vector3 away = transform.position - _scatterFrom;
                away.y = 0f;
                move = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
                speed = Mathf.Max(Data.scatterSpeed, Data.fleeSpeed);
            }
            else if (Data.flees && _threat && flatToThreat.magnitude < aware && toThreat.y < aware)
            {
                move = -flatToThreat.normalized;
                speed = Data.fleeSpeed;
            }
            else
            {
                if (Time.time >= _nextTurn)
                {
                    float a = Random.Range(0f, Mathf.PI * 2f);
                    _wanderDir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    _nextTurn = Time.time + Random.Range(2f, 5f);
                }
                move = _wanderDir;
                speed = Data.wanderSpeed;
            }

            Vector3 next = transform.position + move * (speed * Time.deltaTime);
            if (_spawner) next = _spawner.ClampToArea(next);
            next.y = _groundY + HalfHeight;
            transform.position = next;
            if (move.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(move), 8f * Time.deltaTime);
        }
    }
}
