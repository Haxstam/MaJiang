using MaJiangLib;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

public class MatchServer : MonoBehaviour
{
    private void Start()
    {

    }
    private void Update()
    {

    }
    /// <summary>
    /// �Ծַ������ĳ�ʼ��,��Ϊ���������Ծ�ʱ�����ȵ���
    /// </summary>
    public void Init()
    {
        SelfServer = new();
    }
    /// <summary>
    /// �Ծֵĳ�ʼ��
    /// </summary>
    public void MatchInit()
    {

    }
    /// <summary>
    /// �����ӵ��Ծ�˳λ��ӳ���
    /// </summary>
    public TcpClient[] tcpClientNumberList = new TcpClient[4];
    /// <summary>
    /// �Ծ����������������÷�����
    /// </summary>
    public Server SelfServer { get; private set; }
    /// <summary>
    /// �Ծֿ�����
    /// </summary>
    public MainMatchControl MatchControl { get; set; }
    /// <summary>
    /// ��ҿ��в������б�,ÿ�����˴��ʱ����,��Ӧ/���������
    /// </summary>
    public Dictionary<PlayerAction, List<PlayerActionData>>[] AvailableActionDict { get; private set; } = new Dictionary<PlayerAction, List<PlayerActionData>>[4];
    /// <summary>
    /// ���������,�����Ƶ��б�.���ǵ�����(�����ı�����)ռ���Ƶĺܴ�һ����,��˵����洢�Ӷ���������
    /// </summary>
    public Dictionary<Pai, List<Group>>[] AvailableTingPaiDict { get; private set; } = new Dictionary<Pai, List<Group>>[4];
    /// <summary>
    /// ���ÿ�����еĵȴ�ʱ��,���Խ��в���ʱ����Ϊ5s(Ĭ��)
    /// </summary>
    public float[] PlayerBaseTimeList { get; private set; } = new float[4];
    /// <summary>
    /// �����һ��������ͨ�õ���˼��ʱ��,���ڿ�ʼ�µı���ʱ����Ϊ20s(Ĭ��),��Ϊ����ǿ����������
    /// </summary>
    public float[] PlayerThinkingTimeCountList { get; private set; } = new float[4];
    /// <summary>
    /// �����Ϊ�������,���ս��в����������ź������,���ж��Ƿ����
    /// </summary>
    /// <param name="playerActionData"></param>
    /// <param name="player"></param>
    /// <returns></returns>
    public bool PlayerActionSolve(PlayerActionData playerActionData, int player)
    {
        if (AvailableActionDict != null)
        {
            Dictionary<PlayerAction, List<PlayerActionData>> playerActionDict = AvailableActionDict[player];
            // �б�
            List<PlayerActionData> actionList = playerActionDict[playerActionData.PlayerAction];
            if (actionList.Count == 0)
            {
                // ����û�ж�Ӧ����,����false
                return false;
            }
            else
            {
                return actionList.Contains(playerActionData);
            }
        }
        else
        {
            throw new System.Exception("������Ĳ����ж�");
        }

    }
    /// <summary>
    /// �����˴����ʱ����,��ȡ���п��е���Ӧ������
    /// </summary>
    /// <returns></returns>
    public bool AvailableActionSend()
    {
        for (int i = 0; i < MatchControl.PlayerList.Count; i++)
        {
            ShouPai shouPai = MatchControl.PlayerList[i].ShouPai;
            // ���ݳ������λ�ȡ������ҵĿ��в������洢

            // ����(��/��/��)�Ŀ��в���
            AvailableActionDict[i] = GlobalFunction.SingleClaimingAvailableJudge(shouPai, MatchControl);
            // �ٺ͵Ŀ��в���
            if (AvailableTingPaiDict[i].ContainsKey(MatchControl.CurrentPai))
            {
                // ��ΪֻҪ�ܺ�,���ǹ̶���,��˽���ǿ����ٺ�
                AvailableActionDict[i].Add(PlayerAction.Ron, new());
            }
        }

        // [TODO] Send
        throw new System.Exception("δ���");

        return false;
    }
    /// <summary>
    /// �����б�ˢ��,����������Ʒ����ı�,������/�α�/����/����ʱ����,���´���ҵ������б�
    /// </summary>
    /// <param name="playerNumber">�ı��Լ����Ƶ�������</param>
    /// <returns></returns>
    public bool TingPaiRefresh(int playerNumber)
    {
        if (GlobalFunction.TingPaiJudge(MatchControl.PlayerList[playerNumber].ShouPai, out Dictionary<Pai, List<Group>> groups))
        {
            // ����(�������Ƿ�������)���޸��ֵ�
            AvailableTingPaiDict[playerNumber] = groups;
            return true;
        }
        else
        {
            // �жϺ���δ����,�ÿ�
            AvailableTingPaiDict[playerNumber] = null;
            return true;
        }
    }
    /// <summary>
    /// ����ʱ���ˢ��,�����±�����ʼʱ����
    /// </summary>
    public void ThinkingTimeReset()
    {
        PlayerThinkingTimeCountList = new float[4] { 20f, 20f, 20f, 20f };
    }
}
