using System.Collections.Generic;
using System.IO;
using DragonRecipe.Farm;
using DragonRecipe.Food;
using UnityEditor;
using UnityEngine;

namespace DragonRecipe.EditorTools
{
    /// <summary>
    /// 기본 조합표와 조합 동물 에셋을 만든다. 이미 있으면 수치만 덮어쓴다 (아이콘은 유지).
    /// 수치를 에디터에서 직접 고친 뒤에는 다시 돌리지 말 것.
    /// </summary>
    public static class FarmDataBuilder
    {
        const string Dir = "Assets/_Project/Data/Farm";

        struct H
        {
            public string id, name;
            public int satiety, flights;
            public (FoodTrait trait, float share)[] traits;
            public (BuffStat stat, float mul)[] buffs;
        }

        struct R
        {
            public string id;
            public RecipeInput a, b;
            public string result;
            public float chance, minutes;
        }

        static RecipeInput T(FoodTrait t) => new RecipeInput { kind = RecipeInputKind.Trait, trait = t };
        static RecipeInput A(string id) => new RecipeInput { kind = RecipeInputKind.Animal, animalId = id };

        static H Hy(string id, string name, int satiety, int flights,
            (FoodTrait, float)[] traits, params (BuffStat, float)[] buffs)
            => new H { id = id, name = name, satiety = satiety, flights = flights, traits = traits, buffs = buffs };

        const FoodTrait Meat = FoodTrait.Meat, Fish = FoodTrait.Fish, Fire = FoodTrait.Fire, Plant = FoodTrait.Plant;

        // ───────────── 조합 동물 ─────────────
        static readonly H[] Hybrids =
        {
            // 같은 성질끼리: 성공률 높고 빠름, 그 성질을 진하게 담는다
            Hy("iron_boar",    "철갑 멧돼지", 8, 3, new[] { (Meat, 1f) },  (BuffStat.MaxHealth, 1.3f)),
            Hy("cloud_carp",   "구름 잉어",   6, 3, new[] { (Fish, 1f) },  (BuffStat.Glide, 1.12f)),
            Hy("sun_lizard",   "태양 도마뱀", 5, 3, new[] { (Fire, 1f) },  (BuffStat.StaminaRegen, 1.4f)),
            Hy("breeze_deer",  "바람잎 사슴", 5, 3, new[] { (Plant, 1f) }, (BuffStat.Agility, 1.25f)),

            // 다른 성질끼리: 성공률 낮고 오래 걸림, 두 성질을 반씩 담는다
            Hy("sea_ox",       "바다 황소",   8, 3, new[] { (Meat, .5f), (Fish, .5f) },  (BuffStat.CarryStrength, 1.5f)),
            Hy("ember_ram",    "숯불 숫양",   7, 3, new[] { (Meat, .5f), (Fire, .5f) },  (BuffStat.FlapLift, 1.15f)),
            Hy("moss_boar",    "이끼 멧돼지", 8, 3, new[] { (Meat, .5f), (Plant, .5f) }, (BuffStat.MaxHealth, 1.15f), (BuffStat.DamageTaken, 0.8f)),
            Hy("steam_eel",    "증기 장어",   6, 3, new[] { (Fish, .5f), (Fire, .5f) },  (BuffStat.Speed, 1.12f)),
            Hy("lotus_frog",   "연잎 개구리", 5, 3, new[] { (Fish, .5f), (Plant, .5f) }, (BuffStat.DamageTaken, 0.6f)),
            Hy("cinder_sheep", "불씨 버섯양", 6, 3, new[] { (Fire, .5f), (Plant, .5f) }, (BuffStat.MaxStamina, 1.25f)),

            // 특정 동물끼리 / 조합 동물끼리: 성공률 가장 낮고 가장 오래 걸림, 버프가 오래 간다
            Hy("golden_koi",   "황금 잉어",   8, 4, new[] { (Fish, .5f), (Plant, .5f) }, (BuffStat.Glide, 1.15f), (BuffStat.MaxStamina, 1.15f)),
            Hy("thunder_eel",  "천둥 장어",   8, 5, new[] { (Fish, .3f), (Fire, .7f) },  (BuffStat.Speed, 1.15f), (BuffStat.StaminaRegen, 1.3f)),
        };

