using System.Collections.Generic;
using DragonRecipe.Food;
using UnityEngine;

namespace DragonRecipe.Care
{
    /// <summary>
    /// 먹은 먹이에 따라 드래곤 몸 색이 조금씩 물들고, 성체가 되면 모습(뿔·지느러미·불꽃·잎·수정)이 붙는다.
    /// 둥지 / 비행 드래곤 모두에 붙인다. 글 없이 "어느 쪽으로 자라는지"를 보여 주는 장치.
    /// </summary>
    public class DragonLook : MonoBehaviour
    {
        [Tooltip("드래곤 모델 루트 (비우면 자식 'Model')")]
        public Transform model;
        [Tooltip("어릴 때 → 다 자랐을 때 먹이 색이 몸에 스미는 정도")]
        public float minTint = 0.15f;
        public float maxTint = 0.7f;
        public float refreshInterval = 0.5f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        readonly List<Renderer> _body = new List<Renderer>();
        readonly List<Renderer> _wing = new List<Renderer>();
        Color _bodyBase = new Color(0.3f, 0.65f, 0.55f);
        Color _wingBase = new Color(0.85f, 0.55f, 0.35f);
        Material _decoMat;
        MaterialPropertyBlock _block;
        Transform _deco;
        DragonForm _decoForm = (DragonForm)(-1);
        readonly List<Transform> _flames = new List<Transform>();
        float _nextRefresh;
        string _key;

        public static Color TraitColor(FoodTrait t) => t switch
        {
            FoodTrait.Meat => new Color(0.78f, 0.3f, 0.25f),
            FoodTrait.Fish => new Color(0.25f, 0.5f, 0.92f),
            FoodTrait.Fire => new Color(1f, 0.45f, 0.1f),
            _ => new Color(0.38f, 0.78f, 0.3f)
        };

        public static Color FormColor(DragonForm f) => f switch
        {
            DragonForm.Hunter => TraitColor(FoodTrait.Meat),
            DragonForm.Tide => TraitColor(FoodTrait.Fish),
            DragonForm.Ember => TraitColor(FoodTrait.Fire),
            DragonForm.Grove => TraitColor(FoodTrait.Plant),
            DragonForm.Prism => new Color(0.82f, 0.62f, 1f),
            _ => new Color(0.3f, 0.65f, 0.55f)
        };

        /// <summary>먹은 성질 비율로 섞은 색 (아무것도 안 먹었으면 base)</summary>
        public static Color DietColor(DragonState s, Color fallback)
        {
            float total = s.TotalTraits;
            if (total <= 0f) return fallback;
            var c = new Color(0, 0, 0, 1);
            foreach (var t in s.traits)
            {
                var tc = TraitColor(t.trait);
                float w = t.amount / total;
                c.r += tc.r * w;
                c.g += tc.g * w;
                c.b += tc.b * w;
            }
            return c;
        }

        void Awake()
        {
            if (!model) model = transform.Find("Model");
            if (!model) model = transform;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var name = r.sharedMaterial ? r.sharedMaterial.name : "";
                if (name.StartsWith("M_DragonBody"))
                {
                    _body.Add(r);
                    _bodyBase = r.sharedMaterial.color;
                    _decoMat ??= r.sharedMaterial;
                }
                else if (name.StartsWith("M_DragonWing"))
                {
                    _wing.Add(r);
                    _wingBase = r.sharedMaterial.color;
                }
            }
            _block = new MaterialPropertyBlock();
        }

        void OnEnable() => _key = null;

        void Update()
        {
            AnimateFlames();
            if (Time.unscaledTime < _nextRefresh || !DragonCare.IsLoaded) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;
            Refresh(DragonCare.State, DragonCare.Config.care);
        }

        public void Refresh(DragonState s, CareSettings c)
        {
            string key = $"{s.form}|{s.growthXp:0}|{s.TotalTraits:0}|{s.generation}";
            if (key == _key) return;
            _key = key;

            Color body, wing;
            if (s.form != DragonForm.None)
            {
                var fc = FormColor(s.form);
                body = Color.Lerp(_bodyBase, fc, 0.75f);
                wing = Color.Lerp(_wingBase, Color.Lerp(fc, Color.white, 0.25f), 0.6f);
            }
            else
            {
                float grow = Mathf.Clamp01(s.growthXp / Mathf.Max(1f, c.xpToAdult));
                float k = s.TotalTraits > 0f ? Mathf.Lerp(minTint, maxTint, grow) : 0f;
                var diet = DietColor(s, _bodyBase);
                body = Color.Lerp(_bodyBase, diet, k);
                wing = Color.Lerp(_wingBase, Color.Lerp(diet, Color.white, 0.25f), k * 0.6f);
            }
            Paint(_body, body);
            Paint(_wing, wing);
            BuildDecorations(s.form);
        }

        void Paint(List<Renderer> rs, Color c)
        {
            _block.SetColor(BaseColorId, c);
            _block.SetColor(ColorId, c);
            foreach (var r in rs)
                if (r) r.SetPropertyBlock(_block);
        }

        // ───────────── 성체 모습 장식 ─────────────

