using System;
using System.Collections.Generic;
using DragonRecipe.Care;
using DragonRecipe.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DragonRecipe.Tribes
{
    /// <summary>
    /// [개발자 전용] 종족·전쟁 디버그 패널 (에디터 / 개발 빌드에서만).
    /// 호감도 단계를 바로 바꾸고, 전쟁을 일으키고, 비행 없이 진행·승패를 낼 수 있다.
    /// 왼쪽 아래 WAR 버튼이나 F3으로 연다. 둥지·비행 씬 캔버스에 스스로 붙는다.
    /// </summary>
    public class TribeDebugPanel : MonoBehaviour
    {
        static readonly Color BtnIdle = new Color(0.22f, 0.25f, 0.32f, 0.95f);
        static readonly Color BtnOn = new Color(1f, 0.72f, 0.3f, 1f);
        static readonly Color WarBtn = new Color(0.6f, 0.18f, 0.12f, 0.95f);

        /// <summary>단계 버튼이 넣는 호감도 (각 단계의 가운데쯤)</summary>
        static readonly (Standing standing, string label, float rep)[] Tiers =
        {
            (Standing.Hostile, "적대", -80f),
            (Standing.Wary, "경계", -40f),
            (Standing.Neutral, "중립", 0f),
            (Standing.Friendly, "우호", 40f),
            (Standing.Revered, "숭배", 80f)
        };

        static readonly (Tribe a, Tribe b, string label)[] Pairs =
        {
            (Tribe.Human, Tribe.Orc, "사람 vs 오크"),
            (Tribe.Human, Tribe.Elf, "사람 vs 엘프"),
            (Tribe.Orc, Tribe.Elf, "오크 vs 엘프")
        };

        Font _font;
        RectTransform _panel;
        Text _status;
        Image _openImage;
        readonly List<(Tribe tribe, Standing standing, Image img)> _tierButtons = new List<(Tribe, Standing, Image)>();
        InputAction _toggle;

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
            if (!(Application.isEditor || Debug.isDebugBuild)) return;
            if (FindAnyObjectByType<TribeDebugPanel>()) return;
            if (!FindAnyObjectByType<FlightSession>() && !FindAnyObjectByType<DragonRecipe.UI.NestUI>()) return;
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas || c.renderMode == RenderMode.WorldSpace) continue;
                c.gameObject.AddComponent<TribeDebugPanel>();
                return;
            }
        }

        // ───────── 수명 ─────────

        void Awake()
        {
            _toggle = new InputAction("TribeDebug", InputActionType.Button, "<Keyboard>/f3");
            _toggle.performed += _ => Toggle();
        }

        void OnEnable() => _toggle.Enable();
        void OnDisable() => _toggle.Disable();
        void OnDestroy() => _toggle.Dispose();

        void Start()
        {
            var anyText = GetComponentInChildren<Text>(true);
            _font = anyText && anyText.font ? anyText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        void Update()
        {
            if (_panel && _panel.gameObject.activeSelf) Refresh();
        }

        // ───────── 만들기 ─────────

        void Build()
        {
            // 여는 버튼 (왼쪽 아래, DEV 버튼 옆)
            var open = MakeButton(transform, "WarDebugButton", "WAR", new Vector2(0f, 0f), new Vector2(230f, 34f), new Vector2(130f, 52f), 26, Toggle);
            _openImage = open.GetComponent<Image>();
            _openImage.color = new Color(1f, 0.5f, 0.4f, 0.3f);

            var go = new GameObject("TribeDebugPanel", typeof(RectTransform), typeof(Image));
            _panel = (RectTransform)go.transform;
            _panel.SetParent(transform, false);
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(1100f, 800f);
            go.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.1f, 0.95f);

            MakeLabel(_panel, "개발자 모드: 종족·전쟁", new Vector2(0f, 360f), new Vector2(1000f, 56f), 36);

            // 종족별 호감도 단계
            const float w = 150f, h = 62f, gap = 14f;
            float y = 280f;
            foreach (var t in TribeLogic.All)
            {
                var face = new GameObject("Face", typeof(RectTransform)).AddComponent<Image>();
                face.transform.SetParent(_panel, false);
                face.rectTransform.anchoredPosition = new Vector2(-470f, y);
                face.rectTransform.sizeDelta = new Vector2(62f, 62f);
                face.sprite = TribeLook.Face(t);
                face.raycastTarget = false;
                for (int i = 0; i < Tiers.Length; i++)
                {
                    var tier = Tiers[i];
                    var tribe = t;
                    var pos = new Vector2(-330f + i * (w + gap), y);
                    var b = MakeButton(_panel, "Tier", tier.label, new Vector2(0.5f, 0.5f), pos, new Vector2(w, h), 28, () => SetTier(tribe, tier.rep));
                    _tierButtons.Add((t, tier.standing, b.GetComponent<Image>()));
                }
                y -= h + 14f;
            }

            // 전쟁 일으키기
            y -= 20f;
            for (int i = 0; i < Pairs.Length; i++)
            {
                var pair = Pairs[i];
                MakeButton(_panel, "StartWar", $"전쟁: {pair.label}", new Vector2(0.5f, 0.5f), new Vector2(-350f + i * 350f, y), new Vector2(330f, 66f), 28,
                    () => StartWar(pair.a, pair.b)).GetComponent<Image>().color = WarBtn;
            }

            // 전쟁 진행
            y -= 86f;
            var controls = new (string label, Action act)[]
            {
                ("비행 1회 진행", () => Advance(null)),
                ("즉시 승리", () => ResolveNow(true)),
                ("즉시 패배", () => ResolveNow(false)),
                ("적 편 공격", AttackEnemy),
                ("적 무리 부르기", () => { if (TribeWorld.Instance) TribeWorld.Instance.DebugCallWave(); }),
                ("전쟁 취소", CancelWar)
            };
            for (int i = 0; i < controls.Length; i++)
            {
                var c = controls[i];
                MakeButton(_panel, "WarControl", c.label, new Vector2(0.5f, 0.5f), new Vector2(-450f + i * 180f, y), new Vector2(170f, 66f), 24, c.act);
            }

            // 제단
            y -= 86f;
            MakeButton(_panel, "Offerings", "제물 받기 (숭배처럼)", new Vector2(0.5f, 0.5f), new Vector2(-260f, y), new Vector2(360f, 66f), 26, ForceOfferings);
            MakeButton(_panel, "ResetTribes", "종족 초기화", new Vector2(0.5f, 0.5f), new Vector2(140f, y), new Vector2(260f, 66f), 26, ResetTribes);
            MakeButton(_panel, "Close", "닫기", new Vector2(0.5f, 0.5f), new Vector2(420f, y), new Vector2(200f, 66f), 26, Toggle);

            _status = MakeLabel(_panel, "", new Vector2(0f, -340f), new Vector2(1040f, 90f), 26);
            _status.alignment = TextAnchor.MiddleCenter;

            _panel.gameObject.SetActive(false);
        }

        void Toggle()
        {
            if (!_panel) return;
            _panel.gameObject.SetActive(!_panel.gameObject.activeSelf);
            _panel.SetAsLastSibling();
            Refresh();
        }

        // ───────── 동작 ─────────

        static TribeState State => DragonCare.IsLoaded ? DragonCare.Tribes : null;
        static TribeSettings Settings => DragonCare.TribeSettings;

        static void Changed()
        {
            DragonCare.DebugTribesEdited();
            if (TribeWorld.Instance) TribeWorld.Instance.Repopulate(); // 비행 중이면 병사·전쟁터를 바로 다시 세운다
        }

        static void SetTier(Tribe t, float rep)
        {
            var st = State;
            var r = st?.Get(t);
            if (r == null) return;
            r.reputation = rep;
            Changed();
        }

        /// <summary>지금 전쟁을 지우고 새 전쟁을 일으킨다. 우호 이상인 편이 참전을 요청한다</summary>
        static void StartWar(Tribe a, Tribe b)
        {
            var st = State;
            if (st == null) return;
            st.war = new WarState();
            TribeLogic.StartWar(st, Settings, (a, b));
            Changed();
        }

        static void Advance(Func<float> rand)
        {
            if (State == null) return;
            DragonCare.DebugTribeAfterFlight(rand);
            if (TribeWorld.Instance) TribeWorld.Instance.Repopulate();
        }

        /// <summary>남은 비행을 1로 줄이고 바로 승패를 낸다 (참전 중이면 이기거나 지게, 아니면 무작위)</summary>
        static void ResolveNow(bool win)
        {
            var st = State;
            if (st == null || !st.war.Active) return;
            st.war.flightsLeft = 1;
            // rand가 0이면 승률 판정에 이기고 제물도 다 오며, 0.999면 진다
            Advance(win ? () => 0f : () => 0.999f);
        }

        static void AttackEnemy()
        {
            var st = State;
            if (st == null || st.war.status != WarStatus.Joined) return;
            DragonCare.ReportTribeAttack(st.war.Enemy(st.war.joined));
            Changed();
        }

        static void CancelWar()
        {
            var st = State;
            if (st == null) return;
            st.war = new WarState();
            Changed();
        }

        /// <summary>모든 종족이 숭배일 때처럼 제물을 한 번 받는다 (호감도는 그대로)</summary>
        static void ForceOfferings()
        {
            var st = State;
            if (st == null) return;
            var keep = new float[3];
            foreach (var r in st.tribes) keep[(int)r.tribe] = r.reputation;
            var war = st.war;
            st.war = new WarState { cooldown = 99 }; // 전쟁이 끼어들지 않게
            foreach (var r in st.tribes) r.reputation = 100f;
            DragonCare.DebugTribeAfterFlight(() => 0f);
            foreach (var r in st.tribes) r.reputation = keep[(int)r.tribe];
            st.war = war;
            Changed();
        }

        static void ResetTribes()
        {
            if (!DragonCare.IsLoaded) return;
            DragonCare.State.tribes = TribeLogic.CreateNew(Settings);
            Changed();
        }

        // ───────── 표시 ─────────

        void Refresh()
        {
            var st = State;
            if (st == null || !_status) return;
            foreach (var (tribe, standing, img) in _tierButtons)
                img.color = TribeLogic.StandingOf(st, tribe, Settings) == standing ? BtnOn : BtnIdle;

            var w = st.war;
            string rep = "";
            foreach (var t in TribeLogic.All) rep += $"{TribeLook.Name(t)} {TribeLogic.Reputation(st, t):0}   ";
            string war = !w.Active
                ? $"전쟁 없음 (쿨다운 {w.cooldown})"
                : $"{TribeLook.Name(w.sideA)}{(w.requestA ? "(요청)" : "")} vs {TribeLook.Name(w.sideB)}{(w.requestB ? "(요청)" : "")} · {w.status}" +
                  (w.status == WarStatus.Joined ? $" {TribeLook.Name(w.joined)} 편 · 승률 {TribeLogic.WinChance(w, Settings) * 100f:0}%" : "") +
                  $" · 남은 비행 {w.flightsLeft}";
            _status.text = $"{rep}\n{war} · 제단 {st.altar.Count} · 승전 축복 {st.victoryFlightsLeft}";
        }

        // ───────── UI 조각 ─────────

        Button MakeButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = BtnIdle;
            go.GetComponent<Button>().onClick.AddListener(() => onClick());
            var t = MakeLabel(rt, label, Vector2.zero, size, fontSize);
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.sizeDelta = Vector2.zero;
            return go.GetComponent<Button>();
        }

        Text MakeLabel(Transform parent, string text, Vector2 pos, Vector2 size, int fontSize)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.fontSize = fontSize;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.text = text;
            t.raycastTarget = false;
            return t;
        }
    }
}
