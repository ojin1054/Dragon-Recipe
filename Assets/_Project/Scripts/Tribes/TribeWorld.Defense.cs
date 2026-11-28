using System.Collections.Generic;
using DragonRecipe.Care;
using DragonRecipe.CameraRig;
using DragonRecipe.Core;
using DragonRecipe.Hostile;
using UnityEngine;

namespace DragonRecipe.Tribes
{
    /// <summary>
    /// 참전 중 비행 = 방어전.
    /// - 출발: 둥지 동굴 대신 편든 쪽 마을의 망루(긴 나무 발판)에서 달려 나가 이륙한다.
    /// - 이륙하면 적 마을 쪽에서 적 무리가 몇 번에 걸쳐 편든 쪽 마을로 몰려온다 (무리마다 더 많이).
    ///   적은 행군하면서 드래곤을 쏘고, 마을 가운데에 닿으면 뚫린다: 방어 칸이 하나 줄고, 승률이 깎이고, 주민이 쓰러진다.
    /// - 드래곤이 적을 쓰러뜨리면(공격·스킬 → Hit) 승률이 오른다. 편든 쪽 병사도 조금 돕는다 (승률에는 안 셈).
    /// </summary>
    public partial class TribeWorld
    {
        Transform _homeNest, _homeEdge;
        Transform _towerRoot, _towerNest, _towerEdge;
        int _towerTribe = -1;
        int _wave;
        float _raidTimer;

        /// <summary>지금 방어전 중 (참전한 전쟁이 있음)</summary>
        public bool DefenseActive => State != null && State.war.status == WarStatus.Joined;
        /// <summary>이번 비행에서 남은 방어 칸</summary>
        public int DefenseLeft { get; private set; }
        public int DefenseMax => Settings.villageDefense;
        /// <summary>아직 오지 않은 적 무리 수</summary>
        public int WavesLeft => Mathf.Max(0, WaveCount - _wave);
        /// <summary>비행 한 번에 오는 적 무리 수 (전쟁 맵은 더 많다)</summary>
        int WaveCount => _warMap ? Settings.warWaves : Settings.raidWaves;
        /// <summary>침입자 행군 속도</summary>
        float RaidSpeed => _warMap ? Settings.warRaidSpeed : Settings.raidSpeed;
        /// <summary>쓰러지지 않은 침입자 수</summary>
        public int RaidersAlive
        {
            get
            {
                int n = 0;
                foreach (var m in _members)
                    if (m && m.IsRaider && !m.IsDown) n++;
                return n;
            }
        }

        /// <summary>디버그: 다음 적 무리를 바로 부른다 (비행 중일 때)</summary>
        public void DebugCallWave() => _raidTimer = 0f;

        void ResetDefense()
        {
            _wave = 0;
            _raidTimer = _warMap ? Settings.warFirstDelay : Settings.raidFirstDelay;
            DefenseLeft = Settings.villageDefense;
            _reachedSinceBreach = 0;
        }

        // ───────── 출발 자리 ─────────

        /// <summary>참전 중이면 편든 쪽 망루, 아니면 원래 둥지에서 출발하도록 바꾼다 (대기 중이면 바로 옮겨 세움)</summary>
        void ApplyLaunchSite()
        {
            if (!session || !session.dragon) return;
            var st = State;
            Transform nest = _homeNest, edge = _homeEdge;
            // 전쟁 맵은 씬의 진영 절벽(_homeNest)에서 그대로 출발
            if (!_warMap && st != null && st.war.status == WarStatus.Joined && _villages[(int)st.war.joined] != null)
            {
                EnsureTower(st.war.joined, st.war.Enemy(st.war.joined));
                nest = _towerNest;
                edge = _towerEdge;
            }
            if (!nest || session.nest == nest) return;

            session.nest = nest;
            session.launchEdge = edge;
            if (session.Current != FlightSession.State.Ready) return;
            float run = edge ? Mathf.Max(0f, Vector3.Dot(edge.position - nest.position, nest.forward)) : 0f;
            session.dragon.Park(nest.position, nest.eulerAngles.y, run);
            var cam = FindAnyObjectByType<FollowCamera>();
            if (cam) cam.Snap();
        }

