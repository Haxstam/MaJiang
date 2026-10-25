using System;
using System.Collections.Generic;
using System.Linq;

namespace MaJiangLib
{
    /// <summary>
    /// 公用的方法类,这些方法应当可以被所有用户访问,且保持静态
    /// </summary>
    public static class GlobalFunction
    {

        /// <summary>
        /// 洗牌方法,提供列表和随机数种,返回随机打乱后的数组
        /// </summary>
        /// <typeparam name="T">列表中元素的类型</typeparam>
        /// <param name="list">要被打乱列表</param>
        /// <param name="seed">随机数种,通过输入相同的随机数种和列表来获取相同结果</param>
        public static void Shuffle<T>(this IList<T> list, int seed)
        {
            Random random = new(seed);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// 随机牌山生成器,可根据设定生成不同组成的随机牌山
        /// </summary>
        /// <param name="randomNumber">所生成牌山的随机数</param>
        /// <param name="isFourMahJong">标记生成的牌山是不是四人牌山,若为三人牌山,将会移除2w到8w</param>
        /// <param name="haveRedDora">标记生成的牌山是否包含0w,0p,0s三张红宝牌</param>
        /// <param name="haveHonor">标记生成的牌山是否包含字牌</param>
        /// <param name="random">随机数产生器</param>
        /// <returns></returns>
        public static List<Tile> RandomCardGenerator(out int randomNumber, bool isFourMahJong, bool haveRedDora, bool haveHonor, Xoshiro256StarStar random)
        {
            // TODO 暂时先实现四人有赤宝情况
            // 每次生成牌山都要这样生成一个列表,考虑将其作为一个静态变量放在类中跳过生成
            // 对于四人麻将,总共牌数136张,其中王牌14张
            List<Tile> tileList = new List<Tile>();
            for (int i = 1; i < 10; i++)
            {   // 添加1w~9w,1p~9p,1s~9s,其中5w,5p,5s中有一张牌分别被一张0w,0p,0s替代
                foreach (Color color in new List<Color>() { Color.Wans, Color.Tungs, Color.Bamboo })
                {
                    if (i == 5 && haveRedDora)
                    {
                        for (int j = 0; j < 3; j++)
                        {
                            tileList.Add(new(color, i, false));
                        }
                        tileList.Add(new(color, i, true));
                    }
                    else
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            tileList.Add(new(color, i));
                        }
                    }
                }
            }
            for (int i = 1; haveHonor && i < 8; i++)
            {   // 添加1z~7z,分别代表东南西北白发中
                for (int j = 0; j < 4; j++)
                {
                    tileList.Add(new(Color.Honor, i, false));
                }
            }
            randomNumber = random.NextInt32();
            Shuffle(tileList, randomNumber);
            return tileList;
        }
        /// <summary>
        /// 为实现实例方法调用的注册字典,任何实现IByteable的类都要注册于此
        /// </summary>
        public static Dictionary<Type, object> ByteableInstanceDict { get; } = new Dictionary<Type, object>()
        {
            // 仅用于调用接口方法,具体实现不影响
            {typeof(Tile), new Tile(Color.Wans, 1) },
            {typeof(Group), new Group(GroupType.Triple, Color.Wans, new())},
            {typeof(Player), new Player() },
            {typeof(PlayerActionData), new PlayerActionData(new(),new(Color.Wans,1 ),PlayerAction.Chi) },
            {typeof(HandTile),new HandTile(0) },
            {typeof(UserProfile),new UserProfile() },
        };

