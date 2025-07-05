using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace MaJiangLib
{
    /// <summary>
    /// 手牌的类,因为副露等原因设定为类
    /// </summary>
    public class ShouPai
    {
        public ShouPai()
        {
            ShouPaiList = new();
            FuluPaiList = new();
        }
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
        /// 判断是否门清,当没有副露或仅有暗杠副露时为门清
        /// </summary>
        /// <returns></returns>
        public bool IsMenQing()
        {
            bool isMenQing = true;
            foreach (Group fuluPai in FuluPaiList)
            {
                if (fuluPai.GroupType != GroupType.AnKang)
                {
                    isMenQing = false;
                    break;
                }
            }
            return isMenQing;
        }
    }
}
