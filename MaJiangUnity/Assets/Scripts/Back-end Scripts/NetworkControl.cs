using MaJiangLib;
using System.Net;
using UnityEngine;
/// <summary>
/// 所有玩家都拥有的网络控制类,每个玩家只能有一个此类的实例
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
    /// 本端当前网络状态
    /// </summary>
    public static NetworkStatu CurrentNetworkStatu { get; set; }
    /// <summary>
    /// 标记自身是否为房主
    /// </summary>
    public static bool IsHost { get; set; }
    /// <summary>
    /// 标记是否在对局中
    /// </summary>
    public static bool IsPlaying { get; set; }
    public static float BaseTime { get; set; }
    public static float ThinkingTime { get; set; }
    /// <summary>
    /// 自身所在房间的房间号
    /// </summary>
    public static int RoomNumber { get; set; }
    /// <summary>
    /// 正要连接的IP地址
    /// </summary>
    public static IPAddress ConnectingIPAddress { get; set; }
    /// <summary>
    /// 作为客户端时,为要连接的端口,作为服务端时,为自身房间号
    /// </summary>
    public static int ConnectingPort { get; set; }
    /// <summary>
    /// 自身IP地址
    /// </summary>
    public static IPAddress SelfIPAddress { get; set; }
    /// <summary>
    /// 尝试连接方法,仅在当前为Free状态下可用,连接后即尝试开始读取
    /// </summary>
    /// <param name="networkException"></param>
    /// <returns></returns>
    public static bool TryConnect(out NetworkException networkException)
    {
        networkException = null;
        if (IsHost)
        {
            networkException = new("自身为房主,不能连接其他房间");
            return false;
        }
        else if (CurrentNetworkStatu == NetworkStatu.Offline)
        {
            networkException = new("当前为离线状态,无法连接");
            return false;
        }
        else if (CurrentNetworkStatu == NetworkStatu.InRoom)
        {
            networkException = new("已在房间中,无法连接");
            return false;
        }
        else
        {
            if (CurrentNetworkStatu != NetworkStatu.Free)
            {
                networkException = new("当前忙碌中,无法连接");
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
                    networkException = new("连接超时");
                    return false;
                }
            }
        }
    }
    /// <summary>
    /// 加入房间,仅在Connecting状态下可用
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
            networkException = new("连接状态错误,无法加入房间");
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
            networkException = new("状态错误");
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
            networkException = new("在非准备状态下取消准备");
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
            networkException = new("在非准备状态下接收到开始确认");
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
            networkException = new("在不可操作的情况下尝试发送玩家行为");
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
            networkException = new("在非对局状态下接收到终止对局信号");
            return false;
        }
    }
    /// <summary>
    /// 确认类信息的解决
    /// </summary>
    /// <param name="acknowledgeType"></param>
    /// <returns></returns>
    public static bool AcknowledgeSolve(SignalType acknowledgeType)
    {
        if (CurrentNetworkStatu == NetworkStatu.ReadyWait && acknowledgeType == SignalType.ReadyAck)
        {
            // 收到准备确认后,从ReadyWait变为Ready
            CurrentNetworkStatu = NetworkStatu.Ready;
        }
        else if (CurrentNetworkStatu == NetworkStatu.UnReadyWait && acknowledgeType == SignalType.UnReadyAck)
        {
            // 收到取消准备确认后,从UnReadyWait变为InRoom
            CurrentNetworkStatu = NetworkStatu.InRoom;
        }
        return false;
    }
    /// <summary>
    /// 为PackCoder.Ping的包装
    /// </summary>
    public static double Ping
    {
        get { return PackCoder.Ping; }
    }

}
/// <summary>
/// 信号枚举,包含所有信号的类型
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
    /// 离线状态
    /// </summary>
    Offline = 1,
    /// <summary>
    /// 空闲状态
    /// </summary>
    Free,
    /// <summary>
    /// 连接中,为加入房间等待回应时的状态
    /// </summary>
    Connecting,
    /// <summary>
    /// 在房间中,为已加入房间但未准备的状态
    /// </summary>
    InRoom,
    /// <summary>
    /// 准备待回应,(用户端)发送准备后未收到回应时的状态
    /// </summary>
    ReadyWait,
    /// <summary>
    /// 已准备,(用户端)回应准备后的状态
    /// </summary>
    Ready,
    /// <summary>
    /// 取消准备,(用户端)发送取消准备后未收到回应时的状态
    /// </summary>
    UnReadyWait,
    /// <summary>
    /// 等待开始,为准备开始游戏时的状态
    /// </summary>
    StartWait,
    /// <summary>
    /// 等待初始化,(用户端)StartWait已完成后等待初始化消息的状态,(客户端)发送Init信息,等待回应的状态
    /// </summary>
    InitWait,
    /// <summary>
    /// 等待他人,(用户端)已初始化,但其他玩家未初始化时的状态
    /// </summary>
    OtherWait,
    /// <summary>
    /// 等待回合,别家等待出牌/鸣牌时自身的状态
    /// </summary>
    RoundWait,
    /// <summary>
    /// 舍牌阶段,玩家摸牌或响应别家牌时进入舍牌阶段,可加杠/拔北/暗杠/立直/自摸/九种九牌流局,由服务端返回摸牌时触发
    /// </summary>
    DiscardStage,
    /// <summary>
    /// 响应阶段,打出牌/开杠/拔北后进入响应等待阶段,等待服务端确认
    /// </summary>
    ClaimStageWait,
    /// <summary>
    /// 响应等待阶段,等待其他人响应
    /// </summary>
    ClaimStage,
    /// <summary>
    /// 等待操作,(用户端)服务端返回自身可鸣牌时触发,(客户端)用户端可鸣牌时,等待鸣牌
    /// </summary>
    ActionWait,
    /// <summary>
    /// 结束等待
    /// </summary>
    EndWait,
    /// <summary>
    /// 结束状态
    /// </summary>
    EndStage,
    /// <summary>
    /// 对局结束状态
    /// </summary>
    MainEndStage,
}