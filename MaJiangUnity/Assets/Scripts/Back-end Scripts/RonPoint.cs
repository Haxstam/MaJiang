using System.Collections.Generic;
using System.Linq;
using static MaJiangLib.FanCalculator;

namespace MaJiangLib
{
    /// <summary>
    /// 存储和牌时,具体番数,符数,点数和役种的类
    /// </summary>
    public class RonPoint
    {
        public RonPoint(int fan, int fu, int basePoint, List<FanData> fanDatas)
        {
            Fan = fan;
            Fu = fu;
            BasePoint = basePoint;
            FanDatas = fanDatas;
        }
        /// <summary>
        /// 总番数
        /// </summary>
        public int Fan { get; set; }
        /// <summary>
        /// 符数
        /// </summary>
        public int Fu { get; set; }
        /// <summary>
        /// 基本点
        /// </summary>
        public int BasePoint { get; set; }
        /// <summary>
        /// 计算和牌中仅役种的番的和
        /// </summary>
        /// <returns></returns>
        public int YakuFan()
        {
            // 还是不用lambda了
            int count = 0;
            foreach (FanData fanData in FanDatas)
            {
                if (fanData.Yaku != YakuType.Dora && fanData.Yaku != YakuType.AkaDora && fanData.Yaku != YakuType.UraDora)
                {   // 去掉宝牌
                    count += fanData.Fan;
                }
            }
            return count;
        }
        /// <summary>
        /// 番种列表
        /// </summary>
        public List<FanData> FanDatas { get; set; }
    }
}
