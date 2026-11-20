using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <param name="tiles">手中所要响应的牌</param>
        /// <param name="targetTile">被响应的牌</param>
        /// <param name="playerAction">对应操作</param>
        public PlayerActionData(List<Tile> tiles, Tile targetTile, PlayerAction playerAction)
        {
            Tiles = tiles;
            TargetTile = targetTile;
            PlayerAction = playerAction;
        }
        /// <summary>
        /// 完全实现,需要响应者,被响应者,能响应的牌,目标牌和对应操作
        /// </summary>
        /// <param name="selfNumber"></param>
        /// <param name="targetNumber"></param>
        /// <param name="tiles"></param>
        /// <param name="targetTile"></param>
        /// <param name="playerAction"></param>
        public PlayerActionData(int selfNumber, int targetNumber, List<Tile> tiles, Tile targetTile, PlayerAction playerAction)
        {
            SelfNumber = selfNumber;
            TargetPlayerNumber = targetNumber;
            Tiles = tiles;
            TargetTile = targetTile;
            PlayerAction = playerAction;
        }
        /// <summary>
        /// 进行切牌拔北暗杠加杠立直和牌等自身操作时的玩家行为记录,需要能操作的牌和对应操作,或要和牌的目标牌
        /// </summary>
        /// <param name="tiles">能操作的牌</param>
        /// <param name="playerAction">对应操作</param>
        public PlayerActionData(List<Tile> tiles, PlayerAction playerAction)
        {
            if (playerAction == PlayerAction.Ron || playerAction == PlayerAction.Tsumo)
            {
                TargetTile = tiles != null && tiles.Count > 0 ? tiles[0] : null;
                Tiles = new();
            }
            else
            {
                Tiles = tiles ?? new();
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
        public List<Tile> Tiles { get; set; }
        /// <summary>
        /// 被操作的目标牌,仅一张
        /// </summary>
        public Tile TargetTile { get; set; }
        /// <summary>
        /// 目标玩家的序号
        /// </summary>
        public int TargetPlayerNumber { get; set; }
        /// <summary>
        /// 自己的序号
        /// </summary>
        public int SelfNumber { get; set; }
    }
}
