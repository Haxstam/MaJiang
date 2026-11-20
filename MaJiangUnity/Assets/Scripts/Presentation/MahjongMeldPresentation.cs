using System;
using System.Collections.Generic;
using System.Linq;
using MaJiangLib;

namespace MaJiang.Presentation
{
    public enum ClaimDirection { Left, Top, Right }

    /// <summary>只映射副露牌位，不改变面子或来源。明杠与加杠采用相同组结构。</summary>
    public sealed class MahjongMeldPresentation
    {
        public ClaimDirection Direction;
        public Tile First, Second, Claimed, Kan;
        public bool ShowKan;
        public bool ConcealOuter;

        public static MahjongMeldPresentation From(Group group, int owner)
        {
            if (group == null) { throw new ArgumentNullException(nameof(group)); }
            bool closed = group.GroupType == GroupType.ClosedKan;
            bool kan = closed || group.GroupType == GroupType.OpenKan || group.GroupType == GroupType.AddedKan;
            if (owner < 0 || owner > 3 || group.Tiles.Count != (kan ? 4 : 3)) { throw new ArgumentException("Invalid meld display data."); }
            List<Tile> own = group.Tiles.ToList();
            MahjongMeldPresentation result = new() { ShowKan = kan, ConcealOuter = closed };
            if (closed)
            {
                result.Direction = ClaimDirection.Top;
                Tile fourth = own.FirstOrDefault(tile => tile.IsRedDora) ?? own.Last();
                own.RemoveAt(own.FindIndex(tile => ReferenceEquals(tile, fourth)));
                own = own.OrderBy(tile => tile).ToList();
                result.First = own[0]; result.Second = own[1]; result.Claimed = own[2]; result.Kan = fourth;
                return result;
            }
            int offset = (group.FuluSource - owner + 4) % 4;
            result.Direction = offset switch { 3 => ClaimDirection.Left, 2 => ClaimDirection.Top, 1 => ClaimDirection.Right, _ => throw new ArgumentException("Invalid claim source.") };
            int claimed = own.FindLastIndex(tile => Tile.ExactlyEquals(tile, group.SingleTile));
            if (claimed < 0) { throw new ArgumentException("Claimed tile missing from meld."); }
            own.RemoveAt(claimed);
            own = group.GroupType == GroupType.OpenStraight ? own.OrderBy(tile => tile).ToList()
                : own.OrderByDescending(tile => tile.IsRedDora).ThenBy(tile => tile).ToList();
            result.First = own[0]; result.Second = own[1]; result.Claimed = group.SingleTile;
            if (kan)
            {
                result.Kan = own[2];
                // 直接明杠鸣来赤牌时放在上层；由碰升级的加杠保留原ClaimedCard。
                if (group.GroupType == GroupType.OpenKan && group.SingleTile.IsRedDora)
                {
                    result.Kan = group.SingleTile; result.Claimed = own[2];
                }
            }
            return result;
        }
    }
}
