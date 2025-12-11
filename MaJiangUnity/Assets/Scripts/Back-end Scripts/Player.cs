using MaJiangLib;
using System;
using static MaJiangLib.GlobalFunction;

/// <summary>
/// 玩家的类,当开始对局时,就会为每个玩家创建这个类
/// </summary>
public class Player : IPlayerInformation, IByteable<Player>
{
    /// <summary>
    /// 经由玩家外显信息创建玩家个人信息,用于用户端创建对局信息
    /// </summary>
    /// <param name="playerInformation"></param>
    public Player(IPlayerInformation playerInformation)
    {
        PlayerProfile = playerInformation.PlayerProfile;
        PlayerNumber = playerInformation.PlayerNumber;
        Point = playerInformation.Point;
        CurrentStageType = playerInformation.CurrentStageType;
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
    /// 玩家剩余点数
    /// </summary>
    public int Point { get; set; }
    /// <summary>
    /// 玩家手牌
    /// </summary>
    public ShouPai ShouPai { get; set; }
    /// <summary>
    /// 玩家当前状态
    /// </summary>
    public StageType CurrentStageType { get; set; }
    /// <summary>
    /// 玩家操作的行为选择
    /// </summary>
    /// <param name="playerActionData"></param>
    /// <returns></returns>
    public bool DoAction(PlayerActionData playerActionData)
    {

        return false;
    }
    public const int byteSize = 216;
    public int ByteSize { get=> byteSize; }
    public static implicit operator byte[](Player player)
    {
        // 96 bytes PlayerProfile + 1 byte PlayerNumber + 1 byte CurrentStageType + 18 bytes 留空 + 4 bytes Point + 96 bytes ShouPai = 216 bytes
        Span<byte> MainBytes = new byte[216];
        ReplaceBytes(MainBytes, player.PlayerProfile, 0);
        MainBytes[96] = (byte)player.PlayerNumber;
        MainBytes[97] = (byte)player.CurrentStageType;
        ReplaceBytes(MainBytes, BitConverter.GetBytes(player.Point), 116);
        ReplaceBytes(MainBytes, player.ShouPai, 120);
        return MainBytes.ToArray();
    }
    public static Player StaticBytesTo(byte[] bytes, int index = 0)
    {
        byte[] shortBytes = new byte[216];
        Array.Copy(bytes, index, shortBytes, 0, 216);
        Player player = new Player();
        player.PlayerProfile = UserProfile.StaticBytesTo(shortBytes, 0);
        player.PlayerNumber = shortBytes[96];
        player.CurrentStageType = (StageType)shortBytes[97];
        player.Point = BitConverter.ToInt32(shortBytes, 116);
        player.ShouPai = ShouPai.StaticBytesTo(shortBytes, 120);
        return player;
    }
    public Player BytesTo(byte[] bytes, int index = 0) => StaticBytesTo(bytes, index);
    public byte[] GetBytes() => this;
}
