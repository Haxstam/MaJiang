using MaJiangLib;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// ��ҿ����Ե���Ϣ,���Ծ���������Ҷ��ܿ�������Ϣ
/// </summary>
public interface IPlayerInformation
{
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
    /// ��ҵ�ǰ״̬
    /// </summary>
    public StageType CurrentStageType { get; set; }
}
