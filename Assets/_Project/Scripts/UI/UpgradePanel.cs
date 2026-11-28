using System.Collections.Generic;
using DragonRecipe.Care;
using DragonRecipe.Flight;
using DragonRecipe.Food;
using DragonRecipe.Tribes;
using DragonRecipe.Upgrades;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DragonRecipe.UI
{
    /// <summary>
    /// 둥지의 업그레이드 화면. 가운데 심장에서 방사형으로 퍼지는 트리를 그린다 (자리는 UpgradeLayout이 계산).
    /// 화면보다 크므로 끌어서 옮겨 보고, 고른 업그레이드의 값(창고 먹이)과 조건(성장 단계·종족 호감도)은
    /// 그 노드 옆 말풍선 카드에 보여 준다.
    /// 둥지 씬이 열리면 캔버스에 스스로 붙는다 (씬을 다시 만들 필요 없음).
    /// </summary>
    public class UpgradePanel : MonoBehaviour
    {
        const float NodeSize = 96f;
        /// <summary>종족 단계(1~3단) 노드는 조금 크게</summary>
        const float TierNodeSize = 122f;
        const float CardWidth = 600f;
        const float CardHeight = 360f;
        const float ZoomOut = 0.5f;

        static readonly Color Gold = new Color(1f, 0.8f, 0.3f);
        static readonly Color Ready = new Color(0.45f, 0.92f, 0.5f);
        static readonly Color Open = new Color(0.75f, 0.8f, 0.9f);
        static readonly Color GatedRing = new Color(0.36f, 0.39f, 0.47f);
        static readonly Color LockedRing = new Color(0.22f, 0.24f, 0.3f);
        static readonly Color ExcludedRing = new Color(0.55f, 0.22f, 0.22f);
        static readonly Color DormantRing = new Color(0.45f, 0.5f, 0.62f);
        static readonly Color Good = new Color(0.55f, 0.95f, 0.55f);
        static readonly Color Bad = new Color(1f, 0.45f, 0.4f);
        static readonly Color Soft = new Color(0.8f, 0.84f, 0.92f);

        class NodeView
        {
            public UpgradeNode node;
            public RectTransform root;
            public Image ring;
            public Image back;
            public Image icon;
            public Image cross;
            public Image badge;
            public Image select;
            public UpgradeStatus status;
            public bool affordable;
            public float punch;
        }

        class Link
        {
            public Image image;
            public NodeView from;
            public NodeView to;
        }

        Font _font;
        UIIconSet _icons;
        RectTransform _panel;
        Image _openBadge;
        RectTransform _openButton;
        readonly List<NodeView> _nodes = new List<NodeView>();
        readonly List<Link> _links = new List<Link>();
        NodeView _selected;
        RectTransform _content;
        ScrollRect _scroll;
        RectTransform _card;
        UpgradeLayout _layout;
        Text _zoomLabel;
        bool _zoomedOut;

        Image _detailIcon;
        Text _detailName;
        Text _detailDesc;
        IconFlow _detailCost;
        IconFlow _resources;
        Button _buy;
        Image _buyImage;
        Text _buyLabel;
        string _detailKey;
        string _resourceKey;
        float _nextRefresh;
        float _nextBadge;

        static Sprite _circle;

        // ───────── 자동 부착 ─────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

        static void Attach()
        {
            if (FindAnyObjectByType<UpgradePanel>()) return;
            var nest = FindAnyObjectByType<NestUI>();
            if (!nest) return;
            var canvas = nest.GetComponentInParent<Canvas>();
            if (canvas) canvas.gameObject.AddComponent<UpgradePanel>();
        }

        // ───────── 수명 ─────────

        void Start()
        {
            var nest = GetComponentInChildren<NestUI>(true);
            if (!nest) nest = FindAnyObjectByType<NestUI>();
            _icons = nest ? nest.icons : null;
            var anyText = nest && nest.titleText ? nest.titleText : GetComponentInChildren<Text>(true);
            _font = anyText && anyText.font ? anyText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        void OnEnable()
        {
            DragonCare.UpgradesChanged += MarkDirty;
            DragonCare.TribesChanged += MarkDirty;
        }

        void OnDisable()
        {
            DragonCare.UpgradesChanged -= MarkDirty;
            DragonCare.TribesChanged -= MarkDirty;
        }

        void MarkDirty()
        {
            _nextRefresh = 0f;
            _nextBadge = 0f;
        }

        public bool IsOpen => _panel && _panel.gameObject.activeSelf;

        public void Toggle()
        {
            if (!_panel) return;
            bool open = !_panel.gameObject.activeSelf;
            _panel.gameObject.SetActive(open);
            if (!open) return;
            _panel.SetAsLastSibling();
            _detailKey = null;
            _resourceKey = null;
            RefreshNodes();
            // 열 때는 아무것도 고르지 않고 심장을 가운데에 둔다
            Select(null);
            if (_content) _content.anchoredPosition = Vector2.zero;
            RefreshDetail();
        }

        void Update()
        {
            if (!_panel || !DragonCare.IsLoaded) return;
            float now = Time.unscaledTime;

            if (_openBadge && now >= _nextBadge)
            {
                _nextBadge = now + 0.5f;
                bool any = UpgradeLogic.BuyableCount(DragonCare.State, DragonCare.TribeSettings) > 0;
                if (_openBadge.gameObject.activeSelf != any) _openBadge.gameObject.SetActive(any);
            }
            if (_openBadge && _openBadge.gameObject.activeSelf)
                _openBadge.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(now * 5f) * 0.15f);

            if (!IsOpen) return;
            if (now >= _nextRefresh)
            {
                _nextRefresh = now + 0.25f;
                RefreshNodes();
                RefreshDetail();
            }

            float dt = Time.unscaledDeltaTime;
            foreach (var v in _nodes)
            {
                v.punch = Mathf.MoveTowards(v.punch, 0f, dt * 3f);
                float pulse = v.status == UpgradeStatus.Available && v.affordable ? Mathf.Sin(now * 4f) * 0.04f + 0.04f : 0f;
                v.root.localScale = Vector3.one * (1f + pulse + v.punch * 0.45f);
                if (v.punch > 0f) v.ring.color = Color.Lerp(Gold, Color.white, v.punch);
            }
        }

        // ───────── 상태 ─────────

        void RefreshNodes()
        {
            var s = DragonCare.State;
            var ts = DragonCare.TribeSettings;
            foreach (var v in _nodes)
            {
                v.status = UpgradeLogic.StatusOf(v.node, s, ts);
                v.affordable = v.status == UpgradeStatus.Available && UpgradeLogic.CanAfford(v.node, s.storage);
                Paint(v, s, ts);
            }
            foreach (var l in _links)
            {
                bool on = l.from.status == UpgradeStatus.Owned || l.from.status == UpgradeStatus.Dormant;
                bool done = on && (l.to.status == UpgradeStatus.Owned || l.to.status == UpgradeStatus.Dormant);
                l.image.color = done ? new Color(Gold.r, Gold.g, Gold.b, 0.85f)
                    : on ? new Color(0.8f, 0.85f, 0.95f, 0.55f)
                    : new Color(0.5f, 0.55f, 0.65f, 0.22f);
            }
        }

        void Paint(NodeView v, DragonState s, TribeSettings ts)
        {
            Color ring, back = new Color(0.1f, 0.12f, 0.17f, 0.98f);
            float icon = 1f;
            switch (v.status)
            {
                case UpgradeStatus.Owned:
                    ring = Gold;
                    back = new Color(0.24f, 0.19f, 0.08f, 0.98f);
                    break;
                case UpgradeStatus.Dormant:
                    ring = DormantRing;
                    icon = 0.45f;
                    break;
                case UpgradeStatus.Available:
                    ring = v.affordable ? Ready : Open;
                    if (v.affordable) back = new Color(0.1f, 0.2f, 0.14f, 0.98f);
                    break;
                case UpgradeStatus.Gated:
                    ring = GatedRing;
                    icon = 0.45f;
                    break;
                case UpgradeStatus.Excluded:
                    ring = ExcludedRing;
                    icon = 0.2f;
                    break;
                default:
                    ring = LockedRing;
                    icon = 0.25f;
                    break;
            }
            if (v.punch <= 0f) v.ring.color = ring;
            v.back.color = back;
            v.icon.color = new Color(1f, 1f, 1f, icon);
            v.cross.gameObject.SetActive(v.status == UpgradeStatus.Excluded);
            v.select.gameObject.SetActive(v == _selected);
            if (v.badge)
            {
                // 종족 얼굴 배지: 테두리 색 = 지금 그 종족의 태도
                var standing = TribeLogic.StandingOf(s.tribes, v.node.tribe, ts);
                v.badge.color = v.status == UpgradeStatus.Dormant ? new Color(1f, 0.5f, 0.5f, 0.9f) : Color.white;
                var rim = v.badge.transform.parent.GetComponent<Image>();
                if (rim) rim.color = TribeLook.Of(standing);
            }
        }

        void Select(NodeView v)
        {
            _selected = v;
            foreach (var n in _nodes) n.select.gameObject.SetActive(n == v);
            _detailKey = null;
            if (_card)
            {
                _card.gameObject.SetActive(v != null);
                if (v != null) PlaceCard(v);
            }
            RefreshDetail();
        }

        /// <summary>말풍선 카드를 고른 노드 옆에 붙인다 (트리 가운데 쪽으로 열려서 바깥 노드에서도 화면 밖으로 덜 나감)</summary>
        void PlaceCard(NodeView v)
        {
            Vector2 p = v.root.anchoredPosition;
            float scale = 1f / Mathf.Max(0.01f, _content.localScale.x); // 축소해 봐도 글자는 같은 크기
            float side = p.x > 0f ? -1f : 1f;
            float dx = (v.root.sizeDelta.x * 0.5f + 26f) + CardWidth * 0.5f * scale;
            _card.localScale = Vector3.one * scale;
            _card.anchoredPosition = p + new Vector2(side * dx, 0f);
            _card.SetAsLastSibling();
        }

        void ToggleZoom()
        {
            _zoomedOut = !_zoomedOut;
            _content.localScale = Vector3.one * (_zoomedOut ? ZoomOut : 1f);
            if (_zoomLabel) _zoomLabel.text = _zoomedOut ? "+" : "−";
            if (_selected != null) PlaceCard(_selected);
        }

        void OnBuy()
        {
            var v = _selected;
            if (v == null || !DragonCare.BuyUpgrade(v.node.id)) return;
            v.punch = 1f;
            RefreshNodes();
            _detailKey = null;
            RefreshDetail();
        }

        // ───────── 아래 칸 ─────────

        void RefreshDetail()
        {
            var s = DragonCare.State;
            var ts = DragonCare.TribeSettings;

            // 가진 먹이 (값으로 낼 수 있는 것)
            string rk = "";
            foreach (FoodTrait t in System.Enum.GetValues(typeof(FoodTrait))) rk += UpgradeLogic.CountTrait(s.storage, t) + ",";
            rk += UpgradeLogic.CountRare(s.storage);
            if (rk != _resourceKey && _resources && _icons)
            {
                _resourceKey = rk;
                _resources.Clear();
                var row = _resources.AddRow().Icon(_icons.basket, 1.1f).Gap(10f);
                foreach (FoodTrait t in System.Enum.GetValues(typeof(FoodTrait)))
                    row.Icon(_icons.Trait(t)).Text($"{UpgradeLogic.CountTrait(s.storage, t)}").Gap(14f);
                row.Icon(_icons.star).Text($"{UpgradeLogic.CountRare(s.storage)}");
            }

            var v = _selected;
            if (v == null) return;
            var n = v.node;
            string key = $"{n.id}|{v.status}|{v.affordable}|{rk}|{s.stage}";
            if (n.tribeGate) key += "|" + TribeLogic.StandingOf(s.tribes, n.tribe, ts);
            if (key == _detailKey) return;
            _detailKey = key;

            _detailIcon.sprite = IconOf(n);
            _detailName.text = n.name;
            _detailDesc.text = n.desc;

            _detailCost.Clear();
            bool owned = v.status == UpgradeStatus.Owned || v.status == UpgradeStatus.Dormant;
            if (_icons)
            {
                if (!owned && v.status != UpgradeStatus.Excluded)
                {
                    // 값: 성질 아이콘 + 가진 수/낼 수
                    var row = _detailCost.AddRow();
                    foreach (var c in n.costs)
                    {
                        int have = UpgradeLogic.CountTrait(s.storage, c.trait);
                        row.Icon(_icons.Trait(c.trait)).Text($"{have}/{c.count}", have >= c.count ? Good : Bad).Gap(18f);
                    }
                    if (n.rareCost > 0)
                    {
                        int have = UpgradeLogic.CountRare(s.storage);
                        row.Icon(_icons.star).Text($"{have}/{n.rareCost}", have >= n.rareCost ? Good : Bad);
                    }
                }

                // 조건
                if (n.minStage > GrowthStage.Hatchling && !owned)
                    _detailCost.AddRow().Icon(_icons.dragon).Text(CareLogic.StageName(n.minStage), s.stage >= n.minStage ? Good : Bad);
                if (n.tribeGate)
                {
                    var standing = TribeLogic.StandingOf(s.tribes, n.tribe, ts);
                    var row = _detailCost.AddRow().Icon(TribeLook.Face(n.tribe));
                    if (v.status == UpgradeStatus.Dormant)
                        row.Text($"{TribeLook.Name(n.tribe)} · {StandingName(standing)}", Bad).Icon(_icons.no, 0.8f);
                    else
                        row.Text($"{TribeLook.Name(n.tribe)} · {StandingName(n.standing)}", standing >= n.standing ? Good : Bad);
                }
                if (v.status == UpgradeStatus.Locked)
                {
                    var row = _detailCost.AddRow();
                    foreach (var req in n.requires)
                    {
                        if (s.upgrades.Has(req)) continue;
                        var r = UpgradeCatalog.Find(req);
                        if (r != null) row.Icon(IconOf(r), 1f, new Color(1f, 1f, 1f, 0.6f));
                    }
                    row.Icon(_icons.arrowRight, 0.8f).Icon(IconOf(n));
                }
                if (v.status == UpgradeStatus.Excluded) _detailCost.AddRow().Icon(_icons.no).Text("갈림길", Bad);
            }

            bool can = v.status == UpgradeStatus.Available && v.affordable;
            _buy.interactable = can;
            _buy.gameObject.SetActive(!owned && v.status != UpgradeStatus.Excluded);
            _buyImage.color = can ? new Color(0.25f, 0.62f, 0.35f, 1f) : new Color(0.2f, 0.22f, 0.28f, 0.9f);
            _buyLabel.color = can ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        }

        static string StandingName(Standing s) => s switch
        {
            Standing.Hostile => "적대",
            Standing.Wary => "경계",
            Standing.Friendly => "우호",
            Standing.Revered => "숭배",
            _ => "중립"
        };

        Sprite IconOf(UpgradeNode n)
        {
            if (!_icons) return null;
            var f = typeof(UIIconSet).GetField(n.icon);
            var sp = f != null ? f.GetValue(_icons) as Sprite : null;
            return sp ? sp : _icons.star;
        }

        // ───────── 만들기 ─────────

        void Build()
        {
            // 여는 버튼: 도감 · 농장 버튼 옆
            var open = MakeImage(transform, "UpgradeButton", new Vector2(0, 1), new Vector2(390, -590), new Vector2(120, 120), new Color(1f, 1f, 1f, 0.3f));
            _openButton = open.rectTransform;
            open.gameObject.AddComponent<Button>().onClick.AddListener(Toggle);
            var openIcon = MakeImage(open.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(86, 86), Color.white);
            openIcon.sprite = _icons ? _icons.sparkle : null;
            openIcon.raycastTarget = false;
            _openBadge = MakeImage(open.transform, "Badge", new Vector2(1, 1), new Vector2(-8, -8), new Vector2(30, 30), Gold);
            _openBadge.sprite = Circle;
            _openBadge.raycastTarget = false;
            _openBadge.gameObject.SetActive(false);

            // 화면 전체를 덮는 판
            var dim = MakeImage(transform, "UpgradePanel", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.03f, 0.04f, 0.07f, 0.93f));
            _panel = dim.rectTransform;
            _panel.anchorMin = Vector2.zero;
            _panel.anchorMax = Vector2.one;
            _panel.offsetMin = _panel.offsetMax = Vector2.zero;

            var close = MakeImage(_panel, "Close", new Vector2(1, 1), new Vector2(-70, -70), new Vector2(90, 90), new Color(1f, 1f, 1f, 0.2f));
            close.gameObject.AddComponent<Button>().onClick.AddListener(Toggle);
            var closeIcon = MakeImage(close.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64), Color.white);
            closeIcon.sprite = _icons ? _icons.no : null;
            closeIcon.raycastTarget = false;

            _resources = MakeFlow(_panel, "Resources", new Vector2(0, 1), new Vector2(50, -40), new Vector2(760, 70), TextAnchor.MiddleLeft, 46f, 34);
            ((RectTransform)_resources.transform).pivot = new Vector2(0, 1);

            // 축소 / 확대
            var zoom = MakeImage(_panel, "Zoom", new Vector2(1, 1), new Vector2(-180, -70), new Vector2(90, 90), new Color(1f, 1f, 1f, 0.2f));
            zoom.gameObject.AddComponent<Button>().onClick.AddListener(ToggleZoom);
            _zoomLabel = MakeText(zoom.transform, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 90), 60, TextAnchor.MiddleCenter, Color.white);
            _zoomLabel.text = "−";

            // 트리: 화면보다 크다 → 끌어서 옮겨 본다
            _layout = UpgradeLayout.Build(UpgradeCatalog.All);
            var view = MakeImage(_panel, "Scroll", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
            var viewRt = view.rectTransform;
            viewRt.anchorMin = Vector2.zero;
            viewRt.anchorMax = Vector2.one;
            viewRt.offsetMin = viewRt.offsetMax = Vector2.zero;
            viewRt.SetAsFirstSibling(); // 닫기 · 가진 먹이 표시 아래에 깔린다
            view.gameObject.AddComponent<RectMask2D>();

            float side = (_layout.radius + CardWidth + 260f) * 2f;
            var content = MakeImage(viewRt, "Content", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(side, side), new Color(0f, 0f, 0f, 0f));
            _content = content.rectTransform;
            // 빈 곳을 누르면 고른 것을 푼다 (끄는 동작은 ScrollRect가 받는다)
            var clear = content.gameObject.AddComponent<Button>();
            clear.transition = Selectable.Transition.None;
            clear.onClick.AddListener(() => Select(null));

            _scroll = view.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = viewRt;
            _scroll.content = _content;
            _scroll.horizontal = _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Elastic;
            _scroll.scrollSensitivity = 40f;

            // 고리 (깊이 눈금)
            var lines = new GameObject("Links", typeof(RectTransform)).transform;
            lines.SetParent(_content, false);

            // 가운데 심장
            var heart = MakeImage(_content, "Heart", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170, 170), new Color(0.12f, 0.5f, 0.4f, 1f));
            heart.sprite = Circle;
            heart.raycastTarget = false;
            var heartIcon = MakeImage(heart.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 110), Color.white);
            heartIcon.sprite = _icons ? _icons.dragon : null;
            heartIcon.preserveAspect = true;
            heartIcon.raycastTarget = false;

            var byId = new Dictionary<string, NodeView>();
            foreach (var n in UpgradeCatalog.All)
            {
                var v = MakeNode(n);
                _nodes.Add(v);
                byId[n.id] = v;
            }
            foreach (var v in _nodes)
            {
                if (v.node.requires.Length == 0)
                {
                    // 뿌리: 심장에서 이어진다
                    MakeLine(lines, Vector2.zero, v.root.anchoredPosition, 6f).color = new Color(0.8f, 0.85f, 0.95f, 0.45f);
                    continue;
                }
                foreach (var req in v.node.requires)
                    if (byId.TryGetValue(req, out var from))
                        _links.Add(new Link { image = MakeLine(lines, from.root.anchoredPosition, v.root.anchoredPosition, 6f), from = from, to = v });
            }
            // 갈림길 표시: 둘을 붉은 줄로 잇는다
            for (int i = 0; i < _nodes.Count; i++)
                for (int j = i + 1; j < _nodes.Count; j++)
                {
                    var a = _nodes[i].node;
                    var b = _nodes[j].node;
                    if (string.IsNullOrEmpty(a.exclusiveGroup) || a.exclusiveGroup != b.exclusiveGroup) continue;
                    var line = MakeLine(lines, _nodes[i].root.anchoredPosition, _nodes[j].root.anchoredPosition, 4f);
                    line.color = new Color(1f, 0.4f, 0.4f, 0.55f);
                }

            BuildDetail();
            _panel.gameObject.SetActive(false);
        }

        NodeView MakeNode(UpgradeNode n)
        {
            var v = new NodeView { node = n };
            var pos = _layout[n.id];
            float size = n.tier > 0 ? TierNodeSize : NodeSize;
            var go = new GameObject("Node_" + n.id, typeof(RectTransform), typeof(Image), typeof(Button));
            v.root = (RectTransform)go.transform;
            v.root.SetParent(_content, false);
            v.root.anchorMin = v.root.anchorMax = new Vector2(0.5f, 0.5f);
            v.root.anchoredPosition = pos;
            v.root.sizeDelta = new Vector2(size, size);
            // 뿌리 이미지는 누르는 영역만 맡는다 (보이지 않음)
            var hit = go.GetComponent<Image>();
            hit.sprite = Circle;
            hit.color = new Color(1f, 1f, 1f, 0f);
            go.GetComponent<Button>().onClick.AddListener(() => Select(v));

            v.select = Layer(v.root, "Select", 1.22f, new Color(1f, 1f, 1f, 0.9f));
            v.ring = Layer(v.root, "Ring", 1f, Color.white);
            v.back = Layer(v.root, "Back", 0.86f, Color.black);
            v.icon = Layer(v.root, "Icon", 0.6f, Color.white);
            v.icon.sprite = IconOf(n);
            v.cross = Layer(v.root, "Cross", 0.7f, new Color(1f, 0.4f, 0.4f, 0.8f));
            v.cross.sprite = _icons ? _icons.no : null;

            if (n.tribeGate)
            {
                var rim = MakeImage(v.root, "Tribe", new Vector2(1, 1), new Vector2(-6, -6), new Vector2(46, 46), Color.white);
                rim.sprite = Circle;
                rim.raycastTarget = false;
                v.badge = MakeImage(rim.transform, "Face", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34), Color.white);
                v.badge.sprite = TribeLook.Face(n.tribe);
                v.badge.raycastTarget = false;
            }
            if (n.tier > 0)
            {
                // 종족 단계 표시 (1단 · 2단 · 3단)
                var plate = MakeImage(v.root, "Tier", new Vector2(0.5f, 0f), new Vector2(0, 2), new Vector2(74, 36), new Color(0.06f, 0.07f, 0.1f, 0.95f));
                plate.raycastTarget = false;
                MakeText(plate.transform, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(74, 36), 26, TextAnchor.MiddleCenter, Gold).text = $"{n.tier}단";
            }
            return v;
        }

        Image Layer(RectTransform parent, string name, float scale, Color color)
        {
            var img = MakeImage(parent, name, new Vector2(0.5f, 0.5f), Vector2.zero, parent.sizeDelta * scale, color);
            img.sprite = name == "Icon" || name == "Cross" ? null : Circle;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        Image MakeLine(Transform parent, Vector2 a, Vector2 b, float width)
        {
            var d = b - a;
            var img = MakeImage(parent, "Link", new Vector2(0.5f, 0.5f), (a + b) * 0.5f, new Vector2(d.magnitude, width), Color.white);
            img.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            img.raycastTarget = false;
            return img;
        }

        /// <summary>고른 노드 옆에 뜨는 말풍선 카드: 이름 · 효과 · 값과 조건 · 배우기</summary>
        void BuildDetail()
        {
            var box = MakeImage(_content, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight), new Color(0.1f, 0.12f, 0.17f, 0.98f));
            _card = box.rectTransform;
            var rim = MakeImage(box.transform, "Rim", new Vector2(0.5f, 1f), new Vector2(0, -3), new Vector2(CardWidth, 6), Gold);
            rim.raycastTarget = false;
            var bt = box.transform;

            var ring = MakeImage(bt, "IconRing", new Vector2(0, 1), new Vector2(70, -70), new Vector2(96, 96), new Color(1f, 1f, 1f, 0.12f));
            ring.sprite = Circle;
            ring.raycastTarget = false;
            _detailIcon = MakeImage(ring.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64), Color.white);
            _detailIcon.preserveAspect = true;
            _detailIcon.raycastTarget = false;

            _detailName = MakeText(bt, "Name", new Vector2(0, 1), new Vector2(136, -50), new Vector2(440, 56), 42, TextAnchor.MiddleLeft, Color.white);
            _detailDesc = MakeText(bt, "Desc", new Vector2(0, 1), new Vector2(136, -112), new Vector2(440, 76), 30, TextAnchor.UpperLeft, Soft);

            _detailCost = MakeFlow(bt, "Cost", new Vector2(0, 0), new Vector2(30, 100), new Vector2(350, 180), TextAnchor.MiddleLeft, 42f, 30);
            ((RectTransform)_detailCost.transform).pivot = new Vector2(0, 0.5f);

            var buy = MakeImage(bt, "Buy", new Vector2(1, 0), new Vector2(-115, 70), new Vector2(190, 96), Color.gray);
            _buyImage = buy;
            _buy = buy.gameObject.AddComponent<Button>();
            _buy.onClick.AddListener(OnBuy);
            _buyLabel = MakeText(buy.transform, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190, 96), 38, TextAnchor.MiddleCenter, Color.white);
            _buyLabel.text = "배우기";
            box.gameObject.SetActive(false);
        }

        IconFlow MakeFlow(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, TextAnchor align, float iconSize, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = align;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.spacing = 10f;
            var flow = go.AddComponent<IconFlow>();
            flow.font = _font;
            flow.alignment = align;
            flow.iconSize = iconSize;
            flow.fontSize = fontSize;
            flow.rowSpacing = 10f;
            return flow;
        }

        Text MakeText(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(anchor.x, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static Image MakeImage(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        static Sprite Circle
        {
            get
            {
                if (_circle) return _circle;
                const int n = 96;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "UpgradeCircle", wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                float r = n * 0.5f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                        byte a = (byte)(Mathf.Clamp01(r - d) * 255f);
                        px[y * n + x] = new Color32(255, 255, 255, a);
                    }
                tex.SetPixels32(px);
                tex.Apply();
                _circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return _circle;
            }
        }
    }
}
