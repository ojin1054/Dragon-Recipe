using UnityEngine;

namespace DragonRecipe.Tribes
{
    /// <summary>
    /// 마을 주민·병사 한 명. 움직임은 TribeWorld가 한꺼번에 돌리고, 이 컴포넌트는 상태만 들고 있다.
    /// 공격·스킬은 TribeWorld.Hit / Stun / Knockback으로 건드린다.
    /// </summary>
    public class TribeMember : MonoBehaviour
    {
        public Tribe Tribe { get; internal set; }
        /// <summary>무기를 들고 드래곤을 쏠 수 있다</summary>
        public bool IsSoldier { get; internal set; }
        /// <summary>전쟁터에 나간 병사</summary>
        public bool IsWarParty { get; internal set; }
        /// <summary>방어전: 편든 쪽 마을로 몰려오는 적</summary>
        public bool IsRaider { get; internal set; }
        /// <summary>공격받아 쓰러져 있음</summary>
        public bool IsDown => Time.time < downUntil;
        /// <summary>기절·둔화 등으로 멈춰 있음</summary>
        public bool IsStunned => Time.time < stunUntil;

        internal Vector3 home;
        internal float groundY;
        internal Vector3 villageCenter;
        internal Transform weapon;
        internal Renderer tip;
        internal float reload;
        internal float aim;
        internal float fleeUntil = -1f;
        internal Vector3 fleeDir;
        internal float downUntil = -1f;
        internal float stunUntil = -1f;
        internal Vector3 push;
        internal Vector3 wanderTarget;
        internal float nextWander;
        internal float phase;
        /// <summary>전쟁터에서 마주 보는 쪽</summary>
        internal Vector3 warFacing;
        /// <summary>무기를 겨누는 쪽 (자기 위치 기준)</summary>
        internal Vector3 aimOffset;
        /// <summary>방어전: 마을 가운데에 닿음 (다음 정리 때 뚫림 처리)</summary>
        internal bool pendingBreach;
    }
}
