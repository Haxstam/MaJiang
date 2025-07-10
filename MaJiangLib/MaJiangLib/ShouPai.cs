using System;
using System.Collections.Generic;
using System.Linq;

namespace MaJiangLib
{
    /// <summary>
    /// 手牌的类,因为副露等原因设定为类
    /// </summary>
    public class ShouPai : ICloneable
    {
        public ShouPai()
        {

            ShouPaiList = new();
            FuluPaiList = new();
        }
        
        /// <summary>
        /// 标记该手牌所属的玩家
        /// </summary>
        public int Player { get; set; }
        /// <summary>
        /// 手牌列表,不超过13枚
        /// </summary>
        public List<Pai> ShouPaiList { get; set; }
        /// <summary>
        /// 副露牌列表,按面子分组,不超过4组
        /// </summary>
        public List<Group> FuluPaiList { get; set; }
        /// <summary>
        /// 北宝牌计数,仅适用于三麻
        /// </summary>
        public int NorthDoraCount { get; set; }
        /// <summary>
        /// 返回手牌是否门清,仅当副露面子计数为0或副露中仅暗杠时为门清
        /// </summary>
        public bool IsClosedHand
        {
            get { return FuluPaiList.Count <= 0 || FuluPaiList.All(group => group.GroupType == GroupType.AnKang); }
        }

        public object Clone()
        {
            ShouPai shouPai = new ShouPai();
            shouPai.Player = Player;
            shouPai.ShouPaiList = ShouPaiList.ToList();
            shouPai.FuluPaiList = FuluPaiList.ToList();
            shouPai.NorthDoraCount = NorthDoraCount;
            return shouPai;
        }
    }
}
