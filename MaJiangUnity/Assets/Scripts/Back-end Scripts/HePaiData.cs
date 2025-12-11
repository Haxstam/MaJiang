using System.Collections.Generic;

namespace MaJiangLib
{
    /// <summary>
    /// 和牌时的类,是判断番数,自摸,记录时的类
    /// </summary>
    public class HePaiData
    {
        public HePaiData(ShouPai shouPai, Pai singlePai, bool isIsumo, List<Group> group)
        {
            PlayerNumber = shouPai.PlayerNumber;
            ShouPai = shouPai;
            SinglePai = singlePai;
            IsIsumo = isIsumo;
            Groups = group;
        }
        /// <summary>
        /// 副露用构造器
        /// </summary>
        /// <param name="mainMatchControl"></param>
        /// <param name="player"></param>
        /// <param name="group"></param>
        public HePaiData(int player, MainMatchControl mainMatchControl, List<Group> group)
        {
            ShouPai = mainMatchControl.PlayerList[player].ShouPai;
            SinglePai = mainMatchControl.CurrentPai;
            IsIsumo = false;
            Groups = group;
        }
        /// <summary>
        /// 和牌者的序号
        /// </summary>
        public int PlayerNumber { get; set; }
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
        public bool IsClosedHand => ShouPai.IsClosedHand;
        public List<Group> Groups { get; set; }
    }
}