        /// <summary>편든 쪽 마을 앞, 적이 오는 쪽을 바라보는 망루 (달리기 발판 + 기둥 + 깃발)</summary>
        void EnsureTower(Tribe ally, Tribe enemy)
        {
            if (_towerTribe == (int)ally && _towerNest) return;
            if (_towerRoot) Destroy(_towerRoot.gameObject);

            var s = Settings;
            var a = _villages[(int)ally];
            var e = _villages[(int)enemy];
            Vector3 dir = e != null ? e.center - a.center : (area ? area.areaCenter : Vector3.zero) - a.center;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;

            // 둥지 쪽 달리기 길이와 같게
            float runLength = 28f;
            if (_homeNest && _homeEdge)
                runLength = Mathf.Max(10f, Vector3.Dot(_homeEdge.position - _homeNest.position, _homeNest.forward));

            float h = s.watchtowerHeight;
            float groundY = a.center.y;
            Vector3 edgeFlat = a.center + dir * 20f;
            Vector3 startFlat = edgeFlat - dir * runLength;
            var rot = Quaternion.LookRotation(dir);

            _towerRoot = new GameObject($"Watchtower_{ally}").transform;
            _towerRoot.SetParent(transform, false);
            _towerTribe = (int)ally;

            // 발판 (콜라이더 있음: 달리다 내려앉으면 그 위를 미끄러진다)
            float deckLen = runLength + 6f;
            var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deck.name = "Deck";
            deck.transform.SetParent(_towerRoot, false);
            deck.transform.SetPositionAndRotation((startFlat + edgeFlat) * 0.5f - dir * 3f + Vector3.up * (groundY + h - 0.5f), rot);
            deck.transform.localScale = new Vector3(6f, 1f, deckLen);
            var dr = deck.GetComponent<Renderer>();
            dr.sharedMaterial = Mat;
            Tint(dr, new Color(0.55f, 0.4f, 0.25f));

            // 기둥·난간 (콜라이더 없음)
            var wood = new Color(0.4f, 0.28f, 0.16f);
            int posts = Mathf.CeilToInt(deckLen / 6f);
            for (int i = 0; i <= posts; i++)
            {
                Vector3 along = startFlat - dir * 6f + dir * (i * deckLen / posts);
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3 p = along + Vector3.Cross(Vector3.up, dir) * (side * 2.6f);
                    var post = Part(PrimitiveType.Cylinder, "Post", _towerRoot, Vector3.zero, new Vector3(0.5f, h * 0.5f, 0.5f), wood);
                    post.transform.position = new Vector3(p.x, groundY + h * 0.5f, p.z);
                    var rail = Part(PrimitiveType.Cube, "Rail", _towerRoot, Vector3.zero, new Vector3(0.15f, 0.9f, 0.15f), wood);
                    rail.transform.position = new Vector3(p.x, groundY + h + 0.45f, p.z);
                }
            }

            // 편든 종족 깃발 (출발 지점)
            Vector3 flagAt = startFlat - dir * 2f + Vector3.Cross(Vector3.up, dir) * 2.6f;
            var pole = Part(PrimitiveType.Cylinder, "TowerPole", _towerRoot, Vector3.zero, new Vector3(0.2f, 2.5f, 0.2f), wood);
            pole.transform.position = new Vector3(flagAt.x, groundY + h + 2.5f, flagAt.z);
            var flag = Part(PrimitiveType.Cube, "TowerFlag", _towerRoot, Vector3.zero, new Vector3(1.8f, 1.1f, 0.06f), TribeLook.Skin(ally));
            flag.transform.SetPositionAndRotation(new Vector3(flagAt.x, groundY + h + 4.3f, flagAt.z) + rot * Vector3.right * 0.9f, rot);

            float standY = groundY + h + 1.2f; // 둥지와 같은 높이 차 (발판 위 1.2m)
            _towerNest = new GameObject("WarNest").transform;
            _towerNest.SetParent(_towerRoot, false);
            _towerNest.SetPositionAndRotation(new Vector3(startFlat.x, standY, startFlat.z), rot);
            _towerEdge = new GameObject("WarLaunchEdge").transform;
            _towerEdge.SetParent(_towerRoot, false);
            _towerEdge.SetPositionAndRotation(new Vector3(edgeFlat.x, standY, edgeFlat.z), rot);
        }

        // ───────── 적 무리 ─────────

        void UpdateDefense(float dt, TribeState st, bool flying)
        {
            var war = st.war;
            bool joined = war.status == WarStatus.Joined;

            // 쓰러진 침입자는 일어나지 않고 사라진다 / 마을에 닿은 침입자는 뚫고 들어간다
            for (int i = _members.Count - 1; i >= 0; i--)
            {
                var m = _members[i];
                if (!m || !m.IsRaider) continue;
                if (m.pendingBreach)
                {
                    if (joined) RaiderReached(war.joined);
                    Destroy(m.gameObject);
                    _members.RemoveAt(i);
                }
                else if (m.IsDown && m.downUntil - Time.time < downTime - 2.5f)
                {
                    Destroy(m.gameObject);
                    _members.RemoveAt(i);
                }
            }

            if (!joined || !flying) return;
            if (_wave >= WaveCount) return;
            _raidTimer -= dt;
            if (_raidTimer > 0f) return;
            if (_warMap) SpawnWarWave(war.joined, war.Enemy(war.joined), _wave);
            else SpawnWave(war.joined, war.Enemy(war.joined), _wave);
            _wave++;
            _raidTimer = _warMap ? Settings.warInterval : Settings.raidInterval;
        }

