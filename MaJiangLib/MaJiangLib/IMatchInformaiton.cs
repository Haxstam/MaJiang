using System;
using System.Collections.Generic;
using System.Text;

namespace MaJiangLib
{
    public interface IMatchInformation
    {
        /// <summary>
        /// 弃牌堆,存储所有人的弃牌,第0所对应的玩家为东一场的亲家
        /// </summary>
        public List<List<Pai>> QiPaiList { get; set; }
        /// <summary>
        /// 宝牌指示牌列表,存储所有已被展示的宝牌指示牌
        /// </summary>
        public List<Pai> DoraList { get; set; }
        /// <summary>
        /// 里宝牌列表,暂时放在这里
        /// </summary>
        public List<Pai> UraDoraList { get; set; }
        /// <summary>
        /// 当前风场
        /// </summary>
        public WindType Wind { get; set; }
        /// <summary>
        /// 当前为第几局
        /// </summary>
        public int Round { get; set; }
        /// <summary>
        /// 当前为几本场
        /// </summary>
        public int Honba { get; set; }
        /// <summary>
        /// 玩家点数
        /// </summary>
        public List<int> PlayerPoint { get; set; }
        /// <summary>
        /// 记录是否立直,立直者对应序号的值为true
        /// </summary>
        public List<bool> IsRiichi { get; set; }
        /// <summary>
        /// 剩余牌的数目,用于判断河底海底等
        /// </summary>
        public int RemainPaiCount { get; set; }
        /// <summary>
        /// 一发的判断,某人立直后设定其序号对应的为True,有人鸣牌或其本人再打出一张后设定为False
        /// </summary>
        public List<bool> HaveIppatsu { get; set; }
        /// <summary>
        /// 第一巡的指示,用于判断两立直天地和,开局设定为True,有人鸣牌或庄家再摸牌后设定为False
        /// </summary>
        public bool FirstCycleIppatsu { get; set; }
        /// <summary>
        /// 刚摸完岭上牌的状态,拔北或开杠后设定为True,打出牌后设定为False
        /// </summary>
        public List<bool> IsKan { get; set; }

    }
}
