using DragonRecipe.Core;
using DragonRecipe.Tribes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DragonRecipe.Care
{
    /// <summary>둥지 ↔ 비행 씬 이동.</summary>
    public static class SceneFlow
    {
        public static void GoToNest() => Load(DragonCare.Config ? DragonCare.Config.nestScene : "Nest");

        /// <summary>비행 시작. 참전 중이면 전쟁 맵으로 간다</summary>
        public static void GoToFlight()
        {
            var cfg = DragonCare.Config;
            string war = cfg ? cfg.warScene : "WarBattle";
            // 전쟁 맵이 아직 빌드 목록에 없으면 평소 비행 맵(망루 방어전)으로
            if (AtWar && !string.IsNullOrEmpty(war) && Application.CanStreamedLevelBeLoaded(war))
                Load(war);
            else
                Load(cfg ? cfg.flightScene : "FlightPrototype");
        }

        /// <summary>한쪽 편으로 참전한 전쟁이 있음</summary>
        static bool AtWar => DragonCare.IsLoaded && DragonCare.Tribes != null && DragonCare.Tribes.war.status == WarStatus.Joined;

        static void Load(string scene)
        {
            GamePause.Set(false);
            DragonCare.Save();
            SceneManager.LoadScene(scene);
        }
    }
}
