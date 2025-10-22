using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using static MaJiangLib.GlobalFunction;

namespace MaJiangLib
{
    /// <summary>
    /// 网络包制作/解包器,用于网络交互
    /// </summary>
    public static class PackCoder
    {
        /* 1. 包大小固定为1KiB,即1024B.这方面考虑简便直接锁定包大小,避免读取时判断包头尾和循环读取的问题
         * 2. 包结构
         * (1) 8   bytes [0x02, 0x48, 0x61, 0x78, 0x4D, 0x61, 0x6A, 0x50] "\u0002HaxMajP" 数据包头特定标识,UTF-8编码,首位为STX(0x02)从而避免包头误判
         * (2) 8   bytes Unix毫秒时间戳,long类型
         * (3) 4   bytes 包来源IP地址,为IPv4地址类型
         * (4) 48  bytes 包来源用户名,为string类型,UTF-8编码,限定最长为12字符,如果不足则补0
         * (5) 4   bytes 版本号([TODO]暂空,未实现)
         * (6) 32  bytes 留空
         * (7) 912 bytes 包主要数据部分(起始于第104处)
         * (8) 8   bytes [0x03, 0x45, 0x6E, 0x64, 0x50, 0x61, 0x63, 0x6B]"\u0003EndPack" 数据包结束特定标识,UTF-8编码,首位为ETX(0x03)从而避免包尾误判
         * 
         * 3. 包不考虑校验,如果子包没有足够空间就再开一个包.
         * 4. 默认制作数据包时先通过EmptyPack()获取一个空包再加工
         * 5. 对于包内主要数据部分的内容,所有类型的内容必须标注其本身的总大小并具有一个标识,从而使得单个包内可以存储多个信息
         * 6. 子内容结构标识
         * (1) "_RW_" 房间内对话/表情包等信息
         * (2) "_IN_" 对局初始化包,包含对局开始时起始手牌的数据,宝牌等数据
         * (3) "_PA_" 玩家行为,表示为局内玩家的操作
         * (4) "_SI_" 系统信息,表示为断连,重连,系统交互的信息
         * (5) "_SA_" 社交行为,表示为个人信息/好友相关
         * (6) "_MD_" 比赛信息,表示对局信息同步
         * 
         * 7. 玩家操作传输仅传输其所做出的操作,对于玩家能否进行哪些操作则是玩家本地客户端/服务端同步进行计算,服务端在确定操作合规时再进行
         * 8. [TODO] 考虑修改为工厂形式,重构包生成过程
         * 
         * 9. 对于对局信息,目前设定是对于MainMatchControl,其有一个完全转换为Byte[]的方法,但因为信息很多故避免使用此方法
         *    对局信息的更新是按照单次行为在本地的演算来获取的,服务器和用户端之间的对局信息同步则尽可能避免
         *    
         * 10.Pai 2 bytes
         *    Group(Pai) 14 bytes
         *    PlayActionData(Pai) 16 bytes
         *    ShouPai(Pai, Group) 96 bytes
         *    PlayerProfile 96 bytes
         *    Player(ShouPai, PlayerProfile) 216 bytes
         */

        public static string PackHeadStr { get; } = "\u0002HaxMajP";
        public static byte[] PackHeadBytes { get; } = new byte[8] { 0x02, 0x48, 0x61, 0x78, 0x4D, 0x61, 0x6A, 0x50 };
        public static string PackEndStr { get; } = "\u0003EndPack";
        public static byte[] PackEndBytes { get; } = new byte[8] { 0x03, 0x45, 0x6E, 0x64, 0x50, 0x61, 0x63, 0x6B };
        public static int PackSize { get; } = 1024;
        public static int PackEndIndex { get; } = 1016;
        /// <summary>
        /// 延迟,仅TcpClinet在处理包时被修改,单位为毫秒
        /// </summary>
        public static double Ping { get; set; }
        
