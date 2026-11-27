using System.Collections.Generic;
using DragonRecipe.Care;
using UnityEngine;
using UnityEngine.UI;

namespace DragonRecipe.UI
{
    /// <summary>
    /// 둥지 먹이 창고: 들고 온 먹이를 종류별 버튼으로 보여 주고, 누르면 하나씩 먹인다.
    /// 배가 꽉 차면 버튼이 흐려진다.
    /// </summary>
    public class StoragePanel : MonoBehaviour
    {
        public UIIconSet icons;
        [Tooltip("창고 머리줄 (바구니 + 남은 칸)")]
        public IconFlow header;
        [Tooltip("먹이 버튼이 놓일 곳 (GridLayoutGroup)")]
        public RectTransform grid;
        public Font font;
        public Sprite buttonSprite;

        static readonly Color Slot = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color RareSlot = new Color(1f, 0.85f, 0.3f, 0.3f);

        string _key;

        struct Group
        {
            public string id;
            public bool rare;
            public int count;
            public int firstIndex;
        }

        void Update()
        {
            if (!DragonCare.IsLoaded) return;
            var s = DragonCare.State;
            var c = DragonCare.Config.care;
            bool room = CareLogic.HasRoom(s, c);
            string key = $"{s.storage.Count}|{room}|{s.generation}";
            foreach (var f in s.storage) key += f.id[0];
            if (key == _key) return;
            _key = key;
            Rebuild(s, c, room);
        }

        void Rebuild(DragonState s, CareSettings c, bool room)
        {
            if (header && icons)
            {
                header.Clear();
                header.AddRow().Icon(icons.basket, 1.2f).Text($"{s.storage.Count}/{c.storageCapacity}");
            }
            if (!grid) return;
            for (int i = grid.childCount - 1; i >= 0; i--)
            {
                var child = grid.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            foreach (var g in Groups(s.storage))
                MakeButton(g, room);
        }

        static List<Group> Groups(List<StoredFood> storage)
        {
            var groups = new List<Group>();
            for (int i = 0; i < storage.Count; i++)
            {
                var f = storage[i];
                int at = groups.FindIndex(g => g.id == f.id && g.rare == f.rare);
                if (at < 0)
                {
                    groups.Add(new Group { id = f.id, rare = f.rare, count = 1, firstIndex = i });
                }
                else
                {
                    var g = groups[at];
                    g.count++;
                    groups[at] = g;
                }
            }
            return groups;
        }

        void MakeButton(Group g, bool room)
        {
            var food = DragonCare.FindFood(g.id);
            var go = new GameObject($"Feed_{g.id}", typeof(RectTransform));
            go.transform.SetParent(grid, false);
            var bg = go.AddComponent<Image>();
            bg.sprite = buttonSprite;
            bg.type = Image.Type.Sliced;
            bg.color = g.rare ? RareSlot : Slot;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.interactable = room;
            string id = g.id;
            bool rare = g.rare;
            btn.onClick.AddListener(() => Feed(id, rare));

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            var irt = (RectTransform)iconGo.transform;
            irt.anchorMin = new Vector2(0.12f, 0.2f);
            irt.anchorMax = new Vector2(0.88f, 0.92f);
            irt.offsetMin = irt.offsetMax = Vector2.zero;
            var img = iconGo.AddComponent<Image>();
            img.sprite = food && food.icon ? food.icon : icons ? icons.meat : null;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = room ? Color.white : new Color(1f, 1f, 1f, 0.35f); // 배가 꽉 참

            if (g.rare && icons && icons.sparkle)
            {
                var sp = new GameObject("Rare", typeof(RectTransform));
                sp.transform.SetParent(go.transform, false);
                var srt = (RectTransform)sp.transform;
                srt.anchorMin = srt.anchorMax = new Vector2(1f, 1f);
                srt.pivot = new Vector2(1f, 1f);
                srt.sizeDelta = new Vector2(34f, 34f);
                srt.anchoredPosition = new Vector2(-4f, -4f);
                var si = sp.AddComponent<Image>();
                si.sprite = icons.sparkle;
                si.raycastTarget = false;
            }

            if (g.count > 1)
            {
                var tg = new GameObject("Count", typeof(RectTransform));
                tg.transform.SetParent(go.transform, false);
                var trt = (RectTransform)tg.transform;
                trt.anchorMin = new Vector2(0f, 0f);
                trt.anchorMax = new Vector2(1f, 0.35f);
                trt.offsetMin = trt.offsetMax = Vector2.zero;
                var t = tg.AddComponent<Text>();
                t.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                t.fontSize = 26;
                t.alignment = TextAnchor.LowerRight;
                t.text = $"x{g.count} ";
                t.raycastTarget = false;
                var sh = tg.AddComponent<Shadow>();
                sh.effectColor = new Color(0, 0, 0, 0.7f);
            }
        }

        static void Feed(string id, bool rare)
        {
            if (!DragonCare.IsLoaded) return;
            var st = DragonCare.State.storage;
            int index = st.FindIndex(f => f.id == id && f.rare == rare);
            if (index >= 0) DragonCare.FeedFromStorage(index);
        }
    }
}
