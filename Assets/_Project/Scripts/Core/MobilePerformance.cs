using UnityEngine;
using UnityEngine.SceneManagement;

namespace DragonRecipe.Core
{
    /// <summary>
    /// 모바일 화면 깜빡임 대책 (씬마다 스스로 적용).
    /// - 프레임: Android는 targetFrameRate를 안 정하면 30fps로 묶여 빠른 비행에서 뚝뚝 끊겨 보인다 → 60.
    /// - 깊이 정밀도: near 0.3 / far 1500 은 모바일 깊이 버퍼에서 먼 곳 면끼리 앞뒤가 뒤바뀌며 깜빡인다(z-fighting).
    ///   안개가 끝나는 거리 너머는 어차피 안 보이므로 far를 거기까지 줄이고 near를 조금 키운다.
    /// </summary>
    public static class MobilePerformance
    {
        const float MinNear = 0.5f;
        const float FarPastFog = 50f;
        const float FallbackFar = 700f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Application.isMobilePlatform)
            {
                Application.targetFrameRate = 60;
                QualitySettings.vSyncCount = 0;
            }
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            FitCameras();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => FitCameras();

        static void FitCameras()
        {
            float far = RenderSettings.fog && RenderSettings.fogMode == FogMode.Linear
                ? RenderSettings.fogEndDistance + FarPastFog
                : FallbackFar;
            foreach (var cam in Camera.allCameras)
            {
                if (cam.orthographic || cam.targetTexture) continue;
                cam.nearClipPlane = Mathf.Max(cam.nearClipPlane, MinNear);
                cam.farClipPlane = Mathf.Min(cam.farClipPlane, Mathf.Max(far, cam.nearClipPlane * 100f));
            }
        }
    }
}
