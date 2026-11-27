using System.Collections.Generic;
using DragonRecipe.Care;
using DragonRecipe.Core;
using DragonRecipe.Food;
using DragonRecipe.UI;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DragonRecipe.Skills
{
    /// <summary>
    /// 비행 HUD의 스킬 버튼 하나 (구체 칸 또는 브레스 칸): 속성 아이콘 + 원형 충전 게이지 + 충전 점.
    /// 화면 버튼(구체 = 패드 RB, 브레스 = 패드 LB)도 겸한다. 새 스킬이 생기면 반짝임 연출만 보여 준다 (글자 없음).
    /// 씬에 없으면 비행 HUD 캔버스에 스스로 만든다.
    /// </summary>
    public class SkillHUD : MonoBehaviour
    {
        const string SeenKey = "DragonRecipe.SkillSeen";

        [Tooltip("이 버튼이 보여 주는 칸")]
        public SkillKind slot = SkillKind.Orb;
        public DragonSkills skills;
        public UIIconSet icons;
        [Tooltip("캔버스 오른쪽 아래 기준 위치")]
        public Vector2 position = new Vector2(-470f, 410f);
        public float size = 170f;

        RectTransform _root;
        Image _bg, _ring, _icon, _badge, _shade;
        readonly List<Image> _pips = new List<Image>();
        readonly List<RectTransform> _sparkles = new List<RectTransform>();
        float _pop;
        float _unlockAt = -99f;
        SkillSpec _shown;

        static Sprite _circle;
        static readonly Color Dim = new Color(0f, 0f, 0f, 0.6f);

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
            if (FindAnyObjectByType<SkillHUD>()) return;
            var hud = FindAnyObjectByType<FlightHUD>();
            if (!hud) return;
            hud.gameObject.AddComponent<SkillHUD>().slot = SkillKind.Orb;
            var breath = hud.gameObject.AddComponent<SkillHUD>();
            breath.slot = SkillKind.Breath;
            breath.position = new Vector2(-690f, 350f); // 구체 버튼 왼쪽 아래
        }

        // ───────── 수명 ─────────

        void Start()
        {
            if (!skills) skills = FindAnyObjectByType<DragonSkills>();
            if (!icons)
            {
                var buff = FindAnyObjectByType<BuffHUD>();
                if (buff) icons = buff.icons;
            }
            Build();
            if (skills) skills.Used += OnUsed;
        }

        void OnDestroy()
        {
            if (skills) skills.Used -= OnUsed;
        }

        SkillSpec Spec => !skills ? null : slot == SkillKind.Breath ? skills.Breath : skills.Orb;

        void OnUsed(SkillSpec s)
        {
            if (s == Spec) _pop = 1f;
        }

        void Build()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (!canvas) return;
            var go = new GameObject("SkillButton_" + slot, typeof(RectTransform));
            _root = go.GetComponent<RectTransform>();
            _root.SetParent(canvas.transform, false);
            _root.anchorMin = _root.anchorMax = new Vector2(1f, 0f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.anchoredPosition = position;
            _root.sizeDelta = Vector2.one * size;

            _bg = Layer("Back", 1f, new Color(0.08f, 0.1f, 0.14f, 0.65f), Circle);
            _bg.raycastTarget = true; // 화면 버튼으로 누를 수 있게
            var btn = go.AddComponent<OnScreenButton>();
            btn.controlPath = slot == SkillKind.Breath ? "<Gamepad>/leftShoulder" : "<Gamepad>/rightShoulder";

            _ring = Layer("Ring", 1f, Color.white, Circle);
            _ring.type = Image.Type.Filled;
            _ring.fillMethod = Image.FillMethod.Radial360;
            _ring.fillOrigin = (int)Image.Origin360.Top;
            _ring.fillClockwise = true;
            var inner = Layer("Inner", 0.86f, new Color(0.12f, 0.14f, 0.2f, 0.95f), Circle);
            inner.transform.SetAsLastSibling();

            _icon = Layer("Icon", 0.56f, Color.white, null);
            _icon.preserveAspect = true;

            _shade = Layer("Shade", 0.86f, Dim, Circle);
            _shade.type = Image.Type.Filled;
            _shade.fillMethod = Image.FillMethod.Radial360;
            _shade.fillOrigin = (int)Image.Origin360.Top;
            _shade.fillClockwise = false;

            // 브레스 표시 (작은 바람 아이콘)
            _badge = Layer("Badge", 0.34f, Color.white, null);
            _badge.rectTransform.anchoredPosition = new Vector2(size * 0.32f, -size * 0.32f);
            _badge.preserveAspect = true;

            for (int i = 0; i < 6; i++)
            {
                var sp = Layer("Sparkle", 0.22f, new Color(1f, 0.95f, 0.6f, 0f), null);
                sp.preserveAspect = true;
                _sparkles.Add(sp.rectTransform);
            }
            _root.gameObject.SetActive(false);
        }

        Image Layer(string name, float scale, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_root, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * (size * scale);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Image Pip(int i, int n)
        {
            while (_pips.Count <= i)
            {
                var p = Layer("Pip", 0.13f, Color.white, Circle);
                _pips.Add(p);
            }
            return _pips[i];
        }

        void Update()
        {
            if (!_root) return;
            var spec = Spec;
            bool show = spec != null;
            if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
            if (!show)
            {
                _shown = null;
                return;
            }
            if (spec != _shown) Show(spec);

            float dt = Time.unscaledDeltaTime;
            var dragon = skills.dragon;
            float stamina = dragon && dragon.Model != null ? dragon.Model.Stamina : 0f;
            var c = spec.color;

            if (spec.kind == SkillKind.Orb)
            {
                var ch = skills.Charges;
                int count = ch?.count ?? 0;
                int max = ch?.max ?? 1;
                // 다음 충전이 차오르는 고리
                _ring.fillAmount = ch == null || ch.Full ? 1f : ch.Progress;
                _ring.color = ch != null && ch.Full ? c : Color.Lerp(c, Color.gray, 0.5f);
                // 다 쓰면 아이콘이 시계 방향으로 걷혀 간다
                _shade.fillAmount = count == 0 && ch != null ? 1f - ch.Progress : (stamina < spec.staminaCost ? 1f : 0f);
                for (int i = 0; i < _pips.Count || i < max; i++)
                {
                    var p = Pip(i, max);
                    bool on = i < max;
                    p.enabled = on;
                    if (!on) continue;
                    float a = Mathf.PI * (1.25f + 0.5f * (i - (max - 1) * 0.5f) / Mathf.Max(1, max));
                    p.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (size * 0.56f);
                    p.color = i < count ? Color.Lerp(c, Color.white, 0.35f) : new Color(0f, 0f, 0f, 0.45f);
                }
            }
            else
            {
                foreach (var p in _pips) p.enabled = false;
                bool on = skills.IsBreathing;
                // 뿜는 동안 고리가 일렁이고, 스태미나가 모자라면 어둡다
                _ring.fillAmount = 1f;
                float wave = on ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 18f) : 0f;
                _ring.color = Color.Lerp(on ? c : Color.Lerp(c, Color.gray, 0.3f), Color.white, wave * 0.4f);
                _shade.fillAmount = !on && stamina < spec.minStamina ? 1f : 0f;
                if (on) _pop = Mathf.Max(_pop, 0.25f + wave * 0.1f);
            }

            // 눌렀을 때 톡 튀는 크기
            _pop = Mathf.MoveTowards(_pop, 0f, dt * 4f);
            float unlock = Time.unscaledTime - _unlockAt;
            float big = unlock < 1.2f ? Mathf.Sin(Mathf.Clamp01(unlock / 1.2f) * Mathf.PI) * 0.35f : 0f;
            _root.localScale = Vector3.one * (1f + _pop * 0.15f + big);
            UpdateSparkles(unlock);
        }

        void Show(SkillSpec spec)
        {
            _shown = spec;
            _icon.sprite = IconFor(spec);
            _icon.color = Color.white;
            _icon.enabled = _icon.sprite;
            _badge.sprite = icons ? icons.wind : null;
            _badge.enabled = spec.kind == SkillKind.Breath && _badge.sprite;
            foreach (var sp in _sparkles) sp.GetComponent<Image>().sprite = icons ? icons.sparkle : Circle;

            // 처음 보는 스킬이면 반짝인다
            string key = SeenKey + "." + slot;
            if (PlayerPrefs.GetString(key, "") != spec.id)
            {
                PlayerPrefs.SetString(key, spec.id);
                PlayerPrefs.Save();
                _unlockAt = Time.unscaledTime;
            }
        }

        void UpdateSparkles(float t)
        {
            const float dur = 1.6f;
            for (int i = 0; i < _sparkles.Count; i++)
            {
                var rt = _sparkles[i];
                var img = rt.GetComponent<Image>();
                if (t > dur)
                {
                    if (img.color.a > 0f) img.color = new Color(1f, 0.95f, 0.6f, 0f);
                    continue;
                }
                float k = t / dur;
                float a = i / (float)_sparkles.Count * Mathf.PI * 2f + k * 2.5f;
                rt.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (size * (0.35f + k * 0.45f));
                rt.localRotation = Quaternion.Euler(0f, 0f, k * 360f);
                rt.localScale = Vector3.one * (0.6f + Mathf.Sin(k * Mathf.PI) * 0.8f);
                img.color = new Color(1f, 0.95f, 0.6f, 1f - k);
            }
        }

        Sprite IconFor(SkillSpec spec)
        {
            if (!icons) return null;
            if (spec.kind == SkillKind.Orb)
            {
                if (DragonCare.IsLoaded && CareLogic.DominantTrait(DragonCare.State, out var trait, out _)) return icons.Trait(trait);
                return icons.meat;
            }
            var form = DragonCare.IsLoaded ? DragonCare.State.form : DragonForm.Prism;
            return form switch
            {
                DragonForm.Hunter => icons.claw,
                DragonForm.Tide => icons.fish,
                DragonForm.Ember => icons.flame,
                DragonForm.Grove => icons.leaf,
                _ => icons.sparkle
            };
        }

        /// <summary>부드러운 원 스프라이트 (런타임에 한 번 만든다)</summary>
        static Sprite Circle
        {
            get
            {
                if (_circle) return _circle;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "SkillCircle", wrapMode = TextureWrapMode.Clamp };
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
