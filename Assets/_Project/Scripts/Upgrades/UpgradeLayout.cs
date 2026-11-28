using System.Collections.Generic;
using DragonRecipe.Tribes;
using UnityEngine;

namespace DragonRecipe.Upgrades
{
    /// <summary>
    /// 업그레이드 트리를 방사형으로 펼친 자리 (순수 계산 → 테스트 가능).
    /// 가운데(심장)에서 뿌리 노드들이 둘레로 퍼지고, 자식은 한 겹씩 바깥 고리에 놓인다.
    /// 노드마다 자기 가지가 차지할 각도를 "그 고리에서 옆 노드와 겹치지 않을 만큼" 잡으므로,
    /// 갈래 수가 제각각이어도 같은 고리의 노드끼리 겹치지 않는다. 노드를 늘려도 자리를 손으로 잡을 필요가 없다.
    /// </summary>
    public class UpgradeLayout
    {
        /// <summary>같은 고리에서 이웃한 노드 사이 거리 (픽셀)</summary>
        public const float Spacing = 124f;
        /// <summary>고리 사이 거리</summary>
        public const float RingStep = 128f;
        /// <summary>첫 고리 반지름</summary>
        public const float FirstRing = 190f;

        public readonly Dictionary<string, Vector2> positions = new Dictionary<string, Vector2>();
        /// <summary>가장 바깥 노드까지의 거리</summary>
        public float radius;
        /// <summary>가장 깊은 노드가 몇 번째 고리인지</summary>
        public int depth;

        class Twig
        {
            public UpgradeNode node;
            public readonly List<Twig> kids = new List<Twig>();
            public int depth;
            public float width; // 차지하는 각도 (rad)
        }

        public Vector2 this[string id] => positions.TryGetValue(id, out var p) ? p : Vector2.zero;

        public static UpgradeLayout Build(IReadOnlyList<UpgradeNode> nodes)
        {
            var layout = new UpgradeLayout();
            var items = new Dictionary<string, Twig>();
            foreach (var n in nodes) items[n.id] = new Twig { node = n };

            // 트리 모양: 첫 번째 앞 단계가 부모 (나머지 앞 단계는 화면에서 선으로만 잇는다)
            var roots = new List<Twig>();
            foreach (var n in nodes)
            {
                var it = items[n.id];
                if (n.requires != null && n.requires.Length > 0 && items.TryGetValue(n.requires[0], out var parent)) parent.kids.Add(it);
                else roots.Add(it);
            }
            roots.Sort((a, b) => RootOrder(a.node).CompareTo(RootOrder(b.node)));
            foreach (var r in roots) SetDepth(r, 1, layout);

            // 필요한 각도의 합이 한 바퀴를 넘으면 고리를 전부 그만큼 키운다 (각도가 줄어 한 바퀴에 들어감)
            float scale = 1f;
            for (int pass = 0; pass < 4; pass++)
            {
                float total = 0f;
                foreach (var r in roots) total += Measure(r, scale);
                if (total <= Mathf.PI * 2f) break;
                scale *= total / (Mathf.PI * 2f) * 1.02f;
            }

            float sum = 0f;
            foreach (var r in roots) sum += r.width;
            // 남는 각도는 뿌리 사이에 고르게 나눈다
            float gap = roots.Count > 0 ? Mathf.Max(0f, Mathf.PI * 2f - sum) / roots.Count : 0f;

            // 엘프 줄기가 위(12시)를 보도록 돌려서, 시계 방향으로 놓는다
            float cursor = 0f, elfAt = 0f;
            var centers = new List<float>();
            foreach (var r in roots)
            {
                float c = cursor + (r.width + gap) * 0.5f;
                centers.Add(c);
                if (r.node.tribeGate && r.node.tribe == Tribe.Elf) elfAt = c;
                cursor += r.width + gap;
            }
            for (int i = 0; i < roots.Count; i++)
                Place(roots[i], Mathf.PI * 0.5f - (centers[i] - elfAt), scale, layout);

            layout.radius = Radius(layout.depth, scale);
            return layout;
        }

        /// <summary>둘레에 놓는 순서: 비행 · 엘프 · 둥지 · 사람 · 스킬 · 오크 · 먹이 (공통 가지 사이에 종족 줄기가 하나씩)</summary>
        static int RootOrder(UpgradeNode n)
        {
            if (n.tribeGate) return n.tribe == Tribe.Elf ? 1 : n.tribe == Tribe.Human ? 3 : 5;
            return n.branch switch
            {
                UpgradeBranch.Flight => 0,
                UpgradeBranch.Nest => 2,
                UpgradeBranch.Skill => 4,
                _ => 6
            };
        }

        static void SetDepth(Twig it, int depth, UpgradeLayout layout)
        {
            it.depth = depth;
            if (depth > layout.depth) layout.depth = depth;
            foreach (var k in it.kids) SetDepth(k, depth + 1, layout);
        }

        static float Radius(int depth, float scale) => (FirstRing + (depth - 1) * RingStep) * scale;

        /// <summary>이 가지가 차지할 각도 = max(자기 고리에서 노드 하나 폭, 자식들 폭의 합)</summary>
        static float Measure(Twig it, float scale)
        {
            float own = Spacing / Radius(it.depth, scale);
            float kids = 0f;
            foreach (var k in it.kids) kids += Measure(k, scale);
            it.width = Mathf.Max(own, kids);
            return it.width;
        }

        static void Place(Twig it, float angle, float scale, UpgradeLayout layout)
        {
            float r = Radius(it.depth, scale);
            layout.positions[it.node.id] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            float total = 0f;
            foreach (var k in it.kids) total += k.width;
            // 자식은 부모 각도를 가운데 두고 시계 방향으로 나란히
            float a = angle + total * 0.5f;
            foreach (var k in it.kids)
            {
                Place(k, a - k.width * 0.5f, scale, layout);
                a -= k.width;
            }
        }
    }
}
