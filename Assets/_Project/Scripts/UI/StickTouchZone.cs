using UnityEngine;
using UnityEngine.EventSystems;

namespace DragonRecipe.UI
{
    /// <summary>'터치한 곳' 조이스틱 모드에서 화면 절반의 터치를 받아 <see cref="JoystickLayout"/>에 넘긴다.</summary>
    public class StickTouchZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public JoystickLayout layout;

        public void OnPointerDown(PointerEventData e) { if (layout) layout.Press(e); }
        public void OnDrag(PointerEventData e) { if (layout) layout.Drag(e); }
        public void OnPointerUp(PointerEventData e) { if (layout) layout.Release(e); }
    }
}
