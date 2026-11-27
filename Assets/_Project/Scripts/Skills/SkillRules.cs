using System;
using System.Collections.Generic;
using DragonRecipe.Care;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using UnityEngine;

namespace DragonRecipe.Skills
{
    /// <summary>스킬이 닿는 범위. 판정은 Contains, 넓게 거를 때는 center + radius(감싸는 구)</summary>
    public struct SkillArea
    {
        public SkillShape shape;
        public Vector3 origin;
        /// <summary>정규화된 방향 (Cone, Line)</summary>
        public Vector3 dir;
        public float range;
        /// <summary>Cone: 반각(도), Line: 반폭</summary>
        public float width;

        public static SkillArea Sphere(Vector3 center, float radius) =>
            new SkillArea { shape = SkillShape.Ring, origin = center, range = radius, dir = Vector3.forward };

        public static SkillArea Of(SkillSpec s, Vector3 origin, Vector3 dir) => new SkillArea
        {
            shape = s.shape == SkillShape.Projectile ? SkillShape.Ring : s.shape,
            origin = origin,
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward,
            range = s.shape == SkillShape.Projectile ? s.burstRadius : s.range,
            width = s.width
        };

        /// <summary>이 범위를 감싸는 구의 가운데</summary>
        public Vector3 Center => shape == SkillShape.Ring ? origin : origin + dir * (range * 0.5f);

        /// <summary>이 범위를 감싸는 구의 반지름</summary>
        public float Radius => shape switch
        {
            SkillShape.Ring => range,
            SkillShape.Line => range * 0.5f + width,
            _ => Mathf.Max(range * 0.5f, range * Mathf.Sin(Mathf.Min(90f, width) * Mathf.Deg2Rad)) + 0.5f
        };

        /// <summary>point가 범위 안인지. pad만큼 대상 몸집을 봐준다</summary>
        public bool Contains(Vector3 point, float pad = 0f)
        {
            Vector3 d = point - origin;
            switch (shape)
            {
                case SkillShape.Ring:
                    return d.sqrMagnitude <= (range + pad) * (range + pad);
                case SkillShape.Line:
                {
                    float along = Vector3.Dot(d, dir);
                    if (along < -pad || along > range + pad) return false;
                    return (d - dir * along).sqrMagnitude <= (width + pad) * (width + pad);
                }
                default: // Cone
                {
                    float dist = d.magnitude;
                    if (dist > range + pad) return false;
                    if (dist <= pad + 0.5f) return true; // 입 바로 앞
                    float ang = Vector3.Angle(dir, d);
                    // 몸집만큼 각을 조금 넓혀 준다
                    float extra = Mathf.Atan2(pad, dist) * Mathf.Rad2Deg;
                    return ang <= width + extra;
                }
            }
        }
    }

    /// <summary>스킬을 맞을 수 있는 무리 (몬스터, 종족 마을 등). 비행 씬에서 SkillTargets에 등록한다</summary>
    public interface ISkillTarget
    {
        /// <summary>범위 안 대상에게 hit을 준다. 맞은 대상의 몸통(효과 표시용)을 hits에 더하고 맞힌 수를 돌려준다</summary>
        int ApplySkill(SkillArea area, SkillHit hit, List<Transform> hits);

        /// <summary>날아가는 구체(from→to, 반지름 radius)가 대상에 부딪혔는지</summary>
        bool ProjectileHit(Vector3 from, Vector3 to, float radius, out Vector3 point);

        /// <summary>구체 자동 조준 후보 (아직 쓰러지지 않은 대상의 몸통 위치)를 points에 더한다</summary>
        void CollectAimPoints(List<Vector3> points);

        /// <summary>머리 위 체력바용: 살아 있는(쓰러지지 않은) 대상과 남은 체력 비율(0~1)을 into에 더한다</summary>
        void CollectHealth(List<UnitHealth> into);
    }

