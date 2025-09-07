using System;
using System.Collections.Generic;
using static MaJiangLib.GlobalFunction;
namespace MaJiangLib
{
    /// <summary>
    /// ��ҽ��в�������,������Ҳ���ʱ������,����Ŀ����,��Ҳ�������
    /// </summary>
    public class PlayerActionData
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
        /// <summary>
        /// ����Ҳ�����Ϣ���ֽڵ���ʽת��,���ȹ̶�32Bytes,����������Ա����ת��
        /// </summary>
        /// <param name="playerAction">Ŀ�����</param>
        public static implicit operator byte[](PlayerActionData playerActionData)
        {
            /*
             * �ṹ:
             * 1  byte  ��������
             * 1  byte  �������������,��������
             * 1  byte  �������������,����������
             * 5  bytes ����
             * 16 bytes Pais�����Ĵ洢,�ɴ洢���4�Ų�����,������0
             * 8  bytes TargetPais�Ĵ洢,�ɴ洢���2�ű�������,������0
             */
            byte[] MainBytes = new byte[32];
            MainBytes[0] = (byte)playerActionData.PlayerAction;
            if (playerActionData.Pais.Count > 4 || playerActionData.TargetPais.Count > 2)
            {
                // �����Ա�����ཫ����ת��
                throw new System.Exception($"��Ҳ���ת��ʱ������������Ŀ:{playerActionData.Pais},{playerActionData.TargetPais}");
            }
            else
            {
                int index = 8;
                for (int i = 0; i < playerActionData.Pais.Count; i++)
                {
                    ReplaceBytes(MainBytes, playerActionData.Pais[i], index);
                    index += 4;
                }
                index = 24;
                for (int i = 0; i < playerActionData.TargetPais.Count; i++)
                {
                    ReplaceBytes(MainBytes, playerActionData.TargetPais[i], index);
                }
            }
            return MainBytes;
        }
        /// <summary>
        /// ���ֽڵ���Ҳ�����Ϣ����ʽת��,���󳤶ȹ̶�Ϊ32bytes���ֽڴ�
        /// </summary>
        /// <param name="bytes"></param>
        public static implicit operator PlayerActionData(byte[] bytes)
        {
            if (bytes.Length != 32)
            {
                throw new System.Exception($"�ֽڴ�����{bytes.Length}bytes������ת����32bytesҪ��");
            }
            else
            {
                PlayerActionData playerActionData = new(new(), new(), (PlayerAction)bytes[0]);
                playerActionData.SelfNumber = bytes[1];
                playerActionData.TargetPlayerNumber = bytes[2];
                int index = 8;
                for (int i = 0; i < 6; i++)
                {
                    Pai singlePai = Pai.GetPai(bytes, index);
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
                    index += 4;
                }
                return playerActionData;
            }
        }
        /// <summary>
        /// ��ָ���ֽڴ���ĳ������ת��Ϊ�������͵ķ���,��ҪĿ���ֽڴ�������
        /// </summary>
        /// <param name="bytes">Ŀ���ֽڴ�</param>
        /// <param name="index">����</param>
        /// <returns></returns>
        public static PlayerActionData GetPlayerActionData(byte[] bytes, int index = 0)
        {
            byte[] shortBytes = new byte[32];
            Array.Copy(bytes, index, shortBytes, 0, 32);
            return shortBytes;
        }
    }
}
