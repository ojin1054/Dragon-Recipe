using UnityEngine;

namespace DragonRecipe.Tribes
{
    /// <summary>종족·호감도 단계를 화면에 보여 줄 때 쓰는 색 (마을 깃발, 게이지 공용)</summary>
    public static class TribeLook
    {
        public static readonly Color HostileColor = new Color(0.85f, 0.18f, 0.14f);
        public static readonly Color WaryColor = new Color(0.95f, 0.55f, 0.2f);
        public static readonly Color NeutralColor = new Color(0.78f, 0.78f, 0.78f);
        public static readonly Color FriendlyColor = new Color(0.35f, 0.82f, 0.35f);
        public static readonly Color ReveredColor = new Color(1f, 0.84f, 0.25f);

        public static Color Of(Standing s) => s switch
        {
            Standing.Hostile => HostileColor,
            Standing.Wary => WaryColor,
            Standing.Friendly => FriendlyColor,
            Standing.Revered => ReveredColor,
            _ => NeutralColor
        };

        /// <summary>종족 대표 색 (얼굴·옷)</summary>
        public static Color Skin(Tribe t) => t switch
        {
            Tribe.Orc => new Color(0.42f, 0.62f, 0.28f),
            Tribe.Elf => new Color(0.96f, 0.88f, 0.78f),
            _ => new Color(0.93f, 0.75f, 0.6f)
        };

        public static string Name(Tribe t) => t switch
        {
            Tribe.Orc => "오크",
            Tribe.Elf => "엘프",
            _ => "사람"
        };

        static Sprite[] _faces;

        /// <summary>
        /// [임시] 종족 얼굴 아이콘 (16×16 도트, 코드로 그림).
        /// 사람 = 갈색 머리, 오크 = 초록 피부와 엄니, 엘프 = 뾰족한 귀와 금발
        /// </summary>
        public static Sprite Face(Tribe t)
        {
            _faces ??= new Sprite[3];
            int i = (int)t;
            if (_faces[i]) return _faces[i];
            string[] rows = t switch
            {
                Tribe.Orc => new[]
                {
                    "................",
                    "................",
                    ".....oooooo.....",
                    "....oggggggo....",
                    "...oggggggggo...",
                    "...ogkkggkkgo...",
                    "...ogkwggkwgo...",
                    "...oggggggggo...",
                    "...oggggggggo...",
                    "...ogwggggwgo...",
                    "...ogwddddwgo...",
                    "....oggggggo....",
                    ".....oooooo.....",
                    "................",
                    "................",
                    "................"
                },
                Tribe.Elf => new[]
                {
                    "................",
                    ".....oooooo.....",
                    "....oyyyyyyo....",
                    "...oyyyyyyyyo...",
                    "o..oyssssssyo..o",
                    "so.osssssssso.os",
                    ".sosskssskssos..",
                    "..oss.sssssso...",
                    "...osssssssso...",
                    "...osssssssso...",
                    "....ossddsso....",
                    ".....oooooo.....",
                    "................",
                    "................",
                    "................",
                    "................"
                },
                _ => new[]
                {
                    "................",
                    ".....oooooo.....",
                    "....obbbbbbo....",
                    "...obbbbbbbbo...",
                    "...obssssssbo...",
                    "...osssssssso...",
                    "...osskssksso...",
                    "...osssssssso...",
                    "...osssssssso...",
                    "...ossrssrsso...",
                    "....ossrrsso....",
                    ".....oooooo.....",
                    "................",
                    "................",
                    "................",
                    "................"
                }
            };

            var skin = Skin(t);
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 16; y++)
            {
                string row = rows[15 - y].PadRight(16, '.');
                for (int x = 0; x < 16; x++)
                {
                    Color c = row[x] switch
                    {
                        'o' => new Color(0.12f, 0.1f, 0.1f),
                        's' => skin,
                        'g' => skin,
                        'k' => new Color(0.1f, 0.08f, 0.08f),
                        'w' => new Color(0.97f, 0.95f, 0.88f),
                        'd' => new Color(0.3f, 0.12f, 0.1f),
                        'r' => new Color(0.75f, 0.35f, 0.3f),
                        'b' => new Color(0.45f, 0.28f, 0.15f),
                        'y' => new Color(0.98f, 0.86f, 0.45f),
                        _ => Color.clear
                    };
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            _faces[i] = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
            _faces[i].name = $"face_{t}";
            return _faces[i];
        }
    }
}