        void SpawnWave(Tribe ally, Tribe enemy, int wave)
        {
            var a = _villages[(int)ally];
            var e = _villages[(int)enemy];
            if (a == null || e == null) return;
            Vector3 dir = e.center - a.center;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            // 무리마다 조금씩 다른 방향에서
            Vector3 from = a.center + Quaternion.Euler(0f, (wave - 1) * 25f, 0f) * dir * Settings.raidSpawnDistance;
            if (area) from = area.ClampToArea(from);

            int n = TribeLogic.RaidSize(wave, Settings);
            for (int i = 0; i < n; i++)
            {
                float lateral = (i - (n - 1) * 0.5f) * 2.2f + Random.Range(-0.6f, 0.6f);
                var m = Spawn(e, from + side * lateral - dir * Random.Range(0f, 4f), true, false);
                if (m) MakeRaider(m, a.center); // 목표 = 편든 쪽 마을 가운데
            }
        }

        void UpdateRaider(TribeMember m, float dt, Vector3 pos, bool flying, Vector3 target, Vector3 targetVel)
        {
            var s = Settings;
            var st = State;
            Vector3 to = m.home - pos;
            to.y = 0f;
            if (st == null || st.war.status != WarStatus.Joined)
            {
                SetPose(m, pos, m.transform.rotation, 0f);
                return;
            }
            if (to.magnitude <= s.breachRadius)
            {
                m.pendingBreach = true;
                return;
            }

            var combat = s.Combat(m.Tribe);
            Vector3 toTarget = target - pos;
            Vector3 face = to;
            float speedScale = 1f;
            if (flying && toTarget.sqrMagnitude <= combat.range * combat.range)
            {
                // 행군하며 쏜다 (시위를 당길 때는 느려짐)
                m.aimOffset = toTarget;
                AimAndFire(m, dt, target, targetVel, true);
                if (m.aim > 0f)
                {
                    face = new Vector3(toTarget.x, 0f, toTarget.z);
                    speedScale = 0.35f;
                }
            }
            else
            {
                m.aim = 0f;
            }

            pos += to.normalized * RaidSpeed * speedScale * dt;
            float bob = Mathf.Abs(Mathf.Sin(Time.time * 7f + m.phase)) * 0.12f;
            var rot = face.sqrMagnitude > 0.001f ? Quaternion.Slerp(m.transform.rotation, Quaternion.LookRotation(face), 6f * dt) : m.transform.rotation;
            SetPose(m, pos, rot, bob);
            AnimateWeapon(m, dt);
        }

        /// <summary>뚫림: 방어 칸 하나, 승률 깎임, 마을 주민 몇이 쓰러지고 깃발이 번쩍인다</summary>
        void Breach(Tribe ally)
        {
            DefenseLeft = Mathf.Max(0, DefenseLeft - 1);
            DragonCare.ReportWarBreach();
            var v = _villages[(int)ally];
            if (v == null) return;
            v.flash = 1f;
            int knocked = 0;
            foreach (var m in _members)
            {
                if (knocked >= 2) break;
                if (!m || m.Tribe != ally || m.IsSoldier || m.IsDown) continue;
                if ((m.transform.position - v.center).sqrMagnitude > 30f * 30f) continue;
                KnockDown(m);
                knocked++;
            }
        }

        /// <summary>편든 쪽 병사: 사거리 안의 가장 가까운 침입자를 쏜다</summary>
        bool TryDefend(TribeMember m, float dt, Vector3 pos, out Vector3 face)
        {
            face = Vector3.zero;
            var st = State;
            if (st == null || st.war.status != WarStatus.Joined || m.Tribe != st.war.joined) return false;

            var combat = Settings.Combat(m.Tribe);
            TribeMember best = null;
            float bestD = combat.range * combat.range;
            foreach (var r in _members)
            {
                if (!r || !r.IsRaider || r.IsDown) continue;
                float d = (r.transform.position - pos).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = r;
                }
            }
            if (!best) return false;

            Vector3 aimAt = best.transform.position + Vector3.up * 1f;
            Vector3 vel = best.home - best.transform.position;
            vel.y = 0f;
            vel = vel.sqrMagnitude > 0.01f ? vel.normalized * RaidSpeed : Vector3.zero;
            m.aimOffset = aimAt - pos;
            AimAndFire(m, dt, aimAt, vel, false, best.Tribe);
            face = new Vector3(m.aimOffset.x, 0f, m.aimOffset.z);
            return true;
        }

        /// <summary>편든 쪽 화살이 침입자에 맞았는지. 맞으면 쓰러뜨린다 (드래곤이 한 게 아니라 호감도·승률엔 안 셈)</summary>
        bool HitRaider(Vector3 from, Vector3 to, Tribe raiderTribe)
        {
            foreach (var r in _members)
            {
                if (!r || !r.IsRaider || r.IsDown || r.Tribe != raiderTribe) continue;
                if (HostileRules.SegmentDistance(from, to, r.transform.position + Vector3.up * 1f) > 0.9f) continue;
                KnockDown(r);
                return true;
            }
            return false;
        }
    }
}
