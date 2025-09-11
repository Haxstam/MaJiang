using System;
using System.Collections.Generic;
using System.Linq;
using static MaJiangLib.GlobalFunction;

namespace MaJiangLib
{
    /// <summary>
    /// 手牌的类,因为副露等原因设定为类
    /// </summary>
    public class ShouPai : ICloneable, IByteable
    {
        public ShouPai()
        {

            ShouPaiList = new();
            FuluPaiList = new();
        }

        /// <summary>
        /// 标记该手牌所属的玩家
        /// </summary>
        public int Player { get; set; }
        /// <summary>
        /// 手牌列表,不超过13枚
        /// </summary>
        public List<Pai> ShouPaiList { get; set; }
        /// <summary>
        /// 摸牌时所摸到的单张牌
        /// </summary>
        public Pai SinglePai { get; set; }
        /// <summary>
        /// 副露牌列表,按面子分组,不超过4组
        /// </summary>
        public List<Group> FuluPaiList { get; set; }
        /// <summary>
        /// 北宝牌计数,仅适用于三麻
        /// </summary>
        public int NorthDoraCount { get; set; }
        /// <summary>
        /// 返回手牌是否门清,仅当副露面子计数为0或副露中仅暗杠时为门清
        /// </summary>
        public bool IsClosedHand
        {
            get { return FuluPaiList.Count <= 0 || FuluPaiList.All(group => group.GroupType == GroupType.AnKang); }
        }

        public int ByteSize { get; set; } = 160;
        public object Clone()
        {
            ShouPai shouPai = new ShouPai();
            shouPai.Player = Player;
            shouPai.ShouPaiList = ShouPaiList.ToList();
            shouPai.FuluPaiList = FuluPaiList.ToList();
            shouPai.NorthDoraCount = NorthDoraCount;
            return shouPai;
        }
        public static implicit operator byte[](ShouPai shouPai)
        {
            // 1 byte Player + 1 byte NorthDoraCount + 6 bytes 留空 + 52(13*4) bytes ShouPaiList + 4 bytes SinglePai + 96(4*24) bytes FuluPaiList = 160 bytes
            // 手牌类的序列化大小暂时先固定,考虑压缩
            byte[] mainBytes = new byte[160];
            mainBytes[0] = (byte)shouPai.Player;
            mainBytes[1] = (byte)shouPai.NorthDoraCount;
            ReplaceBytes(mainBytes, ListToBytes(shouPai.ShouPaiList), 8);
            ReplaceBytes(mainBytes, shouPai.SinglePai, 60);
            ReplaceBytes(mainBytes, ListToBytes(shouPai.FuluPaiList), 64);
            return mainBytes;
        }
        public byte[] GetBytes() => this;
    }
}
