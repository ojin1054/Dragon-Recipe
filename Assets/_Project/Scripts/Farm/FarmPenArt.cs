using System;
using UnityEngine;

namespace DragonRecipe.Farm
{
    /// <summary>우리 확장 단계 하나의 크기와 꾸밈 (임시 도형 아트)</summary>
    [Serializable]
    public class PenLevel
    {
        [Tooltip("울타리 반지름 (x, z)")]
        public Vector2 half = new Vector2(2.6f, 2.1f);
        [Tooltip("가로대 줄 수 (1~3)")]
        [Range(1, 3)] public int rails = 1;
        [Tooltip("가로대를 하얗게 칠한 울타리")]
        public bool paintedRails;
        [Tooltip("울타리 아래 돌담")]
        public bool stoneBase;
        [Tooltip("입구 문 기둥")]
        public bool gate;
        public bool hay;
        public bool trough;
        public bool shed;
        public bool flowers;
        public bool lanterns;
    }

    /// <summary>
    /// 우리를 도형으로 짓는다. 재질은 하나만 쓰고 색은 MaterialPropertyBlock으로 칠한다.
    /// 나중에 진짜 아트(프리팹)로 바꿀 자리.
    /// </summary>
    public static class FarmPenArt
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        static readonly Color Wood = new Color(0.45f, 0.3f, 0.18f);
        static readonly Color Rail = new Color(0.62f, 0.45f, 0.27f);
        static readonly Color Paint = new Color(0.9f, 0.88f, 0.82f);
        static readonly Color Stone = new Color(0.56f, 0.54f, 0.5f);
        static readonly Color Grass = new Color(0.45f, 0.68f, 0.33f);
        static readonly Color Dirt = new Color(0.55f, 0.42f, 0.28f);
        static readonly Color Water = new Color(0.3f, 0.58f, 0.92f);
        static readonly Color Straw = new Color(0.92f, 0.78f, 0.4f);
        static readonly Color Roof = new Color(0.62f, 0.26f, 0.2f);
        static readonly Color Leaf = new Color(0.28f, 0.56f, 0.27f);
        static readonly Color Light = new Color(1f, 0.85f, 0.45f);
        static readonly Color[] Petals = { new Color(1f, 0.55f, 0.7f), new Color(1f, 0.9f, 0.35f), new Color(0.75f, 0.6f, 1f) };

        static MaterialPropertyBlock _block;

        /// <summary>
        /// 우리 하나를 parent 아래에 짓는다 (parent 위치가 우리 바닥 가운데).
        /// 울타리 앞(-z) 가운데는 문 자리로 비운다.
        /// </summary>
        public static Transform Build(Transform parent, PenLevel lv, Material mat, int seed)
        {
            var root = new GameObject("PenArt").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(seed);
            var h = lv.half;

            // 바닥: 풀밭 + 가운데 흙길
            Box(root, "Grass", new Vector3(0, 0.03f, 0), new Vector3(h.x * 2f + 1.2f, 0.06f, h.y * 2f + 1.2f), Grass, mat);
            Box(root, "Path", new Vector3(0, 0.065f, -h.y * 0.5f), new Vector3(1.1f, 0.02f, h.y + 0.6f), Dirt, mat);

            Fence(root, lv, mat);
            if (lv.hay) Hay(root, lv, mat, rng);
            if (lv.trough) Trough(root, lv, mat);
            if (lv.shed) Shed(root, lv, mat);
            if (lv.flowers) Flowers(root, lv, mat, rng);
            if (lv.lanterns) Lanterns(root, lv, mat);
            return root;
        }

        static void Fence(Transform root, PenLevel lv, Material mat)
        {
            var h = lv.half;
            const float step = 1.3f, gateHalf = 0.8f;
            float postH = 0.75f + lv.rails * 0.3f;
            Vector3[] c =
            {
                new Vector3(-h.x, 0, -h.y), new Vector3(h.x, 0, -h.y),
                new Vector3(h.x, 0, h.y), new Vector3(-h.x, 0, h.y)
            };
            for (int side = 0; side < 4; side++)
            {
                var a = c[side];
                var b = c[(side + 1) % 4];
                // 앞쪽(첫 변)은 가운데를 문으로 비워 두 토막으로
                if (side == 0)
                {
                    FenceRun(root, a, new Vector3(-gateHalf, 0, a.z), lv, mat, postH, step);
                    FenceRun(root, new Vector3(gateHalf, 0, a.z), b, lv, mat, postH, step);
                    if (lv.gate)
                    {
                        Box(root, "GatePostL", new Vector3(-gateHalf, (postH + 0.5f) * 0.5f, a.z), new Vector3(0.3f, postH + 0.5f, 0.3f), Wood, mat);
                        Box(root, "GatePostR", new Vector3(gateHalf, (postH + 0.5f) * 0.5f, a.z), new Vector3(0.3f, postH + 0.5f, 0.3f), Wood, mat);
                        Box(root, "GateTop", new Vector3(0, postH + 0.45f, a.z), new Vector3(gateHalf * 2f + 0.5f, 0.18f, 0.24f),
                            lv.paintedRails ? Paint : Rail, mat);
                    }
                    continue;
                }
                FenceRun(root, a, b, lv, mat, postH, step);
            }
        }

