using System;
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
        /* 1. ����С�̶�Ϊ512bytes,��512B.�ⷽ�濼�Ǽ��ֱ����������С,�����ȡʱ�жϰ�ͷβ��ѭ����ȡ������
         * 2. ���ṹ
         * (1) 8   bytes "HaxMajPk" ���ݰ�ͷ�ض���ʶ,UTF-8����
         * (2) 8   bytes Unixʱ���,long����
         * (3) 4   bytes ����ԴIP��ַ,ΪIPv4��ַ����
         * (4) 48  bytes ����Դ�û���,Ϊstring����,UTF-8����,�޶��Ϊ12�ַ�,���������0
         * (5) 36  bytes ����
         * (6) 400 bytes ����Ҫ���ݲ���(��ʼ�ڵ�104��)
         * (7) 8   bytes "EndPack_" ���ݰ������ض���ʶ,UTF-8����
         * 
         * 3. ��������У��/���ݷְ�,����Ӱ�û���㹻�ռ���ٿ�һ����.
         * 4. Ĭ���������ݰ�ʱ��ͨ��EmptyPack()��ȡһ���հ��ټӹ�
         * 5. ���ڰ�����Ҫ���ݲ��ֵ�����,�������͵����ݱ����ע�䱾����ܴ�С������һ����ʶ,�Ӷ�ʹ�õ������ڿ��Դ洢�����Ϣ
         * 6. �����ݽṹ��ʶ
         * (1) "_RW_" �����ڶԻ�/���������Ϣ
         * (2) "_PA_" �����Ϊ,��ʾΪ������ҵĲ���
         * (3) "_SI_" ϵͳ��Ϣ,��ʾΪ����,����,ϵͳ��������Ϣ
         * (4) "_SA_" �罻��Ϊ,��ʾΪ������Ϣ/�������
         * 
         * 7. ��Ҳ���������������������Ĳ���,��������ܷ������Щ����������ұ��ؿͻ���/�����ͬ�����м���,�������ȷ�������Ϲ�ʱ�ٽ���
         * 8. [TODO] �����޸�Ϊ������ʽ,�ع������ɹ���
         * 
         * 9. ���ڶԾ���Ϣ,Ŀǰ�趨�Ƕ���MainMatchControl,����һ����ȫת��ΪByte[]�ķ���,����Ϊ��Ϣ�ܶ�ʱ���ʹ�ô˷���
         *    �Ծ���Ϣ�ĸ����ǰ��յ�����Ϊ�ڱ��ص���������ȡ��,���������û���֮��ĶԾ���Ϣͬ���򾡿��ܱ���
         *    
         */

        public static string PackHeadStr { get; set; } = "HaxMajPk";
        public static string PackEndStr { get; set; } = "EndPack_";


        /// <summary>
        /// �հ����ɷ���,��Ҫ�û���,����IPv4��ַ
        /// </summary>
        /// <param name="senderName">�����û���</param>
        /// <param name="iPAddress">����IPv4��ַ</param>
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
        /// �ڷ����������ҵ������Ӱ�,���ǵ�UTF-8��������,�޶�����仰Ϊ64�ַ�,Ҳ����󳤶�Ϊ256bytes
        /// </summary>
        /// <param name="pack">���ӹ���</param>
        /// <param name="word">��Ҫ���������,�޶��Ϊ64�ַ�</param>
        /// <param name="roomNumber">Ŀ�귿���,����Ǳ�ڵĳ�ͻ</param>
        /// <returns></returns>
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
        /// ��ҽ��в���ʱ�����͵Ĳ����Ӱ�,һ������³���Ϊ40bytes
        /// </summary>
        /// <param name="pack">���ӹ���</param>
        /// <param name="playerActionData">��������еĲ���</param>
        /// <returns></returns>
        public static byte[] PlayerActionPack(byte[] pack, PlayerActionData playerActionData)
        {
            // ͷ��ʶ"_PA_" 4 bytes + �����ݴ�С(int) 4 bytes + ��Ҳ��� 32 bytes
            byte[] signalByte = Encoding.UTF8.GetBytes("_PA_");
            byte[] playerActionByte = playerActionData;
            int length = signalByte.Length + playerActionByte.Length + 4;
            byte[] lengthByte = BitConverter.GetBytes(length);
            byte[] finalByte = signalByte.Concat(lengthByte).Concat(playerActionByte).ToArray();
            ReplaceBytes(pack, finalByte, 104);
            return pack;
        }
        /// <summary>
        /// �����жϰ��Ƿ�Ϸ�(��ͷβ��ȷ,��С��ȷ),����Ϸ��򷵻�True,��֮�򷵻�False
        /// </summary>
        /// <param name="pack"></param>
        /// <returns>����Ϸ��򷵻�True,��֮�򷵻�False</returns>
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
        /// "_PA_" �����Ϊ�����,��Ҫ��,�����Ҳ������ݺ��Ƿ�ɹ�
        /// </summary>
        /// <param name="pack">Ҫ��İ�</param>
        /// <param name="playerActionData">����������������</pa
        /// ram>
        /// <returns>�������ɹ�,�ṹ��ȷ�򷵻�True,��֮����False</returns>
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
