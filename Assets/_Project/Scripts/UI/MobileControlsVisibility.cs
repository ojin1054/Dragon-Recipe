using UnityEngine;
using UnityEngine.InputSystem;

namespace DragonRecipe.UI
{
    /// <summary>화면 조이스틱/버튼은 모바일·터치 기기·에디터에서만 보인다.</summary>
    public class MobileControlsVisibility : MonoBehaviour
    {
        public GameObject controlsRoot;
        public bool showInEditor = true;

        void Start()
        {
            bool show = Application.isMobilePlatform
                        || Touchscreen.current != null
                        || (Application.isEditor && showInEditor);
            if (controlsRoot) controlsRoot.SetActive(show);
        }
    }
}
