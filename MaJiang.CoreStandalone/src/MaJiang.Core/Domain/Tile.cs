using System;
namespace MaJiangLib
{
    /// <summary>
    /// 牌的类,存储牌的信息
    /// </summary>
    public class Tile : IComparable<Tile>
    {
        // 牌这个类是全程序基础,考虑让其足够灵活

        /// <summary>
        /// 设定牌类型,默认不是红宝牌
        /// </summary>
        /// <param name="color"></param>
        /// <param name="num"></param>
        public Tile(Color color, int num)
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
        public Tile(Color color, int num, bool isRedDora)
        {
            Color = color;
            Number = num;
            IsRedDora = isRedDora;
        }
        /// <summary>
        /// 牌的花色
        /// </summary>
        public Color Color { get; private set; }
        /// <summary>
        /// 牌的数字,不可为0,字牌顺序为东-1,南-2,西-3,北-4,白-5,发-6,中-7
        /// </summary>
        public int Number { get; private set; }
        /// <summary>
        /// 标记是否为红宝牌
        /// </summary>
        public bool IsRedDora { get; private set; }
        /// <summary>
        /// 比较方法,先比较花色(万<筒<索<字牌),再比较数字(比大小),最后比较红宝牌(红宝牌更大)
        /// </summary>
        /// <param name="other">所比较的牌</param>
        /// <returns></returns>
        public int CompareTo(Tile other)
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
                    return IsRedDora.CompareTo(other.IsRedDora);
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
            str = (IsRedDora ? 0 : Number).ToString() + colorStr;
            return str;
        }
        /// <summary>
        /// 判断两张牌是否相同,注意:不考虑红宝牌
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator ==(Tile a, Tile b)
        {
            // 避免访问null对象的成员
            if ((a as object) == null && (b as object) == null)
            {
                return true;
            }
            else if ((a as object) == null || (b as object) == null)
            {
                return false;
            }
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
        public static bool operator !=(Tile a, Tile b)
        {
            return !(a == b);
        }
        /// <summary>
        /// 比较牌种及赤牌标记，不区分同种牌的不同实体；用于精确选择舍牌或鸣牌用牌。
        /// </summary>
        public static bool ExactlyEquals(Tile left, Tile right)
        {
            return left == right && (left is null || left.IsRedDora == right.IsRedDora);
        }
        /// <summary>
        /// 从指定字符串中某索引处转换为牌的方法,需求两个字符
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static Tile BytesTo(string str, int index = 0)
        {
            // 或许可以再语法糖一点?

            if (str.Length < 2)
            {
                throw new Exception("所提供的字符串过短");
            }
            else
            {
                int num = (int)char.GetNumericValue(str[index]);
                Color color;
                switch (str[index + 1])
                {
                    case 'w':
                        color = Color.Wans;
                        break;
                    case 'p':
                        color = Color.Tungs;
                        break;
                    case 's':
                        color = Color.Bamboo;
                        break;
                    case 'z':
                        color = Color.Honor;
                        break;
                    default:
                        throw new Exception("转换时错误:出现意料外花色类型");
                }
                if (num == 0)
                {
                    return new(color, 5, true);
                }
                else
                {
                    return new(color, num, false);
                }
            }
        }
        /// <summary>
        /// ==包装,不考虑是否为红宝牌
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            Tile Card = obj as Tile;
            if (Card != null)
            {
                return Card == this;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// 与牌型相等语义一致，不区分赤牌，保证字典与集合能正确查找同种牌。
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Color, Number);
        }
    }
    /// <summary>
    /// 牌花色的枚举:万,筒,索,字牌
    /// </summary>
    public enum Color : byte
    {
        Wans,
        Tungs,
        Bamboo,
        Honor,
    }
}
