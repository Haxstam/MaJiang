using System;
using System.Collections.Generic;
namespace MaJiangLib
{
    /// <summary>
    /// 面子分组,定义了最基础的面子/雀头的属性
    /// </summary>
    public class Group
    {
        // TODO 目前有一个优化方向是:只存储该面子的第一张牌,副露牌,副露向等

        /// <summary>
        /// 非副露下的构造器,需要面子类型,花色,面子的牌列表
        /// </summary>
        /// <param name="groupType">面子类型</param>
        /// <param name="color">面子花色</param>
        /// <param name="tiles">面子的牌</param>
        public Group(GroupType groupType, Color color, List<Tile> tiles)
        {
            GroupType = groupType;
            Color = color;
            Tiles = new List<Tile>(tiles);
        }
        /// <summary>
        /// 副露的构造器,需要面子类型,花色,鸣牌来源及组成面子的手里的牌和别家的牌,在创建时Tiles不包含所鸣之牌,创建后将副露牌添加进Tiles的末尾
        /// </summary>
        /// <param name="groupType">面子类型</param>
        /// <param name="color">花色</param>
        /// <param name="tiles">自己手中的牌的列表,输入要求不包含所鸣牌</param>
        /// <param name="player">所鸣牌的来源</param>
        /// <param name="singleTile">所鸣的牌</param>
        public Group(GroupType groupType, Color color, List<Tile> tiles, int player, Tile singleTile)
        {
            GroupType = groupType;
            Color = color;
            Tiles = new List<Tile>(tiles);
            Tiles.Add(singleTile);
            SingleTile = singleTile;
            FuluSource = player;
        }
        /// <summary>
        /// 面子类型
        /// </summary>
        public GroupType GroupType { get; set; }
        /// <summary>
        /// 组成面子的牌的列表
        /// </summary>
        public List<Tile> Tiles { get; set; }
        /// <summary>
        /// 面子花色
        /// </summary>
        public Color Color { get; set; }
        /// <summary>
        /// 该面子的来源,门清的面子来源均为自己,如果是副露,则为其所鸣牌的来源玩家
        /// </summary>
        public int FuluSource { get; set; }
        /// <summary>
        /// 所鸣的单牌,仅用于副露下的面子
        /// </summary>
        public Tile SingleTile { get; set; }
        /// <summary>
        /// 在役种判断中对刻子的判断较繁琐,通过该属性简化,当为刻子或杠子时,返回True
        /// </summary>
        public bool IsTriple
        {
            get
            {
                return GroupType == GroupType.Triple || GroupType == GroupType.OpenTriple || GroupType == GroupType.ClosedKan || GroupType == GroupType.OpenKan || GroupType == GroupType.AddedKan;
            }
        }
        /// <summary>
        /// 当该面子是副露面子(明顺子,明刻,明杠,加杠)时,返回True
        /// </summary>
        public bool IsOpen 
        { get
            {
                return GroupType == GroupType.OpenStraight || GroupType == GroupType.OpenTriple || GroupType == GroupType.OpenKan || GroupType == GroupType.AddedKan; 
            }
        }

        public override string ToString()
        {
            string str = "";
            if (GroupType == GroupType.Triple)
            {
                str = str + Tiles[0].ToString() + Tiles[0].ToString() + Tiles[0].ToString();
            }
            else if (GroupType == GroupType.Pair)
            {
                str = str + Tiles[0].ToString() + Tiles[0].ToString();
            }
            else
            {
                str = str + Tiles[0].ToString() + Tiles[1].ToString() + Tiles[2].ToString();
            }
            return str;
        }
        /// <summary>
        /// "=="重载,仅当两组面子完全相同,即花色,牌型和序号都相同时成立,对于吃牌,已知鸣牌两张中较小的那张和所鸣牌即可确定鸣牌
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator ==(Group a, Group b)
        {
            if (ReferenceEquals(a, b)) { return true; }
            if (a is null || b is null) { return false; }
            return a.Color == b.Color && a.GroupType == b.GroupType
                && (a.Tiles.Count == 0 ? b.Tiles.Count == 0 : b.Tiles.Count > 0 && a.Tiles[0] == b.Tiles[0])
                && a.SingleTile == b.SingleTile;
        }
        /// <summary>
        /// "!="重载
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator !=(Group a, Group b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            return obj is Group group && group == this;
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(GroupType, Color, Tiles.Count == 0 ? null : Tiles[0], SingleTile);
        }
    }

}

