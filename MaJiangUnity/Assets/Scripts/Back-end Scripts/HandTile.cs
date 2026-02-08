using System;
using System.Collections.Generic;
using System.Linq;
using static MaJiangLib.GlobalFunction;

namespace MaJiangLib
{
    /// <summary>
    /// 手牌的类,因为副露等原因设定为类
    /// </summary>
    public class HandTile : ICloneable, IByteable<HandTile>
    {
        public HandTile()
        {

            HandTileList = new();
            OpenTileList = new();
        }

        /// <summary>
        /// 标记该手牌所属的玩家
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
            get { return OpenTileList.Count <= 0 || OpenTileList.All(group => group.GroupType == GroupType.AnKang); }
        }
        public const int byteSize = 96;
        public int ByteSize { get => byteSize; }
        public object Clone()
        {
            HandTile handTile = new HandTile();
            handTile.PlayerNumber = PlayerNumber;
            handTile.HandTileList = HandTileList.ToList();
            handTile.OpenTileList = OpenTileList.ToList();
            handTile.NorthDoraCount = NorthDoraCount;
            return handTile;
        }
        public static implicit operator byte[](HandTile handTile)
        {
            // 1 byte Player + 1 byte NorthDoraCount + 10 bytes 留空 + 26(13*2) bytes HandTileList + 2 bytes SingleTile + 56(4*14) bytes OpenTileList = 96 bytes
            // 手牌类的序列化大小暂时先固定,考虑压缩
            Span<byte> mainBytes = new byte[96];
            mainBytes[0] = (byte)handTile.PlayerNumber;
            mainBytes[1] = (byte)handTile.NorthDoraCount;
            ReplaceBytes(mainBytes, ListToBytes(handTile.HandTileList), 12);
            ReplaceBytes(mainBytes, handTile.SingleTile, 38);
            ReplaceBytes(mainBytes, ListToBytes(handTile.OpenTileList), 40);
            return mainBytes.ToArray();
        }
        public byte[] GetBytes() => this;
        public static HandTile StaticBytesTo(byte[] bytes, int index = 0)
        {
            byte[] shortByte = new byte[96];
            Array.Copy(bytes, index, shortByte, 0, 96);
            int player = shortByte[0];
            int northDoraCount = shortByte[1];
            List<Tile> handTileList = BytesToList<Tile>(shortByte, 12, 13);
            Tile singleTile = Tile.StaticBytesTo(shortByte, 38);
            List<Group> openTileList = BytesToList<Group>(shortByte, 40, 4);
            HandTile handTile = new HandTile();
            handTile.NorthDoraCount = northDoraCount;
            handTile.PlayerNumber = player;
            handTile.HandTileList= handTileList;
            handTile.OpenTileList= openTileList;
            handTile.SingleTile = singleTile;
            return handTile;
        }
        public HandTile BytesTo(byte[] bytes, int index =  0) => StaticBytesTo(bytes, index);
    }
}
