using UnityEditor;
using UnityEngine;

namespace DragonRecipe.EditorTools
{
    /// <summary>
    /// Art/Icons 폴더의 PNG는 자동으로 선명한 픽셀 스프라이트로 임포트한다
    /// (Point 필터, 압축 없음, 밉맵 없음).
    /// </summary>
    public class IconImportSettings : AssetPostprocessor
    {
        public const string Folder = "Assets/_Project/Art/Icons";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            Configure((TextureImporter)assetImporter);
        }

        static bool Configure(TextureImporter ti)
        {
            bool changed = ti.textureType != TextureImporterType.Sprite
                           || ti.spriteImportMode != SpriteImportMode.Single
                           || ti.filterMode != FilterMode.Point
                           || ti.textureCompression != TextureImporterCompression.Uncompressed
                           || ti.mipmapEnabled
                           || !Mathf.Approximately(ti.spritePixelsPerUnit, 16f);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.spritePixelsPerUnit = 16f;
            return changed;
        }

        /// <summary>이미 임포트된 텍스처에 설정을 강제 적용 (씬 빌더용)</summary>
        public static void Apply(string path, bool reimport)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && Configure(ti) && reimport)
                ti.SaveAndReimport();
        }
    }
}
