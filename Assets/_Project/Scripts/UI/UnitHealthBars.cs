using System.Collections.Generic;
using DragonRecipe.Core;
using DragonRecipe.Skills;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DragonRecipe.UI
{
    /// <summary>
    /// 비행 씬에서 몬스터·사냥꾼·종족 주민/병사 머리 위에 체력바를 띄운다.
    /// 체력은 스킬 대상(SkillTargets)이 알려 주고, 바는 화면 위 오버레이(HUD 아래 층)에 그린다.
    /// 쓰러진 대상은 바가 사라진다. 씬에 없으면 비행 씬이 열릴 때 스스로 생긴다.
    /// </summary>
    public class UnitHealthBars : MonoBehaviour
    {
        [Tooltip("카메라에서 이보다 멀면 바를 그리지 않는다")]
        public float maxDistance = 120f;
        [Tooltip("가까울 때 바 크기 (픽셀, 1920x1080 기준)")]
        public Vector2 nearSize = new Vector2(90f, 12f);
        [Tooltip("멀 때 바 크기")]
        public Vector2 farSize = new Vector2(40f, 6f);
        [Tooltip("머리 꼭대기 위로 띄우는 높이 (m)")]
        public float headroom = 0.5f;
        [Tooltip("피해를 받으면 흰 잔상이 줄어드는 빠르기 (초당 비율)")]
        public float trailSpeed = 0.8f;

        static readonly Color Back = new Color(0.05f, 0.05f, 0.08f, 0.75f);
        static readonly Color Full = new Color(0.4f, 0.9f, 0.35f);
        static readonly Color Mid = new Color(1f, 0.82f, 0.25f);
        static readonly Color Low = new Color(1f, 0.3f, 0.25f);
        static readonly Color Trail = new Color(1f, 1f, 1f, 0.85f);

        class Bar
        {
            public RectTransform root;
            public RectTransform trail;
            public RectTransform fill;
            public Image fillImage;
        }

        class Track
        {
            public float top;      // 기준점에서 머리 꼭대기까지 (m)
            public float shown;    // 흰 잔상 비율
            public float flash;    // 피해 깜빡임 1 → 0
            public float last = 1f;
            public int seenFrame;
        }

        Canvas _canvas;
        RectTransform _layer;
        readonly List<Bar> _bars = new List<Bar>();
        readonly List<UnitHealth> _units = new List<UnitHealth>();
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
            if (FindAnyObjectByType<UnitHealthBars>()) return;
            if (!FindAnyObjectByType<FlightSession>()) return;
            new GameObject("UnitHealthBars").AddComponent<UnitHealthBars>();
        }

        // ───────── 수명 ─────────

        void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = -10; // HUD보다 아래
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _layer = (RectTransform)transform;
        }

        Bar MakeBar()
        {
            var bar = new Bar
            {
                root = Rect("HealthBar", _layer, Back, out _),
            };
            bar.trail = Rect("Trail", bar.root, Trail, out _);
            bar.fill = Rect("Fill", bar.root, Full, out bar.fillImage);
            foreach (var r in new[] { bar.trail, bar.fill })
            {
                // 왼쪽 끝을 기준으로 가로 길이만 줄인다
                r.anchorMin = new Vector2(0f, 0f);
                r.anchorMax = new Vector2(1f, 1f);
                r.pivot = new Vector2(0f, 0.5f);
                r.offsetMin = new Vector2(2f, 2f);
                r.offsetMax = new Vector2(-2f, -2f);
            }
            _bars.Add(bar);
            return bar;
        }

        static RectTransform Rect(string name, Transform parent, Color c, out Image img)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            img = go.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return rt;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            _units.Clear();
            foreach (var t in SkillTargets.All) t.CollectHealth(_units);

            int used = 0;
            float dt = GamePause.IsPaused ? 0f : Time.deltaTime;
            int frame = Time.frameCount;
            float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;

            if (cam)
            {
                foreach (var u in _units)
                {
                    if (!u.unit) continue;
                    var tr = TrackOf(u.unit);
                    tr.seenFrame = frame;

                    // 잔상·깜빡임은 화면 밖이어도 진행
                    if (u.fraction < tr.last - 0.001f) tr.flash = 1f;
                    tr.last = u.fraction;
                    tr.shown = tr.shown < u.fraction ? u.fraction : Mathf.MoveTowards(tr.shown, u.fraction, trailSpeed * dt);
                    tr.flash = Mathf.MoveTowards(tr.flash, 0f, dt * 4f);

                    Vector3 head = u.unit.position + Vector3.up * (tr.top + headroom);
                    Vector3 sp = cam.WorldToScreenPoint(head);
                    if (sp.z <= 0.1f || sp.z > maxDistance) continue;

                    var bar = used < _bars.Count ? _bars[used] : MakeBar();
                    used++;
                    if (!bar.root.gameObject.activeSelf) bar.root.gameObject.SetActive(true);

                    float k = Mathf.InverseLerp(10f, maxDistance, sp.z);
                    var size = Vector2.Lerp(nearSize, farSize, k);
                    bar.root.sizeDelta = size;
                    bar.root.anchorMin = bar.root.anchorMax = Vector2.zero;
                    bar.root.anchoredPosition = new Vector2(sp.x, sp.y) / scale;

                    float inner = size.x - 4f;
                    bar.fill.sizeDelta = new Vector2(inner * u.fraction - size.x, bar.fill.sizeDelta.y);
                    bar.trail.sizeDelta = new Vector2(inner * tr.shown - size.x, bar.trail.sizeDelta.y);
                    var c = u.fraction > 0.5f ? Color.Lerp(Mid, Full, (u.fraction - 0.5f) * 2f) : Color.Lerp(Low, Mid, u.fraction * 2f);
                    bar.fillImage.color = Color.Lerp(c, Color.white, tr.flash * 0.7f);
                    // 맞으면 살짝 튄다
                    bar.root.localScale = Vector3.one * (1f + tr.flash * 0.25f);
                }
            }

            for (int i = used; i < _bars.Count; i++)
                if (_bars[i].root.gameObject.activeSelf) _bars[i].root.gameObject.SetActive(false);

            // 사라진 대상 정리
            if (frame % 60 == 0)
            {
                _stale.Clear();
                foreach (var kv in _tracks)
                    if (!kv.Key || frame - kv.Value.seenFrame > 120) _stale.Add(kv.Key);
                foreach (var t in _stale) _tracks.Remove(t);
            }
        }

        Track TrackOf(Transform t)
        {
            if (_tracks.TryGetValue(t, out var tr)) return tr;
            // 처음 볼 때 몸 꼭대기 높이를 한 번 잰다 (서 있는 자세 기준)
            float top = 2f;
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds;
                foreach (var r in rs)
                    if (!(r is TrailRenderer)) b.Encapsulate(r.bounds);
                top = b.max.y - t.position.y;
            }
            tr = new Track { top = Mathf.Max(0.5f, top), shown = 1f };
            _tracks[t] = tr;
            return tr;
        }
    }
}
