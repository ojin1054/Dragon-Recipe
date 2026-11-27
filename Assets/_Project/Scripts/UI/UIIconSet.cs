using UnityEngine;

namespace DragonRecipe.UI
{
    /// <summary>
    /// 안내용 픽셀 아이콘 모음. 필드 이름의 snake_case가 파일 이름이 된다
    /// (예: faceHappy → Art/Icons/icon_face_happy.png). 씬 빌더가 자동으로 채운다.
    /// 원본: ArtSource/ui_icons.aseprite (16x16 격자)
    /// </summary>
    [CreateAssetMenu(menuName = "Dragon Recipe/UI Icon Set", fileName = "UIIconSet")]
    public class UIIconSet : ScriptableObject
    {
        [Header("드래곤 기분")]
        public Sprite faceHappy;
        public Sprite faceHungry;
        public Sprite faceStarving;
        public Sprite faceFull;

        [Header("상태")]
        public Sprite meat;
        public Sprite sprout;
        public Sprite no;
        public Sprite trash;
        public Sprite bandage;
        public Sprite potion;
        public Sprite star;

        [Header("비행")]
        public Sprite boom;
        public Sprite check;
        public Sprite clock;
        public Sprite wind;
        public Sprite sparkle;
        public Sprite target;
        public Sprite wing;

        [Header("화살표")]
        public Sprite arrowRight;
        public Sprite arrowDown;
    }
}
