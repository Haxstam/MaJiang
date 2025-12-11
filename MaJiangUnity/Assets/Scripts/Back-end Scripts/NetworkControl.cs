using MaJiangLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
        if (Input.GetKeyUp(KeyCode.P))
        {
            MainClient.Connect("127.0.0.1", 33455);
        }
        if (Input.GetKeyUp(KeyCode.Q))
        {
            Pack p = new Pack();
            p.SignalPack(SignalType.ActionSendAck);
            MainClient.GetStream().Write(p.Bytes);
            Debug.Log("Send!");
        }
        // 增加所有计时器,计时器超时则删除
        foreach (var packMD5 in PackTimeOutDict.Keys.ToList())
        {
            PackTimeOutDict[packMD5] += Time.deltaTime;
            if (PackTimeOutDict[packMD5] >= 10f)
            {
                PackTimeOutDict.Remove(packMD5);
            }
        }
    }

    #region *Client模块*

    /// <summary>
    /// 用户进行连接时的客户端
    /// </summary>
    public TcpClient MainClient { get; set; } = new TcpClient();
    /// <summary>
    /// 客户端TCP连接缓存
    /// </summary>
    private byte[] ClientBuffer { get; set; } = new byte[8192];
    /// <summary>
    /// 客户端TCP数据流
    /// </summary>
    public NetworkStream ClientStream { get; set; }


    public static byte[] testBytes;

    public static IMatchInformation pubControl;
    public bool IsConnect { get; set; }
    public async void SubReadAsync()
    {
        // 因为Read读取的覆写性和读取时可能的不连续性,目前通过循环读取,拼接写入处理的方式获取数据
        byte[] clientBuffer = ClientBuffer;

        // 表示当前将要写入缓存的位置
        int writeIndex = 0;
        // 读取循环
        while (true)
        {
            // 读取循环
            // 每当ReadAsync从数据流中读取数据,都会读取最多1024字节的数据,如果写入指针接近上限,则缩小写入大小
            writeIndex += await ClientStream.ReadAsync(clientBuffer, writeIndex, Math.Min(1024, 8192 - writeIndex));
            if (writeIndex >= 1024)
            {   // 如果存在不少于1024字节的待处理数据,进行处理
                bool isPackReceived = ReadByteSolve(clientBuffer, ref writeIndex);
            }
        }
    }
    /// <summary>
    /// 通过Span匹配字节串的方法,需要主字节串和短字节串
    /// </summary>
    /// <param name="data"></param>
    /// <param name="shortData"></param>
    /// <returns></returns>
    public static int MatchBytes(byte[] mainBytes, byte[] shortBytes)
    {
        Span<byte> SpanBytes = mainBytes;
        ReadOnlySpan<byte> ShortSpanBytes = shortBytes;
        return SpanBytes.IndexOf(ShortSpanBytes);
    }
    /// <summary>
    /// 数据缓存处理,在方法内部根据判断修改buffer和写入指针
    /// </summary>
    /// <param name="buffer">对应连接的接收缓存</param>
    /// <param name="packBytes">处理后的包</param>
    /// <param name="writeIndex">当前写入位置</param>
    /// <returns>当本次处理获取到包时,返回True,如果无数据或无合法包,返回False</returns>
    public static bool ReadByteSolve(byte[] buffer, ref int writeIndex)
    {
        Span<byte> spanBuffer = buffer.AsSpan();
        bool isReceivePack = false;
        while (true)
        {
            // 处理循环
            // 循环判断直到没有任何合法包
            int endIndex = MatchBytes(buffer, PackCoder.PackEndBytes) + 8;
            if (endIndex == -1)
            {
                // 未获取到包尾,返回false
                return isReceivePack;
            }
            else if (endIndex >= 0 && endIndex < 1024)
            {
                // 获取到包尾,但位置小于1024,即为残缺的后半个包,修改该标记首位为0x00并将此包尾之后的数据前移
                buffer[endIndex] = 0x00;
                spanBuffer[(endIndex + 8)..].CopyTo(spanBuffer);
                writeIndex -= endIndex;
            }
            else
            {
                // 获取到包尾且位置不小于1024,进行包合法性判断
                Span<byte> rawPack = spanBuffer.Slice(endIndex - 1024, 1024);
                if (PackCoder.IsLegalPack(rawPack))
                {
                    // 判断成功,标记返回True和所提取的包
                    byte[] packBytes = rawPack.ToArray();
                    PackCoder.Ping = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - BitConverter.ToDouble(rawPack.Slice(8, 8));

                    isReceivePack = true;
                }
                else
                {
                    // 判断失败
                }
                // 无论成功与否,都修改标记首位为0x00并将此包尾之后的数据前移
                buffer[endIndex] = 0x00;
                spanBuffer[(endIndex + 8)..].CopyTo(spanBuffer);
                writeIndex -= 1024;
            }
        }
    }
    public void PackSolve(byte[] packBytes)
    {

    }
    #endregion

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
    public static IPAddress SelfIPAddress { get; set; } = IPAddress.Parse("127.0.0.2");
    /// <summary>
    /// 自身用户档案
    /// </summary>
    public static UserProfile SelfProfile { get; set; } = new("CHaxstam");
    /// <summary>
    /// 主对局控制
    /// </summary>
    public MainMatchControl MainMatchControl { get; set; }
    /// <summary>
    /// 为PackCoder.Ping的包装
    /// </summary>
    public static double Ping
    {
        get { return PackCoder.Ping; }
    }
    /// <summary>
    /// 包超时时间记录,每发送一个包即存储一个标记,用于存储其实际等待时间
    /// </summary>
    private Dictionary<TimeOutMark, float> PackTimeOutDict { get; set; } = new();
    /// <summary>
    /// 计时回调器
    /// </summary>
    private class TimeOutMark
    {
        public TimeOutMark(Func<bool> timeOutHandler, Func<bool> finishHandler, byte[] mD5Bytes)
        {
            TimeOutHandler = timeOutHandler;
            FinishHandler = finishHandler;
            MD5Bytes = mD5Bytes;
            WaitTime = 20f;
        }
        public TimeOutMark(Func<bool> timeOutHandler, Func<bool> finishHandler, byte[] mD5Bytes, SignalType signal)
        {
            TimeOutHandler = timeOutHandler;
            FinishHandler = finishHandler;
            MD5Bytes = mD5Bytes;
            WaitTime = 20f;
            SignalType = signal;
        }
        /// <summary>
        /// 超时处理方法
        /// </summary>
        public Func<bool> TimeOutHandler { get; set; }
        /// <summary>
        /// 返回处理方法
        /// </summary>
        public Func<bool> FinishHandler { get; set; }
        /// <summary>
        /// 包标识符,用作往返操作唯一标识
        /// </summary>
        public byte[] MD5Bytes { get; set; }
        public float WaitTime { get; set; }
        public SignalType SignalType { get; set; }
    }
    #region *方法*

    /// <summary>
    /// 尝试连接方法,仅在当前为Free状态下可用,连接后即尝试开始读取
    /// </summary>
    /// <param name="networkException"></param>
    /// <returns></returns>
    public bool Connect(out NetworkException networkException)
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
                MainClient.ConnectAsync(ConnectingIPAddress, ConnectingPort);
                return true;
            }
        }
    }
    public bool ConnectTimeOutHandler()
    {   // 连接超时,断开
        MainClient.Close();
        IsConnect = false;
        return false;
    }
    public bool ConnectFinishHandler()
    {   // 已连接,获取Stream
        ClientStream = MainClient.GetStream();
        return true;
    }
    /// <summary>
    /// 加入房间,仅在Connecting状态下可用
    /// </summary>
    /// <param name="networkException"></param>
    /// <param name="roomNumber"></param>
    /// <returns></returns>
    public bool JoinRoom(out NetworkException networkException, int roomNumber)
    {
        networkException = null;
        if (CurrentNetworkStatu == NetworkStatu.Connecting)
        {
            Pack pack = new();
            pack.ConnectRoomPack(roomNumber, SelfProfile);
            ClientStream.WriteAsync(pack.Bytes);
            PackTimeOutDict[new(JoinRoomTimeOutHandler, JoinRoomFinishHandler, pack.MD5Code)] = 0f;
            return true;
        }
        else
        {
            networkException = new("连接状态错误,无法加入房间");
            return false;
        }
    }
    /// <summary>
    /// 超时处理调用方法
    /// </summary>
    public bool JoinRoomTimeOutHandler()
    {
        CurrentNetworkStatu = NetworkStatu.Free;
        return false;
    }
    public bool JoinRoomFinishHandler()
    {
        CurrentNetworkStatu = NetworkStatu.InRoom;
        return true;
    }
    public void RoomWordSend(string word)
    {
        Pack pack = new();
        pack.RoomWordPack(RoomNumber, word);
        ClientStream.WriteAsync(pack.Bytes);
        PackTimeOutDict[new(RoomWordSendTimeOutHandler, RoomWordSendFinishHandler, pack.MD5Code)] = 0f;
        // [TODO]
        Debug.Log(word);
    }
    public bool RoomWordSendTimeOutHandler()
    {
        Debug.Log("发送失败");
        return false;
    }
    public bool RoomWordSendFinishHandler()
    {
        return true;
    }
    public void ReadySend()
    {
        Pack pack = new();
        pack.SignalPack(SignalType.Ready);
        ClientStream.WriteAsync(pack.Bytes);
        PackTimeOutDict[new(ReadySendTimeOutHandler, ReadySendFinishHandler, pack.MD5Code)] = 0f;
    }
    public bool ReadySendTimeOutHandler()
    {
        Debug.Log("Ready发送失败");
        return false;
    }
    public bool ReadySendFinishHandler()
    {
        Debug.Log("Ready Solve");
        CurrentNetworkStatu = NetworkStatu.Ready;
        return true;
    }
    public void UnReadySend()
    {
        Pack pack = new();
        pack.SignalPack(SignalType.UnReady);
        ClientStream.WriteAsync(pack.Bytes);
        PackTimeOutDict[new(UnReadySendTimeOutHandler, UnReadySendFinishHandler, pack.MD5Code)] = 0f;

    }
    public bool UnReadySendTimeOutHandler()
    {
        Debug.Log("UnReady发送失败");
        return false;
    }
    public bool UnReadySendFinishHandler()
    {
        Debug.Log("UnReady Solve");
        CurrentNetworkStatu = NetworkStatu.InRoom;
        return true;
    }
    #region //弃用
    public static bool ReadySend(out NetworkException networkException)
    {
        networkException = null;
        if (CurrentNetworkStatu == NetworkStatu.InRoom)
        {
            Pack pack = new();
            pack.SignalPack(SignalType.Ready);

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
    public static bool DiscardActionSend(out NetworkException networkException, PlayerActionData playerActionData)
    {
        networkException = null;
        if (CurrentNetworkStatu == NetworkStatu.DiscardStage)
        {
            // [TODO]

            CurrentNetworkStatu = NetworkStatu.DiscardWait;
            return true;
        }
        else
        {
            networkException = new("在不可操作的情况下尝试发送玩家行为");
            return false;
        }
    }
    public static bool ClaimActionSend()
    {
        return false;
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
    #endregion

    #endregion
}
/// <summary>
/// 信号枚举,包含所有信号的类型
/// </summary>
public enum SignalType : byte
{
    Error = 1,
    Ready,
    ReadyAck,
    UnReady,
    UnReadyAck,
    Start,
    StartAck,
    Init,
    InitAck,
    ActionSend,
    ActionSendAck,
    Action,
    ActionAck,

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
    /// 开始中,设定开始数据
    /// </summary>
    Starting,
    /// <summary>
    /// 自身已设定完成,等待其他玩家
    /// </summary>
    StartWait,
    /// <summary>
    /// 等待回合,别家等待出牌/鸣牌时自身的状态,开局暂未发牌时状态
    /// </summary>
    RoundWait,
    /// <summary>
    /// 舍牌阶段,玩家摸牌或响应别家牌时进入舍牌阶段,可加杠/拔北/暗杠/立直/自摸/九种九牌流局,由服务端返回摸牌时触发
    /// </summary>
    DiscardStage,
    /// <summary>
    /// 舍牌等待阶段,舍牌阶段发送操作后触发,等待服务器返回
    /// </summary>
    DiscardWait,
    /// <summary>
    /// 响应等待阶段,被响应者独有,等待其他人响应
    /// </summary>
    ClaimStage,
    /// <summary>
    /// 等待操作,(用户端)服务端返回自身可鸣牌时触发
    /// </summary>
    ActionStage,
    /// <summary>
    /// 发送鸣牌后等待服务端响应
    /// </summary>
    ActionStageWait,
    /// <summary>
    /// 结束状态,被服务端发送结束后触发
    /// </summary>
    EndStage,
    /// <summary>
    /// 结束等待,被服务端发送结束后且自身确认,等待其他人确认
    /// </summary>
    EndWait,
    /// <summary>
    /// 对局结束状态
    /// </summary>
    MainEndStage,

    // 服务器端特有状态码

    /// <summary>
    /// 房间等待加入状态
    /// </summary>
    RoomWait = 41,
    /// <summary>
    /// 服务器端等待开始,触发开始后等待客户端返回确认时的状态
    /// </summary>
    ServerStartWait,
    /// <summary>
    /// 初始化状态,生成牌山,设定各项数据并发送初始化信息至客户端
    /// </summary>
    ServerInit,
    /// <summary>
    /// 等待客户端发送初始化结束
    /// </summary>
    ServerInitWait,
    /// <summary>
    /// 服务端发送操作至客户端,等待确认返回
    /// </summary>
    ServerActionSendWait,
    /// <summary>
    /// 服务器端等待玩家操作返回
    /// </summary>
    ServerActionWait,
    /// <summary>
    /// 服务器端发送玩家操作至其他玩家,等待其他玩家返回
    /// </summary>
    ServerClaimWait,
    /// <summary>
    /// 服务器端发送结束后等待其他玩家返回
    /// </summary>
    ServerEndWait,
    /// <summary>
    /// 等待被连接
    /// </summary>
    ConnectWait,
    /// <summary>
    /// 等待客户端发送信息
    /// </summary>
    ConnectFree,
}