using System.Collections.Generic;
using UnityEngine;

namespace DragonRecipe.Food
{
    /// <summary>먹이가 사는 곳. 스폰 자리와 돌아다니는 범위를 정한다</summary>
    public enum Habitat
    {
        /// <summary>먹이 id로 정한다 (HabitatRules의 표). 표에 없으면 물고기 = 물, 나머지 = 아무 데나</summary>
        Auto,
        /// <summary>물만 아니면 어디든</summary>
        Anywhere,
        /// <summary>나무·바위에서 떨어진 탁 트인 풀밭</summary>
        Meadow,
        /// <summary>나무 근처</summary>
        Forest,
        /// <summary>연못 안. 물 밖으로 나가지 않는다</summary>
        Water,
        /// <summary>연못 둘레 물가</summary>
        Shore,
        /// <summary>바위 근처</summary>
        Rocky
    }

    /// <summary>타원 연못 하나</summary>
    public struct PondArea
    {
        public Vector3 center;
        public float radiusX;
        public float radiusZ;

        /// <summary>연못 안인지. scale 1 = 물 가장자리, 1.5 = 반지름을 1.5배로 넓힌 범위</summary>
        public bool Contains(Vector3 p, float scale = 1f)
        {
            float rx = Mathf.Max(0.01f, radiusX * scale);
            float rz = Mathf.Max(0.01f, radiusZ * scale);
            float dx = (p.x - center.x) / rx;
            float dz = (p.z - center.z) / rz;
            return dx * dx + dz * dz <= 1f;
        }
    }

    public struct RockSpot
    {
        public Vector3 position;
        public float radius;
    }

    /// <summary>씬의 지형지물 (연못·나무·바위) 위치. 먹이를 서식지에 맞게 놓을 때 쓴다</summary>
    public class HabitatMap
    {
        public readonly List<PondArea> ponds = new List<PondArea>();
        public readonly List<Vector3> trees = new List<Vector3>();
        public readonly List<RockSpot> rocks = new List<RockSpot>();

        /// <summary>물 가장자리 바깥으로 이 배율까지가 물가</summary>
        public const float ShoreScale = 1.45f;
        public const float ForestRadius = 9f;
        public const float RockReach = 7f;
        public const float MeadowTreeGap = 8f;
        public const float MeadowRockGap = 4f;

        /// <summary>씬에서 이름으로 지형지물을 모은다 (씬 빌더가 붙인 이름: Pond*, Tree_*, Rock_*, Spire_*)</summary>
        public static HabitatMap Scan()
        {
            var map = new HabitatMap();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                string n = t.name;
                if (n.StartsWith("Pond"))
                {
                    var s = t.lossyScale; // 원기둥 프리미티브: 지름 = 스케일
                    map.ponds.Add(new PondArea { center = t.position, radiusX = s.x * 0.5f, radiusZ = s.z * 0.5f });
                }
                else if (n.StartsWith("Tree_")) map.trees.Add(t.position);
                else if (n.StartsWith("Rock_") || n.StartsWith("Spire_"))
                {
                    var s = t.lossyScale;
                    map.rocks.Add(new RockSpot { position = t.position, radius = Mathf.Max(s.x, s.z) * 0.6f });
                }
            }
            return map;
        }

        public bool InPond(Vector3 p, float scale = 1f)
        {
            foreach (var pond in ponds)
                if (pond.Contains(p, scale)) return true;
            return false;
        }

        bool NearTree(Vector3 p, float dist)
        {
            float d2 = dist * dist;
            foreach (var t in trees)
            {
                float dx = t.x - p.x, dz = t.z - p.z;
                if (dx * dx + dz * dz <= d2) return true;
            }
            return false;
        }

        bool NearRock(Vector3 p, float extra)
        {
            foreach (var r in rocks)
            {
                float dx = r.position.x - p.x, dz = r.position.z - p.z;
                float reach = r.radius + extra;
                if (dx * dx + dz * dz <= reach * reach) return true;
            }
            return false;
        }

        /// <summary>
        /// 이 자리가 서식지에 맞는지. 그 서식지에 필요한 지형지물이 씬에 하나도 없으면 맞는 것으로 친다
        /// (연못 없는 씬에서 물고기가 아예 안 나오는 일 방지).
        /// </summary>
        public bool Fits(Habitat h, Vector3 p)
        {
            switch (h)
            {
                case Habitat.Water:
                    return ponds.Count == 0 || InPond(p, 0.92f);
                case Habitat.Shore:
                    return ponds.Count == 0 || (!InPond(p, 1.02f) && InPond(p, ShoreScale));
                case Habitat.Forest:
                    return !InPond(p, 1.02f) && (trees.Count == 0 || NearTree(p, ForestRadius));
                case Habitat.Rocky:
                    return !InPond(p, 1.02f) && (rocks.Count == 0 || NearRock(p, RockReach));
                case Habitat.Meadow:
                    return !InPond(p, 1.1f) && !NearTree(p, MeadowTreeGap) && !NearRock(p, MeadowRockGap);
                default:
                    return !InPond(p, 1.02f);
            }
        }

        /// <summary>서식지 안의 후보 자리 하나 (높이는 안 정함). 지형지물이 없어 못 고르면 false → 구역 아무 데나 고른다</summary>
        public bool TrySample(Habitat h, out Vector3 p)
        {
            p = default;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            switch (h)
            {
                case Habitat.Water:
                case Habitat.Shore:
                {
                    if (ponds.Count == 0) return false;
                    var pond = ponds[Random.Range(0, ponds.Count)];
                    float r = h == Habitat.Water ? Mathf.Sqrt(Random.value) * 0.85f : Random.Range(1.08f, ShoreScale - 0.05f);
                    p = pond.center + new Vector3(dir.x * pond.radiusX * r, 0f, dir.z * pond.radiusZ * r);
                    return true;
                }
                case Habitat.Forest:
                    if (trees.Count == 0) return false;
                    p = trees[Random.Range(0, trees.Count)] + dir * Random.Range(2f, ForestRadius - 1f);
                    return true;
                case Habitat.Rocky:
                {
                    if (rocks.Count == 0) return false;
                    var rock = rocks[Random.Range(0, rocks.Count)];
                    p = rock.position + dir * (rock.radius + Random.Range(1f, RockReach - 1f));
                    return true;
                }
                default:
                    return false;
            }
        }
    }

    public static class HabitatRules
    {
        /// <summary>먹이별 기본 서식지 (FoodData.habitat이 Auto일 때)</summary>
        public static Habitat Resolve(FoodData f)
        {
            if (!f) return Habitat.Anywhere;
            return f.habitat != Habitat.Auto ? f.habitat : DefaultFor(f.id, f.trait);
        }

        public static Habitat DefaultFor(string id, FoodTrait trait)
        {
            switch (id)
            {
                case "sheep":
                case "rabbit":
                case "golden_melon":
                case "cow":
                case "bison":
                    return Habitat.Meadow;
                case "boar":
                case "deer":
                case "moon_deer":
                case "mushroom":
                    return Habitat.Forest;
                case "salmon":
                    return Habitat.Water;
                case "crab":
                    return Habitat.Shore;
                case "ember_lizard":
                case "fire_goat":
                    return Habitat.Rocky;
                default:
                    return trait == FoodTrait.Fish ? Habitat.Water : Habitat.Anywhere;
            }
        }
    }
}
