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
    }
}
