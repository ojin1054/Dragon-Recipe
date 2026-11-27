using System;
using UnityEngine;

namespace DragonRecipe.Hostile
{
    /// <summary>드래곤을 공격하는 적(사냥꾼 무리, 날개 몬스터) 수치.</summary>
    [Serializable]
    public class HostileParams
    {
        [Header("배치")]
        [Tooltip("출발 절벽(원점) 근처 이 반경 안에는 적을 두지 않는다")]
        public float keepOutRadius = 60f;

        [Header("사냥꾼 무리")]
        public int hunterGroups = 6;
        public int hunterGroupMin = 3;
        public int hunterGroupMax = 6;
        [Tooltip("무리 구성원이 모여 있는 반경")]
        public float hunterGroupSpread = 5f;
        [Tooltip("이 거리 안에 드래곤이 들어오면 활을 쏜다")]
        public float hunterRange = 55f;
        [Tooltip("시위를 당기는 시간 (활이 들리고 화살촉이 빨개짐)")]
        public float hunterAimTime = 0.7f;
        public float hunterReloadMin = 2f;
        public float hunterReloadMax = 3.5f;
        [Tooltip("조준 오차 (도)")]
        public float arrowSpread = 2.5f;
        public float arrowSpeed = 45f;
        public float arrowDamage = 6f;
        public float arrowLifetime = 3f;
        [Tooltip("드래곤이 이만큼 가까이 낮게 덮치면 무리가 흩어져 달아난다 (그동안 쏘지 않음)")]
        public float hunterFleeRadius = 9f;
        public float hunterFleeTime = 3f;
        public float hunterFleeSpeed = 6f;
        public float hunterWalkSpeed = 1.2f;

        [Header("날개 몬스터")]
        public int monsterCount = 5;
        [Tooltip("둥지(맴도는 자리)의 지면 위 높이")]
        public float monsterHoverHeight = 16f;
        public float monsterCircleRadius = 10f;
        [Tooltip("드래곤이 이 거리 안에 오면 쫓아온다")]
        public float monsterAggroRadius = 55f;
        [Tooltip("자기 자리에서 이만큼 멀어지면 포기하고 돌아간다")]
        public float monsterLeashRadius = 110f;
        public float monsterIdleSpeed = 8f;
        public float monsterChaseSpeed = 21f;
        [Tooltip("가까이 붙으면 이 속도로 덤벼든다")]
        public float monsterLungeSpeed = 30f;
        public float monsterLungeRange = 12f;
        [Tooltip("초당 회전 (도)")]
        public float monsterTurnRate = 110f;
        public float biteRange = 1.6f;
        public float biteDamage = 12f;
        [Tooltip("물고 나서 물러나 있는 시간")]
        public float monsterRetreatTime = 2f;
    }
}
