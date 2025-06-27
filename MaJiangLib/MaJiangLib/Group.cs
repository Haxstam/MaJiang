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
    }

}