        static void FenceRun(Transform root, Vector3 a, Vector3 b, PenLevel lv, Material mat, float postH, float step)
        {
            float len = Vector3.Distance(a, b);
            if (len < 0.1f) return;
            int posts = Mathf.Max(1, Mathf.RoundToInt(len / step));
            for (int i = 0; i <= posts; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)posts);
                Box(root, "Post", p + Vector3.up * (postH * 0.5f), new Vector3(0.22f, postH, 0.22f), Wood, mat);
                // 기둥 머리 (칠한 울타리)
                if (lv.paintedRails)
                    Box(root, "PostCap", p + Vector3.up * (postH + 0.06f), new Vector3(0.28f, 0.12f, 0.28f), Paint, mat);
            }

            var mid = (a + b) * 0.5f;
            var rot = Quaternion.LookRotation(b - a, Vector3.up);
            float baseY = 0f;
            if (lv.stoneBase)
            {
                var wall = Box(root, "StoneBase", mid + Vector3.up * 0.2f, new Vector3(0.4f, 0.4f, len + 0.3f), Stone, mat);
                wall.localRotation = rot;
                baseY = 0.3f;
            }
            for (int r = 0; r < lv.rails; r++)
            {
                float t = lv.rails == 1 ? 0.55f : r / (float)(lv.rails - 1);
                float y = Mathf.Lerp(baseY + 0.3f, postH - 0.15f, t);
                var rail = Box(root, "Rail", mid + Vector3.up * y, new Vector3(0.1f, 0.12f, len + 0.15f), lv.paintedRails ? Paint : Rail, mat);
                rail.localRotation = rot;
            }
        }

        static void Hay(Transform root, PenLevel lv, Material mat, System.Random rng)
        {
            var h = lv.half;
            var at = new Vector3(-h.x + 0.9f, 0f, h.y - 0.9f);
            int bales = lv.shed ? 3 : 2;
            for (int i = 0; i < bales; i++)
            {
                var p = at + new Vector3(i * 0.95f, 0.3f, (float)rng.NextDouble() * 0.2f);
                var bale = Box(root, "Hay", p, new Vector3(0.85f, 0.6f, 0.6f), Straw, mat);
                bale.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 20f - 10f, 0);
            }
            if (lv.shed) Box(root, "HayTop", at + new Vector3(0.48f, 0.9f, 0f), new Vector3(0.85f, 0.6f, 0.6f), Straw, mat);
        }

        static void Trough(Transform root, PenLevel lv, Material mat)
        {
            var at = new Vector3(lv.half.x - 0.9f, 0f, -lv.half.y + 1.1f);
            Box(root, "Trough", at + Vector3.up * 0.2f, new Vector3(0.6f, 0.4f, 1.4f), Wood, mat);
            Box(root, "TroughWater", at + Vector3.up * 0.38f, new Vector3(0.44f, 0.06f, 1.24f), Water, mat);
        }

        static void Shed(Transform root, PenLevel lv, Material mat)
        {
            var at = new Vector3(lv.half.x - 1.4f, 0f, lv.half.y - 1.2f);
            Box(root, "ShedBody", at + Vector3.up * 0.75f, new Vector3(2f, 1.5f, 1.6f), Rail, mat);
            Box(root, "ShedDoor", at + new Vector3(0f, 0.5f, -0.81f), new Vector3(0.7f, 1f, 0.04f), Wood, mat);
            var roofL = Box(root, "RoofL", at + new Vector3(-0.55f, 1.75f, 0f), new Vector3(1.35f, 0.12f, 1.9f), Roof, mat);
            roofL.localRotation = Quaternion.Euler(0, 0, 32f);
            var roofR = Box(root, "RoofR", at + new Vector3(0.55f, 1.75f, 0f), new Vector3(1.35f, 0.12f, 1.9f), Roof, mat);
            roofR.localRotation = Quaternion.Euler(0, 0, -32f);
        }

        static void Flowers(Transform root, PenLevel lv, Material mat, System.Random rng)
        {
            var h = lv.half;
            for (int i = 0; i < 14; i++)
            {
                // 울타리 바로 바깥 둘레에 듬성듬성
                float t = (float)rng.NextDouble();
                int side = rng.Next(4);
                var p = side switch
                {
                    0 => new Vector3(Mathf.Lerp(-h.x, h.x, t), 0, -h.y - 0.45f),
                    1 => new Vector3(h.x + 0.45f, 0, Mathf.Lerp(-h.y, h.y, t)),
                    2 => new Vector3(Mathf.Lerp(-h.x, h.x, t), 0, h.y + 0.45f),
                    _ => new Vector3(-h.x - 0.45f, 0, Mathf.Lerp(-h.y, h.y, t))
                };
                if (side == 0 && Mathf.Abs(p.x) < 1.2f) continue; // 문 앞은 비움
                Box(root, "Stem", p + Vector3.up * 0.15f, new Vector3(0.06f, 0.3f, 0.06f), Leaf, mat);
                Box(root, "Petal", p + Vector3.up * 0.33f, Vector3.one * 0.16f, Petals[rng.Next(Petals.Length)], mat);
            }
        }

        static void Lanterns(Transform root, PenLevel lv, Material mat)
        {
            var h = lv.half;
            foreach (var x in new[] { -h.x, h.x })
            {
                var p = new Vector3(x, 0, -h.y);
                Box(root, "LanternPole", p + Vector3.up * 1.1f, new Vector3(0.14f, 2.2f, 0.14f), Wood, mat);
                Box(root, "Lantern", p + Vector3.up * 2.25f, new Vector3(0.3f, 0.3f, 0.3f), Light, mat);
            }
        }

        static Transform Box(Transform parent, string name, Vector3 localPos, Vector3 scale, Color color, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (mat) r.sharedMaterial = mat;
            _block ??= new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            r.SetPropertyBlock(_block);
            return t;
        }
    }
}
