using System.Collections.Generic;
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
        public int Fan { get; set; }
        public int Fu { get; set; }
        public int BasePoint { get; set; }
        public int MyProperty { get; set; }
        public List<FanData> FanDatas { get; set; }
    }
}
