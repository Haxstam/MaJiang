using MaJiangLib;
using System;
using UnityEngine;
using static MaJiangLib.GlobalFunction;

/// <summary>
/// ��ҵ���,����ʼ�Ծ�ʱ,�ͻ�Ϊÿ����Ҵ��������
/// </summary>
public class Player : MonoBehaviour, IPlayerInformation, IByteable
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

    public int ByteSize { get; set; }
    public static implicit operator byte[](Player player)
    {
        // 96 bytes PlayerProfile + 1 byte PlayerNumber + 1 byte CurrentStageType + 38 bytes ���� + 4 bytes Point + 160 bytes ShouPai = 300 bytes
        byte[] MainBytes = new byte[300];
        ReplaceBytes(MainBytes, player.PlayerProfile, 0);
        MainBytes[96] = (byte)player.PlayerNumber;
        MainBytes[97] = (byte)player.CurrentStageType;
        ReplaceBytes(MainBytes, BitConverter.GetBytes(player.Point), 136);
        ReplaceBytes(MainBytes, player.ShouPai, 140);
        return MainBytes;
    }
    public byte[] GetBytes() => this;
}
