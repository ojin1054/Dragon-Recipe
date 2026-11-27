using UnityEngine;
using UnityEngine.InputSystem;

namespace DragonRecipe.Flight
{
    /// <summary>
    /// 키보드(WASD/방향키 + Space), 게임패드(왼쪽 스틱 + A),
    /// 모바일 화면 조이스틱/버튼(OnScreenStick/Button → 가상 게임패드)을 한 번에 받는다.
    /// </summary>
    public class DragonInput : MonoBehaviour
    {
        [Tooltip("켜면 스틱을 위로 밀 때 기수가 내려감 (비행기식)")]
        public bool invertPitch;

        InputAction _move;
        InputAction _flap;
        bool _flapBuffered;

        public Vector2 Move
        {
            get
            {
                var v = _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
                if (invertPitch) v.y = -v.y;
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

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
            _flap.performed += _ => _flapBuffered = true;
        }

        void OnEnable()
        {
            _move.Enable();
            _flap.Enable();
        }

        void OnDisable()
        {
            _move.Disable();
            _flap.Disable();
            _flapBuffered = false;
        }

        void OnDestroy()
        {
            _move.Dispose();
            _flap.Dispose();
        }
    }
}
