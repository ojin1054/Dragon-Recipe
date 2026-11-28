using System.Collections.Generic;
using DragonRecipe.Care;
using DragonRecipe.Core;
using DragonRecipe.Food;
using UnityEngine;

namespace DragonRecipe.UI
{
    /// <summary>드래곤 상태 / 비행 결과를 글 대신 아이콘 줄로 그린다.</summary>
    public static class CareIcons
    {
        static readonly Color Good = new Color(1f, 0.85f, 0.3f);
        static readonly Color Bad = new Color(1f, 0.55f, 0.5f);

        public enum Mood { Happy, Hungry, Starving, Full }

        public static Mood MoodOf(DragonState s, CareSettings c)
        {
            if (CareLogic.IsStarving(s)) return Mood.Starving;
            if (CareLogic.IsHungry(s, c)) return Mood.Hungry;
            if (s.hunger >= c.maxHunger - 0.5f) return Mood.Full;
            return Mood.Happy;
        }

        /// <summary>둥지 상태: 기분 얼굴 + 그 기분이 뜻하는 것</summary>
        public static void Status(IconFlow flow, UIIconSet set, DragonState s, CareSettings c, bool celebrating)
        {
            flow.Clear();
            if (celebrating)
            {
                // 성장했어요!
                flow.AddRow().Icon(set.star, 1.4f).Icon(set.sprout, 1.4f).Icon(set.star, 1.4f);
            }

            switch (MoodOf(s, c))
            {
                case Mood.Starving:
                    // 굶주림 → 성장 멈춤, 비행 약해짐
                    flow.AddRow().Icon(set.faceStarving, 1.6f)
                        .Gap().Icon(set.sprout).Icon(set.no)
                        .Gap().Icon(set.wing).Icon(set.arrowDown);
                    break;
                case Mood.Hungry:
                    // 배고파요 → 사냥하러 날아가요
                    flow.AddRow().Icon(set.faceHungry, 1.6f).Gap().Icon(set.arrowRight).Icon(set.wing).Icon(set.meat);
                    break;
                case Mood.Full:
                    // 배불러요 → 잡은 먹이는 창고로
                    flow.AddRow().Icon(set.faceFull, 1.6f).Gap().Icon(set.meat).Icon(set.arrowRight).Icon(set.basket);
                    break;
                default:
                    flow.AddRow().Icon(set.faceHappy, 1.6f);
                    break;
            }

            // 다쳤어요 → 치료약 필요
            if (s.injured)
                flow.AddRow().Icon(set.bandage, 1.2f).Icon(set.arrowRight).Icon(set.potion, 1.2f);
        }

        static readonly Color Faded = new Color(1f, 1f, 1f, 0.45f);

        /// <summary>지난 비행이 드래곤에게 남긴 것 (먹이 / 공중에서 먹음 / 창고 / 떨어뜨림 / 도감 / 성장 / 부상)</summary>
        /// <param name="simple">true면 포만도만 표시 (성장 경험치·버린 양 생략, 비행 결과 화면용)</param>
        public static void Report(IconFlow flow, UIIconSet set, FlightCareReport r, bool includeFoods = true, bool simple = false)
        {
            if (r == null) return;

            if (r.foodCount == 0)
            {
                flow.AddRow().Icon(set.meat).Icon(set.no); // 먹이를 못 잡았어요
            }
            else
            {
                if (includeFoods) Foods(flow.AddRow(), set, r.foods);

                // 날면서 먹음 → 바로 배가 참
                if (r.eaten.Count > 0)
                {
                    var gain = flow.AddRow().Icon(set.wing).Icon(set.meat).Text($"+{r.absorbed:0}");
                    if (!simple)
                    {
                        gain.Gap().Icon(set.sprout).Text($"+{r.xpGained:0}");
                        if (r.wasted > 0.5f) gain.Gap().Icon(set.trash).Text($"{r.wasted:0}", Bad);
                    }
                }
                // 둥지까지 들고 옴 → 창고
                if (r.stored.Count > 0) Foods(flow.AddRow().Icon(set.basket).Text($"+{r.stored.Count}").Gap(16f), set, r.stored);
                // 창고가 가득 차 버림
                if (r.overflow.Count > 0) flow.AddRow().Icon(set.basket).Icon(set.no).Icon(set.trash).Text($"{r.overflow.Count}", Bad);
                // 추락하며 떨어뜨림
                if (r.dropped.Count > 0) Foods(flow.AddRow().Icon(set.boom).Icon(set.arrowDown).Gap(16f), set, r.dropped, tint: Faded);
                // 도감에 새로 오름 / 희귀 개체
                if (r.newInCodex.Count > 0 || r.rareCount > 0)
                {
                    var row = flow.AddRow();
                    if (r.newInCodex.Count > 0) Foods(row.Icon(set.book).Text("+").Gap(8f), set, r.newInCodex);
                    if (r.rareCount > 0) row.Gap().Icon(set.sparkle).Text($"x{r.rareCount}", Good);
                }
            }

            if (r.grew)
            {
                var row = flow.AddRow().Icon(set.star).Icon(set.sprout, 1.3f).Icon(set.star); // 성장했어요
                if (r.newForm != DragonForm.None) row.Gap().Icon(set.dragon, 1.4f, DragonLook.FormColor(r.newForm)); // 성체 모습
            }
            if (r.injured) flow.AddRow().Icon(set.boom).Icon(set.arrowRight).Icon(set.bandage); // 추락해서 다쳤어요
        }

