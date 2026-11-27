using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DragonRecipe.EditorTools
{
    /// <summary>
    /// 상단 메뉴 Dragon Recipe에서 안드로이드 APK를 바로 뽑는다.
    /// 결과물은 프로젝트 폴더의 Builds/Android/ 아래에 생긴다 (git에는 올라가지 않음).
    /// </summary>
    public static class AndroidBuilder
    {
        const string OutputDir = "Builds/Android";
        const string FileName = "DragonRecipe.apk";

        [MenuItem("Dragon Recipe/Build Android APK", priority = 100)]
        public static void BuildApk()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("APK 빌드", "빌드에 켜진 씬이 없습니다.", "확인");
                return;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("APK 빌드",
                    "Android로 전환하지 못했습니다.\nUnity Hub에서 Android Build Support 모듈이 설치됐는지 확인하세요.",
                    "확인");
                return;
            }

            // APK로 뽑는다 (Google Play용 AAB가 아님)
            EditorUserBuildSettings.buildAppBundle = false;

            Directory.CreateDirectory(OutputDir);
            var path = Path.Combine(OutputDir, FileName);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                var mb = summary.totalSize / (1024f * 1024f);
                Debug.Log($"[AndroidBuilder] APK 빌드 완료: {Path.GetFullPath(path)} ({mb:0.0} MB, {summary.totalTime:mm\\:ss})");
                EditorUtility.RevealInFinder(path);
            }
            else
            {
                Debug.LogError($"[AndroidBuilder] APK 빌드 실패: {summary.result}, 오류 {summary.totalErrors}개. Console 창을 확인하세요.");
            }
        }
    }
}
