using UnityEngine;
using UnityEngine.SceneManagement;

namespace DragonRecipe.Core
{
    /// <summary>
    /// 비행 맵의 출발 지점: 좌우·위아래로 쭉 이어지는 절벽 벽, 그 벽에 파인 동굴(둥지), 벽 밖으로 튀어나온 발판.
    /// 드래곤은 동굴 안 둥지에서 달려 나와 발판 끝에서 도약한다 (+z 방향).
    /// 씬 빌더가 부르고, 예전에 만든 씬(따로 솟은 절벽)은 열릴 때 스스로 바꿔 놓는다.
    /// </summary>
    public static class CliffWall
    {
        public const string RootName = "CliffWall";
        const string FlightScene = "FlightPrototype";

        /// <summary>발판이 벽에서 튀어나온 길이 (절벽 끝에서 벽면까지)</summary>
        public const float LedgeLength = 12f;
        public const float LedgeWidth = 14f;
        /// <summary>벽 높이. 스폰 위치를 찾는 아래 방향 레이(y=200)보다 낮아야 벽 위가 '땅 아님'으로 걸러진다</summary>
        public const float WallTop = 150f;
        public const float WallHalfWidth = 600f;
        public const float WallDepth = 480f;
        public const float CaveWidth = 12f;
        public const float CaveHeight = 10f;
        /// <summary>둥지 뒤로 동굴이 들어간 깊이 (따라오는 카메라가 들어갈 자리)</summary>
        public const float CaveBack = 24f;

