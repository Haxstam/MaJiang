using System;
using System.Collections.Generic;
using System.Text;

namespace MaJiangLib
{
    /// <summary>
    /// 副露牌的类,因为副露不参与手牌听牌重复计算,因此单独列出
    /// </summary>
    public class FuluPai
    {
        /// <summary>
        /// 初始化,必须提供副露类型和副露的面子
        /// </summary>
        /// <param name="fuluType"></param>
        /// <param name="paiList"></param>
        public FuluPai(FuluType fuluType, Group Group) 
        {
            FuluType = fuluType;
            Group= Group;
        }
        /// <summary>
        /// 存储副露类型的区分,杠子必为副露
        /// </summary>
        public FuluType FuluType { get; set; }
        public Group Group { get; set; }

    }
}