    /// <summary>체력바 하나 분량</summary>
    public struct UnitHealth
    {
        public Transform unit;
        /// <summary>남은 체력 0~1</summary>
        public float fraction;

        public UnitHealth(Transform unit, float fraction)
        {
            this.unit = unit;
            this.fraction = Mathf.Clamp01(fraction);
        }
    }

    /// <summary>스킬 대상 등록부</summary>
    public static class SkillTargets
    {
        static readonly List<ISkillTarget> _all = new List<ISkillTarget>();
        public static IReadOnlyList<ISkillTarget> All => _all;

        public static void Register(ISkillTarget t)
        {
            if (t != null && !_all.Contains(t)) _all.Add(t);
        }

        public static void Unregister(ISkillTarget t) => _all.Remove(t);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _all.Clear();
    }

    /// <summary>
    /// 구체 충전: 최대 maxCharges개를 모아 둘 수 있고 하나씩 cooldown초마다 찬다.
    /// </summary>
    [Serializable]
    public class SkillCharges
    {
        public int max = 2;
        public float cooldown = 1.4f;
        public int count;
        /// <summary>다음 충전까지 쌓인 시간</summary>
        public float timer;

        public SkillCharges(int max, float cooldown)
        {
            this.max = Mathf.Max(1, max);
            this.cooldown = Mathf.Max(0.05f, cooldown);
            count = this.max;
        }

        public bool Full => count >= max;

        /// <summary>다음 하나가 찬 정도 0~1 (가득이면 1)</summary>
        public float Progress => Full ? 1f : Mathf.Clamp01(timer / cooldown);

        public void Tick(float dt)
        {
            if (Full)
            {
                timer = 0f;
                return;
            }
            timer += Mathf.Max(0f, dt);
            while (timer >= cooldown && count < max)
            {
                timer -= cooldown;
                count++;
            }
            if (Full) timer = 0f;
        }

        public bool TryUse()
        {
            if (count <= 0) return false;
            count--;
            return true;
        }
    }

    /// <summary>스킬 규칙 (순수 계산)</summary>
    public static class SkillRules
    {
        /// <summary>
        /// 구체 칸: 청소년부터 (성체도 그대로 이어 받음). 가장 많이 먹은 성질의 구체, 아직 아무것도 안 먹었으면 없음.
        /// </summary>
        public static SkillSpec PickOrb(SkillBook book, GrowthStage stage, bool hasTrait, FoodTrait trait)
        {
            if (book == null || stage < GrowthStage.Juvenile || !hasTrait) return null;
            return book.Orb(trait);
        }

        /// <summary>브레스 칸: 성체만. 모습에 맞는 브레스 (모습이 없으면 무지개)</summary>
        public static SkillSpec PickBreath(SkillBook book, GrowthStage stage, DragonForm form)
        {
            if (book == null || stage != GrowthStage.Adult) return null;
            return book.Breath(form == DragonForm.None ? DragonForm.Prism : form);
        }

        public static SkillSpec PickOrb(SkillBook book, DragonState s)
        {
            if (s == null) return null;
            bool has = CareLogic.DominantTrait(s, out var trait, out _);
            return PickOrb(book, s.stage, has, trait);
        }

        public static SkillSpec PickBreath(SkillBook book, DragonState s) => s == null ? null : PickBreath(book, s.stage, s.form);

        /// <summary>구체를 쏠 수 있는지</summary>
        public static bool CanFireOrb(SkillSpec s, SkillCharges charges, float stamina) =>
            s != null && s.kind == SkillKind.Orb && charges != null && charges.count > 0 && stamina >= s.staminaCost;

        /// <summary>브레스를 이어 갈 수 있는지. 새로 뿜을 때는 minStamina가 있어야 하고, 뿜는 중에는 0보다 크면 된다</summary>
        public static bool CanBreathe(SkillSpec s, float stamina, bool alreadyBreathing) =>
            s != null && s.kind == SkillKind.Breath && (alreadyBreathing ? stamina > 0f : stamina >= s.minStamina);

