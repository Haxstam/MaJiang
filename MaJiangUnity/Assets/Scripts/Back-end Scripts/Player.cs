using MaJiangLib;
using UnityEngine;

/// <summary>
/// ��ҵ���,����ʼ�Ծ�ʱ,�ͻ�Ϊÿ����Ҵ��������
/// </summary>
public class Player : MonoBehaviour, IPlayerInformation
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
}
