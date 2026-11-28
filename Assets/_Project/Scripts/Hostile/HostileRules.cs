using UnityEngine;

namespace DragonRecipe.Hostile
{
    /// <summary>적의 조준·추격·명중 계산 (순수 함수).</summary>
    public static class HostileRules
    {
        /// <summary>
        /// 움직이는 목표를 맞히는 발사 방향 (앞질러 쏘기).
        /// 따라잡을 수 없으면 false, dir은 지금 목표 쪽.
        /// </summary>
        public static bool LeadDirection(Vector3 from, Vector3 target, Vector3 targetVelocity, float projectileSpeed, out Vector3 dir)
        {
            Vector3 r = target - from;
            dir = r.sqrMagnitude > 1e-6f ? r.normalized : Vector3.forward;
            if (projectileSpeed <= 0f) return false;

            float a = Vector3.Dot(targetVelocity, targetVelocity) - projectileSpeed * projectileSpeed;
            float b = 2f * Vector3.Dot(r, targetVelocity);
            float c = Vector3.Dot(r, r);
            float t;
            if (Mathf.Abs(a) < 1e-5f)
            {
                if (Mathf.Abs(b) < 1e-5f) return false;
                t = -c / b;
            }
            else
            {
                float disc = b * b - 4f * a * c;
                if (disc < 0f) return false;
                float sq = Mathf.Sqrt(disc);
                float t1 = (-b - sq) / (2f * a);
                float t2 = (-b + sq) / (2f * a);
                t = t1 > 0f && t2 > 0f ? Mathf.Min(t1, t2) : Mathf.Max(t1, t2);
            }
            if (t <= 0f) return false;
            Vector3 aim = r + targetVelocity * t;
            if (aim.sqrMagnitude < 1e-6f) return false;
            dir = aim.normalized;
            return true;
        }

        /// <summary>선분 a→b와 점 p 사이 최단 거리 (빠른 화살이 한 프레임에 목표를 지나쳐도 잡아내기 위해)</summary>
        public static float SegmentDistance(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
            return Vector3.Distance(a + ab * t, p);
        }

        /// <summary>최대 회전각 안에서 원하는 방향으로 몸을 돌린다</summary>
        public static Vector3 Steer(Vector3 forward, Vector3 desired, float maxDegrees)
        {
            if (desired.sqrMagnitude < 1e-6f) return forward;
            if (forward.sqrMagnitude < 1e-6f) return desired.normalized;
            return Vector3.RotateTowards(forward.normalized, desired.normalized, maxDegrees * Mathf.Deg2Rad, 0f).normalized;
        }

        /// <summary>몬스터가 드래곤을 쫓기 시작하는지: 가깝고, 자기 자리에서 너무 멀지 않음</summary>
        /// <summary>그리핀이 드래곤을 의심하는지: 아직 쫓아올 거리는 아니지만 그 근처까지 들어옴</summary>
        public static bool Suspects(Vector3 monster, Vector3 home, Vector3 dragon, HostileParams p)
        {
            float r = p.monsterAggroRadius * Mathf.Max(1f, p.monsterSuspectScale);
            return (dragon - monster).sqrMagnitude <= r * r
                   && (monster - home).sqrMagnitude <= p.monsterLeashRadius * p.monsterLeashRadius;
        }

        public static bool ShouldChase(Vector3 monster, Vector3 home, Vector3 dragon, HostileParams p)
            => (dragon - monster).sqrMagnitude <= p.monsterAggroRadius * p.monsterAggroRadius
               && (monster - home).sqrMagnitude <= p.monsterLeashRadius * p.monsterLeashRadius;

        /// <summary>쫓을 때 속도: 가까이 붙으면 덤벼든다</summary>
        public static float ChaseSpeed(float distance, HostileParams p)
            => distance <= p.monsterLungeRange ? p.monsterLungeSpeed : p.monsterChaseSpeed;

        /// <summary>사냥꾼이 겁먹고 흩어지는지: 드래곤이 가까이, 낮게 덮침</summary>
        public static bool Frightened(Vector3 hunter, Vector3 dragon, HostileParams p)
        {
            Vector3 d = dragon - hunter;
            float flat = new Vector2(d.x, d.z).magnitude;
            return flat <= p.hunterFleeRadius && d.y <= p.hunterFleeRadius;
        }
    }
}