        /// <summary>dt 동안 브레스가 쓰는 스태미나 (남은 양을 넘지 않음)</summary>
        public static float BreathDrain(SkillSpec s, float dt, float stamina) =>
            Mathf.Min(Mathf.Max(0f, stamina), s.staminaPerSecond * Mathf.Max(0f, dt));

        /// <summary>
        /// 적에게 줄 값. scale은 브레스 한 틱의 길이(초)처럼 피해에 곱하는 값이고, 구체는 1.
        /// pushDir은 밀어낼 방향.
        /// </summary>
        public static SkillHit EnemyHit(SkillSpec s, Vector3 pushDir, float scale = 1f)
        {
            var e = s.enemyEffects;
            var hit = new SkillHit { damage = s.damage * Mathf.Max(0f, scale) };
            if ((e & SkillEffect.Stun) != 0) hit.stun = s.effectDuration;
            if ((e & SkillEffect.Sleep) != 0) hit.sleep = s.effectDuration;
            if ((e & SkillEffect.Slow) != 0) hit.slow = s.effectDuration;
            if ((e & SkillEffect.Burn) != 0) hit.burn = s.effectDuration;
            if ((e & SkillEffect.Push) != 0 && pushDir.sqrMagnitude > 1e-6f) hit.push = pushDir.normalized * s.pushForce;
            hit.clearArrows = (e & SkillEffect.ClearArrows) != 0;
            return hit;
        }

        /// <summary>먹이가 그 자리에 멈춰 있는 시간 (기절·속박·잠 중 가장 긴 것, 없으면 0)</summary>
        public static float PreyHold(SkillSpec s)
        {
            var e = s.preyEffects;
            if ((e & (SkillEffect.Stun | SkillEffect.Bind | SkillEffect.Sleep)) == 0) return 0f;
            float mult = (e & SkillEffect.Sleep) != 0 ? 1.5f : 1f;
            return s.effectDuration * mult;
        }

        /// <summary>
        /// 자동 조준: forward에서 maxAngle 안에 있는 후보 중 각도가 가장 작은 것의 번호 (없으면 -1).
        /// 거리 range를 넘는 후보는 뺀다.
        /// </summary>
        public static int PickAimTarget(Vector3 from, Vector3 forward, IList<Vector3> candidates, float range, float maxAngle)
        {
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                var d = candidates[i] - from;
                float dist = d.magnitude;
                if (dist < 1f || dist > range) continue;
                float ang = Vector3.Angle(forward, d);
                if (ang > maxAngle) continue;
                // 각도가 우선, 같은 각이면 가까운 쪽
                float score = ang + dist * 0.05f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// 포물선으로 target에 닿게 쏘는 처음 속도 (속력 speed 고정, 낮은 탄도). 닿지 못하면 곧게 겨눈 방향.
        /// </summary>
        public static Vector3 LaunchVelocity(Vector3 from, Vector3 target, float speed, float gravity)
        {
            Vector3 d = target - from;
            if (gravity <= 0.01f) return d.normalized * speed;
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            float x = flat.magnitude;
            if (x < 0.01f) return d.normalized * speed;
            float y = d.y;
            float v2 = speed * speed;
            float disc = v2 * v2 - gravity * (gravity * x * x + 2f * y * v2);
            if (disc < 0f) return d.normalized * speed;
            float tan = (v2 - Mathf.Sqrt(disc)) / (gravity * x);
            float ang = Mathf.Atan(tan);
            return flat / x * (Mathf.Cos(ang) * speed) + Vector3.up * (Mathf.Sin(ang) * speed);
        }

        /// <summary>점 p와 선분 a-b 사이 거리</summary>
        public static float SegmentDistance(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-8f) return Vector3.Distance(a, p);
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
            return Vector3.Distance(a + ab * t, p);
        }
    }
}