        /// <summary>
        /// 用于判断包是否合法(包头尾正确,大小正确),如果合法则返回True,反之则返回False
        /// </summary>
        /// <param name="pack"></param>
        /// <returns>如果合法则返回True,反之则返回False</returns>
        public static bool IsLegalPack(byte[] pack)
        {
            if (pack.Length == PackSize)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (pack[i] == PackHeadBytes[i] && pack[i + PackEndIndex] == PackEndBytes[i])
                    {

                    }
                    else
                    {
                        return false;
                    }
                }
                return true;
            }
            else
            {
                return false;
            }
        }
        public static bool IsLegalPack(Span<byte> pack)
        {
            if (pack.Length == PackSize)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (pack[i] == PackHeadBytes[i] && pack[i + PackEndIndex] == PackEndBytes[i])
                    {

                    }
                    else
                    {
                        return false;
                    }
                }
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// 空包生成方法,需要用户名,自身IPv4地址
        /// </summary>
        /// <param name="senderName">自身用户名</param>
        /// <param name="iPAddress">自身IPv4地址</param>
        /// <returns></returns>
        [Obsolete]
        public static byte[] EmptyPack(string senderName, IPAddress iPAddress)
        {
            byte[] rawPack = new byte[PackSize];
            ReplaceBytes(rawPack, PackHeadBytes, 0);
            ReplaceBytes(rawPack, BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), 8);
            ReplaceBytes(rawPack, iPAddress.GetAddressBytes(), 16);
            ReplaceBytes(rawPack, Encoding.UTF8.GetBytes(senderName), 20);
            ReplaceBytes(rawPack, PackEndBytes, PackEndIndex);
            return rawPack;
        }
        [Obsolete]
        public static byte[] RoomWordPack(byte[] pack, string word, int roomNumber)
        {
            // 头标识"_RW_" 4 bytes + 房间号(int) 4 bytes + 子内容大小(int) 4 bytes + 内容
            byte[] signalByte = Encoding.UTF8.GetBytes("_RW_");
            byte[] roomNumberByte = BitConverter.GetBytes(roomNumber);
            byte[] wordByte = Encoding.UTF8.GetBytes(word);
            int length = signalByte.Length + roomNumberByte.Length + wordByte.Length + 4;
            byte[] lengthByte = BitConverter.GetBytes(length);
            byte[] finalByte = signalByte.Concat(roomNumberByte).Concat(lengthByte).Concat(wordByte).ToArray();
            ReplaceBytes(pack, finalByte, 104);
            return pack;
        }
        /// <summary>
        /// "_PA_" 玩家进行操作时所发送的操作子包,一般情况下长度为40bytes
        /// </summary>
        /// <param name="pack">待加工包</param>
        /// <param name="playerActionData">玩家所进行的操作</param>
        /// <returns></returns>
        [Obsolete]
        public static byte[] PlayerActionPack(byte[] pack, PlayerActionData playerActionData)
        {
            // 头标识"_PA_" 4 bytes + 子内容大小(int) 4 bytes + 玩家操作 16 bytes
            byte[] signalByte = Encoding.UTF8.GetBytes("_PA_");
            byte[] playerActionByte = playerActionData;
            int length = signalByte.Length + playerActionByte.Length + 4;
            byte[] lengthByte = BitConverter.GetBytes(length);
            byte[] finalByte = signalByte.Concat(lengthByte).Concat(playerActionByte).ToArray();
            ReplaceBytes(pack, finalByte, 104);
            return pack;
        }
        /// <summary>
        /// "_IN_" 初始化包,包含对局开始时起始手牌的数据
        /// </summary>
        /// <returns></returns>
        [Obsolete]
        public static byte[] InitPack(byte[] pack, Player player)
        {

            return new byte[0];
        }
        /// <summary>
        /// "_MD_" 对局信息包,输出本场比赛所有的公共数据用于同步
        /// </summary>
        /// <param name="pack">初始化包</param>
        /// <param name="mainMatchControl">对局信息</param>
        /// <returns></returns>
        [Obsolete]
        public static byte[] MatchDataPack(byte[] pack, MainMatchControl mainMatchControl)
        {

            byte[] signalByte = Encoding.UTF8.GetBytes("_MD_");
            byte[] matchDataByte = mainMatchControl.GetPublicBytes();
            byte[] finalByte = signalByte.Concat(matchDataByte).ToArray();
            ReplaceBytes(pack, finalByte, 104);
            return pack;
        }
        /// <summary>
        /// "_PA_" 玩家行为解包器,需要包,输出玩家操作数据和是否成功
        /// </summary>
        /// <param name="pack">要解的包</param>
        /// <param name="playerActionData">输出的玩家数据类型</param>
        /// <returns>如果解包成功,结构正确则返回True,反之返回False</returns>
        
    }
    
}
