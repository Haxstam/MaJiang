using MaJiangLib;
using System;
using UnityEngine;
using static MaJiangLib.GlobalFunction;

/// <summary>
/// ��ҵ���,����ʼ�Ծ�ʱ,�ͻ�Ϊÿ����Ҵ��������
/// </summary>
public class Player :IPlayerInformation, IByteable<Player>
{
    /// <summary>
    /// �������������Ϣ������Ҹ�����Ϣ,�����û��˴����Ծ���Ϣ
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
    // ������ڴ洢����ұ��˿ɼ��Ϳɲ����ĳ�Ա�ͽ����ó�Ա
    /// <summary>
    /// ��ұ�����û���Ϣ
    /// </summary>
    public UserProfile PlayerProfile { get; set; }
    /// <summary>
    /// ��ұ��,Ҳ������,�Զ�һʱ�Ķ���Ϊ0
    /// </summary>
    public int PlayerNumber { get; set; }
    /// <summary>
    /// ���ʣ�����
    /// </summary>
    public int Point { get; set; }
    /// <summary>
    /// �������
    /// </summary>
    public ShouPai ShouPai { get; set; }
    /// <summary>
    /// ��ҵ�ǰ״̬
    /// </summary>
    public StageType CurrentStageType { get; set; }
    /// <summary>
    /// ��Ҳ�������Ϊѡ��
    /// </summary>
    /// <param name="playerActionData"></param>
    /// <returns></returns>
    public bool DoAction(PlayerActionData playerActionData)
    {

        return false;
    }

    public int ByteSize { get; set; } = 216;
    public static implicit operator byte[](Player player)
    {
        // 96 bytes PlayerProfile + 1 byte PlayerNumber + 1 byte CurrentStageType + 18 bytes ���� + 4 bytes Point + 96 bytes ShouPai = 216 bytes
        byte[] MainBytes = new byte[216];
        ReplaceBytes(MainBytes, player.PlayerProfile, 0);
        MainBytes[96] = (byte)player.PlayerNumber;
        MainBytes[97] = (byte)player.CurrentStageType;
        ReplaceBytes(MainBytes, BitConverter.GetBytes(player.Point), 116);
        ReplaceBytes(MainBytes, player.ShouPai, 120);
        return MainBytes;
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
