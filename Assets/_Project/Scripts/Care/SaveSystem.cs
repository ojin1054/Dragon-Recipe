using System;
using System.IO;
using UnityEngine;

namespace DragonRecipe.Care
{
    /// <summary>드래곤 상태를 JSON 파일로 저장/불러오기 (persistentDataPath).</summary>
    public static class SaveSystem
    {
        /// <summary>테스트 등에서 저장 위치를 바꿀 때 사용. null이면 기본 위치</summary>
        public static string PathOverride;

        public static string SavePath => PathOverride ?? Path.Combine(Application.persistentDataPath, "dragon_save.json");

        public static bool TryLoad(out DragonState state)
        {
            state = null;
            try
            {
                if (!File.Exists(SavePath)) return false;
                state = JsonUtility.FromJson<DragonState>(File.ReadAllText(SavePath));
                return state != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] 저장 파일을 읽지 못했습니다: {e.Message}");
                state = null;
                return false;
            }
        }

        public static void Save(DragonState state)
        {
            try
            {
                string path = SavePath;
                string tmp = path + ".tmp";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(tmp, JsonUtility.ToJson(state, true));
                // 쓰는 도중 꺼져도 기존 저장이 깨지지 않게 임시 파일 → 교체
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 저장 실패: {e.Message}");
            }
        }

        public static void Delete()
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
        }
    }
}
