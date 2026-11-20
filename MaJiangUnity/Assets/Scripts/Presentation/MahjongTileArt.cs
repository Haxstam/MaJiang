using MaJiangLib;
using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace MaJiang.Presentation
{
    /// <summary>直接引用作者提供的原始PNG，不复制或改写Images目录。Font可在未来替换为项目字体。</summary>
    [CreateAssetMenu(menuName = "Mahjong/Tile Art")]
    public sealed class MahjongTileArt : ScriptableObject
    {
        public Texture2D Front;
        public Texture2D Back;
        public Texture2D[] Faces = new Texture2D[34];
        public Texture2D[] RedFives = new Texture2D[3];
        public Sprite DiscardFrame;
        public TMP_FontAsset Font;
        public Font SourceFont;
        private TMP_FontAsset runtimeFont;
        private readonly Dictionary<Texture2D, Sprite> sprites = new();

        public Sprite SpriteOf(Texture2D texture)
        {
            if (texture == null) { return null; }
            if (!sprites.TryGetValue(texture, out Sprite sprite) || sprite == null)
            {
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                sprite.name = "麻将牌面"; sprites[texture] = sprite;
            }
            return sprite;
        }

        public Texture2D Face(Tile tile) => tile == null ? null : tile.IsRedDora ? RedFives[(int)tile.Color] : Faces[HandAnalysis.Face(tile)];
        public TMP_FontAsset GetFont()
        {
            if (Font != null) { return Font; }
            if (runtimeFont == null && SourceFont != null)
            {
                runtimeFont = TMP_FontAsset.CreateFontAsset(SourceFont);
                runtimeFont.name = "思源黑体运行时字体";
                runtimeFont.isMultiAtlasTexturesEnabled = true;
            }
            return runtimeFont != null ? runtimeFont : TMP_Settings.defaultFontAsset;
        }
    }
}
