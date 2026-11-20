using MaJiangLib;
using System;

/// <summary>
/// 玩家的类,当开始对局时,就会为每个玩家创建这个类
/// </summary>
public class Player : IPlayerInformation
{
    /// <summary>
    /// 经由玩家外显信息创建玩家个人信息,用于用户端创建对局信息
    /// </summary>
    /// <param name="playerInformation"></param>
    public Player(IPlayerInformation playerInformation)
    {
        PlayerProfile = playerInformation.PlayerProfile;
        PlayerNumber = playerInformation.PlayerNumber;
        CurrentStageType = playerInformation.CurrentStageType;
    }
    public Player(UserProfile playerProfile, int playerNumber, HandTile handTile, StageType currentStageType)
    {
        PlayerProfile = playerProfile;
        PlayerNumber = playerNumber;
        PlayerHandTile = handTile;
        CurrentStageType = currentStageType;
    }

    public Player()
    {

    }
    // 玩家类内存储仅玩家本人可见和可操作的成员和交互用成员
    /// <summary>
    /// 玩家本身的用户信息
    /// </summary>
    public UserProfile PlayerProfile { get; set; }
    /// <summary>
    /// 玩家编号,也即座次,以东一时的东家为0
    /// </summary>
    public int PlayerNumber { get; set; }
    /// <summary>
    /// 玩家手牌
    /// </summary>
    public HandTile PlayerHandTile { get; set; }
    /// <summary>
    /// 玩家当前状态
    /// </summary>
    public StageType CurrentStageType { get; set; }
}
