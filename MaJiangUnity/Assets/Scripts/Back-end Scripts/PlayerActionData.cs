using System;
using System.Collections.Generic;
using static MaJiangLib.GlobalFunction;
namespace MaJiangLib
{
    /// <summary>
    /// 玩家进行操作的类,包含玩家操作时手中牌,操作目标牌,玩家操作类型
    /// </summary>
    public class PlayerActionData
    {
        /// <summary>
        /// 进行吃碰杠等鸣牌操作时的玩家行为记录,需要能响应的牌,目标牌和对应操作
        /// </summary>
        /// <param name="pais">手中所要响应的牌</param>
        /// <param name="targetPai">被响应的牌</param>
        /// <param name="playerAction">对应操作</param>
        public PlayerActionData(List<Pai> pais, List<Pai> targetPai, PlayerAction playerAction)
        {
            Pais = pais;
            TargetPais = targetPai;
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
                TargetPais = pais;
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
        /// 被操作的目标牌,注意:当本类存储已确定的操作信息时,TargetPais默认仅包含一个确定对象,成员数最大为2
        /// </summary>
        public List<Pai> TargetPais { get; set; }
        /// <summary>
        /// 目标玩家的序号
        /// </summary>
        public int TargetPlayerNumber { get; set; }
        /// <summary>
        /// 自己的序号
        /// </summary>
        public int SelfNumber { get; set; }
        /// <summary>
        /// 从玩家操作信息到字节的隐式转换,长度固定32Bytes,不允许过大成员数的转换
        /// </summary>
        /// <param name="playerAction">目标操作</param>
        public static implicit operator byte[](PlayerActionData playerActionData)
        {
            /*
             * 结构:
             * 1  byte  操作类型
             * 1  byte  操作发出者序号,即鸣牌者
             * 1  byte  操作承受者序号,即被鸣牌者
             * 5  bytes 留空
             * 16 bytes Pais变量的存储,可存储最大4张操作牌,不足则补0
             * 8  bytes TargetPais的存储,可存储最大2张被操作牌,不足则补0
             */
            byte[] MainBytes = new byte[32];
            MainBytes[0] = (byte)playerActionData.PlayerAction;
            if (playerActionData.Pais.Count > 4 || playerActionData.TargetPais.Count > 2)
            {
                // 如果成员数过多将不能转换
                throw new System.Exception($"玩家操作转换时出现意外牌数目:{playerActionData.Pais},{playerActionData.TargetPais}");
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
        /// 从字节到玩家操作信息的隐式转换,需求长度固定为32bytes的字节串
        /// </summary>
        /// <param name="bytes"></param>
        public static implicit operator PlayerActionData(byte[] bytes)
        {
            if (bytes.Length != 32)
            {
                throw new System.Exception($"字节串长度{bytes.Length}bytes不符合转换的32bytes要求");
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
                            // 前4张为Pais
                            playerActionData.Pais.Add(singlePai);
                        }
                        else
                        {
                            // 后两张是TargetPais
                            playerActionData.TargetPais.Add(singlePai);
                        }
                    }
                    index += 4;
                }
                return playerActionData;
            }
        }
        /// <summary>
        /// 从指定字节串中某索引处转换为操作类型的方法,需要目标字节串和索引
        /// </summary>
        /// <param name="bytes">目标字节串</param>
        /// <param name="index">索引</param>
        /// <returns></returns>
        public static PlayerActionData GetPlayerActionData(byte[] bytes, int index = 0)
        {
            byte[] shortBytes = new byte[32];
            Array.Copy(bytes, index, shortBytes, 0, 32);
            return shortBytes;
        }
    }
}
