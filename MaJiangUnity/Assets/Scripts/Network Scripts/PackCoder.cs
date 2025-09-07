using System;
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
        /* 1. 包大小固定为512bytes,即512B.这方面考虑简便直接锁定包大小,避免读取时判断包头尾和循环读取的问题
         * 2. 包结构
         * (1) 8   bytes "HaxMajPk" 数据包头特定标识,UTF-8编码
         * (2) 8   bytes Unix时间戳,long类型
         * (3) 4   bytes 包来源IP地址,为IPv4地址类型
         * (4) 48  bytes 包来源用户名,为string类型,UTF-8编码,限定最长为12字符,如果不足则补0
         * (5) 36  bytes 留空
         * (6) 400 bytes 包主要数据部分(起始于第104处)
         * (7) 8   bytes "EndPack_" 数据包结束特定标识,UTF-8编码
         * 
         * 3. 包不考虑校验/数据分包,如果子包没有足够空间就再开一个包.
         * 4. 默认制作数据包时先通过EmptyPack()获取一个空包再加工
         * 5. 对于包内主要数据部分的内容,所有类型的内容必须标注其本身的总大小并具有一个标识,从而使得单个包内可以存储多个信息
         * 6. 子内容结构标识
         * (1) "_RW_" 房间内对话/表情包等信息
         * (2) "_PA_" 玩家行为,表示为局内玩家的操作
         * (3) "_SI_" 系统信息,表示为断连,重连,系统交互的信息
         * (4) "_SA_" 社交行为,表示为个人信息/好友相关
         * 
         * 7. 玩家操作传输仅传输其所做出的操作,对于玩家能否进行哪些操作则是玩家本地客户端/服务端同步进行计算,服务端在确定操作合规时再进行
         * 8. [TODO] 考虑修改为工厂形式,重构包生成过程
         * 
         * 9. 对于对局信息,目前设定是对于MainMatchControl,其有一个完全转换为Byte[]的方法,但因为信息很多故避免使用此方法
         *    对局信息的更新是按照单次行为在本地的演算来获取的,服务器和用户端之间的对局信息同步则尽可能避免
         *    
         */

        public static string PackHeadStr { get; set; } = "HaxMajPk";
        public static string PackEndStr { get; set; } = "EndPack_";


        /// <summary>
        /// 空包生成方法,需要用户名,自身IPv4地址
        /// </summary>
        /// <param name="senderName">自身用户名</param>
        /// <param name="iPAddress">自身IPv4地址</param>
        /// <returns></returns>
        public static byte[] EmptyPack(string senderName, IPAddress iPAddress)
        {
            byte[] rawPack = new byte[512];
            ReplaceBytes(rawPack, Encoding.UTF8.GetBytes(PackHeadStr), 0);
            ReplaceBytes(rawPack, BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), 8);
            ReplaceBytes(rawPack, iPAddress.GetAddressBytes(), 16);
            ReplaceBytes(rawPack, Encoding.UTF8.GetBytes(senderName), 20);
            ReplaceBytes(rawPack, Encoding.UTF8.GetBytes(PackEndStr), 504);
            return rawPack;
        }
        /// <summary>
        /// 在房间内聊天室的语言子包,考虑到UTF-8编码限制,限定最长单句话为64字符,也即最大长度为256bytes
        /// </summary>
        /// <param name="pack">待加工包</param>
        /// <param name="word">所要传输的内容,限定最长为64字符</param>
        /// <param name="roomNumber">目标房间号,避免潜在的冲突</param>
        /// <returns></returns>
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
        /// 玩家进行操作时所发送的操作子包,一般情况下长度为40bytes
        /// </summary>
        /// <param name="pack">待加工包</param>
        /// <param name="playerActionData">玩家所进行的操作</param>
        /// <returns></returns>
        public static byte[] PlayerActionPack(byte[] pack, PlayerActionData playerActionData)
        {
            // 头标识"_PA_" 4 bytes + 子内容大小(int) 4 bytes + 玩家操作 32 bytes
            byte[] signalByte = Encoding.UTF8.GetBytes("_PA_");
            byte[] playerActionByte = playerActionData;
            int length = signalByte.Length + playerActionByte.Length + 4;
            byte[] lengthByte = BitConverter.GetBytes(length);
            byte[] finalByte = signalByte.Concat(lengthByte).Concat(playerActionByte).ToArray();
            ReplaceBytes(pack, finalByte, 104);
            return pack;
        }
        /// <summary>
        /// 用于判断包是否合法(包头尾正确,大小正确),如果合法则返回True,反之则返回False
        /// </summary>
        /// <param name="pack"></param>
        /// <returns>如果合法则返回True,反之则返回False</returns>
        public static bool IsLegalPack(byte[] pack)
        {
            if (pack.Length == 512 && Encoding.UTF8.GetString(pack, 0, 8) == PackHeadStr && Encoding.UTF8.GetString(pack, 504, 8) == PackEndStr)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// "_PA_" 玩家行为解包器,需要包,输出玩家操作数据和是否成功
        /// </summary>
        /// <param name="pack">要解的包</param>
        /// <param name="playerActionData">输出的玩家数据类型</pa
        /// ram>
        /// <returns>如果解包成功,结构正确则返回True,反之返回False</returns>
        public static bool PlayerActionDecode(byte[] pack, out PlayerActionData playerActionData)
        {
            playerActionData = null;
            if (Encoding.UTF8.GetString(pack, 104, 4) == "_PA_")
            {
                int length = BitConverter.ToInt32(pack, 108);
                playerActionData = PlayerActionData.GetPlayerActionData(pack, 112);
                return true;
            }
            else
            {
                return false;
            }
        }
    }

}
