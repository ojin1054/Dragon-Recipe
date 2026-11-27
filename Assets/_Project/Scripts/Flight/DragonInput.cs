using System;
using DragonRecipe.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DragonRecipe.Flight
{
    /// <summary>
    /// 키보드(WASD/방향키 + Space, Shift/C, Esc/P), 게임패드(왼쪽 스틱 + A, B/RT, Start),
    /// 모바일 화면 조이스틱/버튼(OnScreenStick/Button → 가상 게임패드)을 한 번에 받는다.
    /// </summary>
    public class DragonInput : MonoBehaviour
    {
        const string InvertPitchKey = "DragonRecipe.InvertPitch";

        InputAction _move;
        InputAction _flap;
        InputAction _crouch;
        InputAction _pause;
        bool _flapBuffered;

        /// <summary>일시정지 버튼(Esc / P / 패드 Start)이 눌림</summary>
        public event Action PausePressed;

        /// <summary>
        /// 상/하 반전. 기본(꺼짐)은 스틱 위 = 기수 위, 켜면 스틱 위 = 기수 아래 (비행기식).
        /// 기기에 저장된다.
        /// </summary>
        public bool InvertPitch
        {
            get => PlayerPrefs.GetInt(InvertPitchKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(InvertPitchKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public Vector2 Move
        {
            get
            {
                if (GamePause.IsPaused || _move == null) return Vector2.zero;
                var v = _move.ReadValue<Vector2>();
                if (InvertPitch) v.y = -v.y;
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        /// <summary>웅크리기 버튼(이륙 전용)을 지금 누르고 있는지</summary>
        public bool CrouchHeld => !GamePause.IsPaused && _crouch != null && _crouch.IsPressed();

        /// <summary>마지막 호출 이후 날갯짓 버튼이 눌렸는지 (한 번 읽으면 지워짐)</summary>
        public bool ConsumeFlap()
        {
            bool f = _flapBuffered;
            _flapBuffered = false;
            return f;
        }

        void Awake()
        {
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _move.AddBinding("<Gamepad>/leftStick");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");

            _flap = new InputAction("Flap", InputActionType.Button);
            _flap.AddBinding("<Keyboard>/space");
            _flap.AddBinding("<Gamepad>/buttonSouth");
            _flap.performed += _ =>
            {
                if (!GamePause.IsPaused) _flapBuffered = true; // 일시정지 중 누른 건 재개 후에 터지지 않게
            };

            _crouch = new InputAction("Crouch", InputActionType.Button);
            _crouch.AddBinding("<Keyboard>/leftShift");
            _crouch.AddBinding("<Keyboard>/c");
            _crouch.AddBinding("<Gamepad>/buttonEast");
            _crouch.AddBinding("<Gamepad>/rightTrigger");

            _pause = new InputAction("Pause", InputActionType.Button);
            _pause.AddBinding("<Keyboard>/escape");
            _pause.AddBinding("<Keyboard>/p");
            _pause.AddBinding("<Gamepad>/start");
            _pause.performed += _ => PausePressed?.Invoke();
        }

        void OnEnable()
        {
            _move.Enable();
            _flap.Enable();
            _crouch.Enable();
            _pause.Enable();
        }

        void OnDisable()
        {
            _move.Disable();
            _flap.Disable();
            _crouch.Disable();
            _pause.Disable();
            _flapBuffered = false;
        }

        void OnDestroy()
        {
            _move.Dispose();
            _flap.Dispose();
            _crouch.Dispose();
            _pause.Dispose();
        }
    }
}
