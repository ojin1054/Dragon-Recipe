using System;
using UnityEngine;

namespace DragonRecipe.UI
{
    public enum StickMode
    {
        /// <summary>정해진 자리에 고정</summary>
        Fixed,
        /// <summary>화면을 누른 곳으로 조이스틱이 옮겨 온다</summary>
        Floating,
    }

    /// <summary>화면 조이스틱 설정 (크기·자리·고정 여부). 기기에 저장된다.</summary>
    public static class StickSettings
    {
        const string Key = "DragonRecipe.Stick.";
        public const float MinSize = 0.6f;
        public const float MaxSize = 1.6f;
        public const float SizeStep = 0.1f;

        /// <summary>설정이 바뀜</summary>
        public static event Action Changed;

        public static StickMode Mode
        {
            get => PlayerPrefs.GetInt(Key + "Mode", (int)StickMode.Fixed) == (int)StickMode.Floating ? StickMode.Floating : StickMode.Fixed;
            set { PlayerPrefs.SetInt(Key + "Mode", (int)value); Save(); }
        }

        /// <summary>기본 크기 대비 배율 (0.6 ~ 1.6)</summary>
        public static float Size
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(Key + "Size", 1f), MinSize, MaxSize);
            set { PlayerPrefs.SetFloat(Key + "Size", Mathf.Clamp(Mathf.Round(value * 100f) / 100f, MinSize, MaxSize)); Save(); }
        }

        /// <summary>자리를 직접 옮긴 적이 있는지 (없으면 씬에 놓인 기본 자리)</summary>
        public static bool HasPosition => PlayerPrefs.HasKey(Key + "X");

        /// <summary>조이스틱 중심 자리. 화면 왼쪽 아래 (0,0) ~ 오른쪽 위 (1,1)</summary>
        public static Vector2 Position
        {
            get => new Vector2(Mathf.Clamp01(PlayerPrefs.GetFloat(Key + "X", 0f)), Mathf.Clamp01(PlayerPrefs.GetFloat(Key + "Y", 0f)));
            set
            {
                PlayerPrefs.SetFloat(Key + "X", Mathf.Clamp01(value.x));
                PlayerPrefs.SetFloat(Key + "Y", Mathf.Clamp01(value.y));
                Save();
            }
        }

        public static void ClearPosition()
        {
            PlayerPrefs.DeleteKey(Key + "X");
            PlayerPrefs.DeleteKey(Key + "Y");
            Save();
        }

        /// <summary>모두 기본값으로</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(Key + "Mode");
            PlayerPrefs.DeleteKey(Key + "Size");
            PlayerPrefs.DeleteKey(Key + "X");
            PlayerPrefs.DeleteKey(Key + "Y");
            Save();
        }

        /// <summary>테스트용: 지금 값을 보관했다가 되돌린다</summary>
        public static Action Snapshot()
        {
            bool hadMode = PlayerPrefs.HasKey(Key + "Mode"), hadSize = PlayerPrefs.HasKey(Key + "Size"), hadPos = HasPosition;
            var mode = Mode;
            float size = Size;
            var pos = Position;
            return () =>
            {
                ResetAll();
                if (hadMode) PlayerPrefs.SetInt(Key + "Mode", (int)mode);
                if (hadSize) PlayerPrefs.SetFloat(Key + "Size", size);
                if (hadPos) { PlayerPrefs.SetFloat(Key + "X", pos.x); PlayerPrefs.SetFloat(Key + "Y", pos.y); }
                Save();
            };
        }

        static void Save()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
