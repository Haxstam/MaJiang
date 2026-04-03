using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering;

namespace MaJiangLib
{
    /// <summary>
    /// 和牌时的类,是判断番数,自摸,记录时的类
    /// </summary>
    public class RonData
    {
        public RonData(int playerNumber, Tile singleTile, bool isTsumo, List<Group> group)
        {
            PlayerNumber = playerNumber;
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
        public RonData(int player, MainMatchControl mainMatchControl, List<Group> group, bool isTsumo)
        {
            SingleTile = mainMatchControl.CurrentTile;
            IsTsumo = isTsumo;
            IsRiichi = mainMatchControl.IsRiichi[player];
            Groups = group;
            DoraPointerList = mainMatchControl.DoraPointerList;
            if (IsRiichi)
            {
                // 仅当立直和牌时,才会掀开里宝牌指示牌
                UraDoraPointerList = mainMatchControl.UraDoraPointerList;
            }
            KitaCount = mainMatchControl.KitaCount[player];
        }
        /// <summary>
        /// 和牌者的序号
        /// </summary>
        public int PlayerNumber { get; set; }
        /// <summary>
        /// 和牌时的第十四张牌
        /// </summary>
        public Tile SingleTile { get; set; }
        /// <summary>
        /// 是自摸
        /// </summary>
        public bool IsTsumo { get; set; }
        /// <summary>
        /// 是立直
        /// </summary>
        public bool IsRiichi { get; set; }
        /// <summary>
        /// 宝牌指示牌
        /// </summary>
        public List<Tile> DoraPointerList { get; set; }
        /// <summary>
        /// 里宝牌指示牌
        /// </summary>
        public List<Tile> UraDoraPointerList { get; set; }
        /// <summary>
        /// 拔北宝牌数
        /// </summary>
        public int KitaCount { get; set; }
        /// <summary>
        /// 是门前清
        /// </summary>
        public bool IsClosedHand 
        {
            get { return !Groups.Any(g => g.IsOpen); }
        }
        public List<Group> Groups { get; set; }
    }
}
