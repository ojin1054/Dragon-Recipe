using System.Collections.Generic;
using DragonRecipe.Core;
using DragonRecipe.Tribes;
using UnityEngine;

namespace DragonRecipe.Skills
{
    /// <summary>
    /// 종족 마을(TribeWorld)의 주민·병사가 드래곤 스킬을 맞게 잇는다.
    /// 피해가 쌓여 1이 넘으면 쓰러뜨리고(TribeWorld.Hit → 호감도 반영), 기절·잠·둔화는 Stun, 밀기는 Knockback.
    /// TribeWorld와 같은 오브젝트에 DragonSkills가 붙인다.
    /// </summary>
    public class TribeSkillTarget : MonoBehaviour, ISkillTarget
    {
        public TribeWorld world;
        [Tooltip("이만큼 피해가 쌓이면 쓰러진다")]
        public float knockDownAt = 1f;
        [Tooltip("불붙은 동안 초당 피해")]
        public float burnDamagePerSecond = 0.6f;
        [Tooltip("둔화는 이 비율만큼의 시간 동안 멈춤으로 대신한다")]
        public float slowAsStun = 0.4f;
        [Tooltip("같은 대상을 다시 밀 수 있는 간격 (브레스가 매 틱 밀어 날려 버리지 않게)")]
        public float pushInterval = 0.35f;

        class Mark
        {
            public float damage;
            public float burnUntil = -1f;
            public float lastPush = -99f;
        }

        readonly Dictionary<TribeMember, Mark> _marks = new Dictionary<TribeMember, Mark>();
        readonly List<TribeMember> _burning = new List<TribeMember>();
        readonly List<TribeMember> _gone = new List<TribeMember>();

        public static void EnsureFor(TribeWorld w)
        {
            if (!w || w.GetComponent<TribeSkillTarget>()) return;
            w.gameObject.AddComponent<TribeSkillTarget>().world = w;
        }

        void OnEnable() => SkillTargets.Register(this);
        void OnDisable() => SkillTargets.Unregister(this);

        void Start()
        {
            if (!world) world = GetComponent<TribeWorld>();
            if (world && world.session) world.session.StateChanged += OnState;
        }

        void OnDestroy()
        {
            if (world && world.session) world.session.StateChanged -= OnState;
        }

        void OnState(FlightSession.State s)
        {
            if (s == FlightSession.State.Ready) _marks.Clear();
        }

        static Vector3 BodyOf(TribeMember m) => m.transform.position + Vector3.up * 0.9f;

        Mark MarkOf(TribeMember m)
        {
            if (!_marks.TryGetValue(m, out var k)) _marks[m] = k = new Mark();
            return k;
        }

        public int ApplySkill(SkillArea area, SkillHit hit, List<Transform> hits)
        {
            if (!world) return 0;
            int n = 0;
            foreach (var m in world.MembersIn(area.Center, area.Radius + 1f))
            {
                if (!area.Contains(BodyOf(m), 0.6f)) continue;
                var k = MarkOf(m);
                float stop = Mathf.Max(hit.stun, hit.sleep, hit.slow * slowAsStun);
                if (stop > 0f) world.Stun(m, stop);
                if (hit.push.sqrMagnitude > 0f && Time.time - k.lastPush >= pushInterval)
                {
                    world.Knockback(m, hit.push);
                    k.lastPush = Time.time;
                }
                if (hit.burn > 0f) k.burnUntil = Mathf.Max(k.burnUntil, Time.time + hit.burn);
                Damage(m, k, hit.damage);
                hits?.Add(m.transform);
                n++;
            }
            if (hit.clearArrows) world.ClearProjectilesIn(area.Center, area.Radius);
            return n;
        }

        void Damage(TribeMember m, Mark k, float amount)
        {
            if (amount <= 0f || !m || m.IsDown) return;
            k.damage += amount;
            if (k.damage < knockDownAt) return;
            k.damage = 0f;
            k.burnUntil = -1f;
            world.Hit(m); // 쓰러뜨리고 호감도 반영
        }

        public bool ProjectileHit(Vector3 from, Vector3 to, float radius, out Vector3 point)
        {
            point = default;
            if (!world) return false;
            float best = float.MaxValue;
            foreach (var m in world.Members)
            {
                if (!m || m.IsDown) continue;
                var b = BodyOf(m);
                if (SkillRules.SegmentDistance(from, to, b) > radius + 0.7f) continue;
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
            if (!world) return;
            // 자동 조준은 병사만 (주민을 겨누면 호감도가 의도치 않게 바뀐다)
            foreach (var m in world.Members)
                if (m && !m.IsDown && m.IsSoldier) points.Add(BodyOf(m));
        }

        public void CollectHealth(List<UnitHealth> into)
        {
            if (!world) return;
            float max = Mathf.Max(0.01f, knockDownAt);
            foreach (var m in world.Members)
            {
                if (!m || m.IsDown || !m.gameObject.activeInHierarchy) continue;
                float dmg = _marks.TryGetValue(m, out var k) ? k.damage : 0f;
                into.Add(new UnitHealth(m.transform, 1f - dmg / max));
            }
        }

        void Update()
        {
            if (GamePause.IsPaused || _marks.Count == 0) return;
            float now = Time.time;
            _burning.Clear();
            _gone.Clear();
            foreach (var kv in _marks)
            {
                if (!kv.Key) _gone.Add(kv.Key); // 사라진 침입자 등
                else if (now < kv.Value.burnUntil) _burning.Add(kv.Key);
            }
            foreach (var m in _gone) _marks.Remove(m);
            foreach (var m in _burning)
                if (m) Damage(m, _marks[m], burnDamagePerSecond * Time.deltaTime);
        }
    }
}
