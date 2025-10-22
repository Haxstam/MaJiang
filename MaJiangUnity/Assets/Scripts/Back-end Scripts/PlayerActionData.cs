using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
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
        public PlayerActionData(List<Pai> pais, Pai targetPai, PlayerAction playerAction)
        {
            Pais = pais;
            TargetPai = targetPai;
            PlayerAction = playerAction;
        }
        /// <summary>
        /// ��ȫʵ��,��Ҫ��Ӧ��,����Ӧ��,����Ӧ����,Ŀ���ƺͶ�Ӧ����
        /// </summary>
        /// <param name="selfNumber"></param>
        /// <param name="targetNumber"></param>
        /// <param name="pais"></param>
        /// <param name="targetPai"></param>
        /// <param name="playerAction"></param>
        public PlayerActionData(int selfNumber, int targetNumber, List<Pai> pais, Pai targetPai, PlayerAction playerAction)
        {
            SelfNumber = selfNumber;
            TargetPlayerNumber = targetNumber;
            Pais = pais;
            TargetPai = targetPai;
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
                TargetPai = pais[0];
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
        /// ��������Ŀ����,��һ��
        /// </summary>
        public Pai TargetPai { get; set; }
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
             * 2  bytes TargetPais�Ĵ洢
             * 2  bytes ����
             */
            byte[] MainBytes = new byte[16];
            MainBytes[0] = (byte)playerActionData.PlayerAction;
            MainBytes[1] = (byte)playerActionData.SelfNumber;
            MainBytes[2] = (byte)playerActionData.TargetPlayerNumber;
            if (playerActionData.Pais.Count > 4)
            {
                // �����Ա�����ཫ����ת��
                throw new System.Exception($"��Ҳ���ת��ʱ������������Ŀ:{playerActionData.Pais},{playerActionData.TargetPai}");
            }
            else
            {
                ReplaceBytes(playerActionData, ListToBytes(playerActionData.Pais), 4);
                ReplaceBytes(playerActionData, playerActionData.TargetPai, 12);
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
            PlayerActionData playerActionData = new(new(), new(Color.Wans, 1), (PlayerAction)bytes[0]);
            playerActionData.SelfNumber = shortBytes[1];
            playerActionData.TargetPlayerNumber = shortBytes[2];
            int paiIndex = 4;
            for (int i = 0; i < 5; i++)
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
                        playerActionData.TargetPai = singlePai;
                    }
                }
                paiIndex += 4;
            }
            return playerActionData;
        }
        public PlayerActionData BytesTo(byte[] bytes, int index = 0) => StaticBytesTo(bytes, index);

        public static bool operator ==(PlayerActionData a, PlayerActionData b)
        {
            if ((a as object) == null && (b as object) == null)
            {
                return true;
            }
            else if ((a as object) == null || (b as object) == null)
            {
                return false;
            }
            if (a.SelfNumber != b.SelfNumber)
            {
                return false;
            }
            if (!a.Pais.SequenceEqual(b.Pais))
            {
                return false;
            }
            if (a.TargetPai != b.TargetPai || a.SelfNumber != b.SelfNumber || a.TargetPlayerNumber != b.TargetPlayerNumber || a.PlayerAction != b.PlayerAction)
            {
                return false;
            }
            return true;
        }
        public static bool operator != (PlayerActionData a, PlayerActionData b) 
        { 
            return !(a == b); 
        }
        public override bool Equals(object obj)
        {
            PlayerActionData playerActionData = obj as PlayerActionData;
            if (playerActionData != null)
            {
                return playerActionData == this;
            }
            else 
            {
                return false; 
            }
        }
        public override int GetHashCode()
        {
            HashCode paisHash = new();
            foreach (Pai listPai in Pais)
            {
                paisHash.Add(listPai.GetHashCode());
            }
            return HashCode.Combine(SelfNumber, TargetPlayerNumber, paisHash.ToHashCode(), TargetPai, PlayerAction);
        }
    }
}
