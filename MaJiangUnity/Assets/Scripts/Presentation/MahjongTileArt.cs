using MaJiangLib;
using UnityEngine;

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
        public Font Font;
        private static Font systemFont;

        public Texture2D Face(Tile tile) => tile == null ? null : tile.IsRedDora ? RedFives[(int)tile.Color] : Faces[HandAnalysis.Face(tile)];
        public Font GetFont()
        {
            if (Font != null) { return Font; }
            if (systemFont == null)
            {
                systemFont = UnityEngine.Font.CreateDynamicFontFromOSFont(new[] { "DengXian", "Microsoft YaHei", "SimHei", "Arial" }, 32);
            }
            return systemFont;
        }
    }
}
