using System;
using System.Collections.Generic;
using System.Linq;

namespace MaJiangLib
{
    /// <summary>
    /// 手牌的类,因为副露等原因设定为类
    /// </summary>
    public class HandTile : ICloneable
    {
        public HandTile(int playerNumber)
        {
            PlayerNumber = playerNumber;
            HandTileList = new();
            OpenTileList = new();
            NorthDoraCount = 0;
        }

        /// <summary>
        /// TODO 标记该手牌所属的玩家,但此值在Player里也有存储且两者一定相等,考虑删去一个
        /// </summary>
        public int PlayerNumber { get; set; }
        /// <summary>
        /// 手牌列表,不超过13枚
        /// </summary>
        public List<Tile> HandTileList { get; set; }
        /// <summary>
        /// 摸牌时所摸到的单张牌
        /// </summary>
        public Tile SingleTile { get; set; }
        /// <summary>
        /// 副露牌列表,按面子分组,不超过4组
        /// </summary>
        public List<Group> OpenTileList { get; set; }
        /// <summary>
        /// 北宝牌计数,仅适用于三麻
        /// </summary>
        public int NorthDoraCount { get; set; }
        /// <summary>
        /// 返回手牌是否门清,仅当副露面子计数为0或副露中仅暗杠时为门清
        /// </summary>
        public bool IsClosedHand
        {
            get { return OpenTileList.Count <= 0 || OpenTileList.All(group => group.GroupType == GroupType.ClosedKan); }
        }

        public object Clone()
        {
            HandTile handTile = new HandTile(PlayerNumber);
            handTile.PlayerNumber = PlayerNumber;
            handTile.HandTileList = HandTileList.ToList();
            handTile.OpenTileList = OpenTileList.Select(StateCopy.Group).ToList();
            handTile.SingleTile = SingleTile;
            handTile.NorthDoraCount = NorthDoraCount;
            return handTile;
        }
    }
}