        /// <summary>먹이 종류별 얼굴 아이콘을 잡은 수만큼 (maxIcons를 넘으면 아이콘 하나 + xN)</summary>
        public static IconFlow.Row Foods(IconFlow.Row row, UIIconSet set, IEnumerable<FoodData> foods, int maxIcons = 3, Color? tint = null)
        {
            var counts = new Dictionary<FoodData, int>();
            var order = new List<FoodData>();
            foreach (var f in foods)
            {
                if (!f) continue;
                if (!counts.ContainsKey(f)) order.Add(f);
                counts[f] = counts.TryGetValue(f, out int n) ? n + 1 : 1;
            }
            foreach (var f in order)
            {
                var icon = f.icon ? f.icon : set.meat;
                int n = counts[f];
                if (n <= maxIcons)
                    for (int i = 0; i < n; i++) row.Icon(icon, 1f, tint);
                else
                    row.Icon(icon, 1f, tint).Text($"x{n}");
                row.Gap(12f);
            }
            return row;
        }

        /// <summary>비행 결과 화면 제목 배지 (추락했을 때만)</summary>
        public static void ResultTitle(IconFlow flow, UIIconSet set, FlightResult r)
        {
            flow.Clear();
            if (!r.Injured) return; // 무사히 내리면 배지를 띄우지 않는다
            var row = flow.AddRow();
            if (r.reason == FlightEndReason.OutOfBounds) row.Icon(set.arrowDown).Icon(set.boom).Text("추락");
            else row.Icon(set.boom).Text("추락");
        }

        /// <summary>
        /// 비행 결과 화면 하단 띠 (두 줄).
        /// 첫 줄: 체공 · 최고 속도 · 아슬아슬 · 연속 사냥 | 잡은 먹이.
        /// 둘째 줄: 그 먹이가 어디로 갔는지 (먹음 / 창고 / 떨어뜨림 / 도감 / 희귀) 와 성장 · 부상.
        /// </summary>
        public static void ResultStrip(IconFlow flow, UIIconSet set, FlightResult r, FlightCareReport report)
        {
            flow.Clear();
            var top = flow.AddRow().Icon(set.clock).Text($"{r.airtime:0.0}s")
                .Gap(28f).Icon(set.wind).Text($"{r.maxSpeed * 3.6f:0}")
                .Gap(28f).Icon(set.target).Text($"{r.nearMissCount}");
            if (r.maxCombo >= 2) top.Gap(28f).Icon(set.claw).Text($"x{r.maxCombo}", Good); // 연속 사냥

            var foods = new List<FoodData>();
            foreach (var c in r.caught) foods.Add(c.food);
            top.Gap(44f);
            if (foods.Count > 0) Foods(top, set, foods, maxIcons: 3);
            else top.Icon(set.meat).Icon(set.no); // 먹이를 못 잡았어요

            var bottom = flow.AddRow();
            bool any = false;
            if (report != null && report.foodCount > 0)
            {
                // 날면서 먹음 → 바로 배가 참
                if (report.eaten.Count > 0) { bottom.Icon(set.wing).Icon(set.meat).Text($"+{report.absorbed:0}").Gap(28f); any = true; }
                // 둥지까지 들고 옴 → 창고
                if (report.stored.Count > 0) { bottom.Icon(set.basket).Text($"+{report.stored.Count}").Gap(28f); any = true; }
                // 창고가 가득 차 버림
                if (report.overflow.Count > 0) { bottom.Icon(set.basket).Icon(set.no).Text($"{report.overflow.Count}", Bad).Gap(28f); any = true; }
                // 추락하며 떨어뜨림
                if (report.dropped.Count > 0) { bottom.Icon(set.boom).Icon(set.arrowDown).Text($"{report.dropped.Count}", Bad).Gap(28f); any = true; }
                // 도감에 새로 오름 / 희귀 개체
                if (report.newInCodex.Count > 0) { Foods(bottom.Icon(set.book).Text("+").Gap(8f), set, report.newInCodex); bottom.Gap(16f); any = true; }
                if (report.rareCount > 0) { bottom.Icon(set.sparkle).Text($"x{report.rareCount}", Good).Gap(28f); any = true; }
            }
            if (report != null && report.grew)
            {
                bottom.Icon(set.star).Icon(set.sprout, 1.2f).Icon(set.star); // 성장했어요
                if (report.newForm != DragonForm.None) bottom.Icon(set.dragon, 1.2f, DragonLook.FormColor(report.newForm));
                bottom.Gap(28f);
                any = true;
            }
            if (r.Injured) { bottom.Icon(set.bandage).Icon(set.arrowRight).Icon(set.potion); any = true; } // 다쳤어요 → 치료약
            if (!any) bottom.Gap(1f);
        }
    }
}
