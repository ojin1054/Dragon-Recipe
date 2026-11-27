using DragonRecipe.Care;
using DragonRecipe.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace DragonRecipe.UI
{
    /// <summary>농장 동물 · 버프 아이콘 고르기와 그리기</summary>
    public static class FarmIcons
    {
        public static readonly Color BuffTint = new Color(1f, 0.85f, 0.35f);
        public static readonly Color HybridTint = new Color(1f, 0.9f, 0.55f);
        public static readonly Color Unknown = new Color(0f, 0f, 0f, 0.85f);

        public static Sprite Buff(UIIconSet set, BuffStat stat) => stat switch
        {
            BuffStat.MaxStamina => set.potion,
            BuffStat.StaminaRegen => set.clock,
            BuffStat.FlapLift => set.wing,
            BuffStat.Speed => set.arrowRight,
            BuffStat.Glide => set.wind,
            BuffStat.Agility => set.target,
            BuffStat.CarryStrength => set.claw,
            BuffStat.MaxHealth => set.heart ? set.heart : set.bandage,
            _ => set.bandage
        };

        /// <summary>버프 하나: 효과 아이콘들 + 남은 비행 횟수 점</summary>
        public static IconFlow.Row Buff(IconFlow.Row row, UIIconSet set, ActiveBuff b, float scale = 1f)
        {
            foreach (var e in b.effects) row.Icon(Buff(set, e.stat), scale, BuffTint);
            return row.Text(new string('•', Mathf.Clamp(b.flightsLeft, 0, 9)), BuffTint).Gap(18f);
        }

        /// <summary>
        /// 농장 동물 아이콘을 parent 안에 꽉 차게 그린다.
        /// 조합 동물에 아이콘이 없으면 성질 아이콘 두 개를 겹쳐 그리고 반짝임을 붙인다.
        /// </summary>
        public static void Draw(RectTransform parent, UIIconSet set, FarmAnimal a, Color? tint = null)
        {
            if (a == null || !set) return;
            var c = tint ?? Color.white;
            if (!a.hybrid)
            {
                var food = DragonCare.FindFood(a.id);
                Layer(parent, food && food.icon ? food.icon : set.Trait(a.trait), new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), c);
                if (a.rare && set.sparkle && tint == null)
                    Layer(parent, set.sparkle, new Vector2(0.62f, 0.62f), new Vector2(0.98f, 0.98f), Color.white);
                return;
            }
            DrawHybrid(parent, set, DragonCare.CombineTable ? DragonCare.CombineTable.Hybrid(a.id) : null, a.trait, tint);
        }

        public static void DrawHybrid(RectTransform parent, UIIconSet set, HybridAnimalData h, Food.FoodTrait mainTrait,
            Color? tint = null)
        {
            if (!set) return;
            if (h && h.icon)
            {
                Layer(parent, h.icon, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), tint ?? Color.white);
                return;
            }
            var c = tint ?? HybridTint;
            var first = mainTrait;
            var second = mainTrait;
            if (h != null && h.traits.Count > 0)
            {
                first = h.traits[0].trait;
                second = h.traits[h.traits.Count - 1].trait;
            }
            if (first == second)
            {
                Layer(parent, set.Trait(first), new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), c);
            }
            else
            {
                Layer(parent, set.Trait(first), new Vector2(0.04f, 0.3f), new Vector2(0.66f, 0.92f), c);
                Layer(parent, set.Trait(second), new Vector2(0.34f, 0.06f), new Vector2(0.96f, 0.68f), c);
            }
            if (set.sparkle && tint == null)
                Layer(parent, set.sparkle, new Vector2(0f, 0f), new Vector2(0.36f, 0.36f), Color.white);
        }

        public static Image Layer(RectTransform parent, Sprite sprite, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(sprite ? sprite.name : "Icon", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = color;
            return img;
        }
    }
}
