using System;
using MaJiangLib;

namespace MaJiang.Mortal
{
    /// <summary>麻将领域牌与 mjai 牌名之间的精确转换；赤五不会合并为普通五。</summary>
    public static class MjaiTiles
    {
        private static readonly string[] Honors = { "E", "S", "W", "N", "P", "F", "C" };

        /// <summary>null 仅表示协议中的未知牌。</summary>
        public static string Format(Tile tile)
        {
            if (tile is null) { return "?"; }
            if (tile.Color == Color.Honor && tile.Number >= 1 && tile.Number <= 7 && !tile.IsRedDora) { return Honors[tile.Number - 1]; }
            if (tile.Color < Color.Wans || tile.Color > Color.Bamboo || tile.Number < 1 || tile.Number > 9 || (tile.IsRedDora && tile.Number != 5))
            {
                throw new ArgumentException("Invalid physical tile.", nameof(tile));
            }
            return tile.Number.ToString() + "mps"[(int)tile.Color] + (tile.IsRedDora ? "r" : "");
        }

        /// <summary>解析真实操作用牌；禁止模型以未知牌 ? 提交操作。</summary>
        public static Tile Parse(string text)
        {
            int honor = Array.IndexOf(Honors, text);
            if (honor >= 0) { return new Tile(Color.Honor, honor + 1); }
            if (text == null || (text.Length != 2 && text.Length != 3) || text[0] < '1' || text[0] > '9')
            {
                throw new FormatException("Invalid mjai tile: " + text);
            }
            int suit = "mps".IndexOf(text[1]);
            bool red = text.Length == 3;
            if (suit < 0 || (red && (text[0] != '5' || text[2] != 'r'))) { throw new FormatException("Invalid mjai tile: " + text); }
            return new Tile((Color)suit, text[0] - '0', red);
        }
    }
}
