using MaJiangLib;
using System.Net;
using UnityEngine;
/// <summary>
/// ������Ҷ�ӵ�е����������,ÿ�����ֻ����һ�������ʵ��
/// </summary>
public class NetworkControl : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        CurrentNetworkStatu = NetworkStatu.Offline;
    }

    // Update is called once per frame
    void Update()
    {

    }

    public static Client SelfClient { get; set; } = new();
    /// <summary>
    /// ���˵�ǰ����״̬
    /// </summary>
    public static NetworkStatu CurrentNetworkStatu { get; set; }
    /// <summary>
    /// ��������Ƿ�Ϊ����
    /// </summary>
    public static bool IsHost { get; set; }
    /// <summary>
    /// ����Ƿ��ڶԾ���
    /// </summary>
    public static bool IsPlaying { get; set; }
    public static float BaseTime { get; set; }
    public static float ThinkingTime { get; set; }
    /// <summary>
    /// �������ڷ���ķ����
    /// </summary>
    public static int RoomNumber { get; set; }
    /// <summary>
    /// ��Ҫ���ӵ�IP��ַ
    /// </summary>
    public static IPAddress ConnectingIPAddress { get; set; }
    /// <summary>
    /// ��Ϊ�ͻ���ʱ,ΪҪ���ӵĶ˿�,��Ϊ�����ʱ,Ϊ�������
    /// </summary>
    public static int ConnectingPort { get; set; }
    /// <summary>
    /// ����IP��ַ
    /// </summary>
    public static IPAddress SelfIPAddress { get; set; }
    /// <summary>
    /// �������ӷ���,���ڵ�ǰΪFree״̬�¿���,���Ӻ󼴳��Կ�ʼ��ȡ
    /// </summary>
    /// <param name="networkException"></param>
    /// <returns></returns>
    public static bool TryConnect(out NetworkException networkException)
    {
        networkException = null;
        if (IsHost)
        {
            networkException = new("����Ϊ����,����������������");
            return false;
        }
        else if (CurrentNetworkStatu == NetworkStatu.Offline)
        {
            networkException = new("��ǰΪ����״̬,�޷�����");
            return false;
        }
        else if (CurrentNetworkStatu == NetworkStatu.InRoom)
        {
            networkException = new("���ڷ�����,�޷�����");
            return false;
        }
        else
        {
            if (CurrentNetworkStatu != NetworkStatu.Free)
            {
                networkException = new("��ǰæµ��,�޷�����");
                return false;
            }
            else
            {
                CurrentNetworkStatu = NetworkStatu.Connecting;
                bool isConnect = SelfClient.TimeOutConnectAsync(ConnectingIPAddress, ConnectingPort);
                if (isConnect)
                {
                    SelfClient.SubReadAsync();
                    return true;
                }
                else
                {
                    networkException = new("���ӳ�ʱ");
                    return false;
                }
            }
        }
    }
    /// <summary>
    /// ���뷿��,����Connecting״̬�¿���
    /// </summary>
    /// <param name="networkException"></param>
    /// <param name="roomNumber"></param>
    /// <returns></returns>
    public static bool JoinRoom(out NetworkException networkException, int roomNumber)
    {
        networkException = null;
        if (CurrentNetworkStatu == NetworkStatu.Connecting)
        {
            RoomNumber = roomNumber;
            CurrentNetworkStatu = NetworkStatu.InRoom;
            return true;
        }
        else
        {
            networkException = new("����״̬����,�޷����뷿��");
            return false;
        }
    }
    public static void RoomWordSolve(string word)
    {
        // [TODO]
        Debug.Log(word);
    }
    public static bool ReadySend(out NetworkException networkException)
    {
        networkException = null;
        if (CurrentNetworkStatu == NetworkStatu.InRoom)
        {
            // [TODO]
            CurrentNetworkStatu = NetworkStatu.ReadyWait;

            return true;
        }
        else
        {
            networkException = new("״̬����");
            return false;
        }
    }
    public static bool UnReadySend(out NetworkException networkException)
    {
        networkException = null;
        if (CurrentNetworkStatu == NetworkStatu.Ready)
        {
            // [TODO]
            CurrentNetworkStatu = NetworkStatu.UnReadyWait;
            return true;
        }
        else
        {
            networkException = new("�ڷ�׼��״̬��ȡ��׼��");
            return false;
        }
    }
    public static bool StartSolve(out NetworkException networkException)
    {
        networkException = null;
        if (CurrentNetworkStatu == NetworkStatu.Ready)
        {
            CurrentNetworkStatu = NetworkStatu.StartWait;
            return true;
        }
        else
        {
            networkException = new("�ڷ�׼��״̬�½��յ���ʼȷ��");
            return false;
        }
    }
    public static bool ActionSend(out NetworkException networkException, PlayerActionData playerActionData)
    {
        networkException = null;
        if (CurrentNetworkStatu == NetworkStatu.ActionWait)
        {
            // [TODO]
            return true;
        }
        else
        {
            networkException = new("�ڲ��ɲ���������³��Է��������Ϊ");
            return false;
        }
    }
    public static bool EndSolve(out NetworkException networkException)
    {
        networkException = null;
        if (IsPlaying)
        {
            CurrentNetworkStatu = NetworkStatu.EndStage;
            return true;
        }
        else
        {
            networkException = new("�ڷǶԾ�״̬�½��յ���ֹ�Ծ��ź�");
            return false;
        }
    }
    /// <summary>
    /// ȷ������Ϣ�Ľ��
    /// </summary>
    /// <param name="acknowledgeType"></param>
    /// <returns></returns>
    public static bool AcknowledgeSolve(SignalType acknowledgeType)
    {
        if (CurrentNetworkStatu == NetworkStatu.ReadyWait && acknowledgeType == SignalType.ReadyAck)
        {
            // �յ�׼��ȷ�Ϻ�,��ReadyWait��ΪReady
            CurrentNetworkStatu = NetworkStatu.Ready;
        }
        else if (CurrentNetworkStatu == NetworkStatu.UnReadyWait && acknowledgeType == SignalType.UnReadyAck)
        {
            // �յ�ȡ��׼��ȷ�Ϻ�,��UnReadyWait��ΪInRoom
            CurrentNetworkStatu = NetworkStatu.InRoom;
        }
        return false;
    }
    /// <summary>
    /// ΪPackCoder.Ping�İ�װ
    /// </summary>
    public static double Ping
    {
        get { return PackCoder.Ping; }
    }

}
/// <summary>
/// �ź�ö��,���������źŵ�����
/// </summary>
public enum SignalType : byte
{
    Ready = 1,
    ReadyAck,
    UnReady,
    UnReadyAck,
    Start,
    StartAck,

}
public enum NetworkStatu
{
    /// <summary>
    /// ����״̬
    /// </summary>
    Offline = 1,
    /// <summary>
    /// ����״̬
    /// </summary>
    Free,
    /// <summary>
    /// ������,Ϊ���뷿��ȴ���Ӧʱ��״̬
    /// </summary>
    Connecting,
    /// <summary>
    /// �ڷ�����,Ϊ�Ѽ��뷿�䵫δ׼����״̬
    /// </summary>
    InRoom,
    /// <summary>
    /// ׼������Ӧ,(�û���)����׼����δ�յ���Ӧʱ��״̬
    /// </summary>
    ReadyWait,
    /// <summary>
    /// ��׼��,(�û���)��Ӧ׼�����״̬
    /// </summary>
    Ready,
    /// <summary>
    /// ȡ��׼��,(�û���)����ȡ��׼����δ�յ���Ӧʱ��״̬
    /// </summary>
    UnReadyWait,
    /// <summary>
    /// �ȴ���ʼ,Ϊ׼����ʼ��Ϸʱ��״̬
    /// </summary>
    StartWait,
    /// <summary>
    /// �ȴ���ʼ��,(�û���)StartWait����ɺ�ȴ���ʼ����Ϣ��״̬,(�ͻ���)����Init��Ϣ,�ȴ���Ӧ��״̬
    /// </summary>
    InitWait,
    /// <summary>
    /// �ȴ�����,(�û���)�ѳ�ʼ��,���������δ��ʼ��ʱ��״̬
    /// </summary>
    OtherWait,
    /// <summary>
    /// �ȴ��غ�,��ҵȴ�����/����ʱ�����״̬
    /// </summary>
    RoundWait,
    /// <summary>
    /// ���ƽ׶�,������ƻ���Ӧ�����ʱ�������ƽ׶�,�ɼӸ�/�α�/����/��ֱ/����/���־�������,�ɷ���˷�������ʱ����
    /// </summary>
    DiscardStage,
    /// <summary>
    /// ��Ӧ�׶�,�����/����/�α��������Ӧ�ȴ��׶�,�ȴ������ȷ��
    /// </summary>
    ClaimStageWait,
    /// <summary>
    /// ��Ӧ�ȴ��׶�,�ȴ���������Ӧ
    /// </summary>
    ClaimStage,
    /// <summary>
    /// �ȴ�����,(�û���)����˷������������ʱ����,(�ͻ���)�û��˿�����ʱ,�ȴ�����
    /// </summary>
    ActionWait,
    /// <summary>
    /// �����ȴ�
    /// </summary>
    EndWait,
    /// <summary>
    /// ����״̬
    /// </summary>
    EndStage,
    /// <summary>
    /// �Ծֽ���״̬
    /// </summary>
    MainEndStage,
}