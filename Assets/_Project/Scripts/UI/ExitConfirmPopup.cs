using DragonRecipe.Care;
using DragonRecipe.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DragonRecipe.UI
{
    /// <summary>
    /// 모바일 뒤로가기(Android Back = Input System의 Keyboard/escape)를 누르면 "게임을 종료할까요?" 팝업을 띄운다.
    /// 열려 있는 동안 게임은 멈추고, 다시 뒤로가기를 누르거나 "취소"를 누르면 닫힌다. "종료"는 저장 후 앱을 끈다.
    /// 씬과 상관없이 하나만 살아 있다 (스스로 생성, DontDestroyOnLoad).
    /// 에디터·개발 빌드에서는 F4로 시험할 수 있다 (PC의 Esc는 비행 일시정지로 쓰므로 비워 둠).
    /// </summary>
    public class ExitConfirmPopup : MonoBehaviour
    {
        static ExitConfirmPopup _instance;
        public static bool IsOpen => _instance && _instance._root && _instance._root.activeSelf;

        InputAction _back;
        GameObject _root;
        bool _pausedBefore;
        bool _built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance) return;
            if (!(Application.isMobilePlatform || Application.isEditor || Debug.isDebugBuild)) return;
            var go = new GameObject("ExitConfirmPopup");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ExitConfirmPopup>();
        }

        void Awake()
        {
            _back = new InputAction("Back", InputActionType.Button);
            if (Application.isMobilePlatform) _back.AddBinding("<Keyboard>/escape");
            if (Application.isEditor || Debug.isDebugBuild) _back.AddBinding("<Keyboard>/f4");
            _back.performed += _ => OnBack();

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // 모든 HUD·패널 위
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
        }

        void OnEnable() => _back.Enable();
        void OnDisable() => _back.Disable();

        void OnDestroy()
        {
            _back.Dispose();
            if (_instance == this) _instance = null;
        }

        void OnBack()
        {
            if (IsOpen) Close();
            else Open();
        }

        void Open()
        {
            if (!_built) Build();
            _pausedBefore = GamePause.IsPaused;
            GamePause.Set(true);
            _root.SetActive(true);
        }

        void Close()
        {
            _root.SetActive(false);
            GamePause.Set(_pausedBefore);
        }

        void Quit()
        {
            DragonCare.Save();
#if UNITY_EDITOR
            Close();
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ───────── 만들기 ─────────

        void Build()
        {
            _built = true;
            var font = FindFont();

            // 화면 전체를 어둡게 덮어 뒤 UI 터치를 막는다
            _root = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            var dim = (RectTransform)_root.transform;
            dim.SetParent(transform, false);
            dim.anchorMin = Vector2.zero;
            dim.anchorMax = Vector2.one;
            dim.offsetMin = dim.offsetMax = Vector2.zero;
            _root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            var boxRt = (RectTransform)box.transform;
            boxRt.SetParent(dim, false);
            boxRt.sizeDelta = new Vector2(760f, 380f);
            box.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.15f, 0.97f);

            var label = new GameObject("Message", typeof(RectTransform)).AddComponent<Text>();
            label.rectTransform.SetParent(boxRt, false);
            label.rectTransform.anchoredPosition = new Vector2(0f, 70f);
            label.rectTransform.sizeDelta = new Vector2(700f, 120f);
            label.font = font;
            label.fontSize = 46;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = "게임을 종료할까요?";
            label.raycastTarget = false;

            MakeButton(boxRt, "종료", new Vector2(-170f, -90f), new Color(0.72f, 0.24f, 0.18f, 1f), font, Quit);
            MakeButton(boxRt, "취소", new Vector2(170f, -90f), new Color(0.26f, 0.3f, 0.38f, 1f), font, Close);

            _root.SetActive(false);
        }

        static void MakeButton(Transform parent, string text, Vector2 pos, Color color, Font font, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(280f, 110f);
            go.GetComponent<Image>().color = color;
            go.GetComponent<Button>().onClick.AddListener(onClick);

            var label = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
            label.rectTransform.SetParent(rt, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.font = font;
            label.fontSize = 40;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = text;
            label.raycastTarget = false;
        }

        /// <summary>한글이 나오는 씬 UI의 글꼴을 빌려 쓴다 (없으면 기본 글꼴)</summary>
        static Font FindFont()
        {
            foreach (var t in FindObjectsByType<Text>(FindObjectsSortMode.None))
                if (t.font) return t.font;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
