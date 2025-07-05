using System.Collections.Generic;

namespace MaJiangLib
{
    /// <summary>
    /// 和牌时的类,是判断番数,自摸,记录时的类
    /// </summary>
    public class HePaiData
    {
        public HePaiData(int player, ShouPai shouPai, Pai singlePai, bool isIsumo, bool isClosedHand, List<Group> group)
        {
            Player = player;
            ShouPai = shouPai;
            SinglePai = singlePai;
            IsIsumo = isIsumo;
            IsClosedHand = isClosedHand;
            Groups = group;
        }
        /// <summary>
        /// 和牌者的序号
        /// </summary>
        public int Player { get; set; }
        /// <summary>
        /// 手牌列表
        /// </summary>
        public ShouPai ShouPai { get; set; }
        /// <summary>
        /// 和牌时的第十四张牌
        /// </summary>
        public Pai SinglePai { get; set; }
        /// <summary>
        /// 是自摸
        /// </summary>
        public bool IsIsumo { get; set; }
        /// <summary>
        /// 是门前清
        /// </summary>
        public bool IsClosedHand { get; set; }
        public List<Group> Groups { get; set; }
    }
}
