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
                    // 배불러요 → 지금 잡은 먹이는 버려져요
                    flow.AddRow().Icon(set.faceFull, 1.6f).Gap().Icon(set.meat).Icon(set.arrowRight).Icon(set.trash);
                    break;
                default:
                    flow.AddRow().Icon(set.faceHappy, 1.6f);
                    break;
            }

            // 다쳤어요 → 치료약 필요
            if (s.injured)
                flow.AddRow().Icon(set.bandage, 1.2f).Icon(set.arrowRight).Icon(set.potion, 1.2f);
        }

        /// <summary>지난 비행이 드래곤에게 남긴 것 (먹이 / 포만도 / 성장 / 버림 / 부상)</summary>
        public static void Report(IconFlow flow, UIIconSet set, FlightCareReport r, bool includeFoods = true)
        {
            if (r == null) return;

            if (r.foodCount == 0)
            {
                flow.AddRow().Icon(set.meat).Icon(set.no); // 먹이를 못 잡았어요
            }
            else
            {
                if (includeFoods) Foods(flow.AddRow(), set, r.foods);
                var gain = flow.AddRow().Icon(set.meat).Text($"+{r.absorbed:0}").Gap().Icon(set.sprout).Text($"+{r.xpGained:0}");
                if (r.wasted > 0.5f) gain.Gap().Icon(set.trash).Text($"{r.wasted:0}", Bad);
            }

            if (r.grew) flow.AddRow().Icon(set.star).Icon(set.sprout, 1.3f).Icon(set.star); // 성장했어요
            if (r.injured) flow.AddRow().Icon(set.boom).Icon(set.arrowRight).Icon(set.bandage); // 추락해서 다쳤어요
        }

        /// <summary>먹이 종류별 아이콘을 개수만큼</summary>
        public static IconFlow.Row Foods(IconFlow.Row row, UIIconSet set, IEnumerable<FoodData> foods)
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
                row.Repeat(f.icon ? f.icon : set.meat, counts[f], maxIcons: 3).Gap(12f);
            return row;
        }

        /// <summary>비행 결과 화면 제목 (착지 / 추락)</summary>
        public static void ResultTitle(IconFlow flow, UIIconSet set, FlightResult r)
        {
            flow.Clear();
            var row = flow.AddRow();
            if (!r.Injured) row.Icon(set.wing, 1.6f).Icon(set.check, 1.6f);
            else if (r.reason == FlightEndReason.OutOfBounds) row.Icon(set.arrowDown, 1.6f).Icon(set.boom, 1.6f).Icon(set.bandage, 1.6f);
            else row.Icon(set.boom, 1.6f).Icon(set.bandage, 1.6f);
        }

        /// <summary>비행 결과 화면 본문</summary>
        public static void ResultBody(IconFlow flow, UIIconSet set, FlightResult r, FlightCareReport report)
        {
            flow.Clear();
            flow.AddRow().Icon(set.clock).Text($"{r.airtime:0.0}s").Gap(40f).Icon(set.wind).Text($"{r.maxSpeed * 3.6f:0} km/h");
            flow.AddRow().Icon(set.sparkle).Text($"{r.perfectFlapCount}").Gap(40f).Icon(set.target).Text($"{r.nearMissCount}");

            var foods = new List<FoodData>();
            foreach (var c in r.caught) foods.Add(c.food);
            if (foods.Count > 0) Foods(flow.AddRow(), set, foods);

            if (report != null)
            {
                Report(flow, set, report, includeFoods: false);
            }
            else
            {
                if (foods.Count == 0) flow.AddRow().Icon(set.meat).Icon(set.no);
                if (r.Injured) flow.AddRow().Icon(set.bandage).Icon(set.arrowRight).Icon(set.potion);
            }
        }
    }
}
