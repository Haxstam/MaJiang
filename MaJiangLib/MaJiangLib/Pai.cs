using System;

namespace MaJiangLib
{
    /// <summary>
    /// 牌的类,存储牌的信息
    /// </summary>
    public class Pai : IComparable<Pai>
    {
        /// <summary>
        /// 设定牌类型,默认不是红宝牌
        /// </summary>
        /// <param name="color"></param>
        /// <param name="num"></param>
        public Pai(Color color, int num)
        {
            Color = color;
            Number = num;
            IsRedDora = false;
        }
        /// <summary>
        /// 牌的全设定,需要指定红宝牌
        /// </summary>
        /// <param name="color"></param>
        /// <param name="num"></param>
        /// <param name="isRedDora"></param>
        public Pai(Color color, int num, bool isRedDora)
        {
            Color = color;
            Number = num;
            IsRedDora = isRedDora;
        }
        /// <summary>
        /// 牌的花色
        /// </summary>
        public Color Color { get; set; }
        /// <summary>
        /// 牌的数字,字牌顺序为东-1,南-2,西-3,北-4,白-5,发-6,中-7
        /// --(若为8则为国士无双十三面的标志)
        /// </summary>
        public int Number { get; set; }
        public bool IsRedDora { get; set; }
        /// <summary>
        /// 比较方法,先比较花色(万<筒<索<字牌),再比较数字(比大小),最后比较红宝牌(红宝牌更大)
        /// </summary>
        /// <param name="other">所比较的牌</param>
        /// <returns></returns>
        public int CompareTo(Pai other)
        {
            if (Color > other.Color)
            {
                return 1;
            }
            else if (Color < other.Color)
            {
                return -1;
            }
            else
            {
                if (Number == other.Number)
                {
                    if (IsRedDora)
                    {
                        return 1;
                    }
                    else if (other.IsRedDora)
                    {
                        return -1;
                    }
                    else
                    {
                        return 0;
                    }
                }
                else
                {
                    return Number - other.Number;
                }

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
            str = colorStr + Number.ToString();
            return str;
        }
        /// <summary>
        /// 判断两张牌是否相同,注意:不考虑红宝牌
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator ==(Pai a, Pai b)
        {
            if (a.Number == b.Number && a.Color == b.Color)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// 判断两张牌是否相同,注意:不考虑红宝牌
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator !=(Pai a, Pai b)
        {
            if (a.Number == b.Number && a.Color == b.Color)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }
    /// <summary>
    /// 牌花色的枚举:万,筒,索,字牌
    /// </summary>
    public enum Color
    {
        Wans,
        Tungs,
        Bamboo,
        Honor,
    }
}
