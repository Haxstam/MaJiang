using System;
using System.Collections.Generic;
using System.Linq;
using static MaJiangLib.GlobalFunction;

namespace MaJiangLib
{
    /// <summary>
    /// 手牌的类,因为副露等原因设定为类
    /// </summary>
    public class ShouPai : ICloneable, IByteable<ShouPai>
    {
        public ShouPai()
        {

            ShouPaiList = new();
            FuluPaiList = new();
        }

        /// <summary>
        /// 标记该手牌所属的玩家
        /// </summary>
        public int PlayerNumber { get; set; }
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
        public const int byteSize = 96;
        public int ByteSize { get => byteSize; }
        public object Clone()
        {
            ShouPai shouPai = new ShouPai();
            shouPai.PlayerNumber = PlayerNumber;
            shouPai.ShouPaiList = ShouPaiList.ToList();
            shouPai.FuluPaiList = FuluPaiList.ToList();
            shouPai.NorthDoraCount = NorthDoraCount;
            return shouPai;
        }
        public static implicit operator byte[](ShouPai shouPai)
        {
            // 1 byte Player + 1 byte NorthDoraCount + 10 bytes 留空 + 26(13*2) bytes ShouPaiList + 2 bytes SinglePai + 56(4*14) bytes FuluPaiList = 96 bytes
            // 手牌类的序列化大小暂时先固定,考虑压缩
            Span<byte> mainBytes = new byte[96];
            mainBytes[0] = (byte)shouPai.PlayerNumber;
            mainBytes[1] = (byte)shouPai.NorthDoraCount;
            ReplaceBytes(mainBytes, ListToBytes(shouPai.ShouPaiList), 12);
            ReplaceBytes(mainBytes, shouPai.SinglePai, 38);
            ReplaceBytes(mainBytes, ListToBytes(shouPai.FuluPaiList), 40);
            return mainBytes.ToArray();
        }
        public byte[] GetBytes() => this;
        public static ShouPai StaticBytesTo(byte[] bytes, int index = 0)
        {
            byte[] shortByte = new byte[96];
            Array.Copy(bytes, index, shortByte, 0, 96);
            int player = shortByte[0];
            int northDoraCount = shortByte[1];
            List<Pai> shouPaiList = BytesToList<Pai>(shortByte, 12, 13);
            Pai singlePai = Pai.StaticBytesTo(shortByte, 38);
            List<Group> fuluPaiList = BytesToList<Group>(shortByte, 40, 4);
            ShouPai shouPai = new ShouPai();
            shouPai.NorthDoraCount = northDoraCount;
            shouPai.PlayerNumber = player;
            shouPai.ShouPaiList= shouPaiList;
            shouPai.FuluPaiList= fuluPaiList;
            shouPai.SinglePai = singlePai;
            return shouPai;
        }
        public ShouPai BytesTo(byte[] bytes, int index =  0) => StaticBytesTo(bytes, index);
    }
}
