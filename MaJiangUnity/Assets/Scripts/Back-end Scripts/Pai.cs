﻿using System;
using static MaJiangLib.GlobalFunction;
namespace MaJiangLib
{
    /// <summary>
    /// 牌的类,存储牌的信息
    /// </summary>
    public class Pai : IComparable<Pai>
    {
        // 牌这个类是全程序基础,考虑让其足够灵活

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
        /// 牌的数字,不可为0,字牌顺序为东-1,南-2,西-3,北-4,白-5,发-6,中-7
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
            if (IsRedDora)
            {
                Number = 0;
            }
            str = Number.ToString() + colorStr;
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
        public static bool operator !=(Pai a, Pai b)
        {
            return !(a == b);
        }
        /// <summary>
        /// 从牌到Byte[]的隐式转换,转换后占用空间为4 Bytes
        /// </summary>
        /// <param name="pai">目标牌</param>
        public static implicit operator byte[](Pai pai)
        {
            // 结构:牌序号 1 byte + 牌花色 1 byte + 是否为红宝牌 1 byte + 留空 1 byte
            byte[] MainBytes = new byte[4];
            MainBytes[0] = (byte)pai.Number;
            MainBytes[1] = (byte)pai.Color;
            ReplaceBytes(MainBytes, BitConverter.GetBytes(pai.IsRedDora), 2);
            return MainBytes;
        }
        /// <summary>
        /// 从Byte[]到牌的隐式转换,需要4Bytes的字节串
        /// </summary>
        /// <param name="bytes"></param>
        public static implicit operator Pai(byte[] bytes)
        {
            if (bytes.Length != 4)
            {
                throw new Exception($"字节串长度{bytes.Length}bytes不符合转换的4bytes要求");
            }
            else
            {
                if (bytes[0] == 0)
                {
                    // 序号为0,不存在该牌,判定为null
                    return null;
                }
                int num = bytes[0];
                Color color = (Color)bytes[1];
                bool isRedDora = BitConverter.ToBoolean(bytes, 2);
                return new(color, num, isRedDora);
            }
        }
        /// <summary>
        /// 从指定字节串中某索引处转换为牌的方法,为简易包装
        /// </summary>
        /// <param name="bytes">要操作的字符串</param>
        /// <param name="index">位置索引</param>
        /// <returns>返回所转换而成的牌</returns>
        public static Pai GetPai(byte[] bytes, int index = 0)
        {
            byte[] shortBytes = new byte[4];
            Array.Copy(bytes, index, shortBytes, 0, 4);
            return shortBytes;
        }
        /// <summary>
        /// 从指定字符串中某索引处转换为牌的方法,需求两个字符
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static Pai GetPai(string str, int index = 0)
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
