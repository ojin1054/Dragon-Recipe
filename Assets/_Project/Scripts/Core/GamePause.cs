using System;
using UnityEngine;

namespace DragonRecipe.Core
{
    /// <summary>전역 일시정지 상태. Time.timeScale을 0으로 만들고, 게임 입력은 무시한다.</summary>
    public static class GamePause
    {
        public static bool IsPaused { get; private set; }
        public static event Action<bool> Changed;

        public static void Set(bool paused)
        {
            if (IsPaused == paused) return;
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            Changed?.Invoke(paused);
        }

        public static void Toggle() => Set(!IsPaused);

        // 도메인 리로드를 끈 에디터 설정에서도 Play 시작 때 상태가 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsPaused = false;
            Changed = null;
            Time.timeScale = 1f;
        }
    }
}
