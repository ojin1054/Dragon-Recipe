using System.Collections.Generic;
using DragonRecipe.Skills;
using UnityEngine;

namespace DragonRecipe.Hostile
{
    /// <summary>
    /// 드래곤 스킬(구체·브레스)이 적에게 닿았을 때: 체력, 기절·잠·둔화·화상, 밀려남, 화살 떨어뜨리기.
    /// 체력이 다하면 쓰러져 다시 날기 전까지 움직이지 않는다.
    /// </summary>
    public partial class HostileSpawner : ISkillTarget
    {
        [Header("스킬에 맞을 때")]
        [Tooltip("사냥꾼·몬스터 체력 (스킬 피해로만 준다)")]
        public float enemyHealth = 3f;
        [Tooltip("불붙은 동안 초당 피해")]
        public float burnDamagePerSecond = 0.8f;
        [Tooltip("둔화 중 움직임 배율")]
        [Range(0.1f, 1f)] public float slowFactor = 0.35f;
        [Tooltip("밀려나는 속도가 줄어드는 빠르기 (1/초)")]
        public float pushDamping = 4f;

        class SkillStatus
        {
            public float hp;
            public float stunUntil = -1f;
            public float sleepUntil = -1f;
            public float slowUntil = -1f;
            public float burnUntil = -1f;
            public Vector3 push;
            public bool down;
            public float fallSpeed;
        }

        readonly Dictionary<object, SkillStatus> _skill = new Dictionary<object, SkillStatus>();

        void OnEnable() => SkillTargets.Register(this);
        void OnDisable() => SkillTargets.Unregister(this);

        SkillStatus StatusOf(object enemy)
        {
            if (!_skill.TryGetValue(enemy, out var s))
            {
                s = new SkillStatus { hp = enemyHealth };
                _skill[enemy] = s;
            }
            return s;
        }

        void ResetSkillStatus()
        {
            _skill.Clear();
            foreach (var h in _hunters)
                if (h.root) h.root.rotation = Quaternion.Euler(0f, h.root.eulerAngles.y, 0f);
            foreach (var m in _monsters)
                if (m.root) m.root.gameObject.SetActive(true);
        }

        static Vector3 BodyOf(Hunter h) => h.root.position + Vector3.up * 1f;
        static Vector3 BodyOf(Monster m) => m.root.position;

        // ───────── ISkillTarget ─────────

        public int ApplySkill(SkillArea area, SkillHit hit, List<Transform> hits)
        {
            if (!_built) return 0;
            int n = 0;
            foreach (var h in _hunters)
            {
                if (!h.root || !area.Contains(BodyOf(h), 0.8f)) continue;
                var s = StatusOf(h);
                if (s.down) continue;
                Apply(s, hit);
                hits?.Add(h.root);
                n++;
            }
            foreach (var m in _monsters)
            {
                if (!m.root || !m.root.gameObject.activeSelf || !area.Contains(BodyOf(m), 1.2f)) continue;
                var s = StatusOf(m);
                if (s.down) continue;
                Apply(s, hit);
                hits?.Add(m.root);
                n++;
            }
            if (hit.clearArrows) ClearArrowsIn(area);
            return n;
        }

