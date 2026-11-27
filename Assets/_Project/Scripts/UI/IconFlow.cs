using UnityEngine;
using UnityEngine.UI;

namespace DragonRecipe.UI
{
    /// <summary>
    /// 아이콘과 짧은 숫자를 줄 단위로 늘어놓는 UI 컨테이너.
    /// flow.Clear(); flow.AddRow().Icon(set.meat).Text("+20");
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class IconFlow : MonoBehaviour
    {
        public Font font;
        public int fontSize = 34;
        public float iconSize = 48f;
        public Color textColor = Color.white;
        public float itemSpacing = 8f;
        public float rowSpacing = 6f;
        [Tooltip("줄 정렬 (UpperLeft / UpperCenter / LowerLeft 등)")]
        public TextAnchor alignment = TextAnchor.UpperLeft;

        VerticalLayoutGroup _layout;

        void Awake() => EnsureLayout();

        void EnsureLayout()
        {
            if (_layout) return;
            _layout = GetComponent<VerticalLayoutGroup>();
            if (!_layout) _layout = gameObject.AddComponent<VerticalLayoutGroup>();
            _layout.childAlignment = alignment;
            _layout.childControlWidth = true;
            _layout.childControlHeight = true;
            _layout.childForceExpandWidth = false;
            _layout.childForceExpandHeight = false;
            _layout.spacing = rowSpacing;
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                child.SetActive(false); // Destroy는 프레임 끝에 일어나므로 레이아웃에서 바로 빠지게
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        public Row AddRow()
        {
            EnsureLayout();
            var go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = RowAlignment(alignment);
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.spacing = itemSpacing;
            return new Row(this, go.transform);
        }

        static TextAnchor RowAlignment(TextAnchor a)
        {
            switch (a)
            {
                case TextAnchor.UpperCenter:
                case TextAnchor.MiddleCenter:
                case TextAnchor.LowerCenter:
                    return TextAnchor.MiddleCenter;
                case TextAnchor.UpperRight:
                case TextAnchor.MiddleRight:
                case TextAnchor.LowerRight:
                    return TextAnchor.MiddleRight;
                default:
                    return TextAnchor.MiddleLeft;
            }
        }

        public readonly struct Row
        {
            readonly IconFlow _flow;
            readonly Transform _root;

            public Row(IconFlow flow, Transform root)
            {
                _flow = flow;
                _root = root;
            }

            /// <param name="scale">기본 아이콘 크기 배율</param>
            public Row Icon(Sprite sprite, float scale = 1f)
            {
                if (!sprite) return this;
                var go = new GameObject(sprite.name, typeof(RectTransform));
                go.transform.SetParent(_root, false);
                var img = go.AddComponent<Image>();
                img.sprite = sprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = le.minWidth = _flow.iconSize * scale;
                le.preferredHeight = le.minHeight = _flow.iconSize * scale;
                return this;
            }

            /// <summary>아이콘을 count번 반복 (먹이 여러 마리 등). 너무 많으면 "xN"으로 줄인다</summary>
            public Row Repeat(Sprite sprite, int count, int maxIcons = 4, float scale = 1f)
            {
                if (count <= 0) return this;
                if (count <= maxIcons)
                {
                    for (int i = 0; i < count; i++) Icon(sprite, scale);
                    return this;
                }
                return Icon(sprite, scale).Text($"x{count}");
            }

            public Row Text(string text, Color? color = null)
            {
                var go = new GameObject("Text", typeof(RectTransform));
                go.transform.SetParent(_root, false);
                var t = go.AddComponent<Text>();
                t.font = _flow.font ? _flow.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                t.fontSize = _flow.fontSize;
                t.color = color ?? _flow.textColor;
                t.text = text;
                t.alignment = TextAnchor.MiddleLeft;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                var shadow = go.AddComponent<Shadow>();
                shadow.effectColor = new Color(0, 0, 0, 0.6f);
                shadow.effectDistance = new Vector2(2, -2);
                return this;
            }

            public Row Gap(float width = 24f)
            {
                var go = new GameObject("Gap", typeof(RectTransform));
                go.transform.SetParent(_root, false);
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = le.minWidth = width;
                return this;
            }
        }
    }
}
