using DragonRecipe.Care;
using UnityEngine;

namespace DragonRecipe.UI
{
    /// <summary>비행 HUD: 지금 붙어 있는 농장 버프 아이콘과 남은 비행 횟수 점</summary>
    public class BuffHUD : MonoBehaviour
    {
        public UIIconSet icons;
        public IconFlow flow;

        string _key;

        void Update()
        {
            if (!flow || !icons || !DragonCare.IsLoaded) return;
            var buffs = DragonCare.Farm.buffs;
            string key = "";
            foreach (var b in buffs) key += $"{b.sourceId}:{b.flightsLeft}|";
            if (key == _key) return;
            _key = key;

            flow.Clear();
            if (buffs.Count == 0) return;
            var row = flow.AddRow();
            foreach (var b in buffs) row = FarmIcons.Buff(row, icons, b);
        }
    }
}