        // ───────────── 조합표 ─────────────
        static readonly R[] Recipes =
        {
            new R { id = "meat_meat",   a = T(Meat),  b = T(Meat),  result = "iron_boar",    chance = .80f, minutes = 20 },
            new R { id = "fish_fish",   a = T(Fish),  b = T(Fish),  result = "cloud_carp",   chance = .80f, minutes = 20 },
            new R { id = "fire_fire",   a = T(Fire),  b = T(Fire),  result = "sun_lizard",   chance = .80f, minutes = 20 },
            new R { id = "plant_plant", a = T(Plant), b = T(Plant), result = "breeze_deer",  chance = .80f, minutes = 20 },

            new R { id = "meat_fish",   a = T(Meat),  b = T(Fish),  result = "sea_ox",       chance = .65f, minutes = 35 },
            new R { id = "meat_fire",   a = T(Meat),  b = T(Fire),  result = "ember_ram",    chance = .65f, minutes = 35 },
            new R { id = "meat_plant",  a = T(Meat),  b = T(Plant), result = "moss_boar",    chance = .65f, minutes = 35 },
            new R { id = "fish_fire",   a = T(Fish),  b = T(Fire),  result = "steam_eel",    chance = .60f, minutes = 40 },
            new R { id = "fish_plant",  a = T(Fish),  b = T(Plant), result = "lotus_frog",   chance = .65f, minutes = 35 },
            new R { id = "fire_plant",  a = T(Fire),  b = T(Plant), result = "cinder_sheep", chance = .60f, minutes = 40 },

            // 연어 + 황금멜론은 fish_plant보다 우선한다 (id로 고른 재료가 더 많음)
            new R { id = "salmon_golden_melon", a = A("salmon"),    b = A("golden_melon"), result = "golden_koi",  chance = .50f, minutes = 60 },
            new R { id = "steam_eel_sun_lizard", a = A("steam_eel"), b = A("sun_lizard"),  result = "thunder_eel", chance = .45f, minutes = 90 },
        };

        /// <summary>조합표 에셋이 있으면 그대로 쓰고, 없으면 기본값으로 만든다</summary>
        public static CombineTable EnsureTable()
        {
            var t = AssetDatabase.LoadAssetAtPath<CombineTable>($"{Dir}/CombineTable.asset");
            return t ? t : BuildTable();
        }

        [MenuItem("Dragon Recipe/Build Farm Data")]
        public static void Build() => BuildTable();

        /// <summary>조합 동물과 조합표 에셋을 기본값으로 만들거나 덮어쓴다</summary>
        public static CombineTable BuildTable()
        {
            Directory.CreateDirectory(Dir);

            var made = new Dictionary<string, HybridAnimalData>();
            foreach (var h in Hybrids)
            {
                var d = LoadOrCreate<HybridAnimalData>($"{Dir}/Hybrid_{h.id}.asset");
                d.id = h.id;
                d.displayName = h.name;
                d.satiety = h.satiety;
                d.buffFlights = h.flights;
                d.traits = new List<TraitShare>();
                foreach (var (trait, share) in h.traits) d.traits.Add(new TraitShare { trait = trait, share = share });
                d.buffs = new List<BuffEffect>();
                foreach (var (stat, mul) in h.buffs) d.buffs.Add(new BuffEffect { stat = stat, multiplier = mul });
                EditorUtility.SetDirty(d);
                made[h.id] = d;
            }

            var table = LoadOrCreate<CombineTable>($"{Dir}/CombineTable.asset");
            table.recipes = new List<CombineRecipe>();
            foreach (var r in Recipes)
            {
                if (!made.TryGetValue(r.result, out var result))
                {
                    Debug.LogError($"[FarmDataBuilder] {r.id}: 결과 동물 '{r.result}'이 없습니다");
                    continue;
                }
                table.recipes.Add(new CombineRecipe
                {
                    id = r.id, a = r.a, b = r.b, result = result,
                    successChance = r.chance, durationMinutes = r.minutes
                });
            }
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FarmDataBuilder] 조합 동물 {made.Count}종, 조합 {table.recipes.Count}개를 만들었습니다");
            return table;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }
    }
}
