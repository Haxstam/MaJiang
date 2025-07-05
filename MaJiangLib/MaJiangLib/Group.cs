using System.Collections.Generic;

namespace MaJiangLib
{
    /// <summary>
    /// 面子分组，由分组类型设定为Straight顺子,Triple刻子,Pair对子/雀头,Numbers存储组成的数字
    /// </summary>
    public class Group
    {
        /// <summary>
        /// 考虑花色的构造器
        /// </summary>
        /// <param name="groupType">面子类型</param>
        /// <param name="color">面子花色</param>
        /// <param name="nums">面子数字</param>
        public Group(GroupType groupType, Color color, List<Pai> pais)
        {
            GroupType = groupType;
            Color = color;
            Pais = pais;
        }
        public GroupType GroupType { get; set; }
        public List<Pai> Pais { get; set; }
        public Color Color { get; set; }
        /// <summary>
        /// 在役种判断中对刻子的判断较繁琐,通过该属性简化,当为刻子或杠子时,返回True
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
            if (GroupType == GroupType.Triple)
            {
                str = str + Pais[0].ToString() + Pais[0].ToString() + Pais[0].ToString();
            }
            else if (GroupType == GroupType.Pair)
            {
                str = str + Pais[0].ToString() + Pais[0].ToString();
            }
            else
            {
                str = str + Pais[0].ToString() + Pais[1].ToString() + Pais[2].ToString();
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
            if (a.Color == b.Color && a.GroupType == b.GroupType && a.Pais[0] == b.Pais[0])
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
            if (a.Color == b.Color && a.GroupType == b.GroupType && a.Pais[0] == b.Pais[0])
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

