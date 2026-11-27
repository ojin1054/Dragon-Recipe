using DragonRecipe.Core;
using UnityEngine.SceneManagement;

namespace DragonRecipe.Care
{
    /// <summary>둥지 ↔ 비행 씬 이동.</summary>
    public static class SceneFlow
    {
        public static void GoToNest() => Load(DragonCare.Config ? DragonCare.Config.nestScene : "Nest");

        public static void GoToFlight() => Load(DragonCare.Config ? DragonCare.Config.flightScene : "FlightPrototype");

        static void Load(string scene)
        {
            GamePause.Set(false);
            DragonCare.Save();
            SceneManager.LoadScene(scene);
        }
    }
}
