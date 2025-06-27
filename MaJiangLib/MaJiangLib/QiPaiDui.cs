using System;
using System.Collections.Generic;
using System.Text;

namespace MaJiangLib
{
    /// <summary>
    /// 弃牌堆的类,存储弃牌的列表和从属阵营,不受鸣牌影响
    /// </summary>
    public class QiPaiDui
    {

        public QiPaiDui(int camp) 
        {
            Camp = camp;
            PaiDui = new();
        }
        public int Camp { get; set; }
        public List<Pai> PaiDui { get; set; }
    }
}
