using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using static MaJiangLib.GlobalFunction;

namespace MaJiangLib
{
    /// <summary>
    /// ���������/�����,�������罻��
    /// </summary>
    public static class PackCoder
    {
        /* 1. ����С�̶�Ϊ1KiB,��1024B.�ⷽ�濼�Ǽ��ֱ����������С,�����ȡʱ�жϰ�ͷβ��ѭ����ȡ������
         * 2. ���ṹ
         * (1) 8   bytes [0x02, 0x48, 0x61, 0x78, 0x4D, 0x61, 0x6A, 0x50] "\u0002HaxMajP" ���ݰ�ͷ�ض���ʶ,UTF-8����,��λΪSTX(0x02)�Ӷ������ͷ����
         * (2) 8   bytes Unix����ʱ���,long����
         * (3) 4   bytes ����ԴIP��ַ,ΪIPv4��ַ����
         * (4) 48  bytes ����Դ�û���,Ϊstring����,UTF-8����,�޶��Ϊ12�ַ�,���������0
         * (5) 4   bytes �汾��([TODO]�ݿ�,δʵ��)
         * (6) 32  bytes ����
         * (7) 912 bytes ����Ҫ���ݲ���(��ʼ�ڵ�104��)
         * (8) 8   bytes [0x03, 0x45, 0x6E, 0x64, 0x50, 0x61, 0x63, 0x6B]"\u0003EndPack" ���ݰ������ض���ʶ,UTF-8����,��λΪETX(0x03)�Ӷ������β����
         * 
         * 3. ��������У��,����Ӱ�û���㹻�ռ���ٿ�һ����.
         * 4. Ĭ���������ݰ�ʱ��ͨ��EmptyPack()��ȡһ���հ��ټӹ�
         * 5. ���ڰ�����Ҫ���ݲ��ֵ�����,�������͵����ݱ����ע�䱾����ܴ�С������һ����ʶ,�Ӷ�ʹ�õ������ڿ��Դ洢�����Ϣ
         * 6. �����ݽṹ��ʶ
         * (1) "_RW_" �����ڶԻ�/���������Ϣ
         * (2) "_IN_" �Ծֳ�ʼ����,�����Ծֿ�ʼʱ��ʼ���Ƶ�����,���Ƶ�����
         * (3) "_PA_" �����Ϊ,��ʾΪ������ҵĲ���
         * (4) "_SI_" ϵͳ��Ϣ,��ʾΪ����,����,ϵͳ��������Ϣ
         * (5) "_SA_" �罻��Ϊ,��ʾΪ������Ϣ/�������
         * (6) "_MD_" ������Ϣ,��ʾ�Ծ���Ϣͬ��
         * 
         * 7. ��Ҳ���������������������Ĳ���,��������ܷ������Щ����������ұ��ؿͻ���/�����ͬ�����м���,�������ȷ�������Ϲ�ʱ�ٽ���
         * 8. [TODO] �����޸�Ϊ������ʽ,�ع������ɹ���
         * 
         * 9. ���ڶԾ���Ϣ,Ŀǰ�趨�Ƕ���MainMatchControl,����һ����ȫת��ΪByte[]�ķ���,����Ϊ��Ϣ�ܶ�ʱ���ʹ�ô˷���
         *    �Ծ���Ϣ�ĸ����ǰ��յ�����Ϊ�ڱ��ص���������ȡ��,���������û���֮��ĶԾ���Ϣͬ���򾡿��ܱ���
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
        /// �ӳ�,��TcpClinet�ڴ����ʱ���޸�,��λΪ����
        /// </summary>
        public static double Ping { get; set; }
        
        /// <summary>
        /// �����жϰ��Ƿ�Ϸ�(��ͷβ��ȷ,��С��ȷ),����Ϸ��򷵻�True,��֮�򷵻�False
        /// </summary>
        /// <param name="pack"></param>
        /// <returns>����Ϸ��򷵻�True,��֮�򷵻�False</returns>
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
        /// �հ����ɷ���,��Ҫ�û���,����IPv4��ַ
        /// </summary>
        /// <param name="senderName">�����û���</param>
        /// <param name="iPAddress">����IPv4��ַ</param>
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
            // ͷ��ʶ"_RW_" 4 bytes + �����(int) 4 bytes + �����ݴ�С(int) 4 bytes + ����
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
        /// "_PA_" ��ҽ��в���ʱ�����͵Ĳ����Ӱ�,һ������³���Ϊ40bytes
        /// </summary>
        /// <param name="pack">���ӹ���</param>
        /// <param name="playerActionData">��������еĲ���</param>
        /// <returns></returns>
        [Obsolete]
        public static byte[] PlayerActionPack(byte[] pack, PlayerActionData playerActionData)
        {
            // ͷ��ʶ"_PA_" 4 bytes + �����ݴ�С(int) 4 bytes + ��Ҳ��� 16 bytes
            byte[] signalByte = Encoding.UTF8.GetBytes("_PA_");
            byte[] playerActionByte = playerActionData;
            int length = signalByte.Length + playerActionByte.Length + 4;
            byte[] lengthByte = BitConverter.GetBytes(length);
            byte[] finalByte = signalByte.Concat(lengthByte).Concat(playerActionByte).ToArray();
            ReplaceBytes(pack, finalByte, 104);
            return pack;
        }
        /// <summary>
        /// "_IN_" ��ʼ����,�����Ծֿ�ʼʱ��ʼ���Ƶ�����
        /// </summary>
        /// <returns></returns>
        [Obsolete]
        public static byte[] InitPack(byte[] pack, Player player)
        {

            return new byte[0];
        }
        /// <summary>
        /// "_MD_" �Ծ���Ϣ��,��������������еĹ�����������ͬ��
        /// </summary>
        /// <param name="pack">��ʼ����</param>
        /// <param name="mainMatchControl">�Ծ���Ϣ</param>
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
        /// "_PA_" �����Ϊ�����,��Ҫ��,�����Ҳ������ݺ��Ƿ�ɹ�
        /// </summary>
        /// <param name="pack">Ҫ��İ�</param>
        /// <param name="playerActionData">����������������</param>
        /// <returns>�������ɹ�,�ṹ��ȷ�򷵻�True,��֮����False</returns>
        
    }
    
}
