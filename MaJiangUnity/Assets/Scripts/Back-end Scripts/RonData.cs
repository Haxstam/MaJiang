using System.Collections.Generic;

namespace MaJiangLib
{
    /// <summary>
    /// 和牌时的类,是判断番数,自摸,记录时的类
    /// </summary>
    public class RonData
    {
        public RonData(HandTile handTile, Tile singleTile, bool isTsumo, List<Group> group)
        {
            PlayerNumber = handTile.PlayerNumber;
            RonTile = handTile;
            SingleTile = singleTile;
            IsTsumo = isTsumo;
            Groups = group;
        }
        /// <summary>
        /// 副露用构造器
        /// </summary>
        /// <param name="mainMatchControl"></param>
        /// <param name="player"></param>
        /// <param name="group"></param>
        public RonData(int player, MainMatchControl mainMatchControl, List<Group> group)
        {
            RonTile = mainMatchControl.PlayerList[player].PlayerHandTile;
            SingleTile = mainMatchControl.CurrentTile;
            IsTsumo = false;
            Groups = group;
        }
        /// <summary>
        /// 和牌者的序号
        /// </summary>
        public int PlayerNumber { get; set; }
        /// <summary>
        /// 手牌列表
        /// </summary>
        public HandTile RonTile { get; set; }
        /// <summary>
        /// 和牌时的第十四张牌
        /// </summary>
        public Tile SingleTile { get; set; }
        /// <summary>
        /// 是自摸
        /// </summary>
        public bool IsTsumo { get; set; }
        /// <summary>
        /// 是门前清
        /// </summary>
        public bool IsClosedHand => RonTile.IsClosedHand;
        public List<Group> Groups { get; set; }
    }
}
