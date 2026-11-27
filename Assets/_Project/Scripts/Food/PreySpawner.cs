using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DragonRecipe.Food
{
    /// <summary>
    /// 비행 구역 지면에 먹이를 뿌리고, 잡히면 다른 곳에 다시 스폰한다.
    /// 무리 짓는 먹이(양)는 몇 마리씩 모여 나오고, 가끔 희귀 개체가 섞인다.
    /// </summary>
    public class PreySpawner : MonoBehaviour
    {
        public FoodData[] foods;
        [Tooltip("모든 먹이가 공유하는 머티리얼 (색은 MaterialPropertyBlock으로)")]
        public Material preyMaterial;
        public Transform threat;
        public int count = 30;
        public Vector3 areaCenter = new Vector3(0, 0, 200);
        public Vector2 areaSize = new Vector2(300, 340);
        [Tooltip("이 반경 안(출발 절벽 근처)에는 스폰하지 않음")]
        public float keepOutRadius = 25f;
        public float respawnDelay = 4f;

        [Header("무리 / 희귀")]
        [Tooltip("무리 구성원이 모여 있는 반경")]
        public float herdSpread = 7f;
        [Tooltip("무리 중 하나가 잡히면 이 반경 안의 같은 무리가 흩어진다")]
        public float scatterRadius = 25f;
        public float scatterTime = 4f;
        [Range(0f, 1f)] public float rareChance = 0.06f;

        [Header("드래곤 성장")]
        [Tooltip("동물이 드래곤을 알아채는 거리 배율 (성장 단계에서 받아 옴)")]
        public float awarenessScale = 1f;

        readonly List<Prey> _prey = new List<Prey>();
        int _nextHerd;

        public IReadOnlyList<Prey> All => _prey;

        void Start()
        {
            if (foods == null || foods.Length == 0) return;
            while (_prey.Count < count)
            {
                var food = PickFood();
                int size = Mathf.Max(1, Random.Range(food.herdMin, Mathf.Max(food.herdMin, food.herdMax) + 1));
                size = Mathf.Min(size, count - _prey.Count);
                int herd = size > 1 ? _nextHerd++ : -1;
                for (int i = 0; i < size; i++)
                {
                    var prey = Create(food);
                    prey.HerdId = herd;
                    PlaceRandom(prey); // 무리의 두 번째부터는 먼저 놓인 구성원 근처에 놓인다
                }
            }
        }

        public void OnCaught(Prey prey)
        {
            ScatterHerd(prey);
            prey.MarkCaught();
            StartCoroutine(RespawnLater(prey));
        }

        /// <summary>잡힌 먹이의 무리가 흩어져 달아난다</summary>
        public void ScatterHerd(Prey caught)
        {
            if (caught.HerdId < 0) return;
            Vector3 at = caught.transform.position;
            foreach (var p in _prey)
            {
                if (p == caught || !p.IsAvailable || p.HerdId != caught.HerdId) continue;
                if ((p.transform.position - at).sqrMagnitude <= scatterRadius * scatterRadius)
                    p.Scatter(at, scatterTime);
            }
        }

        /// <summary>새 비행을 시작할 때 모든 먹이를 다시 배치</summary>
        public void ResetAll()
        {
            StopAllCoroutines();
            foreach (var p in _prey) p.MarkCaught();
            foreach (var p in _prey) PlaceRandom(p);
        }

        IEnumerator RespawnLater(Prey prey)
        {
            yield return new WaitForSeconds(respawnDelay);
            PlaceRandom(prey);
        }

        public Vector3 ClampToArea(Vector3 p)
        {
            Vector2 half = areaSize * 0.5f;
            p.x = Mathf.Clamp(p.x, areaCenter.x - half.x, areaCenter.x + half.x);
            p.z = Mathf.Clamp(p.z, areaCenter.z - half.y, areaCenter.z + half.y);
            return p;
        }

        FoodData PickFood()
        {
            float total = 0f;
            foreach (var f in foods) total += Mathf.Max(0f, f.spawnWeight);
            float r = Random.value * total;
            foreach (var f in foods)
            {
                r -= Mathf.Max(0f, f.spawnWeight);
                if (r <= 0f) return f;
            }
            return foods[foods.Length - 1];
        }

        Prey FindHerdmate(Prey prey)
        {
            if (prey.HerdId < 0) return null;
            foreach (var p in _prey)
                if (p != prey && p.IsAvailable && p.HerdId == prey.HerdId) return p;
            return null;
        }

        void PlaceRandom(Prey prey)
        {
            bool rare = Random.value < rareChance;
            Vector2 half = areaSize * 0.5f;
            var mate = FindHerdmate(prey);
            for (int attempt = 0; attempt < 30; attempt++)
            {
                Vector3 p;
                if (mate && attempt < 20)
                {
                    var off = Random.insideUnitCircle * herdSpread;
                    p = ClampToArea(mate.transform.position + new Vector3(off.x, 0, off.y));
                    p.y = 200f;
                }
                else
                {
                    p = new Vector3(
                        areaCenter.x + Random.Range(-half.x, half.x),
                        200f,
                        areaCenter.z + Random.Range(-half.y, half.y));
                }
                if (new Vector2(p.x, p.z).magnitude < keepOutRadius) continue;
                if (!Physics.Raycast(p, Vector3.down, out var hit, 400f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.95f || hit.point.y > 1f) continue; // 나무/바위/절벽 위는 제외
                prey.Place(hit.point, rare);
                return;
            }
            prey.MarkCaught(); // 자리를 못 찾으면 잠시 숨겨 둠
            StartCoroutine(RespawnLater(prey));
        }

        Prey Create(FoodData food)
        {
            var root = new GameObject($"Prey_{food.id}");
            root.transform.SetParent(transform, false);

            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true; // 드래곤 충돌 판정엔 걸리지 않고, 낚아채기 트리거에만 걸린다
            col.size = food.bodySize + Vector3.one * 0.4f;

            var parts = new List<Renderer>();
            parts.Add(AddPart(root.transform, PrimitiveType.Cube, Vector3.zero, food.bodySize));
            float headSize = Mathf.Min(food.bodySize.x, food.bodySize.y) * 0.7f;
            parts.Add(AddPart(root.transform, PrimitiveType.Cube,
                new Vector3(0, food.bodySize.y * 0.35f, food.bodySize.z * 0.5f + headSize * 0.3f),
                Vector3.one * headSize));

            // 희귀 개체 표시: 머리 위에서 도는 반짝이
            var sparkle = AddPart(root.transform, PrimitiveType.Cube,
                new Vector3(0, food.bodySize.y + 0.6f, 0), Vector3.one * 0.3f).gameObject;
            sparkle.name = "RareSparkle";
            sparkle.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            var sb = new MaterialPropertyBlock();
            sb.SetColor("_BaseColor", new Color(1f, 0.95f, 0.5f));
            sb.SetColor("_Color", new Color(1f, 0.95f, 0.5f));
            sparkle.GetComponent<Renderer>().SetPropertyBlock(sb);
            sparkle.SetActive(false);

            var prey = root.AddComponent<Prey>();
            prey.Init(food, this, threat, parts.ToArray(), sparkle);
            _prey.Add(prey);
            return prey;
        }

        Renderer AddPart(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(type);
            DestroyImmediate(part.GetComponent<Collider>()); // 즉시 제거해야 같은 프레임의 배치 Raycast에 안 걸림
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = scale;
            var r = part.GetComponent<Renderer>();
            if (preyMaterial) r.sharedMaterial = preyMaterial;
            return r;
        }
    }
}