        void BuildDecorations(DragonForm form)
        {
            if (form == _decoForm) return;
            _decoForm = form;
            _flames.Clear();
            if (_deco) Destroy(_deco.gameObject);
            if (form == DragonForm.None) return;

            _deco = new GameObject($"Form_{form}").transform;
            _deco.SetParent(model, false);
            var main = FormColor(form);
            var light = Color.Lerp(main, Color.white, 0.45f);
            var dark = Color.Lerp(main, Color.black, 0.35f);

            switch (form)
            {
                case DragonForm.Hunter: // 크게 휜 뿔 + 등 가시
                    Deco(PrimitiveType.Cube, new Vector3(-0.2f, 1.05f, 1.15f), new Vector3(-55, -15, 0), new Vector3(0.14f, 0.7f, 0.14f), light);
                    Deco(PrimitiveType.Cube, new Vector3(0.2f, 1.05f, 1.15f), new Vector3(-55, 15, 0), new Vector3(0.14f, 0.7f, 0.14f), light);
                    for (int i = 0; i < 4; i++)
                        Deco(PrimitiveType.Cube, new Vector3(0, 0.5f, 0.6f - i * 0.45f), new Vector3(45, 0, 45), Vector3.one * (0.3f - i * 0.04f), dark);
                    break;
                case DragonForm.Tide: // 등지느러미 + 꼬리지느러미
                    Deco(PrimitiveType.Cube, new Vector3(0, 0.75f, -0.1f), new Vector3(-20, 0, 0), new Vector3(0.05f, 0.7f, 1.3f), light);
                    Deco(PrimitiveType.Cube, new Vector3(0, 0.1f, -2.9f), new Vector3(0, 0, 0), new Vector3(1.4f, 0.05f, 0.7f), light);
                    Deco(PrimitiveType.Cube, new Vector3(-0.32f, 0.6f, 1.45f), new Vector3(0, -30, 0), new Vector3(0.05f, 0.25f, 0.35f), dark);
                    Deco(PrimitiveType.Cube, new Vector3(0.32f, 0.6f, 1.45f), new Vector3(0, 30, 0), new Vector3(0.05f, 0.25f, 0.35f), dark);
                    break;
                case DragonForm.Ember: // 목과 머리를 따라 흔들리는 불꽃 갈기
                    for (int i = 0; i < 6; i++)
                    {
                        float t = i / 5f;
                        var pos = Vector3.Lerp(new Vector3(0, 0.95f, 1.35f), new Vector3(0, 0.55f, -0.6f), t);
                        var col = Color.Lerp(new Color(1f, 0.85f, 0.2f), main, t);
                        _flames.Add(Deco(PrimitiveType.Cube, pos, new Vector3(0, 45, 0), Vector3.one * Mathf.Lerp(0.35f, 0.22f, t), col));
                    }
                    break;
                case DragonForm.Grove: // 머리의 잎 + 날개 끝 잎사귀
                    Deco(PrimitiveType.Cube, new Vector3(-0.15f, 1.0f, 1.35f), new Vector3(0, 0, 35), new Vector3(0.45f, 0.05f, 0.25f), light);
                    Deco(PrimitiveType.Cube, new Vector3(0.15f, 1.0f, 1.35f), new Vector3(0, 0, -35), new Vector3(0.45f, 0.05f, 0.25f), light);
                    AddWingLeaf("WingL", -1, main);
                    AddWingLeaf("WingR", 1, main);
                    Deco(PrimitiveType.Sphere, new Vector3(0, 0.15f, -3.0f), Vector3.zero, Vector3.one * 0.45f, light);
                    break;
                case DragonForm.Prism: // 등에 박힌 무지갯빛 수정
                    var rainbow = new[]
                    {
                        new Color(1f, 0.5f, 0.5f), new Color(1f, 0.85f, 0.4f), new Color(0.5f, 0.9f, 0.5f),
                        new Color(0.45f, 0.7f, 1f), new Color(0.8f, 0.55f, 1f)
                    };
                    for (int i = 0; i < rainbow.Length; i++)
                        Deco(PrimitiveType.Cube, new Vector3(0, 0.55f, 0.8f - i * 0.4f), new Vector3(45, 45, 0), new Vector3(0.18f, 0.45f, 0.18f), rainbow[i]);
                    Deco(PrimitiveType.Cube, new Vector3(0, 1.05f, 1.4f), new Vector3(45, 45, 0), Vector3.one * 0.22f, Color.white);
                    break;
            }
        }

        void AddWingLeaf(string wingName, int side, Color c)
        {
            var wing = model.Find(wingName);
            if (!wing) return;
            var leaf = Deco(PrimitiveType.Cube, Vector3.zero, Vector3.zero, new Vector3(0.9f, 0.05f, 0.6f), c);
            leaf.SetParent(wing, false); // 날개와 같이 퍼덕이게
            leaf.localPosition = new Vector3(2.6f * side, 0.02f, -0.4f);
            leaf.localRotation = Quaternion.Euler(0, -35f * side, 0);
        }

        Transform Deco(PrimitiveType type, Vector3 localPos, Vector3 euler, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            DestroyImmediate(go.GetComponent<Collider>()); // 드래곤 충돌은 SphereCast로만
            go.transform.SetParent(_deco, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (_decoMat) r.sharedMaterial = _decoMat;
            var b = new MaterialPropertyBlock();
            b.SetColor(BaseColorId, color);
            b.SetColor(ColorId, color);
            r.SetPropertyBlock(b);
            return go.transform;
        }

        void AnimateFlames()
        {
            for (int i = 0; i < _flames.Count; i++)
            {
                var f = _flames[i];
                if (!f) continue;
                float s = 1f + Mathf.Sin(Time.time * 12f + i * 1.7f) * 0.25f;
                float baseSize = Mathf.Lerp(0.35f, 0.22f, i / 5f);
                f.localScale = new Vector3(baseSize, baseSize * s * 1.4f, baseSize);
            }
        }
    }
}
