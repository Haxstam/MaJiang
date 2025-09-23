using System;
using System.Collections.Generic;
using static MaJiangLib.GlobalFunction;
namespace MaJiangLib
{
    /// <summary>
    /// ��ҽ��в�������,������Ҳ���ʱ������,����Ŀ����,��Ҳ�������
    /// </summary>
    public class PlayerActionData : IByteable<PlayerActionData>
    {
        /// <summary>
        /// ���г����ܵ����Ʋ���ʱ�������Ϊ��¼,��Ҫ����Ӧ����,Ŀ���ƺͶ�Ӧ����
        /// </summary>
        /// <param name="pais">������Ҫ��Ӧ����</param>
        /// <param name="targetPai">����Ӧ����</param>
        /// <param name="playerAction">��Ӧ����</param>
        public PlayerActionData(List<Pai> pais, List<Pai> targetPai, PlayerAction playerAction)
        {
            Pais = pais;
            TargetPais = targetPai;
            PlayerAction = playerAction;
        }
        /// <summary>
        /// �������ưα����ܼӸ���ֱ���Ƶ��������ʱ�������Ϊ��¼,��Ҫ�ܲ������ƺͶ�Ӧ����,��Ҫ���Ƶ�Ŀ����
        /// </summary>
        /// <param name="pais">�ܲ�������</param>
        /// <param name="playerAction">��Ӧ����</param>
        public PlayerActionData(List<Pai> pais, PlayerAction playerAction)
        {
            if (PlayerAction == PlayerAction.Ron || PlayerAction == PlayerAction.Tsumo)
            {
                TargetPais = pais;
            }
            else
            {
                Pais = pais;
            }
            PlayerAction = playerAction;
        }
        /// <summary>
        /// �������ֵ���������������Ϊ��¼,��Ҫ��������еĲ���
        /// </summary>
        /// <param name="playerAction">��Ӧ����</param>
        public PlayerActionData(PlayerAction playerAction)
        {
            PlayerAction = playerAction;
        }
        public PlayerAction PlayerAction { get; set; }
        /// <summary>
        /// �����д���������,��Ա�����Ϊ4
        /// </summary>
        public List<Pai> Pais { get; set; }
        /// <summary>
        /// ��������Ŀ����,ע��:������洢��ȷ���Ĳ�����Ϣʱ,TargetPaisĬ�Ͻ�����һ��ȷ������,��Ա�����Ϊ2
        /// </summary>
        public List<Pai> TargetPais { get; set; }
        /// <summary>
        /// Ŀ����ҵ����
        /// </summary>
        public int TargetPlayerNumber { get; set; }
        /// <summary>
        /// �Լ������
        /// </summary>
        public int SelfNumber { get; set; }
        public int ByteSize { get; set; } = 16;
        /// <summary>
        /// ����Ҳ�����Ϣ���ֽڵ���ʽת��,���ȹ̶�16Bytes,����������Ա����ת��
        /// </summary>
        /// <param name="playerAction">Ŀ�����</param>
        public static implicit operator byte[](PlayerActionData playerActionData)
        {
            /*
             * �ṹ:
             * 1  byte  ��������
             * 1  byte  �������������,��������
             * 1  byte  �������������,����������
             * 1  bytes ����
             * 8  bytes Pais�����Ĵ洢,�ɴ洢���4�Ų�����,������0
             * 4  bytes TargetPais�Ĵ洢,�ɴ洢���2�ű�������,������0
             */
            byte[] MainBytes = new byte[16];
            MainBytes[0] = (byte)playerActionData.PlayerAction;
            // [TODO] �����߳����ߴ�ʵ��

            if (playerActionData.Pais.Count > 4 || playerActionData.TargetPais.Count > 2)
            {
                // �����Ա�����ཫ����ת��
                throw new System.Exception($"��Ҳ���ת��ʱ������������Ŀ:{playerActionData.Pais},{playerActionData.TargetPais}");
            }
            else
            {
                ReplaceBytes(playerActionData, ListToBytes(playerActionData.Pais), 4);
                ReplaceBytes(playerActionData, ListToBytes(playerActionData.TargetPais), 12);
            }
            return MainBytes;
        }
        public byte[] GetBytes() => this;

        /// <summary>
        /// ��ָ���ֽڴ���ĳ������ת��Ϊ�������͵ķ���,��ҪĿ���ֽڴ�������
        /// </summary>
        /// <param name="bytes">Ŀ���ֽڴ�</param>
        /// <param name="index">����</param>
        /// <returns></returns>
        public static PlayerActionData StaticBytesTo(byte[] bytes, int index = 0)
        {
            byte[] shortBytes = new byte[16];
            Array.Copy(bytes, index, shortBytes, 0, 16);
            PlayerActionData playerActionData = new(new(), new(), (PlayerAction)bytes[0]);
            playerActionData.SelfNumber = shortBytes[1];
            playerActionData.TargetPlayerNumber = shortBytes[2];
            int paiIndex = 4;
            for (int i = 0; i < 6; i++)
            {
                Pai singlePai = Pai.StaticBytesTo(shortBytes, paiIndex);
                if (singlePai != null)
                {
                    if (i <= 3)
                    {
                        // ǰ4��ΪPais
                        playerActionData.Pais.Add(singlePai);
                    }
                    else
                    {
                        // ��������TargetPais
                        playerActionData.TargetPais.Add(singlePai);
                    }
                }
                paiIndex += 4;
            }
            return playerActionData;
        }
        public PlayerActionData BytesTo(byte[] bytes, int index = 0) => StaticBytesTo(bytes, index);
    }
}
