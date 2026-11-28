using System.Collections.Generic;
using DragonRecipe.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DragonRecipe.Care
{
    /// <summary>
    /// 둥지 드래곤이 마우스·터치를 쳐다보고, 만지면 좋아한다.
    /// - 쳐다보기: 마우스는 움직이기만 해도, 터치는 누르고 있는 동안(뗀 뒤에도 잠깐) 그쪽으로 고개를 돌린다.
    ///   아무 입력이 없으면 카메라 쪽을 보며 가끔 두리번거린다.
    /// - 쓰다듬기: 드래곤을 누르면 폴짝 뛰고 하트가 뜬다. 누른 채로 있으면 눈을 감고 꼬리를 흔들며 하트가 계속 뜬다.
    /// UI 위를 누른 것, 우리(농장)를 열어 둔 동안은 반응하지 않는다. 능력치에는 영향이 없다.
    /// 둥지 씬의 NestDragonView에 스스로 붙는다 (씬을 다시 만들 필요 없음).
    /// </summary>
    public class NestDragonTouch : MonoBehaviour
    {
        [Header("쳐다보기")]
        [Tooltip("고개가 좌우 / 위아래로 돌아가는 한계 (도)")]
        public float maxYaw = 65f;
        public float maxPitch = 35f;
        public float turnSpeed = 7f;
        [Tooltip("터치를 뗀 뒤에도 그 자리를 보고 있는 시간 (초)")]
        public float lingerTime = 2.5f;

        [Header("쓰다듬기")]
        [Tooltip("누른 것으로 치는 몸통 / 머리 반경 (드래곤 크기 1 기준, m)")]
        public float bodyRadius = 1.3f;
        public float headRadius = 0.8f;
        public float hopHeight = 0.35f;
        [Tooltip("누르고 있는 동안 하트가 뜨는 간격 (초)")]
        public float heartInterval = 0.4f;
        public float heartSize = 70f;

        class Heart
        {
            public RectTransform rt;
            public Image image;
            public Vector2 velocity;
            public float age;
        }

        NestDragonView _view;
        Transform _model, _pivot, _head, _eyeL, _eyeR, _tail;
        Vector3 _eyeScale = Vector3.one;
        Transform _decoRoot;
        readonly List<Transform> _adopted = new List<Transform>();
        float _nextDecoCheck;

        Camera _cam;
        FarmPanel _farm;
        Canvas _canvas;
        Sprite _heartSprite;
        readonly List<Heart> _hearts = new List<Heart>();
        PointerEventData _ped;
        readonly List<RaycastResult> _uiHits = new List<RaycastResult>();

        Vector2 _lookScreen;
        float _lookUntil = -1f;
        float _yaw, _pitch;
        bool _petting;
        float _joy;      // 0 = 평소, 1 = 쓰다듬는 중
        float _hop;      // 1 → 0
        float _nextHeart;

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
            var view = FindAnyObjectByType<NestDragonView>();
            if (view && !view.GetComponent<NestDragonTouch>()) view.gameObject.AddComponent<NestDragonTouch>();
        }

        // ───────── 수명 ─────────

        void Start()
        {
            _view = GetComponent<NestDragonView>();
            _model = _view && _view.model ? _view.model : transform.Find("Model");
            if (!_model) return;

            _head = _model.Find("Head");
            _eyeL = _model.Find("EyeL");
            _eyeR = _model.Find("EyeR");
            _tail = _model.Find("TailPivot");
            if (_eyeL) _eyeScale = _eyeL.localScale;

            // 목 밑동을 축으로 머리 쪽 부품을 한데 묶는다
            _pivot = new GameObject("HeadPivot").transform;
            _pivot.SetParent(_model, false);
            _pivot.localPosition = new Vector3(0f, 0.25f, 0.85f);
            foreach (var name in new[] { "Neck", "Head", "EyeL", "EyeR", "HornL", "HornR" })
            {
                var part = _model.Find(name);
                if (part) part.SetParent(_pivot, true);
            }

            var nest = FindAnyObjectByType<NestUI>();
            if (nest)
            {
                _canvas = nest.GetComponentInParent<Canvas>();
                _heartSprite = nest.icons ? nest.icons.heart : null;
            }
            _farm = FindAnyObjectByType<FarmPanel>();
        }

        void LateUpdate()
        {
            if (!_pivot) return;
            if (!_cam) _cam = Camera.main;
            if (!_cam) return;
            float dt = Time.deltaTime;
            AdoptHeadDecorations();

            bool away = _farm && _farm.IsOpen; // 카메라가 우리 쪽을 보고 있음
            var pointer = Pointer.current;
            bool pressed = false, pressedNow = false;
            Vector2 sp = _lookScreen;
            if (pointer != null && !away)
            {
                sp = pointer.position.ReadValue();
                pressed = pointer.press.isPressed;
                pressedNow = pointer.press.wasPressedThisFrame;
                // 마우스·펜은 떠 있기만 해도 따라본다. 터치는 누르고 있을 때만 위치가 의미 있다
                bool hovers = pointer is Mouse || pointer is Pen;
                bool inside = sp.x >= 0f && sp.y >= 0f && sp.x <= Screen.width && sp.y <= Screen.height;
                if (inside && (hovers || pressed))
                {
                    _lookScreen = sp;
                    _lookUntil = Time.unscaledTime + lingerTime;
                }
            }

            UpdatePetting(sp, pressed, pressedNow, away, dt);
            UpdateLook(!away && Time.unscaledTime < _lookUntil, dt);
            UpdateBody(dt);
            UpdateHearts(dt);
        }

        // ───────── 쳐다보기 ─────────

        void UpdateLook(bool tracking, float dt)
        {
            Vector3 headPos = _head ? _head.position : _pivot.position;
            Vector3 camPos = _cam.transform.position;
            Vector3 focus;
            if (tracking)
            {
                // 드래곤과 카메라 사이에 세운 면 위의 포인터 자리를 본다
                Vector3 toCam = camPos - headPos;
                float dist = toCam.magnitude;
                var plane = new Plane(-_cam.transform.forward, headPos + toCam.normalized * Mathf.Min(dist * 0.5f, 3f * Mathf.Max(0.5f, transform.lossyScale.x)));
                var ray = _cam.ScreenPointToRay(_lookScreen);
                focus = plane.Raycast(ray, out float d) ? ray.GetPoint(d) : camPos;
            }
            else
            {
                // 평소: 카메라 쪽을 보며 천천히 두리번
                float t = Time.time;
                focus = camPos + _cam.transform.right * (Mathf.Sin(t * 0.4f) * 2.5f) + _cam.transform.up * (Mathf.Sin(t * 0.27f + 1f) * 0.8f);
            }

            Vector3 local = _model.InverseTransformPoint(focus) - _pivot.localPosition;
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -maxYaw, maxYaw);
            float flat = Mathf.Sqrt(local.x * local.x + local.z * local.z);
            float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, Mathf.Max(0.01f, flat)) * Mathf.Rad2Deg, -maxPitch, maxPitch);

            float k = 1f - Mathf.Exp(-turnSpeed * dt);
            _yaw = Mathf.Lerp(_yaw, yaw, k);
            _pitch = Mathf.Lerp(_pitch, pitch, k);
            // 쓰다듬으면 고개를 부비적
            float nuzzle = Mathf.Sin(Time.time * 9f) * 7f * _joy;
            _pivot.localRotation = Quaternion.Euler(_pitch, _yaw, nuzzle);
        }

        // ───────── 쓰다듬기 ─────────

        void UpdatePetting(Vector2 sp, bool pressed, bool pressedNow, bool away, float dt)
        {
            if (!pressed || away) _petting = false;
            if (pressedNow && !away && !OverUi(sp) && HitsDragon(sp))
            {
                _petting = true;
                _hop = 1f;
                SpawnHeart();
                _nextHeart = Time.unscaledTime + heartInterval;
            }
            if (_petting && Time.unscaledTime >= _nextHeart && HitsDragon(sp))
            {
                SpawnHeart();
                _nextHeart = Time.unscaledTime + heartInterval;
            }
            _joy = Mathf.MoveTowards(_joy, _petting ? 1f : 0f, dt * (_petting ? 5f : 1.2f));
        }

        bool HitsDragon(Vector2 sp)
        {
            var ray = _cam.ScreenPointToRay(sp);
            float scale = Mathf.Max(0.1f, transform.lossyScale.x);
            if (DistanceToRay(ray, _model.position) <= bodyRadius * scale) return true;
            return _head && DistanceToRay(ray, _head.position) <= headRadius * scale;
        }

        static float DistanceToRay(Ray ray, Vector3 point)
        {
            float along = Mathf.Max(0f, Vector3.Dot(point - ray.origin, ray.direction));
            return Vector3.Distance(ray.GetPoint(along), point);
        }

        bool OverUi(Vector2 sp)
        {
            var es = EventSystem.current;
            if (!es) return false;
            _ped ??= new PointerEventData(es);
            _ped.position = sp;
            _uiHits.Clear();
            es.RaycastAll(_ped, _uiHits);
            return _uiHits.Count > 0;
        }

        void UpdateBody(float dt)
        {
            // 폴짝
            if (_hop > 0f)
            {
                _hop = Mathf.Max(0f, _hop - dt * 3.2f);
                _model.localPosition = Vector3.up * (Mathf.Sin((1f - _hop) * Mathf.PI) * hopHeight);
            }
            else if (_model.localPosition != Vector3.zero)
            {
                _model.localPosition = Vector3.zero;
            }

            // 눈을 지그시 감는다
            var eye = new Vector3(_eyeScale.x, _eyeScale.y * Mathf.Lerp(1f, 0.15f, _joy), _eyeScale.z);
            if (_eyeL) _eyeL.localScale = eye;
            if (_eyeR) _eyeR.localScale = eye;

            // 꼬리 살랑살랑
            if (_tail) _tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 14f) * 28f * _joy, 0f);
        }

        // ───────── 하트 ─────────

        void SpawnHeart()
        {
            if (!_canvas || !_heartSprite) return;
            Vector3 above = (_head ? _head.position : _model.position) + Vector3.up * (0.7f * transform.lossyScale.x);
            Vector3 screen = _cam.WorldToScreenPoint(above);
            if (screen.z <= 0f) return;
            var canvasRect = (RectTransform)_canvas.transform;
            var uiCam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCam, out var local)) return;

            var go = new GameObject("PetHeart", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvasRect, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = local + new Vector2(Random.Range(-50f, 50f), Random.Range(-10f, 20f));
            rt.sizeDelta = Vector2.one * heartSize;
            var img = go.GetComponent<Image>();
            img.sprite = _heartSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            _hearts.Add(new Heart { rt = rt, image = img, velocity = new Vector2(Random.Range(-40f, 40f), Random.Range(150f, 210f)) });
        }

        void UpdateHearts(float dt)
        {
            const float life = 1f;
            for (int i = _hearts.Count - 1; i >= 0; i--)
            {
                var h = _hearts[i];
                h.age += dt;
                if (!h.rt || h.age >= life)
                {
                    if (h.rt) Destroy(h.rt.gameObject);
                    _hearts.RemoveAt(i);
                    continue;
                }
                float t = h.age / life;
                h.rt.anchoredPosition += h.velocity * dt;
                h.rt.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.1f, Mathf.Clamp01(t * 5f));
                h.image.color = new Color(1f, 1f, 1f, 1f - t * t);
            }
        }

        void OnDisable()
        {
            foreach (var h in _hearts)
                if (h.rt) Destroy(h.rt.gameObject);
            _hearts.Clear();
        }

        // ───────── 성체 장식 ─────────

        /// <summary>성체 모습 장식(DragonLook) 중 머리에 달린 것(뿔·잎·갈기 앞쪽)도 고개와 같이 돌게 한다</summary>
        void AdoptHeadDecorations()
        {
            if (_decoRoot || Time.time < _nextDecoCheck) return;
            _nextDecoCheck = Time.time + 0.5f;

            // 장식이 다시 만들어졌으면(모습이 바뀜, 새 알) 전에 데려온 것은 지운다
            foreach (var t in _adopted)
                if (t) Destroy(t.gameObject);
            _adopted.Clear();

            for (int i = 0; i < _model.childCount; i++)
            {
                var c = _model.GetChild(i);
                if (c.name.StartsWith("Form_")) _decoRoot = c;
            }
            if (!_decoRoot) return;
            for (int i = _decoRoot.childCount - 1; i >= 0; i--)
            {
                var d = _decoRoot.GetChild(i);
                var p = d.localPosition;
                if (p.z < 1.1f || p.y < 0.55f) continue; // 등·꼬리 장식은 몸에 그대로
                d.SetParent(_pivot, true);
                _adopted.Add(d);
            }
        }
    }
}
