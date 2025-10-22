using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using static MaJiangLib.GlobalFunction;
namespace MaJiangLib
{
    /// <summary>
    /// 玩家进行操作的类,包含玩家操作时手中牌,操作目标牌,玩家操作类型
    /// </summary>
    public class PlayerActionData : IByteable<PlayerActionData>
    {
        /// <summary>
        /// 进行吃碰杠等鸣牌操作时的玩家行为记录,需要能响应的牌,目标牌和对应操作
        /// </summary>
        /// <param name="pais">手中所要响应的牌</param>
        /// <param name="targetPai">被响应的牌</param>
        /// <param name="playerAction">对应操作</param>
        public PlayerActionData(List<Pai> pais, Pai targetPai, PlayerAction playerAction)
        {
            Pais = pais;
            TargetPai = targetPai;
            PlayerAction = playerAction;
        }
        /// <summary>
        /// 完全实现,需要响应者,被响应者,能响应的牌,目标牌和对应操作
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
        /// 进行切牌拔北暗杠加杠立直和牌等自身操作时的玩家行为记录,需要能操作的牌和对应操作,或要和牌的目标牌
        /// </summary>
        /// <param name="pais">能操作的牌</param>
        /// <param name="playerAction">对应操作</param>
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
        /// 进行流局等特殊操作的玩家行为记录,需要玩家所进行的操作
        /// </summary>
        /// <param name="playerAction">对应操作</param>
        public PlayerActionData(PlayerAction playerAction)
        {
            PlayerAction = playerAction;
        }
        public PlayerAction PlayerAction { get; set; }
        /// <summary>
        /// 手牌中待操作的牌,成员数最大为4
        /// </summary>
        public List<Pai> Pais { get; set; }
        /// <summary>
        /// 被操作的目标牌,仅一张
        /// </summary>
        public Pai TargetPai { get; set; }
        /// <summary>
        /// 目标玩家的序号
        /// </summary>
        public int TargetPlayerNumber { get; set; }
        /// <summary>
        /// 自己的序号
        /// </summary>
        public int SelfNumber { get; set; }
        public int ByteSize { get; set; } = 16;
        /// <summary>
        /// 从玩家操作信息到字节的隐式转换,长度固定16Bytes,不允许过大成员数的转换
        /// </summary>
        /// <param name="playerAction">目标操作</param>
        public static implicit operator byte[](PlayerActionData playerActionData)
        {
            /*
             * 结构:
             * 1  byte  操作类型
             * 1  byte  操作发出者序号,即鸣牌者
             * 1  byte  操作承受者序号,即被鸣牌者
             * 1  bytes 留空
             * 8  bytes Pais变量的存储,可存储最大4张操作牌,不足则补0
             * 2  bytes TargetPais的存储
             * 2  bytes 留空
             */
            byte[] MainBytes = new byte[16];
            MainBytes[0] = (byte)playerActionData.PlayerAction;
            MainBytes[1] = (byte)playerActionData.SelfNumber;
            MainBytes[2] = (byte)playerActionData.TargetPlayerNumber;
            if (playerActionData.Pais.Count > 4)
            {
                // 如果成员数过多将不能转换
                throw new System.Exception($"玩家操作转换时出现意外牌数目:{playerActionData.Pais},{playerActionData.TargetPai}");
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
        /// 从指定字节串中某索引处转换为操作类型的方法,需要目标字节串和索引
        /// </summary>
        /// <param name="bytes">目标字节串</param>
        /// <param name="index">索引</param>
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
                        // 前4张为Pais
                        playerActionData.Pais.Add(singlePai);
                    }
                    else
                    {
                        // 后两张是TargetPais
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
