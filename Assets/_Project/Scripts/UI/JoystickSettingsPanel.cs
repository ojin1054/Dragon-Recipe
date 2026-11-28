using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DragonRecipe.UI
{
    /// <summary>
    /// 일시정지 메뉴 안의 조이스틱 설정: 고정 / 터치한 곳, 크기 −/+, 자리 옮기기, 기본값.
    /// 씬에 없으면 <see cref="PauseMenu"/>가 실행 중에 만든다 (<see cref="Build"/>).
    /// </summary>
    public class JoystickSettingsPanel : MonoBehaviour
    {
        public GameObject mainBox;
        public GameObject box;
        public GameObject editOverlay;
        public Image dim;
        public Button modeButton;
        public Text modeLabel;
        public Button smallerButton;
        public Button biggerButton;
        public Text sizeLabel;
        public Button moveButton;
        public Button resetButton;
        public Button backButton;
        public Button doneButton;

        Color _dimColor;
        bool _wired;

        public bool IsOpen => box && box.activeSelf;
        public bool IsMoving => editOverlay && editOverlay.activeSelf;

        void Awake() => Wire();

        void Wire()
        {
            if (_wired || !modeButton) return;
            _wired = true;
            if (dim) _dimColor = dim.color;
            modeButton?.onClick.AddListener(ToggleMode);
            smallerButton?.onClick.AddListener(() => StickSettings.Size -= StickSettings.SizeStep);
            biggerButton?.onClick.AddListener(() => StickSettings.Size += StickSettings.SizeStep);
            moveButton?.onClick.AddListener(BeginMove);
            resetButton?.onClick.AddListener(StickSettings.ResetAll);
            backButton?.onClick.AddListener(Close);
            doneButton?.onClick.AddListener(EndMove);
        }

        void OnEnable() => StickSettings.Changed += Refresh;
        void OnDisable() => StickSettings.Changed -= Refresh;

        public void Open()
        {
            if (mainBox) mainBox.SetActive(false);
            if (box) box.SetActive(true);
            Refresh();
            Select(modeButton);
        }

        public void Close()
        {
            EndMove();
            if (box) box.SetActive(false);
            if (mainBox) mainBox.SetActive(true);
        }

        /// <summary>메뉴가 닫힐 때: 처음 화면으로</summary>
        public void ResetView()
        {
            EndMove();
            if (box) box.SetActive(false);
            if (mainBox) mainBox.SetActive(true);
        }

        void ToggleMode()
        {
            StickSettings.Mode = StickSettings.Mode == StickMode.Fixed ? StickMode.Floating : StickMode.Fixed;
        }

        public void BeginMove()
        {
            var layout = JoystickLayout.Active;
            if (!layout) return;
            if (box) box.SetActive(false);
            if (editOverlay) editOverlay.SetActive(true);
            if (dim) dim.color = new Color(_dimColor.r, _dimColor.g, _dimColor.b, 0.15f);
            layout.BeginEdit();
            Select(doneButton);
        }

        public void EndMove()
        {
            if (!IsMoving) return;
            editOverlay.SetActive(false);
            if (dim) dim.color = _dimColor;
            if (JoystickLayout.Active) JoystickLayout.Active.EndEdit();
            if (box) box.SetActive(true);
            Select(moveButton);
        }

        public void Refresh()
        {
            if (modeLabel) modeLabel.text = StickSettings.Mode == StickMode.Fixed ? "조이스틱: 고정" : "조이스틱: 터치한 곳";
            float s = StickSettings.Size;
            if (sizeLabel) sizeLabel.text = $"크기 {Mathf.RoundToInt(s * 100f)}%";
            if (smallerButton) smallerButton.interactable = s > StickSettings.MinSize + 0.001f;
            if (biggerButton) biggerButton.interactable = s < StickSettings.MaxSize - 0.001f;
            if (moveButton) moveButton.interactable = JoystickLayout.Active;
        }

        static void Select(Button b)
        {
            if (b && EventSystem.current) EventSystem.current.SetSelectedGameObject(b.gameObject);
        }

        // ── 실행 중에 만들기 ──

        /// <summary>
        /// 일시정지 메뉴 상자(<paramref name="mainBox"/>)와 같은 모양으로 설정 상자를 만든다.
        /// 버튼은 <paramref name="template"/>을 복제한다.
        /// </summary>
        public static JoystickSettingsPanel Build(RectTransform dimRoot, RectTransform mainBox, Button template)
        {
            var panel = dimRoot.gameObject.AddComponent<JoystickSettingsPanel>();
            panel.mainBox = mainBox.gameObject;
            panel.dim = dimRoot.GetComponent<Image>();
            var font = template.GetComponentInChildren<Text>(true).font;

            // 설정 상자 (메뉴 상자와 같은 크기·색)
            var box = new GameObject("StickSettings", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(dimRoot, false);
            var brt = (RectTransform)box.transform;
            brt.anchorMin = mainBox.anchorMin;
            brt.anchorMax = mainBox.anchorMax;
            brt.pivot = mainBox.pivot;
            brt.anchoredPosition = mainBox.anchoredPosition;
            brt.sizeDelta = mainBox.sizeDelta;
            var srcImg = mainBox.GetComponent<Image>();
            var boxImg = box.GetComponent<Image>();
            if (srcImg) { boxImg.color = srcImg.color; boxImg.sprite = srcImg.sprite; boxImg.type = srcImg.type; }
            panel.box = box;

            var title = MakeText(brt, "Title", font, 56, new Vector2(0, brt.sizeDelta.y * 0.5f - 70f), new Vector2(680, 90));
            title.text = "조이스틱";

            float h = brt.sizeDelta.y;
            float top = h * 0.5f - 200f;
            panel.modeButton = Clone(template, brt, "Mode", new Vector2(0, top), new Vector2(480, 100), "", out panel.modeLabel);

            panel.smallerButton = Clone(template, brt, "Smaller", new Vector2(-185, top - 120), new Vector2(110, 100), "-", out _);
            panel.biggerButton = Clone(template, brt, "Bigger", new Vector2(185, top - 120), new Vector2(110, 100), "+", out _);
            panel.sizeLabel = MakeText(brt, "Size", font, 40, new Vector2(0, top - 120), new Vector2(240, 100));

            panel.moveButton = Clone(template, brt, "Move", new Vector2(0, top - 240), new Vector2(480, 100), "자리 옮기기", out _);
            panel.resetButton = Clone(template, brt, "Reset", new Vector2(0, top - 360), new Vector2(480, 100), "기본값으로", out _);
            panel.backButton = Clone(template, brt, "Back", new Vector2(0, top - 480), new Vector2(480, 100), "돌아가기", out _);
            var backImg = panel.backButton.targetGraphic as Image;
            if (backImg) backImg.color = new Color(0.55f, 0.8f, 1f);
            box.SetActive(false);

            // 자리 옮기기: 화면 전체로 드래그를 받는 판 + 완료 버튼
            var overlay = new GameObject("StickMoveOverlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(dimRoot, false);
            var ort = (RectTransform)overlay.transform;
            ort.anchorMin = Vector2.zero;
            ort.anchorMax = Vector2.one;
            ort.offsetMin = ort.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = Color.clear;
            overlay.AddComponent<StickEditOverlay>();
            panel.doneButton = Clone(template, ort, "Done", Vector2.zero, new Vector2(300, 100), "완료", out _);
            var drt = (RectTransform)panel.doneButton.transform;
            drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 1f);
            drt.anchoredPosition = new Vector2(0, -90);
            overlay.SetActive(false);
            panel.editOverlay = overlay;

            panel.Wire();
            panel.Refresh();
            return panel;
        }

        internal static Button Clone(Button template, RectTransform parent, string name, Vector2 pos, Vector2 size, string label, out Text text)
        {
            var b = Instantiate(template, parent);
            b.name = name;
            b.onClick.RemoveAllListeners();
            b.interactable = true;
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            text = b.GetComponentInChildren<Text>(true);
            if (text)
            {
                text.text = label;
                text.rectTransform.sizeDelta = size - new Vector2(20, 10);
            }
            return b;
        }

        static Text MakeText(RectTransform parent, string name, Font font, int fontSize, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }
    }
}
