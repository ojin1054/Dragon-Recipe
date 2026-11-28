using System.Collections.Generic;
using DragonRecipe.Care;
using DragonRecipe.Core;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using DragonRecipe.Tribes;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DragonRecipe.UI
{
    /// <summary>
    /// 드래곤이 다가오면 동물·종족 주민/병사·그리핀 머리 위에 경계 표시를 띄운다.
    /// - 그리핀: 쫓아올 거리 근처까지 오면 ?, 쫓아오기 시작하면 !
    /// - 의심(노란 ?): 알아채는 거리 가까이 들어옴
    /// - 발견(빨간 !): 도망치기 시작했거나 (동물·주민) 드래곤을 겨누는 중 (병사)
    /// 비행 씬이 열리면 스스로 생긴다. 체력바(UnitHealthBars)보다 위 층에 그린다.
    /// </summary>
    public class AwarenessMarks : MonoBehaviour
    {
        public enum Level
        {
            None,
            Suspicious,
            Alerted
        }

        [Tooltip("알아채는 거리의 이 배율 안이면 의심 (도망치는 동물)")]
        public float suspectScale = 1.6f;
        [Tooltip("겁먹는 거리의 이 배율 안이면 의심 (주민)")]
        public float villagerSuspectScale = 2.2f;
        [Tooltip("조건이 풀린 뒤에도 !를 유지하는 시간 (초)")]
        public float alertHold = 1.2f;
        [Tooltip("조건이 풀린 뒤에도 ?를 유지하는 시간 (초)")]
        public float suspectHold = 0.4f;
        [Tooltip("카메라에서 이보다 멀면 그리지 않는다")]
        public float maxDistance = 120f;
        public float nearFontSize = 46f;
        public float farFontSize = 22f;
        [Tooltip("머리 꼭대기 위로 띄우는 높이 (m). 체력바보다 조금 위")]
        public float headroom = 0.5f;
        [Tooltip("체력바를 비켜 올리는 화면 높이 (픽셀, 1920x1080 기준)")]
        public float screenLift = 26f;

        public static readonly Color SuspectColor = new Color(1f, 0.85f, 0.15f);
        public static readonly Color AlertColor = new Color(1f, 0.22f, 0.18f);

        class Track
        {
            public Level level;
            public float holdUntil;
            public float pop;   // 바뀐 순간 1 → 0
            public float top = -1f;
            public int seenFrame;
        }

        class Mark
        {
            public RectTransform root;
            public Text text;
        }

        Canvas _canvas;
        Font _font;
        FlightSession _session;
        DragonRecipe.Hostile.HostileSpawner _hostiles;
        readonly List<Mark> _marks = new List<Mark>();
        readonly Dictionary<Transform, Track> _tracks = new Dictionary<Transform, Track>();
        readonly List<Transform> _stale = new List<Transform>();

        // ───────── 자동 부착 ─────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

        static void Attach()
        {
            if (FindAnyObjectByType<AwarenessMarks>()) return;
            if (!FindAnyObjectByType<FlightSession>()) return;
            new GameObject("AwarenessMarks").AddComponent<AwarenessMarks>();
        }

        // ───────── 판정 (순수 규칙) ─────────

        /// <summary>동물의 경계 단계. 움직이지 않는 식물은 표시하지 않는다</summary>
        public static Level PreyLevel(bool flees, float wanderSpeed, bool scattering, float aware,
            float flatDist, float up, float suspectScale)
        {
            if (!flees && wanderSpeed <= 0f) return Level.None;
            if (flees && (scattering || (flatDist < aware && up < aware))) return Level.Alerted;
            float s = flees ? aware * suspectScale : aware;
            return flatDist < s && up < s ? Level.Suspicious : Level.None;
        }

        /// <summary>주민·병사의 경계 단계</summary>
        public static Level MemberLevel(bool fleeing, bool aiming, bool soldier, float flatDist, float up,
            float suspectRadius, float shootRange, float dist)
        {
            if (fleeing || aiming) return Level.Alerted;
            if (flatDist <= suspectRadius && up <= suspectRadius) return Level.Suspicious;
            if (soldier && dist <= shootRange) return Level.Suspicious;
            return Level.None;
        }

        // ───────── 수명 ─────────

        void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = -9; // 체력바(-10) 위, HUD 아래
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var anyText = FindAnyObjectByType<Text>();
            _font = anyText && anyText.font ? anyText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        void LateUpdate()
        {
            if (!_session) _session = FindAnyObjectByType<FlightSession>();
            var dragon = _session ? _session.dragon : null;
            var cam = Camera.main;
            int frame = Time.frameCount;
            float now = Time.time;
            float dt = GamePause.IsPaused ? 0f : Time.deltaTime;
            int used = 0;

            if (dragon && dragon.IsFlying && cam)
            {
                Vector3 d = dragon.transform.position;
                float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;

                if (_session.spawner)
                    foreach (var p in _session.spawner.All)
                    {
                        if (!p || !p.IsAvailable || !p.Data || !p.gameObject.activeInHierarchy) continue;
                        // 버섯·멜론 같은 식물은 알아채지 못한다
                        var lv = p.IsHeld || p.Data.IsStationary ? Level.None : PreyLevel(p.Data.flees, p.Data.wanderSpeed, p.IsScattering,
                            p.AwarenessRadius, Flat(d, p.transform.position), d.y - p.transform.position.y, suspectScale);
                        Draw(p.transform, lv, cam, scale, now, dt, frame, ref used);
                    }

                // 그리핀: 의심(?) → 쫓아옴(!)
                if (!_hostiles && frame % 30 == 0) _hostiles = FindAnyObjectByType<DragonRecipe.Hostile.HostileSpawner>();
                if (_hostiles)
                    for (int i = 0; i < _hostiles.GriffinCount; i++)
                    {
                        var g = _hostiles.Griffin(i, d, out bool suspicious, out bool alerted);
                        if (!g) continue;
                        Draw(g, alerted ? Level.Alerted : suspicious ? Level.Suspicious : Level.None, cam, scale, now, dt, frame, ref used);
                    }

                var world = TribeWorld.Instance;
                if (world)
                {
                    float suspect = world.frightRadius * villagerSuspectScale;
                    var settings = DragonCare.TribeSettings;
                    foreach (var m in world.Members)
                    {
                        if (!m || !m.gameObject.activeInHierarchy) continue;
                        var lv = Level.None;
                        if (!m.IsDown && !m.IsStunned && !m.IsRaider)
                        {
                            Vector3 pos = m.transform.position;
                            float range = m.IsSoldier && settings != null ? settings.Combat(m.Tribe).range : 0f;
                            lv = MemberLevel(now < m.fleeUntil, m.aim > 0f, m.IsSoldier, Flat(d, pos), d.y - pos.y,
                                suspect, range, Vector3.Distance(d, pos));
                        }
                        Draw(m.transform, lv, cam, scale, now, dt, frame, ref used);
                    }
                }
            }

            for (int i = used; i < _marks.Count; i++)
                if (_marks[i].root.gameObject.activeSelf) _marks[i].root.gameObject.SetActive(false);

            if (frame % 60 == 0)
            {
                _stale.Clear();
                foreach (var kv in _tracks)
                    if (!kv.Key || frame - kv.Value.seenFrame > 120) _stale.Add(kv.Key);
                foreach (var t in _stale) _tracks.Remove(t);
            }
        }

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        void Draw(Transform unit, Level now, Camera cam, float scale, float time, float dt, int frame, ref int used)
        {
            if (now == Level.None && !_tracks.ContainsKey(unit)) return;
            var tr = TrackOf(unit);
            tr.seenFrame = frame;

            // 단계 오르면 바로, 내려갈 때는 잠시 유지
            if (now >= tr.level)
            {
                if (now > tr.level) tr.pop = 1f;
                if (now != Level.None)
                {
                    tr.level = now;
                    tr.holdUntil = time + (now == Level.Alerted ? alertHold : suspectHold);
                }
            }
            else if (time >= tr.holdUntil)
            {
                tr.level = now;
                if (now != Level.None) tr.holdUntil = time + suspectHold;
            }
            tr.pop = Mathf.MoveTowards(tr.pop, 0f, dt * 5f);
            if (tr.level == Level.None) return;

            if (tr.top < 0f) tr.top = MeasureTop(unit);
            Vector3 sp = cam.WorldToScreenPoint(unit.position + Vector3.up * (tr.top + headroom));
            if (sp.z <= 0.1f || sp.z > maxDistance) return;

            var mark = used < _marks.Count ? _marks[used] : MakeMark();
            used++;
            if (!mark.root.gameObject.activeSelf) mark.root.gameObject.SetActive(true);

            bool alert = tr.level == Level.Alerted;
            float k = Mathf.InverseLerp(10f, maxDistance, sp.z);
            float size = Mathf.Lerp(nearFontSize, farFontSize, k);
            float bob = alert ? 0f : Mathf.Sin(time * 5f + unit.GetInstanceID()) * 3f;
            mark.text.text = alert ? "!" : "?";
            mark.text.color = alert ? AlertColor : SuspectColor;
            mark.text.fontSize = Mathf.RoundToInt(size);
            mark.root.sizeDelta = new Vector2(size * 1.2f, size * 1.4f);
            mark.root.anchoredPosition = new Vector2(sp.x, sp.y) / scale + new Vector2(0f, screenLift + size * 0.5f + bob);
            mark.root.localScale = Vector3.one * (1f + tr.pop * 0.7f);
        }

        Track TrackOf(Transform t)
        {
            if (!_tracks.TryGetValue(t, out var tr)) _tracks[t] = tr = new Track();
            return tr;
        }

        static float MeasureTop(Transform t)
        {
            float top = 2f;
            var rs = t.GetComponentsInChildren<Renderer>();
            bool any = false;
            Bounds b = default;
            foreach (var r in rs)
            {
                if (r is TrailRenderer || r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (any) top = b.max.y - t.position.y;
            return Mathf.Max(0.5f, top);
        }

        Mark MakeMark()
        {
            var go = new GameObject("AwarenessMark", typeof(RectTransform), typeof(Text), typeof(Outline));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0.08f, 0.08f, 0.12f, 0.9f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);
            var mark = new Mark { root = rt, text = text };
            _marks.Add(mark);
            return mark;
        }
    }
}
