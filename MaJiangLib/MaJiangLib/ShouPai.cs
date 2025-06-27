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
        public List<Pai> ShouPaiList { get; set; }
        public List<FuluPai> FuluPaiList { get; set; }
        /// <summary>
        /// 判断是否门清,当没有副露或仅有暗杠副露时为门清
        /// </summary>
        /// <returns></returns>
        public bool IsMenQing()
        {
            bool isMenQing = true;
            foreach (FuluPai fuluPai in FuluPaiList)
            {
                if (fuluPai.FuluType != FuluType.AnGang)
                {
                    isMenQing = false;
                    break;
                }
            }
            return isMenQing;
        }
    }
}
