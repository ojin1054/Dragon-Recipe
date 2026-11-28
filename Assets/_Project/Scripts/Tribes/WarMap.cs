using UnityEngine;

namespace DragonRecipe.Tribes
{
    /// <summary>
    /// 전쟁 맵 표시. 이게 있는 비행 씬은 참전 중에만 쓰는 전용 전장이다.
    /// - 편든 종족 진영 절벽(씬의 Nest / LaunchEdge)에서 이륙한다.
    /// - 절벽 아래에 편든 쪽 진영, 반대편 끝에 적 진영이 서고, 적이 대규모로 몰려온다.
    /// - 진영 깃발 색은 실제로 편든 종족·적 종족에 맞춰 칠한다.
    /// 배치와 행군은 TribeWorld가 맡는다.
    /// </summary>
    public class WarMap : MonoBehaviour
    {
        public static WarMap Current { get; private set; }

        [Tooltip("편든 쪽 진영 가운데 (절벽 아래). 적이 여기까지 오면 뚫린다")]
        public Vector3 allyCamp = new Vector3(0f, 0f, 45f);
        [Tooltip("적 진영 가운데 (반대편 끝). 적 무리가 여기서 출발한다")]
        public Vector3 enemyCamp = new Vector3(0f, 0f, 320f);
        [Tooltip("편든 종족 색으로 칠할 깃발 (절벽 위)")]
        public Renderer[] allyBanners;
        [Tooltip("적 종족 색으로 칠할 깃발 (적 진영 뒤)")]
        public Renderer[] enemyBanners;

        void Awake() => Current = this;

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        /// <summary>진영 깃발을 두 종족 색으로 칠한다</summary>
        public void Paint(Tribe ally, Tribe enemy)
        {
            var block = new MaterialPropertyBlock();
            Paint(allyBanners, TribeLook.Skin(ally), block);
            Paint(enemyBanners, TribeLook.Skin(enemy), block);
        }

        static void Paint(Renderer[] list, Color c, MaterialPropertyBlock block)
        {
            if (list == null) return;
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            foreach (var r in list)
                if (r) r.SetPropertyBlock(block);
        }
    }
}
