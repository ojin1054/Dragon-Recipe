using UnityEngine;

namespace DragonRecipe.Tribes
{
    /// <summary>가축 목장(<see cref="TribeHerds"/>)이 마을 자리와 프리미티브 만들기를 빌려 쓰는 창구</summary>
    public partial class TribeWorld
    {
        /// <summary>마을이 세워졌으면 그 가운데 자리</summary>
        public bool TryGetVillageCenter(Tribe tribe, out Vector3 center)
        {
            var v = _villages[(int)tribe];
            center = v != null ? v.center : default;
            return v != null;
        }

        /// <summary>콜라이더 없는 색 프리미티브 (주민과 같은 재질)</summary>
        public GameObject MakePart(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Color color)
            => Part(type, name, parent, localPos, scale, color);
    }
}
