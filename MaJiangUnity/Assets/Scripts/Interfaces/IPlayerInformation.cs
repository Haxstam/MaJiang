using MaJiangLib;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 玩家可外显的信息,即对局中所有玩家都能看见的信息
/// </summary>
public interface IPlayerInformation
{
    /// <summary>
    /// 玩家本身的用户信息
    /// </summary>
    public UserProfile PlayerProfile { get; set; }
    /// <summary>
    /// 玩家编号,也即座次,以东一时的东家为0
    /// </summary>
    public int PlayerNumber { get; set; }
    /// <summary>
    /// 玩家剩余点数
    /// </summary>
    public int Point { get; set; }
    /// <summary>
    /// 玩家当前状态
    /// </summary>
    public StageType CurrentStageType { get; set; }
}