        public bool ProjectileHit(Vector3 from, Vector3 to, float radius, out Vector3 point)
        {
            point = default;
            if (!_built) return false;
            float best = float.MaxValue;
            foreach (var h in _hunters)
            {
                if (!h.root || (_skill.TryGetValue(h, out var s) && s.down)) continue;
                var b = BodyOf(h);
                if (SkillRules.SegmentDistance(from, to, b) > radius + 0.8f) continue;
                float d = (b - from).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    point = b;
                }
            }
            foreach (var m in _monsters)
            {
                if (!m.root || !m.root.gameObject.activeSelf || (_skill.TryGetValue(m, out var s) && s.down)) continue;
                var b = BodyOf(m);
                if (SkillRules.SegmentDistance(from, to, b) > radius + 1.2f) continue;
                float d = (b - from).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    point = b;
                }
            }
            return best < float.MaxValue;
        }

        public void CollectAimPoints(List<Vector3> points)
        {
            if (!_built) return;
            foreach (var h in _hunters)
                if (h.root && !(_skill.TryGetValue(h, out var s) && s.down)) points.Add(BodyOf(h));
            foreach (var m in _monsters)
                if (m.root && m.root.gameObject.activeSelf && !(_skill.TryGetValue(m, out var s) && s.down)) points.Add(BodyOf(m));
        }

        public void CollectHealth(List<UnitHealth> into)
        {
            if (!_built) return;
            float max = Mathf.Max(0.01f, enemyHealth);
            foreach (var h in _hunters)
            {
                if (!h.root) continue;
                float hp = max;
                if (_skill.TryGetValue(h, out var s))
                {
                    if (s.down) continue;
                    hp = s.hp;
                }
                into.Add(new UnitHealth(h.root, hp / max));
            }
            foreach (var m in _monsters)
            {
                if (!m.root || !m.root.gameObject.activeSelf) continue;
                float hp = max;
                if (_skill.TryGetValue(m, out var s))
                {
                    if (s.down) continue;
                    hp = s.hp;
                }
                into.Add(new UnitHealth(m.root, hp / max));
            }
        }

        void Apply(SkillStatus s, SkillHit hit)
        {
            float now = Time.time;
            if (hit.stun > 0f) s.stunUntil = Mathf.Max(s.stunUntil, now + hit.stun);
            if (hit.slow > 0f) s.slowUntil = Mathf.Max(s.slowUntil, now + hit.slow);
            if (hit.burn > 0f) s.burnUntil = Mathf.Max(s.burnUntil, now + hit.burn);
            if (hit.sleep > 0f) s.sleepUntil = Mathf.Max(s.sleepUntil, now + hit.sleep);
            else if (hit.damage > 0f) s.sleepUntil = -1f; // 맞으면 깬다
            if (hit.push.sqrMagnitude > 0f) s.push = hit.push;
            Damage(s, hit.damage);
        }

        void Damage(SkillStatus s, float amount)
        {
            if (amount <= 0f || s.down) return;
            s.hp -= amount;
            if (s.hp <= 0f)
            {
                s.down = true;
                s.burnUntil = -1f;
            }
        }

        void ClearArrowsIn(SkillArea area)
        {
            for (int i = _arrows.Count - 1; i >= 0; i--)
            {
                var a = _arrows[i];
                if (a.t)
                {
                    if (a.stuck || !area.Contains(a.t.position, 0.6f)) continue;
                    Destroy(a.t.gameObject);
                }
                _arrows.RemoveAt(i);
            }
        }

        // ───────── 매 프레임 (Update에서 불림) ─────────

        /// <summary>기절·잠·쓰러짐이면 true (평소 행동을 건너뜀). 둔화면 dt를 줄인다</summary>
        bool CommonHold(object enemy, ref float dt, out SkillStatus s)
        {
            if (!_skill.TryGetValue(enemy, out s)) return false;
            float now = Time.time;
            float realDt = dt;
            if (now < s.burnUntil) Damage(s, burnDamagePerSecond * realDt);
            if (s.down) return true;
            if (now < s.slowUntil) dt *= slowFactor;
            return now < s.stunUntil || now < s.sleepUntil;
        }

        Vector3 TakePush(SkillStatus s, float dt)
        {
            if (s.push.sqrMagnitude < 0.01f) return Vector3.zero;
            var step = s.push * dt;
            s.push *= Mathf.Exp(-pushDamping * dt);
            return step;
        }

        bool SkillHoldHunter(Hunter h, ref float dt)
        {
            if (!h.root) return true;
            float realDt = dt;
            bool held = CommonHold(h, ref dt, out var s);
            if (s == null) return false;

            var pos = h.root.position;
            var push = TakePush(s, realDt);
            if (push.sqrMagnitude > 0f)
            {
                push.y = 0f;
                pos += push;
                if (area) pos = area.ClampToArea(pos);
                pos.y = h.groundY;
                h.root.position = pos;
            }

            if (s.down)
            {
                // 뒤로 넘어져 누워 있는다
                var lie = Quaternion.Euler(-85f, h.root.eulerAngles.y, 0f);
                h.root.rotation = Quaternion.Slerp(h.root.rotation, lie, 8f * realDt);
                h.aim = 0f;
                if (h.tip) SetColor(h.tip, TipIdle);
                return true;
            }
            if (held)
            {
                h.aim = 0f; // 시위가 풀린다
                if (h.tip) SetColor(h.tip, TipIdle);
            }
            return held;
        }

        bool SkillHoldMonster(Monster m, ref float dt)
        {
            if (!m.root || !m.root.gameObject.activeSelf) return true;
            float realDt = dt;
            bool held = CommonHold(m, ref dt, out var s);
            if (s == null) return false;

            var pos = m.root.position + TakePush(s, realDt);
            if (s.down)
            {
                // 날개를 접고 떨어진다. 땅에 닿으면 사라짐
                s.fallSpeed += 25f * realDt;
                pos.y -= s.fallSpeed * realDt;
                m.root.position = pos;
                m.root.rotation = Quaternion.Slerp(m.root.rotation, Quaternion.Euler(70f, m.root.eulerAngles.y, 160f), 3f * realDt);
                if (m.wingL) m.wingL.localRotation = Quaternion.Euler(0f, 0f, 70f);
                if (m.wingR) m.wingR.localRotation = Quaternion.Euler(0f, 0f, -70f);
                if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out var g, 3f, ~0, QueryTriggerInteraction.Ignore) || pos.y < -30f)
                    m.root.gameObject.SetActive(false);
                return true;
            }
            m.root.position = pos;
            if (held)
            {
                // 그 자리에서 비틀거리며 떠 있다
                m.chasing = false;
                m.root.position += Vector3.up * (Mathf.Sin(Time.time * 6f) * 0.4f * realDt);
                float flap = Mathf.Sin(Time.time * 4f) * 15f;
                if (m.wingL) m.wingL.localRotation = Quaternion.Euler(0f, 0f, flap);
                if (m.wingR) m.wingR.localRotation = Quaternion.Euler(0f, 0f, -flap);
                foreach (var e in m.eyes) SetColor(e, EyeIdle);
            }
            return held;
        }
    }
}
