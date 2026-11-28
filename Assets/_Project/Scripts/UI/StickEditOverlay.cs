using UnityEngine;
using UnityEngine.EventSystems;

namespace DragonRecipe.UI
{
    /// <summary>조이스틱 자리 옮기기 중 화면 전체의 터치·드래그를 받아 조이스틱을 그 자리로 옮긴다.</summary>
    public class StickEditOverlay : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public void OnPointerDown(PointerEventData e) => Move(e);
        public void OnDrag(PointerEventData e) => Move(e);

        static void Move(PointerEventData e)
        {
            if (JoystickLayout.Active) JoystickLayout.Active.DragHome(e);
        }
    }
}
