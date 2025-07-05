using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaJIangNet
{
    /// <summary>
    /// 单局对局进行时的所有信息的存储类
    /// </summary>
    internal class MatchInformationBase
    {

        /// <summary>
        /// 局数,通常只能为1-4
        /// </summary>
        public int Round { get; set; }
        /// <summary>
        /// 本场数
        /// </summary>
        public int Honba { get; set; }
        /// <summary>
        /// 立直棒数量
        /// </summary>
        public int RiichiStick { get; set; }

    }
}
