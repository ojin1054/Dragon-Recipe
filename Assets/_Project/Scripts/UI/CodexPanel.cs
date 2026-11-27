using DragonRecipe.Care;
using DragonRecipe.Food;
using UnityEngine;
using UnityEngine.UI;

namespace DragonRecipe.UI
{
    /// <summary>
    /// 도감: 먹이(처음엔 검은 실루엣 → 잡은 수 → 성질 → 희귀 별)와 키워 본 성체 모습.
    /// </summary>
    public class CodexPanel : MonoBehaviour
    {
        public UIIconSet icons;
        public GameObject panel;
        public Button openButton;
        public Button closeButton;
        [Tooltip("먹이 기록")]
        public IconFlow foodFlow;
        [Tooltip("성체 모습 기록")]
        public IconFlow formFlow;

        static readonly Color Unknown = new Color(0f, 0f, 0f, 0.85f);
        static readonly DragonForm[] Forms =
            { DragonForm.Hunter, DragonForm.Tide, DragonForm.Ember, DragonForm.Grove, DragonForm.Prism };

        void Awake()
        {
            if (openButton) openButton.onClick.AddListener(Open);
            if (closeButton) closeButton.onClick.AddListener(Close);
            if (panel) panel.SetActive(false);
        }

        public bool IsOpen => panel && panel.activeSelf;

        public void Open()
        {
            if (!panel) return;
            panel.SetActive(true);
            Rebuild();
        }

        public void Close()
        {
            if (panel) panel.SetActive(false);
        }

        public void Rebuild()
        {
            if (!DragonCare.IsLoaded || !icons) return;
            var s = DragonCare.State;

            if (foodFlow)
            {
                foodFlow.Clear();
                var foods = DragonCare.Config.foods;
                if (foods != null)
                {
                    foreach (var f in foods)
                    {
                        if (!f) continue;
                        var row = foodFlow.AddRow();
                        var entry = s.Codex(f.id);
                        int caught = entry?.caught ?? 0;
                        var icon = f.icon ? f.icon : icons.meat;
                        if (caught == 0)
                        {
                            row.Icon(icon, 1.3f, Unknown); // 아직 못 잡은 먹이: 검은 실루엣
                            continue;
                        }
                        row.Icon(icon, 1.3f).Text($"x{caught}").Gap(20f);
                        // 몇 번 잡아 보면 성질이 드러난다
                        if (caught >= CareLogic.CodexTraitReveal) row.Icon(icons.Trait(f.trait));
                        if (entry.rareCaught > 0) row.Gap(12f).Icon(icons.star).Text($"x{entry.rareCaught}");
                    }
                }
            }

            if (formFlow)
            {
                formFlow.Clear();
                var row = formFlow.AddRow().Icon(icons.egg).Text($"x{s.generation}").Gap(30f);
                foreach (var form in Forms)
                {
                    bool known = s.HasForm(form);
                    row.Icon(icons.dragon, 1.4f, known ? DragonLook.FormColor(form) : Unknown);
                }
            }
        }
    }
}
