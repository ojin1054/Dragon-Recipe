using System;
using System.Collections.Generic;
using DragonRecipe.Care;
using DragonRecipe.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace DragonRecipe.UI
{
    /// <summary>
    /// 둥지 동물 농장: 우리(동물 칸), 먹이기 / 조합 버튼, 조합 칸 진행 게이지, 붙은 버프.
    /// 동물을 하나 고르면 먹이기, 둘 고르면 조합 미리보기(결과 실루엣 · 확률 · 시간)가 켜진다.
    /// 상자 안의 내용은 실행할 때 직접 만든다 (씬 빌더는 상자와 버튼만 만든다).
    /// </summary>
    public class FarmPanel : MonoBehaviour
    {
        public UIIconSet icons;
        public Font font;
        public Sprite buttonSprite;
        public Button openButton;
        public Button closeButton;
        [Tooltip("열고 닫는 전체 덮개")]
        public GameObject panel;
        [Tooltip("내용이 들어갈 상자 (1300 x 900 기준)")]
        public RectTransform box;
        [Tooltip("농장을 열면 우리를 비추는 둥지 카메라")]
        public NestCamera nestCamera;
        [Tooltip("농장을 열어 둔 동안 감출 둥지 화면 조각 (우리가 가려지지 않게)")]
        public CanvasGroup[] hideWhileOpen;
        [Tooltip("[디버그] 상점이 생기기 전까지 우리를 넓히는 버튼")]
        public Button debugUpgradeButton;
        [Tooltip("끝난 조합을 확인하는 간격 (초)")]
        public float collectInterval = 1f;

        static readonly Color SlotColor = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color SelectedColor = new Color(1f, 0.8f, 0.3f, 0.75f);
        static readonly Color LockedColor = new Color(0f, 0f, 0f, 0.45f);
        static readonly Color ActionColor = new Color(1f, 0.75f, 0.35f, 0.9f);
        static readonly Color BarBack = new Color(0f, 0f, 0f, 0.5f);
        static readonly Color BarFill = new Color(1f, 0.8f, 0.35f);
        static readonly Color SuccessColor = new Color(0.55f, 0.95f, 0.5f, 0.6f);
        static readonly Color FailColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);

        IconFlow _header;
        IconFlow _buffs;
        RectTransform _pen;
        RectTransform _preview;
        Text _previewText;
        Button _feedButton;
        Button _combineButton;
        RectTransform _jobs;
        RectTransform _storage;
        RectTransform _resultPop;

        readonly List<string> _selected = new List<string>();
        readonly List<(Image fill, Text time, CombineJob job)> _jobViews = new List<(Image, Text, CombineJob)>();
        string _key;
        float _nextCollect;
        float _popUntil;
        bool _built;

        void Awake()
        {
            if (openButton) openButton.onClick.AddListener(Open);
            if (closeButton) closeButton.onClick.AddListener(Close);
            if (debugUpgradeButton) debugUpgradeButton.onClick.AddListener(() => DragonCare.UpgradePen());
            if (panel) panel.SetActive(false);
            DragonCare.CombinesCollected += OnCollected;
        }

        void OnDestroy() => DragonCare.CombinesCollected -= OnCollected;

        public bool IsOpen => panel && panel.activeSelf;

        public void Open()
        {
            if (!panel) return;
            panel.SetActive(true);
            _selected.Clear();
            _key = null;
            SetNestView(farm: true);
        }

        public void Close()
        {
            if (panel) panel.SetActive(false);
            SetNestView(farm: false);
        }

        void SetNestView(bool farm)
        {
            if (nestCamera) nestCamera.ShowFarm(farm);
            if (hideWhileOpen == null) return;
            foreach (var g in hideWhileOpen)
            {
                if (!g) continue;
                g.alpha = farm ? 0f : 1f;
                g.blocksRaycasts = !farm;
                g.interactable = !farm;
            }
        }

        void Update()
        {
            if (!DragonCare.IsLoaded || DragonCare.FarmSettings == null) return;

            // 둥지에 있는 동안은 닫혀 있어도 끝난 조합을 거둔다
            if (Time.unscaledTime >= _nextCollect)
            {
                _nextCollect = Time.unscaledTime + collectInterval;
                DragonCare.CollectCombines();
            }

            AnimateOpenButton();
            if (!IsOpen || !box || !icons) return;
            if (!_built) Build();

            var f = DragonCare.Farm;
            _selected.RemoveAll(uid => FarmLogic.Find(f, uid) == null);
            string key = Key(f);
            if (key != _key)
            {
                _key = key;
                Rebuild(f);
            }
            UpdateJobs();
            if (_resultPop) _resultPop.gameObject.SetActive(Time.unscaledTime < _popUntil);
        }

        /// <summary>조합이 끝나 거둘 게 있거나 막 거뒀으면 농장 버튼이 통통 튄다</summary>
        void AnimateOpenButton()
        {
            if (!openButton) return;
            var f = DragonCare.Farm;
            bool ready = false;
            var now = DragonCare.Clock();
            foreach (var j in f.jobs) ready |= j.IsReady(now);
            bool bounce = !IsOpen && (ready || Time.unscaledTime < _popUntil);
            float s = bounce ? 1f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) * 0.12f : 1f;
            openButton.transform.localScale = Vector3.one * s;
        }

        string Key(FarmState f)
        {
            var s = DragonCare.State;
            string k = $"{f.animals.Count}|{f.jobs.Count}|{f.penUpgradeLevel}|{s.storage.Count}|{CareLogic.HasRoom(s, DragonCare.Config.care)}|";
            foreach (var a in f.animals) k += a.uid[0];
            foreach (var b in f.buffs) k += $"{b.sourceId}{b.flightsLeft}";
            foreach (var u in _selected) k += u;
            return k;
        }

        // ───────────── 만들기 ─────────────

        void Build()
        {
            _built = true;
            _header = Flow("Header", new Vector2(40, -30), new Vector2(600, 80), TextAnchor.UpperLeft, 60f);
            _buffs = Flow("Buffs", new Vector2(560, -30), new Vector2(600, 80), TextAnchor.UpperRight, 52f);

            _pen = Grid("Pen", new Vector2(40, -130), new Vector2(760, 370), 112f, 6);

            _preview = Rect("Preview", box, new Vector2(840, -130), new Vector2(420, 130));
            var h = _preview.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            h.spacing = 8f;
            _previewText = MakeText(box, "PreviewInfo", new Vector2(840, -265), new Vector2(420, 50), 34, TextAnchor.MiddleLeft);

            _feedButton = ActionButton("Feed", new Vector2(840, -330), icons.meat, OnFeed);
            _combineButton = ActionButton("Combine", new Vector2(1060, -330), icons.sparkle, OnCombine);

            _jobs = Rect("Jobs", box, new Vector2(40, -530), new Vector2(1220, 150));
            var jh = _jobs.gameObject.AddComponent<HorizontalLayoutGroup>();
            jh.childAlignment = TextAnchor.MiddleLeft;
            jh.childControlWidth = jh.childControlHeight = true;
            jh.childForceExpandWidth = jh.childForceExpandHeight = false;
            jh.spacing = 20f;

            // [임시] 창고 → 우리. 포획해서 바로 데려오는 기능이 합쳐지면 뺀다
            var basket = Rect("StorageIcon", box, new Vector2(40, -720), new Vector2(90, 90));
            FarmIcons.Layer(basket, icons.basket, Vector2.zero, Vector2.one, Color.white);
            var arrow = Rect("StorageArrow", box, new Vector2(135, -735), new Vector2(60, 60));
            FarmIcons.Layer(arrow, icons.arrowRight, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0.6f));
            _storage = Grid("Storage", new Vector2(210, -710), new Vector2(1050, 160), 96f, 9);

            _resultPop = Rect("ResultPop", box, new Vector2(840, -40), new Vector2(160, 80));
            _resultPop.gameObject.SetActive(false);
        }

        void Rebuild(FarmState f)
        {
            var settings = DragonCare.FarmSettings;
            var s = DragonCare.State;
            int cap = FarmLogic.PenCapacity(f, settings);
            int maxCap = settings.PenCapacity(settings.penUpgrades.Count);

            _header.Clear();
            _header.AddRow().Icon(icons.fence ? icons.fence : icons.basket, 1.1f).Text($"{f.animals.Count}/{cap}");

            _buffs.Clear();
            if (f.buffs.Count > 0)
            {
                var row = _buffs.AddRow();
                foreach (var b in f.buffs) row = FarmIcons.Buff(row, icons, b, 0.8f);
            }

            // 우리
            ClearChildren(_pen);
            for (int i = 0; i < maxCap; i++)
            {
                if (i < f.animals.Count) AnimalSlot(_pen, f.animals[i]);
                else EmptySlot(_pen, locked: i >= cap);
            }

            RebuildActions(f, s);
            RebuildJobs(f, settings);
            RebuildStorage(s, f, settings);
        }

        void RebuildActions(FarmState f, DragonState s)
        {
            ClearChildren(_preview);
            _previewText.text = "";
            bool room = CareLogic.HasRoom(s, DragonCare.Config.care);
            _feedButton.interactable = _selected.Count == 1 && room;
            SetIconAlpha(_feedButton, _feedButton.interactable);
            _combineButton.interactable = false;

            foreach (var uid in _selected) PreviewCell(FarmLogic.Find(f, uid));
            if (_selected.Count == 2)
            {
                PreviewIcon(icons.arrowRight, 0.6f);
                var block = DragonCare.CanCombine(_selected[0], _selected[1], out var recipe);
                if (recipe != null)
                {
                    var cell = Cell(_preview, 96f);
                    bool known = f.Discovered(recipe.id);
                    FarmIcons.DrawHybrid(cell, icons, recipe.result, FarmLogic.MainTrait(recipe.result), known ? (Color?)null : FarmIcons.Unknown);
                    var a = FarmLogic.Find(f, _selected[0]);
                    var b = FarmLogic.Find(f, _selected[1]);
                    float chance = FarmLogic.SuccessChance(f, DragonCare.CombineTable, a, b);
                    _previewText.text = $"{chance * 100f:0}%   {FormatMinutes(recipe.durationMinutes)}";
                    _combineButton.interactable = block == CombineBlock.None;
                }
                else
                {
                    PreviewIcon(icons.no, 0.9f);
                }
            }
            SetIconAlpha(_combineButton, _combineButton.interactable);
        }

        void RebuildJobs(FarmState f, FarmSettings settings)
        {
            ClearChildren(_jobs);
            _jobViews.Clear();
            for (int i = 0; i < settings.combineSlots + f.bonusCombineSlots; i++)
            {
                var slot = Rect($"Job_{i}", _jobs, Vector2.zero, new Vector2(580, 140));
                var le = slot.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 580;
                le.preferredHeight = 140;
                var bg = slot.gameObject.AddComponent<Image>();
                bg.sprite = buttonSprite;
                bg.type = Image.Type.Sliced;
                bg.color = SlotColor;

                if (i >= f.jobs.Count)
                {
                    var empty = Rect("Empty", slot, new Vector2(20, -20), new Vector2(100, 100));
                    FarmIcons.Layer(empty, icons.sparkle, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0.2f));
                    continue;
                }

                var job = f.jobs[i];
                var ia = Rect("A", slot, new Vector2(16, -20), new Vector2(100, 100));
                FarmIcons.Draw(ia, icons, job.inputA);
                var ib = Rect("B", slot, new Vector2(116, -20), new Vector2(100, 100));
                FarmIcons.Draw(ib, icons, job.inputB);

                var barBg = Rect("Bar", slot, new Vector2(236, -50), new Vector2(320, 30));
                barBg.gameObject.AddComponent<Image>().color = BarBack;
                var fillRt = Rect("Fill", barBg, Vector2.zero, Vector2.zero);
                fillRt.anchorMin = Vector2.zero;
                fillRt.anchorMax = Vector2.one;
                fillRt.offsetMin = new Vector2(4, 4);
                fillRt.offsetMax = new Vector2(-4, -4);
                var fill = fillRt.gameObject.AddComponent<Image>();
                fill.sprite = buttonSprite;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.color = BarFill;

                var clock = Rect("Clock", slot, new Vector2(236, -88), new Vector2(40, 40));
                FarmIcons.Layer(clock, icons.clock, Vector2.zero, Vector2.one, Color.white);
                var time = MakeText(slot, "Time", new Vector2(284, -86), new Vector2(280, 44), 30, TextAnchor.MiddleLeft);
                _jobViews.Add((fill, time, job));
            }
        }

        void UpdateJobs()
        {
            var now = DragonCare.Clock();
            foreach (var (fill, time, job) in _jobViews)
            {
                if (fill) fill.fillAmount = job.Progress(now);
                if (!time) continue;
                var left = TimeSpan.FromTicks(Math.Max(0, job.readyUtcTicks - now.Ticks));
                time.text = left.TotalSeconds <= 0 ? "" : FormatMinutes((float)left.TotalMinutes);
            }
        }

        void RebuildStorage(DragonState s, FarmState f, FarmSettings settings)
        {
            ClearChildren(_storage);
            bool room = FarmLogic.HasPenRoom(f, settings);
            var seen = new HashSet<string>();
            for (int i = 0; i < s.storage.Count; i++)
            {
                var food = s.storage[i];
                string key = food.id + food.rare;
                if (!seen.Add(key)) continue;
                int count = 0;
                foreach (var x in s.storage) if (x.id == food.id && x.rare == food.rare) count++;

                var btn = SlotButton(_storage, SlotColor, out var rt);
                btn.interactable = room;
                var a = new FarmAnimal { id = food.id, trait = food.trait, rare = food.rare };
                FarmIcons.Draw(rt, icons, a, room ? (Color?)null : new Color(1f, 1f, 1f, 0.35f));
                if (count > 1) Count(rt, count);
                string id = food.id;
                bool rare = food.rare;
                btn.onClick.AddListener(() =>
                {
                    int index = DragonCare.State.storage.FindIndex(x => x.id == id && x.rare == rare);
                    DragonCare.MoveStorageToFarm(index);
                });
            }
        }

        // ───────────── 버튼 ─────────────

        void Toggle(string uid)
        {
            if (_selected.Remove(uid)) return;
            if (_selected.Count >= 2) _selected.RemoveAt(1);
            _selected.Add(uid);
        }

        void OnFeed()
        {
            if (_selected.Count != 1) return;
            if (DragonCare.FeedFarmAnimal(_selected[0])) _selected.Clear();
        }

        void OnCombine()
        {
            if (_selected.Count != 2) return;
            if (DragonCare.StartCombine(_selected[0], _selected[1])) _selected.Clear();
        }

        void OnCollected(List<CombineResult> results)
        {
            if (results == null || results.Count == 0) return;
            _popUntil = Time.unscaledTime + 3f;
            if (!_resultPop) return;
            ClearChildren(_resultPop);
            var r = results[results.Count - 1];
            var bg = _resultPop.GetComponent<Image>();
            if (!bg) bg = _resultPop.gameObject.AddComponent<Image>();
            bg.sprite = buttonSprite;
            bg.type = Image.Type.Sliced;
            bg.color = r.success ? SuccessColor : FailColor;
            var mark = Rect("Mark", _resultPop, new Vector2(4, -4), new Vector2(72, 72));
            FarmIcons.Layer(mark, r.success ? icons.check : icons.boom, Vector2.zero, Vector2.one, Color.white);
            if (r.animal == null) return;
            var who = Rect("Animal", _resultPop, new Vector2(84, -4), new Vector2(72, 72));
            FarmIcons.Draw(who, icons, r.animal);
            if (r.firstDiscovery && icons.star)
            {
                var star = Rect("New", _resultPop, new Vector2(130, 10), new Vector2(40, 40));
                FarmIcons.Layer(star, icons.star, Vector2.zero, Vector2.one, Color.white);
            }
        }

        // ───────────── 조각 ─────────────

        void AnimalSlot(RectTransform parent, FarmAnimal a)
        {
            bool selected = _selected.Contains(a.uid);
            var btn = SlotButton(parent, selected ? SelectedColor : SlotColor, out var rt);
            FarmIcons.Draw(rt, icons, a);
            if (selected) rt.localScale = Vector3.one * 1.06f;
            string uid = a.uid;
            btn.onClick.AddListener(() =>
            {
                Toggle(uid);
                _key = null;
            });
        }

        void EmptySlot(RectTransform parent, bool locked)
        {
            var go = new GameObject(locked ? "Locked" : "Empty", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = buttonSprite;
            img.type = Image.Type.Sliced;
            img.color = locked ? LockedColor : new Color(1f, 1f, 1f, 0.05f);
        }

        Button SlotButton(RectTransform parent, Color color, out RectTransform rt)
        {
            var go = new GameObject("Slot", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            rt = (RectTransform)go.transform;
            var bg = go.AddComponent<Image>();
            bg.sprite = buttonSprite;
            bg.type = Image.Type.Sliced;
            bg.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            return btn;
        }

        void PreviewCell(FarmAnimal a)
        {
            if (a == null) return;
            FarmIcons.Draw(Cell(_preview, 96f), icons, a);
        }

        void PreviewIcon(Sprite sprite, float scale)
        {
            var c = Cell(_preview, 96f * scale);
            FarmIcons.Layer(c, sprite, Vector2.zero, Vector2.one, Color.white);
        }

        static RectTransform Cell(RectTransform parent, float size)
        {
            var go = new GameObject("Cell", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = size;
            le.preferredHeight = le.minHeight = size;
            return (RectTransform)go.transform;
        }

        Button ActionButton(string name, Vector2 pos, Sprite icon, Action onClick)
        {
            var rt = Rect(name, box, pos, new Vector2(200, 150));
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = buttonSprite;
            bg.type = Image.Type.Sliced;
            bg.color = ActionColor;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => onClick());
            var ic = FarmIcons.Layer(rt, icon, new Vector2(0.2f, 0.12f), new Vector2(0.8f, 0.88f), Color.white);
            ic.name = "Icon";
            return btn;
        }

        static void SetIconAlpha(Button b, bool on)
        {
            var ic = b.transform.Find("Icon")?.GetComponent<Image>();
            if (ic) ic.color = new Color(1f, 1f, 1f, on ? 1f : 0.3f);
        }

        void Count(RectTransform parent, int count)
        {
            var t = MakeText(parent, "Count", Vector2.zero, Vector2.zero, 26, TextAnchor.LowerRight);
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1f, 0.35f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            t.text = $"x{count} ";
        }

        IconFlow Flow(string name, Vector2 pos, Vector2 size, TextAnchor align, float iconSize)
        {
            var rt = Rect(name, box, pos, size);
            var flow = rt.gameObject.AddComponent<IconFlow>();
            flow.font = font;
            flow.fontSize = 38;
            flow.iconSize = iconSize;
            flow.alignment = align;
            return flow;
        }

        RectTransform Grid(string name, Vector2 pos, Vector2 size, float cell, int columns)
        {
            var rt = Rect(name, box, pos, size);
            var g = rt.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(cell, cell);
            g.spacing = new Vector2(14, 14);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = columns;
            return rt;
        }

        /// <summary>왼쪽 위 기준 사각형</summary>
        static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        Text MakeText(Transform parent, string name, Vector2 pos, Vector2 size, int fontSize, TextAnchor align)
        {
            var rt = Rect(name, parent, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            var sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.7f);
            return t;
        }

        static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        static string FormatMinutes(float minutes)
        {
            var t = TimeSpan.FromMinutes(Mathf.Max(0f, minutes));
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours}시간 {t.Minutes}분";
            if (t.TotalMinutes >= 1) return $"{(int)Math.Ceiling(t.TotalMinutes)}분";
            return $"{Math.Max(1, (int)Math.Ceiling(t.TotalSeconds))}초";
        }
    }
}
