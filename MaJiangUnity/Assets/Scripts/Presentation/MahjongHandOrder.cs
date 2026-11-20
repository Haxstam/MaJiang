using System.Collections.Generic;
using System.Linq;
using MaJiangLib;

namespace MaJiang.Presentation
{
    /// <summary>只排序显示索引，保留投影原顺序和点击身份；摸牌仍由视图单独放在末尾。</summary>
    public static class MahjongHandOrder
    {
        public static int[] SortedIndices(IReadOnlyList<Tile> tiles)
            => Enumerable.Range(0, tiles.Count).OrderBy(index => tiles[index]).ToArray();
    }
}
