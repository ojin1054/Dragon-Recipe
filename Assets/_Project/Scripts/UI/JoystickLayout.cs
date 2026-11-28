using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DragonRecipe.UI
{
    /// <summary>
    /// 화면 조이스틱(StickArea + 손잡이 OnScreenStick)에 <see cref="StickSettings"/>를 적용한다.
    /// - 크기: 바탕·손잡이·움직이는 범위를 함께 키우고 줄인다.
    /// - 자리: 직접 옮긴 자리 (화면 비율로 저장해 해상도가 바뀌어도 같은 곳).
    /// - 터치한 곳 모드: 조이스틱이 있는 쪽 화면 절반 어디든 누르면 그 자리로 조이스틱이 옮겨 오고, 떼면 제자리로.
    /// 씬에 따로 놓지 않아도 씬이 열릴 때 OnScreenStick마다 자동으로 붙는다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class JoystickLayout : MonoBehaviour
    {
        public OnScreenStick stick;

        /// <summary>지금 씬의 조이스틱 (없으면 null)</summary>
        public static JoystickLayout Active { get; private set; }

        RectTransform _area;
        RectTransform _knob;
        RectTransform _parent;
        Image _bg;
        Image _knobImage;
        Color _bgColor;
        Color _knobColor;
        Vector2 _baseArea;
        Vector2 _baseKnob;
        float _baseRange;
        Vector2 _defaultAnchored;
        StickTouchZone _zone;
        Vector2 _lastParentSize;
        bool _ready;
        bool _held;
        int _pointerId;

        public RectTransform Area => _area;
        /// <summary>손잡이</summary>
        public RectTransform Knob => _knob;
        /// <summary>손잡이가 움직이는 범위 (크기 설정에 따라 바뀜)</summary>
        public float MovementRange => stick ? stick.movementRange : 0f;
        public StickTouchZone Zone => _zone;
        public bool IsEditing { get; private set; }
        public bool IsHeld => _held;

        /// <summary>조이스틱 중심 (부모 기준 로컬 좌표)</summary>
        public Vector2 Center => _area ? (Vector2)_area.localPosition : Vector2.zero;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => InstallAll();

        /// <summary>씬의 화면 조이스틱마다 붙인다</summary>
        public static void InstallAll()
        {
            foreach (var s in FindObjectsByType<OnScreenStick>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var p = s.transform.parent;
                if (!p || !(p is RectTransform) || p.GetComponent<JoystickLayout>()) continue;
                var layout = p.gameObject.AddComponent<JoystickLayout>();
                layout.stick = s;
                layout.Init();
            }
        }

        void Awake() => Init();

        void Init()
        {
            if (_ready) return;
            if (!stick) stick = GetComponentInChildren<OnScreenStick>(true);
            if (!stick) return;
            _area = (RectTransform)transform;
            _knob = (RectTransform)stick.transform;
            _parent = _area.parent as RectTransform;
            if (!_parent) return;
            _bg = GetComponent<Image>();
            _knobImage = stick.GetComponent<Image>();
            if (_bg) _bgColor = _bg.color;
            if (_knobImage) _knobColor = _knobImage.color;
            _baseArea = _area.sizeDelta;
            _baseKnob = _knob.sizeDelta;
            _baseRange = stick.movementRange;
            _defaultAnchored = _area.anchoredPosition;
            stick.behaviour = OnScreenStick.Behaviour.RelativePositionWithStaticOrigin;

            // 누른 곳으로 옮겨 오는 모드에서 터치를 받는 투명한 판 (조이스틱 바로 아래)
            var zoneGo = new GameObject("StickTouchZone", typeof(RectTransform), typeof(Image));
            zoneGo.transform.SetParent(_parent, false);
            zoneGo.transform.SetSiblingIndex(_area.GetSiblingIndex());
            var img = zoneGo.GetComponent<Image>();
            img.color = Color.clear;
            _zone = zoneGo.AddComponent<StickTouchZone>();
            _zone.layout = this;
            _ready = true;
            if (isActiveAndEnabled) Apply();
        }

        void OnEnable()
        {
            if (!_ready) Init();
            if (!_ready) return;
            Active = this;
            StickSettings.Changed += Apply;
            Apply();
        }

        void OnDisable()
        {
            StickSettings.Changed -= Apply;
            if (Active == this) Active = null;
            if (_held) Release(null);
        }

        void LateUpdate()
        {
            // 화면 크기가 바뀌면 저장한 비율에 맞춰 다시 놓는다
            if (_ready && _parent.rect.size != _lastParentSize) Apply();
        }

        /// <summary>설정을 다시 읽어 크기·자리·터치 판을 맞춘다</summary>
        public void Apply()
        {
            if (!_ready) return;
            _lastParentSize = _parent.rect.size;
            float s = StickSettings.Size;
            _area.sizeDelta = _baseArea * s;
            _knob.sizeDelta = _baseKnob * s;
            stick.movementRange = _baseRange * s;
            if (!_held) MoveTo(HomeLocal());
            UpdateZone();
            UpdateLook();
        }

        /// <summary>손을 뗐을 때 조이스틱이 있는 자리 (부모 로컬 좌표)</summary>
        public Vector2 HomeLocal()
        {
            if (!StickSettings.HasPosition)
            {
                var keep = _area.localPosition;
                _area.anchoredPosition = _defaultAnchored;
                Vector2 home = _area.localPosition;
                _area.localPosition = keep;
                return Clamp(home);
            }
            var r = _parent.rect;
            return Clamp(r.min + Vector2.Scale(StickSettings.Position, r.size));
        }

        Vector2 Clamp(Vector2 local)
        {
            var r = _parent.rect;
            var half = _area.rect.size * 0.5f;
            float minX = r.xMin + half.x, maxX = r.xMax - half.x;
            float minY = r.yMin + half.y, maxY = r.yMax - half.y;
            local.x = minX <= maxX ? Mathf.Clamp(local.x, minX, maxX) : r.center.x;
            local.y = minY <= maxY ? Mathf.Clamp(local.y, minY, maxY) : r.center.y;
            return local;
        }

        void MoveTo(Vector2 local)
        {
            var p = _area.localPosition;
            _area.localPosition = new Vector3(local.x, local.y, p.z);
        }

        void UpdateZone()
        {
            if (!_zone) return;
            bool on = StickSettings.Mode == StickMode.Floating && !IsEditing;
            _zone.gameObject.SetActive(on);
            if (!on) return;
            // 조이스틱이 있는 쪽 화면 절반
            var rt = (RectTransform)_zone.transform;
            bool left = HomeLocal().x <= _parent.rect.center.x;
            rt.anchorMin = new Vector2(left ? 0f : 0.5f, 0f);
            rt.anchorMax = new Vector2(left ? 0.5f : 1f, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void UpdateLook()
        {
            // 자리를 옮기는 동안에는 조이스틱을 또렷하게
            if (_bg) _bg.color = IsEditing ? new Color(_bgColor.r, _bgColor.g, _bgColor.b, Mathf.Max(0.45f, _bgColor.a)) : _bgColor;
            if (_knobImage) _knobImage.color = IsEditing ? new Color(_knobColor.r, _knobColor.g, _knobColor.b, Mathf.Max(0.85f, _knobColor.a)) : _knobColor;
        }

        bool ScreenToLocal(PointerEventData e, out Vector2 local)
        {
            var cam = e != null ? (e.pressEventCamera ? e.pressEventCamera : e.enterEventCamera) : null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, e.position, cam, out local);
        }

        // ── 터치한 곳 모드 ──

        /// <summary>터치 판을 누름: 그 자리로 조이스틱을 옮기고 바로 조작을 시작한다</summary>
        public void Press(PointerEventData e)
        {
            if (!_ready || _held || IsEditing) return;
            if (ScreenToLocal(e, out var local)) MoveTo(Clamp(local));
            _held = true;
            _pointerId = e.pointerId;
            stick.OnPointerDown(e);
        }

        public void Drag(PointerEventData e)
        {
            if (_held && e.pointerId == _pointerId) stick.OnDrag(e);
        }

        public void Release(PointerEventData e)
        {
            if (!_held || (e != null && e.pointerId != _pointerId)) return;
            _held = false;
            if (e != null) stick.OnPointerUp(e);
            else stick.SendMessage("EndInteraction", SendMessageOptions.DontRequireReceiver);
            MoveTo(HomeLocal());
        }

        // ── 자리 옮기기 (설정 화면) ──

        public void BeginEdit()
        {
            IsEditing = true;
            Apply();
        }

        public void EndEdit()
        {
            IsEditing = false;
            Apply();
        }

        /// <summary>자리 옮기기 중 손가락/마우스 위치로 조이스틱을 옮기고 저장한다</summary>
        public void DragHome(PointerEventData e)
        {
            if (!_ready || !ScreenToLocal(e, out var local)) return;
            SetHomeLocal(local);
        }

        /// <summary>조이스틱 중심을 부모 로컬 좌표로 정해 저장</summary>
        public void SetHomeLocal(Vector2 local)
        {
            var r = _parent.rect;
            local = Clamp(local);
            if (r.width <= 0f || r.height <= 0f) return;
            StickSettings.Position = new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
        }
    }
}
