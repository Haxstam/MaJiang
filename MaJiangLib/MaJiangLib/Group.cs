using System.Data.Common;
using System.Reflection.Emit;

namespace MaJiangLib
{
    /// <summary>
    /// 面子分组，由分组类型设定为Straight顺子,Triple刻子,Pair对子/雀头,Numbers存储组成的数字
    /// </summary>
    public class Group
    {
        /// <summary>
        /// 不考虑花色的构造器,用于DFS算法,为内部构造器
        /// </summary>
        /// <param name="groupType">面子类型</param>
        /// <param name="nums">面子数字</param>
        internal Group(GroupType groupType, params int[] nums)
        {
            GroupType = groupType;
            Numbers = nums;
        }
        /// <summary>
        /// 考虑花色的构造器
        /// </summary>
        /// <param name="groupType">面子类型</param>
        /// <param name="color">面子花色</param>
        /// <param name="nums">面子数字</param>
        public Group(GroupType groupType, Color color, params int[] nums)
        {
            GroupType = groupType;
            Color = color;
            Numbers = nums;
        }
        public GroupType GroupType { get; set; }
        public int[] Numbers { get; set; }
        public Color Color { get; set; }
        /// <summary>
        /// 在役种判断中对刻子的判断较繁琐,通过该属性简化
        /// </summary>
        /// <returns></returns>
        public bool IsTriple
        {
            get 
            {
                return GroupType == GroupType.Triple || GroupType == GroupType.MingTriple || GroupType == GroupType.AnKang || GroupType == GroupType.MingKang || GroupType == GroupType.JiaKang;
            }
        }

        public override string ToString()
        {
            string str = "";
            string colorStr = "";
            switch (Color)
            {
                case Color.Wans:
                    colorStr = "w";
                    break;
                case Color.Tungs:
                    colorStr = "p";
                    break;
                case Color.Bamboo:
                    colorStr = "s";
                    break;
                case Color.Honor:
                    colorStr = "z";
                    break;
                default:
                    break;
            }
            if (GroupType == GroupType.Triple)
            {
                str = str + colorStr + Numbers[0].ToString() + Numbers[0].ToString() + Numbers[0].ToString();
            }
            else if (GroupType == GroupType.Pair)
            {
                str = str + colorStr + Numbers[0].ToString() + Numbers[0].ToString();
            }
            else
            {
                str = str + colorStr + Numbers[0].ToString() + Numbers[1].ToString() + Numbers[2].ToString();
            }
            return str;
        }
        /// <summary>
        /// "=="重载,仅当两组面子完全相同,即花色,牌型和序号都相同时成立
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator ==(Group a, Group b)
        {
            if (a.Color == b.Color && a.GroupType == b.GroupType && a.Numbers[0] == b.Numbers[0])
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// "!="重载
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator !=(Group a, Group b)
        {
            if (a.Color == b.Color && a.GroupType == b.GroupType && a.Numbers[0] == b.Numbers[0])
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }

}