        /// <summary>벽면의 z (이보다 뒤는 전부 바위)</summary>
        public static float WallZ(float edgeZ) => edgeZ - LedgeLength;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == FlightScene) Upgrade();
        }

        /// <summary>예전 씬: 따로 솟은 절벽('Cliff')과 임시 동굴('Cave')을 벽으로 바꾼다</summary>
        public static bool Upgrade()
        {
            var worldGo = GameObject.Find("World");
            if (!worldGo) return false;
            var world = worldGo.transform;
            if (world.Find(RootName)) return false;
            var oldCliff = world.Find("Cliff");
            var ring = world.Find("NestRing");
            if (!oldCliff || !ring) return false;

            float floorY = oldCliff.position.y + oldCliff.lossyScale.y * 0.5f;
            float edgeZ = oldCliff.position.z + oldCliff.lossyScale.z * 0.5f;
            var rock = oldCliff.GetComponent<Renderer>() ? oldCliff.GetComponent<Renderer>().sharedMaterial : null;
            var oldCave = world.Find("Cave");
            var caveRenderer = oldCave ? oldCave.GetComponentInChildren<Renderer>() : null;
            var caveRock = caveRenderer ? caveRenderer.sharedMaterial : rock;

            Remove(oldCliff.gameObject);
            if (oldCave) Remove(oldCave.gameObject);
            Build(world, new Vector3(ring.position.x, floorY, ring.position.z), edgeZ, rock, caveRock);

            // 벽 속에 묻히는 나무·바위는 치운다
            var obstacles = world.Find("Obstacles");
            if (obstacles)
                foreach (Transform o in obstacles)
                    if (o.position.z < edgeZ + 4f) o.gameObject.SetActive(false);
            Physics.SyncTransforms();
            return true;
        }

        static void Remove(GameObject go)
        {
            go.SetActive(false);
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

        /// <summary>
        /// nestFloor = 둥지 자리 바닥 (y = 발판 높이), edgeZ = 발판 끝(도약 지점)의 z.
        /// 벽면은 edgeZ - <see cref="LedgeLength"/>, 동굴은 벽면에서 둥지 뒤까지 뚫려 있다.
        /// </summary>
        public static Transform Build(Transform world, Vector3 nestFloor, float edgeZ, Material rock, Material caveRock)
        {
            var root = new GameObject(RootName).transform;
            root.SetParent(world, false);

            float h = nestFloor.y;
            float cx = nestFloor.x;
            float wallZ = WallZ(edgeZ);
            float backZ = wallZ - WallDepth;
            float caveEnd = nestFloor.z - CaveBack;
            float halfW = CaveWidth * 0.5f;
            float roof = h + CaveHeight;
            var rng = new System.Random(11);

            // ── 벽 몸통: 동굴 자리만 비우고 네 덩어리로 채운다 ──
            Box(root, "WallBelow", cx - WallHalfWidth, cx + WallHalfWidth, 0f, h, backZ, wallZ, rock);
            Box(root, "WallLeft", cx - WallHalfWidth, cx - halfW, h, WallTop, backZ, wallZ, rock);
            Box(root, "WallRight", cx + halfW, cx + WallHalfWidth, h, WallTop, backZ, wallZ, rock);
            Box(root, "WallAbove", cx - halfW, cx + halfW, roof, WallTop, backZ, wallZ, rock);
            Box(root, "WallBehindCave", cx - halfW, cx + halfW, h, roof, backZ, caveEnd, rock);

            // ── 동굴 안쪽 면 (어두운 바위. 벽 몸통 바로 안쪽에 얇게 덧댄다) ──
            const float skin = 0.3f;
            Box(root, "CaveFloor", cx - halfW, cx + halfW, h - skin, h + 0.02f, caveEnd, wallZ, caveRock, collider: false); // 벽 몸통 윗면과 겹쳐 깜빡이지 않게 살짝 위
            Box(root, "CaveSideL", cx - halfW, cx - halfW + skin, h, roof, caveEnd, wallZ, caveRock, collider: false);
            Box(root, "CaveSideR", cx + halfW - skin, cx + halfW, h, roof, caveEnd, wallZ, caveRock, collider: false);
            Box(root, "CaveRoof", cx - halfW, cx + halfW, roof - skin, roof, caveEnd, wallZ, caveRock, collider: false);
            Box(root, "CaveEnd", cx - halfW, cx + halfW, h, roof, caveEnd, caveEnd + skin, caveRock, collider: false);

            // ── 발판: 벽에서 튀어나온 판 + 아래로 갈수록 짧아지는 받침 (옆에서 보면 비스듬한 턱) ──
            float lw = LedgeWidth * 0.5f;
            const float slab = 1.6f;
            Box(root, "Ledge", cx - lw, cx + lw, h - slab, h, wallZ, edgeZ, rock);
            float[] reach = { 0.72f, 0.5f, 0.33f, 0.2f, 0.1f };
            float y = h - slab;
            for (int i = 0; i < reach.Length; i++)
            {
                float step = 2.2f + i * 0.5f;
                float inset = 0.6f + i * 0.5f;
                Box(root, $"LedgeBrace_{i}", cx - lw + inset, cx + lw - inset, y - step, y, wallZ, wallZ + LedgeLength * reach[i], rock);
                y -= step;
            }

            // ── 입구 둘레 바위: 네모난 입구를 울퉁불퉁한 아치처럼 (콜라이더 없음) ──
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                float s = Range(rng, 2.4f, 3.6f);
                float arch = Mathf.Sin(t * Mathf.PI) * 1.2f; // 가운데가 조금 높다
                Lump(root, PrimitiveType.Sphere, $"MouthTop_{i}",
                    new Vector3(cx + Mathf.Lerp(-halfW, halfW, t), roof + 0.2f + arch * 0.3f - (1f - Mathf.Sin(t * Mathf.PI)) * 1.6f, wallZ + 0.2f),
                    new Vector3(s, s * 0.8f, s * 0.6f), Vector3.zero, caveRock);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    float s = Range(rng, 2f, 3.2f);
                    Lump(root, PrimitiveType.Sphere, $"MouthSide_{(side < 0 ? "L" : "R")}{i}",
                        new Vector3(cx + side * (halfW + Range(rng, 0f, 0.4f)), h + CaveHeight * (0.15f + 0.3f * i), wallZ + 0.2f),
                        new Vector3(s * 0.7f, s, s * 0.6f), Vector3.zero, caveRock);
                }
            }

            // ── 벽면 결: 가로 지층 턱과 튀어나온 바위로 밋밋한 면을 깬다 (콜라이더 없음) ──
            for (int i = 0; i < 46; i++)
            {
                float x = cx + Range(rng, -WallHalfWidth * 0.75f, WallHalfWidth * 0.75f);
                float yy = Range(rng, 4f, WallTop - 6f);
                float len = Range(rng, 18f, 70f);
                float thick = Range(rng, 1.2f, 3.5f);
                if (Mathf.Abs(x - cx) < halfW + len * 0.5f + 3f && yy > h - 16f && yy < roof + 6f) continue; // 입구·발판 주변은 비운다
                Lump(root, PrimitiveType.Cube, $"Strata_{i}", new Vector3(x, yy, wallZ + 0.1f),
                    new Vector3(len, thick, Range(rng, 1.2f, 2.6f)), new Vector3(0f, 0f, Range(rng, -2.5f, 2.5f)), i % 3 == 0 ? caveRock : rock);
            }
            for (int i = 0; i < 30; i++)
            {
                float x = cx + Range(rng, -WallHalfWidth * 0.7f, WallHalfWidth * 0.7f);
                float yy = Range(rng, 2f, WallTop - 10f);
                float s = Range(rng, 5f, 13f);
                if (Mathf.Abs(x - cx) < halfW + s && yy > h - 18f && yy < roof + s) continue;
                Lump(root, PrimitiveType.Cube, $"Crag_{i}", new Vector3(x, yy, wallZ - s * 0.2f),
                    new Vector3(s, s * Range(rng, 0.7f, 1.6f), s * 0.8f),
                    new Vector3(Range(rng, -12f, 12f), Range(rng, 0f, 60f), Range(rng, -12f, 12f)), rock);
            }

            // ── 종유석 (콜라이더 없음) ──
            for (int i = 0; i < 12; i++)
            {
                float len = Range(rng, 0.8f, 2f);
                Lump(root, PrimitiveType.Cube, $"Stalactite_{i}",
                    new Vector3(cx + Range(rng, -halfW * 0.85f, halfW * 0.85f), roof - len * 0.5f, Range(rng, caveEnd + 1f, wallZ - 1.5f)),
                    new Vector3(0.35f, len, 0.35f), new Vector3(0f, 45f, 0f), caveRock);
            }

            // ── 둥지를 비추는 따뜻한 불빛 ──
            var lightGo = new GameObject("CaveGlow");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.position = new Vector3(cx, h + 3.5f, nestFloor.z - 0.5f);
            var glow = lightGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.72f, 0.42f);
            glow.intensity = 4f;
            glow.range = CaveWidth * 1.8f;
            glow.shadows = LightShadows.None;

            return root;
        }

        static float Range(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        static GameObject Box(Transform root, string name, float x0, float x1, float y0, float y1, float z0, float z1,
            Material mat, bool collider = true)
        {
            return Lump(root, PrimitiveType.Cube, name,
                new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f),
                new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0)), Vector3.zero, mat, collider);
        }

        static GameObject Lump(Transform root, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Vector3 euler,
            Material mat, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
            go.transform.localScale = scale;
            if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider)
            {
                var c = go.GetComponent<Collider>();
                if (Application.isPlaying) { c.enabled = false; Object.Destroy(c); }
                else Object.DestroyImmediate(c);
            }
            return go;
        }
    }
}
