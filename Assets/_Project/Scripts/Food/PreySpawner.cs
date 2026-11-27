using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DragonRecipe.Food
{
    /// <summary>비행 구역 지면에 먹이를 뿌리고, 잡히면 다른 곳에 다시 스폰한다.</summary>
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

        readonly List<Prey> _prey = new List<Prey>();
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public IReadOnlyList<Prey> All => _prey;

        void Start()
        {
            if (foods == null || foods.Length == 0) return;
            for (int i = 0; i < count; i++)
            {
                var prey = Create(PickFood());
                PlaceRandom(prey);
            }
        }

        public void OnCaught(Prey prey)
        {
            prey.MarkCaught();
            StartCoroutine(RespawnLater(prey));
        }

        /// <summary>새 비행을 시작할 때 모든 먹이를 다시 배치</summary>
        public void ResetAll()
        {
            StopAllCoroutines();
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

        void PlaceRandom(Prey prey)
        {
            Vector2 half = areaSize * 0.5f;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var p = new Vector3(
                    areaCenter.x + Random.Range(-half.x, half.x),
                    200f,
                    areaCenter.z + Random.Range(-half.y, half.y));
                if (new Vector2(p.x, p.z).magnitude < keepOutRadius) continue;
                if (!Physics.Raycast(p, Vector3.down, out var hit, 400f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.95f || hit.point.y > 1f) continue; // 나무/바위/절벽 위는 제외
                prey.Place(hit.point);
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

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, food.color);
            block.SetColor(ColorId, food.color);

            AddPart(root.transform, PrimitiveType.Cube, Vector3.zero, food.bodySize, block);
            float headSize = Mathf.Min(food.bodySize.x, food.bodySize.y) * 0.7f;
            AddPart(root.transform, PrimitiveType.Cube,
                new Vector3(0, food.bodySize.y * 0.35f, food.bodySize.z * 0.5f + headSize * 0.3f),
                Vector3.one * headSize, block);

            var prey = root.AddComponent<Prey>();
            prey.Init(food, this, threat);
            _prey.Add(prey);
            return prey;
        }

        void AddPart(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, MaterialPropertyBlock block)
        {
            var part = GameObject.CreatePrimitive(type);
            DestroyImmediate(part.GetComponent<Collider>()); // 즉시 제거해야 같은 프레임의 배치 Raycast에 안 걸림
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = scale;
            var r = part.GetComponent<Renderer>();
            if (preyMaterial) r.sharedMaterial = preyMaterial;
            r.SetPropertyBlock(block);
        }
    }
}
