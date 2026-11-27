using System.Collections.Generic;
using DragonRecipe.Care;
using DragonRecipe.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DragonRecipe.Tribes
{
    /// <summary>
    /// 종족 호감도 UI (코드로 만든다, 씬에 스스로 붙음).
    /// - 종족마다 얼굴 + 호감도 게이지 (색 = 단계, 가운데 눈금 = 중립). 바뀌면 게이지가 번쩍이고 ▲▼가 잠깐 뜬다.
    /// - 둥지: 제단에 쌓인 제물 아이콘, 전쟁 카드 (두 얼굴, 남은 비행 점, 참전 / 거절 버튼, 승률 게이지)
    /// - 비행: 오른쪽 위에 작게 게이지만
    /// 안내 문구 없이 아이콘·색·게이지로만 보여 준다.
    /// </summary>
    public class TribePanel : MonoBehaviour
    {
        class Gauge
        {
            public Tribe tribe;
            public Image face;
            public Image fill;
            public Image flash;
            public Text delta;
            public float shownRep;
            public float flashT;
            public float deltaT;
        }

        static readonly Color PanelBg = new Color(0.08f, 0.1f, 0.14f, 0.75f);
        static readonly Color Up = new Color(0.45f, 1f, 0.45f);
        static readonly Color Down = new Color(1f, 0.4f, 0.35f);
        static readonly Color Gold = new Color(1f, 0.84f, 0.25f);

        bool _compact;
        Font _font;
        RectTransform _root;
        readonly List<Gauge> _gauges = new List<Gauge>();
        RectTransform _altar;
        RectTransform _war;
        string _altarKey, _warKey;

        // 비행 중 방어전 표시: 편든 쪽 얼굴 + 방어 칸, 적 얼굴 + 남은 침입자·무리
        RectTransform _defense;
        Image _defAlly, _defEnemy;
        Text _defPips, _defEnemyCount;
        int _defShownLeft = -1;
        float _defShake;

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
            if (FindAnyObjectByType<TribePanel>()) return;
            bool flight = FindAnyObjectByType<FlightSession>();
            bool nest = FindAnyObjectByType<DragonRecipe.UI.NestUI>();
            if (!flight && !nest) return;
            Canvas canvas = null;
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace) { canvas = c; break; }
            if (!canvas) return;
            var go = new GameObject("TribePanel", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var p = go.AddComponent<TribePanel>();
            p._compact = flight;
        }

        // ───────── 만들기 ─────────

        void Start()
        {
            var anyText = GetComponentInParent<Canvas>().GetComponentInChildren<Text>(true);
            _font = anyText ? anyText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _root = (RectTransform)transform;
            if (_compact)
            {
                Place(_root, new Vector2(1, 1), new Vector2(-40, -195), new Vector2(390, 120), new Vector2(1, 1));
                BuildDefense();
            }
            else
            {
                Place(_root, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(600, 330), new Vector2(0.5f, 1));
                Img(_root, "Bg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, PanelBg);
            }

            float col = _compact ? 125f : 190f;
            foreach (var t in TribeLogic.All)
            {
                float x = ((int)t - 1) * col;
                _gauges.Add(MakeGauge(t, x));
            }
            if (!_compact)
            {
                _altar = Box(_root, "Altar", new Vector2(0.5f, 1), new Vector2(0, -205), new Vector2(560, 60));
                var h = _altar.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.childAlignment = TextAnchor.MiddleCenter;
                h.spacing = 6f;
                h.childControlWidth = h.childControlHeight = false;
                _war = Box(_root, "War", new Vector2(0.5f, 0), new Vector2(0, -12), new Vector2(600, 200));
                _war.pivot = new Vector2(0.5f, 1);

                DragonCare.CollectOfferings(); // 창고에 자리가 생겼으면 제단의 제물을 옮긴다
                ShowLastReport();
            }
            DragonCare.TribesChanged += OnTribesChanged;
            foreach (var g in _gauges) g.shownRep = Rep(g.tribe);
        }

        void OnDestroy() => DragonCare.TribesChanged -= OnTribesChanged;

        void BuildDefense()
        {
            _defense = Box(_root, "Defense", new Vector2(1, 0), new Vector2(0, -12), new Vector2(390, 120));
            _defense.pivot = new Vector2(1, 1);
            Img(_defense, "Bg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.25f, 0.06f, 0.06f, 0.7f));

            _defAlly = Img(_defense, "Ally", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -32), new Vector2(52, 52), Color.white);
            _defPips = Text(_defense, "", Vector2.zero, 30, TribeLook.FriendlyColor);
            Place(_defPips.rectTransform, new Vector2(0, 1), new Vector2(78, -32), new Vector2(300, 50), new Vector2(0, 0.5f));
            _defPips.alignment = TextAnchor.MiddleLeft;

            _defEnemy = Img(_defense, "Enemy", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -88), new Vector2(52, 52), Color.white);
            _defEnemyCount = Text(_defense, "", Vector2.zero, 30, Down);
            Place(_defEnemyCount.rectTransform, new Vector2(0, 1), new Vector2(78, -88), new Vector2(300, 50), new Vector2(0, 0.5f));
            _defEnemyCount.alignment = TextAnchor.MiddleLeft;
            _defense.gameObject.SetActive(false);
        }

        void RefreshDefense(TribeState st)
        {
            if (!_defense) return;
            var world = TribeWorld.Instance;
            bool on = world && world.DefenseActive;
            if (_defense.gameObject.activeSelf != on) _defense.gameObject.SetActive(on);
            if (!on) return;

            var war = st.war;
            _defAlly.sprite = TribeLook.Face(war.joined);
            _defEnemy.sprite = TribeLook.Face(war.Enemy(war.joined));
            int left = world.DefenseLeft;
            if (_defShownLeft >= 0 && left < _defShownLeft) _defShake = 1f; // 뚫리면 흔들림
            _defShownLeft = left;
            _defPips.text = new string('●', left) + new string('○', Mathf.Max(0, world.DefenseMax - left));
            _defPips.color = left <= 1 ? Down : TribeLook.FriendlyColor;
            // 남은 침입자 수 + 아직 오지 않은 무리 점
            _defEnemyCount.text = $"x{world.RaidersAlive}  {new string('▲', world.WavesLeft)}";

            if (_defShake > 0f)
            {
                _defShake = Mathf.Max(0f, _defShake - Time.unscaledDeltaTime * 2.5f);
                _defAlly.rectTransform.anchoredPosition = new Vector2(40, -32) + Random.insideUnitCircle * 10f * _defShake;
                _defAlly.color = Color.Lerp(Color.white, Down, _defShake);
            }
            else
            {
                _defAlly.rectTransform.anchoredPosition = new Vector2(40, -32);
                _defAlly.color = Color.white;
            }
        }

        Gauge MakeGauge(Tribe t, float x)
        {
            float faceSize = _compact ? 56f : 96f;
            float barW = _compact ? 110f : 170f;
            float barH = _compact ? 14f : 22f;
            float top = _compact ? -6f : -14f;

            var g = new Gauge { tribe = t };
            var cell = Box(_root, $"Tribe_{t}", new Vector2(0.5f, 1), new Vector2(x, top), new Vector2(barW, faceSize + barH + 50f));
            cell.pivot = new Vector2(0.5f, 1);

            g.face = Img(cell, "Face", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(faceSize, faceSize), Color.white);
            g.face.sprite = TribeLook.Face(t);
            g.face.rectTransform.pivot = new Vector2(0.5f, 1);

            var bar = Img(cell, "Bar", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -faceSize - 8f), new Vector2(barW, barH), new Color(0, 0, 0, 0.5f));
            bar.rectTransform.pivot = new Vector2(0.5f, 1);
            g.fill = Img(bar.rectTransform, "Fill", Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2), TribeLook.NeutralColor);
            g.fill.type = Image.Type.Filled;
            g.fill.fillMethod = Image.FillMethod.Horizontal;
            g.fill.sprite = WhiteSprite();
            // 가운데(중립) 눈금
            Img(bar.rectTransform, "Mid", new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(-1.5f, -3), new Vector2(1.5f, 3), new Color(1, 1, 1, 0.8f));
            g.flash = Img(bar.rectTransform, "Flash", Vector2.zero, Vector2.one, new Vector2(-4, -4), new Vector2(4, 4), new Color(1, 1, 1, 0));

            var dt = new GameObject("Delta", typeof(RectTransform)).AddComponent<Text>();
            dt.transform.SetParent(cell, false);
            Place(dt.rectTransform, new Vector2(1, 1), new Vector2(_compact ? 4f : 10f, 0), new Vector2(80, 50), new Vector2(0, 1));
            dt.font = _font;
            dt.fontSize = _compact ? 26 : 36;
            dt.alignment = TextAnchor.UpperLeft;
            dt.raycastTarget = false;
            dt.text = "";
            dt.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);
            g.delta = dt;
            return g;
        }

        // ───────── 갱신 ─────────

        static float Rep(Tribe t) => DragonCare.IsLoaded ? TribeLogic.Reputation(DragonCare.Tribes, t) : 0f;

        void OnTribesChanged()
        {
            foreach (var g in _gauges)
            {
                float rep = Rep(g.tribe);
                float d = rep - g.shownRep;
                if (Mathf.Abs(d) < 0.01f) continue;
                Pulse(g, d);
                g.shownRep = rep;
            }
        }

        void Pulse(Gauge g, float d)
        {
            g.flashT = 1f;
            g.deltaT = 3f;
            g.delta.text = d > 0f ? "▲" : "▼";
            g.delta.color = d > 0f ? Up : Down;
        }

        /// <summary>둥지에 돌아왔을 때 지난 비행 동안 바뀐 호감도를 ▲▼로</summary>
        void ShowLastReport()
        {
            var r = DragonCare.LastTribeReport;
            if (r == null) return;
            foreach (var g in _gauges)
            {
                float d = r.Delta(g.tribe);
                if (Mathf.Abs(d) >= 0.5f) Pulse(g, d);
            }
        }

        void Update()
        {
            if (!DragonCare.IsLoaded) return;
            var st = DragonCare.Tribes;
            var s = DragonCare.TribeSettings;
            float dt = Time.unscaledDeltaTime;

            foreach (var g in _gauges)
            {
                float rep = TribeLogic.Reputation(st, g.tribe);
                var standing = TribeLogic.StandingOf(rep, s);
                g.fill.fillAmount = TribeLogic.Normalized(rep, s);
                g.fill.color = TribeLook.Of(standing);
                // 숭배: 얼굴이 금빛으로 은은하게, 적대: 붉게
                g.face.color = standing == Standing.Revered ? Color.Lerp(Color.white, Gold, 0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f))
                    : standing == Standing.Hostile ? new Color(1f, 0.7f, 0.7f) : Color.white;
                if (g.flashT > 0f)
                {
                    g.flashT = Mathf.Max(0f, g.flashT - dt * 2.5f);
                    g.flash.color = new Color(1, 1, 1, 0.8f * g.flashT * g.flashT);
                    g.face.rectTransform.localScale = Vector3.one * (1f + 0.15f * g.flashT);
                }
                if (g.deltaT > 0f)
                {
                    g.deltaT -= dt;
                    var c = g.delta.color;
                    c.a = Mathf.Clamp01(g.deltaT);
                    g.delta.color = c;
                }
            }

            if (_compact)
            {
                RefreshDefense(st);
                return;
            }
            RefreshAltar(st);
            RefreshWar(st, s);
        }

        void RefreshAltar(TribeState st)
        {
            string key = "";
            foreach (var o in st.altar) key += $"{o.foodId}{(o.rare ? "*" : "")},";
            if (key == _altarKey) return;
            _altarKey = key;
            Clear(_altar);
            foreach (var o in st.altar)
            {
                var food = DragonCare.FindFood(o.foodId);
                var icon = new GameObject("Offering", typeof(RectTransform)).AddComponent<Image>();
                icon.transform.SetParent(_altar, false);
                icon.rectTransform.sizeDelta = new Vector2(52, 52);
                icon.sprite = food ? food.icon : null;
                icon.color = o.rare ? Gold : Color.white;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }
        }

        void RefreshWar(TribeState st, TribeSettings s)
        {
            var war = st.war;
            var r = DragonCare.LastTribeReport;
            string key = $"{war.status}{war.sideA}{war.sideB}{war.flightsLeft}{war.joined}{war.contribution}|{r?.warEnded}{r?.warWinner}{r?.warWon}";
            if (key == _warKey) return;
            _warKey = key;
            Clear(_war);

            if (!war.Active)
            {
                // 방금 끝난 전쟁 결과: 이긴 쪽 얼굴이 크게, 진 쪽은 어둡게
                if (r != null && r.warEnded)
                {
                    var bg = Img(_war, "Bg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, PanelBg);
                    bg.raycastTarget = false;
                    var win = Face(_war, r.warWinner, new Vector2(-90, -30), 110f, Color.white);
                    Face(_war, r.warLoser, new Vector2(90, -55), 70f, new Color(0.35f, 0.35f, 0.35f));
                    if (r.warJoined)
                        Img(win.rectTransform, "Result", new Vector2(1, 1), new Vector2(1, 1), new Vector2(10, 10), new Vector2(46, 46),
                            r.warWon ? Gold : Down).sprite = WhiteSprite();
                }
                return;
            }

            var panel = Img(_war, "Bg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.25f, 0.06f, 0.06f, 0.8f));
            panel.raycastTarget = false;
            // 칼끝을 맞댄 두 얼굴
            var a = Face(_war, war.sideA, new Vector2(-150, -20), 90f, Color.white);
            var b = Face(_war, war.sideB, new Vector2(150, -20), 90f, Color.white);
            Swords(_war, new Vector2(0, -60));

            // 남은 비행 횟수 점
            var dots = Text(_war, new string('●', Mathf.Max(0, war.flightsLeft)), new Vector2(0, -130), 30, Color.white);
            dots.alignment = TextAnchor.MiddleCenter;

            switch (war.status)
            {
                case WarStatus.Requested:
                    if (war.requestA) JoinButton(war.sideA, new Vector2(-150, -150));
                    if (war.requestB) JoinButton(war.sideB, new Vector2(150, -150));
                    Button(_war, "거절", new Vector2(0, -165), new Vector2(130, 56), new Color(0.3f, 0.3f, 0.3f, 0.9f), () => DragonCare.DeclineWar());
                    break;
                case WarStatus.Joined:
                    // 편든 쪽 얼굴에 금테, 승률 게이지
                    Outline(war.joined == war.sideA ? a : b);
                    var bar = Img(_war, "WinBar", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(300, 18), new Color(0, 0, 0, 0.5f));
                    var fill = Img(bar.rectTransform, "Fill", Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2), Gold);
                    fill.sprite = WhiteSprite();
                    fill.type = Image.Type.Filled;
                    fill.fillMethod = Image.FillMethod.Horizontal;
                    fill.fillAmount = TribeLogic.WinChance(war, s);
                    break;
                case WarStatus.Declined:
                case WarStatus.Bystander:
                    a.color = b.color = new Color(0.8f, 0.8f, 0.8f);
                    break;
            }
        }

        void JoinButton(Tribe side, Vector2 pos)
        {
            var btn = Button(_war, "참전", pos, new Vector2(150, 60), new Color(0.75f, 0.25f, 0.15f, 0.95f), () => DragonCare.JoinWar(side));
            // 버튼 안에 그 종족 얼굴
            var f = Img((RectTransform)btn.transform, "Face", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(46, 46), Color.white);
            f.sprite = TribeLook.Face(side);
            f.raycastTarget = false;
        }

        // ───────── UI 조각 ─────────

        Image Face(RectTransform parent, Tribe t, Vector2 pos, float size, Color tint)
        {
            var img = Img(parent, $"Face_{t}", new Vector2(0.5f, 1), new Vector2(0.5f, 1), pos, new Vector2(size, size), tint);
            img.sprite = TribeLook.Face(t);
            img.rectTransform.pivot = new Vector2(0.5f, 1);
            img.raycastTarget = false;
            return img;
        }

        void Outline(Image face)
        {
            var o = face.gameObject.AddComponent<UnityEngine.UI.Outline>();
            o.effectColor = Gold;
            o.effectDistance = new Vector2(5, -5);
        }

        void Swords(RectTransform parent, Vector2 pos)
        {
            for (int i = -1; i <= 1; i += 2)
            {
                var s = Img(parent, "Sword", new Vector2(0.5f, 1), new Vector2(0.5f, 1), pos, new Vector2(12, 90), new Color(0.85f, 0.87f, 0.9f));
                s.sprite = WhiteSprite();
                s.raycastTarget = false;
                s.rectTransform.localRotation = Quaternion.Euler(0, 0, 35f * i);
            }
        }

        Text Text(RectTransform parent, string text, Vector2 pos, int size, Color color)
        {
            var t = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
            t.transform.SetParent(parent, false);
            Place(t.rectTransform, new Vector2(0.5f, 1), pos, new Vector2(400, 40), new Vector2(0.5f, 0.5f));
            t.font = _font;
            t.fontSize = size;
            t.color = color;
            t.text = text;
            t.raycastTarget = false;
            return t;
        }

        Button Button(RectTransform parent, string label, Vector2 pos, Vector2 size, Color color, System.Action onClick)
        {
            var img = Img(parent, $"Button_{label}", new Vector2(0.5f, 1), new Vector2(0.5f, 1), pos, size, color);
            img.sprite = WhiteSprite();
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            var t = Text((RectTransform)img.transform, label, Vector2.zero, 30, Color.white);
            t.alignment = TextAnchor.MiddleCenter;
            Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(label == "참전" ? 18f : 0f, 0), size, new Vector2(0.5f, 0.5f));
            return btn;
        }

        static void Clear(RectTransform r)
        {
            for (int i = r.childCount - 1; i >= 0; i--)
            {
                var c = r.GetChild(i).gameObject;
                c.SetActive(false);
                Destroy(c);
            }
        }

        static RectTransform Box(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            Place(rt, anchor, pos, size, new Vector2(0.5f, 0.5f));
            return rt;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        /// <summary>앵커 사이를 채우는 이미지 (anchorMin != anchorMax면 offsetMin/Max로, 같으면 위치·크기로)</summary>
        static Image Img(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 a, Vector2 b, Color color)
        {
            var img = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            img.transform.SetParent(parent, false);
            var rt = img.rectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            if (anchorMin == anchorMax)
            {
                rt.anchoredPosition = a;
                rt.sizeDelta = b;
            }
            else
            {
                rt.offsetMin = a;
                rt.offsetMax = b;
            }
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Sprite _white;

        static Sprite WhiteSprite()
        {
            if (_white) return _white;
            var tex = Texture2D.whiteTexture;
            _white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return _white;
        }
    }
}