        #region *序列化方法*
        /// <summary>
        /// 字节替换,需要目标字节,短字节和索引
        /// </summary>
        /// <param name="targetBytes">要被替换的较长字节</param>
        /// <param name="shortBytes">用于替换的较短字节</param>
        /// <param name="index">长字节被替换的起始索引</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void ReplaceBytes(byte[] targetBytes, byte[] shortBytes, int index)
        {
            if (index < 0 || index + shortBytes.Length > targetBytes.Length)
            {
                throw new ArgumentOutOfRangeException("替换范围超出目标数组长度");
            }
            Span<byte> spanBytes = targetBytes;
            Span<byte> sliceBytes = spanBytes.Slice(index, shortBytes.Length);
            shortBytes.CopyTo(sliceBytes);
        }
        /// <summary>
        /// 使用Span<>的字节替换,需要目标Span<>,短字节和索引
        /// </summary>
        /// <param name="targetBytes">Span<byte>类型的字节段</param>
        /// <param name="shortBytes">短字节</param>
        /// <param name="index">索引</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void ReplaceBytes(Span<byte> targetBytes, byte[] shortBytes, int index)
        {
            if (index < 0 || index + shortBytes.Length > targetBytes.Length)
            {
                throw new ArgumentOutOfRangeException("替换范围超出目标数组长度");
            }
            Span<byte> sliceBytes = targetBytes.Slice(index, shortBytes.Length);
            shortBytes.CopyTo(sliceBytes);
        }
        /// <summary>
        /// 对T的列表进行序列化的方法,T必须实现接口IByteable
        /// </summary>
        /// <typeparam name="T">类型T必须实现IByteable</typeparam>
        /// <param name="list">包含T的列表</param>
        /// <returns>列表成员依次序列化后的字节串</returns>
        public static byte[] ListToBytes<T>(List<T> list) where T : IByteable<T>
        {
            if (list.Count == 0)
            {
                return new byte[0];
            }
            else
            {
                int byteSize = list[0].ByteSize;
                Span<byte> mainBytes = new byte[list.Count * byteSize];
                for (int i = 0; i < list.Count; i++)
                {
                    ReplaceBytes(mainBytes, list[i].GetBytes(), i * byteSize);
                }
                return mainBytes.ToArray();
            }

        }
        public static byte[] ListToBytes(List<bool> list)
        {
            Span<byte> mainBytes = new byte[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                mainBytes[i] = BitConverter.GetBytes(list[i])[0];
            }
            return mainBytes.ToArray();
        }
        public static byte[] ListToBytes(List<int> list)
        {
            Span<byte> mainBytes = new byte[list.Count * 4];

            for (int i = 0; i < list.Count; i++)
            {
                ReplaceBytes(mainBytes, BitConverter.GetBytes(list[i]), 4 * i);
            }
            return mainBytes.ToArray();
        }
        /// <summary>
        /// 从字节串到列表的转换方法,列表成员类型必须实现IByteable接口
        /// </summary>
        /// <typeparam name="T">类型T必须实现IByteable接口并于GlobalFunction.ByteableInstanceDict中注册</typeparam>
        /// <param name="bytes">待输入的字节串</param>
        /// <param name="startIndex">转换起始索引</param>
        /// <param name="count">要转换的最大数量,如果转换达到该值则返回,如果遇到首位为0也返回,-1为尽可能转换直到遇到0</param>
        /// <returns></returns>
        public static List<T> BytesToList<T>(byte[] bytes, int startIndex = 0, int count = -1) where T : IByteable<T>
        {
            // 因为C# 9.0 的限制,像BytesTo()这样的静态方法无法在接口中声明
            // 因此对于BytesToList(),其限制where T : IByteable并不保证T可以从字节串转化而来
            // 对于任何实现了该接口的类型,其必须在ByteableInstanceDict内注册
            IByteable<T> instance = (IByteable<T>)ByteableInstanceDict[typeof(T)];
            List<T> list = new List<T>();
            int listIndex = startIndex;
            if (count != -1)
            {

                for (int i = 0; i < count; i++)
                {
                    if (bytes[listIndex] == 0)
                    {
                        // 将要被转化的元素的首位为0,表示列表已结束,停止转化
                        break;
                    }
                    list.Add(instance.BytesTo(bytes, listIndex));
                    listIndex += instance.ByteSize;
                }
            }
            else
            {
                while (true)
                {
                    try
                    {
                        if (bytes[listIndex] == 0)
                        {
                            // 将要被转化的元素的首位为0,表示列表已结束,停止转化
                            break;
                        }
                        list.Add(instance.BytesTo(bytes, listIndex));
                        listIndex += instance.ByteSize;
                    }
                    catch
                    {
                        // 转化出现错误,停止转化
                        break;
                    }
                }
            }
            return list;
        }
        /// <summary>
        /// 解码指定数量的整数。count 为 -1 时读取剩余完整整数，零值不是结束标记。
        /// </summary>
        public static List<int> BytesToIntList(byte[] bytes, int startIndex, int count)
        {
            int elementCount = GetPrimitiveListCount(bytes, startIndex, count, sizeof(int));
            List<int> values = new(elementCount);
            for (int index = 0; index < elementCount; index++)
            {
                values.Add(BitConverter.ToInt32(bytes, startIndex + index * sizeof(int)));
            }
            return values;
        }

        /// <summary>
        /// 解码指定数量的单字节布尔值。count 为 -1 时读取剩余字节，false 是有效值。
        /// </summary>
        public static List<bool> BytesToBoolList(byte[] bytes, int startIndex, int count)
        {
            int elementCount = GetPrimitiveListCount(bytes, startIndex, count, sizeof(byte));
            List<bool> values = new(elementCount);
            for (int index = 0; index < elementCount; index++)
            {
                byte value = bytes[startIndex + index];
                if (value > 1)
                {
                    throw new ArgumentException("布尔值编码只能为0或1。", nameof(bytes));
                }
                values.Add(value == 1);
            }
            return values;
        }

        private static int GetPrimitiveListCount(byte[] bytes, int startIndex, int count, int elementSize)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            if (startIndex < 0 || startIndex > bytes.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }
            int remaining = bytes.Length - startIndex;
            if (count == -1)
            {
                if (remaining % elementSize != 0)
                {
                    throw new ArgumentException("剩余字节不是完整元素。", nameof(bytes));
                }
                return remaining / elementSize;
            }
            if (count < 0 || count > remaining / elementSize)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            return count;
        }
        #endregion

    }
}
