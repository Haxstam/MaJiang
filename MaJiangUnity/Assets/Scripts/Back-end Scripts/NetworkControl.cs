using MaJiangLib;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using TMPro;
using Unity.Profiling;
using UnityEngine;
/// <summary>
/// 所有玩家都拥有的网络控制类,每个玩家只能有一个此类的实例
/// </summary>
public class NetworkControl : MonoBehaviour, INetworkInformation
{
    /*
     * 1.目前包的发送如果需要Ack返回,则会通过TaskID来确定对应包确实返回了
     * 2.目前暂时设定服务器只有四个用户连接,同时只有一个房间
     */


    // 单例不好调试,先不用了

    ///// <summary>
    ///// 单例
    ///// </summary>
    //public static NetworkControl Instance { get; private set; }
    private void Awake()
    {
        // 调试部分
        SelfIPAddress = IPAddress.Parse(testIPAddress);
        SelfProfile = new(testName);
        Debug.Log($"I'm {testName}");
        SelfStatu = new(1, NetworkStatu.Free, SelfIPAddress, SelfProfile);
    }
    // Start is called before the first frame update
    void Start()
    {

    }
    private static readonly ProfilerMarker MyMethodMarker = new ProfilerMarker("MyMethod");
    // Update is called once per frame
    void Update()
    {
        // 调试部分
        if (Input.GetKeyDown(KeyCode.N))
        {
            Debug.Log("OK");
        }
        // 用不同标记选择不同用户
        if (Input.GetKey(clientKeyCode))
        {
            if (Input.GetKeyDown(KeyCode.Z))
            {
                MainClient.Connect(IPAddress.Parse("127.0.0.1"), 11111);
            }
        }

        NetworkReceiveUpdate();
        SyncTime += Time.deltaTime;
        if (SyncTime >= 0.5f)
        {
            SyncUpdate();
        }
        textMeshPro.text = 
            $"IP:{selfStatu.SelfIPAddress}\n" +
            $"NetworkStatu:{selfStatu.CurrentNetworkStatu}\n" +
            $""
            ;
    }

    public static void WriteDebug(int a)
    {
        Debug.Log(a);
    }
    public static void WriteDebug(List<Tile> list)
    {
        string a = "";
        foreach (Tile p in list)
        {
            a += p.ToString();
            a += " ";
        }
        Debug.Log(a);
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
    /// <summary>
    /// 标记是否连接
    /// </summary>
    public bool IsConnect
    {
        get { return MainClient.Connected; }
    }
    /// <summary>
    /// 连接用任务ID,因为连接本身的返回比较特殊,所以用
    /// </summary>
    public int ConnectTaskID { get; set; } = -1;
    // 表示当前将要写入缓存的位置
    int writeIndex = 0;
    public MainMatchControl MatchControl { get; set; }
    //public void ReadUpdate()
    //{
    //    byte[] clientBuffer = ClientBuffer;
    //    try
    //    {
    //        // 每当Read从数据流中读取数据,都会读取最多8192字节的数据,如果写入指针接近上限,则缩小写入大小
    //        if (ClientStream.DataAvailable)
    //        {
    //            writeIndex += ClientStream.Read(clientBuffer, writeIndex, 8192 - writeIndex);
    //        }
    //        if (writeIndex >= 1024)
    //        {   // 如果存在不少于1024字节的待处理数据,进行处理
    //            bool isPackReceived = ReadByteSolve(clientBuffer, ref writeIndex);
    //        }
    //    }
    //    catch (Exception)
    //    {
    //        throw;
    //    }
    //}
    ///// <summary>
    ///// 通过Span匹配字节串的方法,需要主字节串和短字节串
    ///// </summary>
    ///// <param name="data"></param>
    ///// <param name="shortData"></param>
    ///// <returns></returns>
    //public static int MatchBytes(byte[] mainBytes, byte[] shortBytes)
    //{
    //    Span<byte> SpanBytes = mainBytes;
    //    ReadOnlySpan<byte> ShortSpanBytes = shortBytes;
    //    return SpanBytes.IndexOf(ShortSpanBytes);
    //}
    ///// <summary>
    ///// 数据缓存处理,在方法内部根据判断修改buffer和写入指针
    ///// </summary>
    ///// <param name="buffer">对应连接的接收缓存</param>
    ///// <param name="packBytes">处理后的包</param>
    ///// <param name="writeIndex">当前写入位置</param>
    ///// <returns>当本次处理获取到包时,返回True,如果无数据或无合法包,返回False</returns>
    //public bool ReadByteSolve(byte[] buffer, ref int writeIndex)
    //{
    //    Span<byte> spanBuffer = buffer.AsSpan();
    //    bool isReceivePack = false;
    //    while (true)
    //    {
    //        // 处理循环
    //        // 循环判断直到没有任何合法包
    //        int endIndex = MatchBytes(buffer, PackCoder.PackEndBytes) + 8;
    //        if (endIndex == -1)
    //        {
    //            if (writeIndex > 7169)
    //            {
    //                // 清空
    //                writeIndex = 0;
    //            }
    //            // 未获取到包尾,返回false
    //            return isReceivePack;
    //        }
    //        else if (endIndex >= 0 && endIndex < 1024)
    //        {
    //            // 获取到包尾,但位置小于1024,即为残缺的后半个包,修改该标记首位为0x00并将此包尾之后的数据前移
    //            buffer[endIndex] = 0x00;
    //            spanBuffer[(endIndex + 8)..].CopyTo(spanBuffer);
    //            writeIndex -= (endIndex + 8);
    //        }
    //        else
    //        {
    //            // 获取到包尾且位置不小于1024,进行包合法性判断
    //            Span<byte> rawPack = spanBuffer.Slice(endIndex - 1024, 1024);
    //            if (PackCoder.IsLegalPack(rawPack))
    //            {
    //                // 判断成功,标记返回True和所提取的包
    //                byte[] packBytes = rawPack.ToArray();
    //                PackSolve(packBytes);
    //                // PackCoder.Ping = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - BitConverter.ToDouble(rawPack.Slice(8, 8));
    //                isReceivePack = true;
    //            }
    //            else
    //            {
    //                // 判断失败
    //            }
    //            // 无论成功与否,都修改标记首位为0x00并将此包尾之后的数据前移
    //            buffer[endIndex] = 0x00;
    //            spanBuffer[(endIndex + 8)..].CopyTo(spanBuffer);
    //            writeIndex -= (endIndex + 8);
    //        }
    //    }
    //}

    /// <summary>
    /// 包信息处理方法
    /// </summary>
    /// <param name="packBytes"></param>
    public void PackSolve(byte[] packBytes)
    {
        Pack pack = new(packBytes);
        int taskID = pack.TaskID;
        if (pack.PackType == PackType.Acknowledge)
        {
        }
        if (pack.PackType == PackType.ClientStatuPack)
        {
            ServerClientStatu = pack.ClientStatuPackDecode();
            IsSync = selfStatu == ServerClientStatu;
        }
    }


    public void NetworkReceiveUpdate()
    {
        // 基础安全检查
        if (MainClient == null || !MainClient.Connected || ClientStream == null)
        {
            return;
        }

        try
        {
            // 一直读取直到读满缓冲区
            while (ClientStream.DataAvailable && writeIndex < ClientBuffer.Length)
            {
                int spaceLeft = ClientBuffer.Length - writeIndex;
                // 读取数据并存放到 writeIndex 之后的位置
                int bytesRead = ClientStream.Read(ClientBuffer, writeIndex, spaceLeft);
                if (bytesRead == 0)
                {
                    break;
                }
                writeIndex += bytesRead;
            }

            // 当包尾位置大于1024则分析
            Span<byte> bufferSpan = ClientBuffer.AsSpan();
            ReadOnlySpan<byte> endMarker = Pack.PackEndBytes;

            while (true)
            {
                // 在已写入数据的范围内进行读取
                int matchIndex = bufferSpan.Slice(0, writeIndex).IndexOf(endMarker);

                // 如果找不到包尾，说明当前数据不足以构成一个完整的包
                if (matchIndex == -1)
                {
                    // 已写满但还没有找到,直接清除
                    if (writeIndex >= 8100)
                    {
                        Debug.LogWarning("清理数据...");
                        writeIndex = 0;
                    }
                    break;
                }

                // 计算长度
                int endPosition = matchIndex + 8;

                // 找到包尾且大于1024
                if (endPosition >= 1024)
                {
                    // 校验
                    int packStart = endPosition - 1024;
                    Span<byte> packCandidate = bufferSpan.Slice(packStart, 1024);

                    if (Pack.IsLegalPack(packCandidate))
                    {
                        // 校验成功并处理
                        PackSolve(packCandidate.ToArray());
                    }
                    else
                    {
                        Debug.LogWarning("Found end marker but packet CRC check failed.");
                    }
                }
                // 将 endPosition 之后的所有有效数据移动到缓冲区的最前面
                int remainingAfterProcess = writeIndex - endPosition;
                if (remainingAfterProcess > 0)
                {
                    bufferSpan.Slice(endPosition, remainingAfterProcess).CopyTo(bufferSpan);
                }

                // 更新索引位置
                writeIndex = remainingAfterProcess;
            }
        }
        catch (Exception e)
        {
            // 如果这里捕获到异常，通常是底层 Socket 断开或资源释放
            Debug.LogError($"NetworkRead Error: {e.Message}");
        }
    }

    #endregion

    #region *状态*
    public ClientStatu selfStatu;

    public ClientStatu SelfStatu
    {
        get { return selfStatu; }
        set { selfStatu = value; }
    }

    public ClientStatu serverClientStatu;

    public ClientStatu ServerClientStatu
    {
        get { return serverClientStatu; }
        set { serverClientStatu = value; }
    }

    /// <summary>
    /// 自身所在房间的房间号
    /// </summary>
    public int RoomNumber
    {
        get { return selfStatu.RoomNumber; }
        set { selfStatu.RoomNumber = value; }
    }
    /// <summary>
    /// 自身IP地址
    /// </summary>
    public IPAddress SelfIPAddress
    {
        get { return selfStatu.SelfIPAddress; }
        set { selfStatu.SelfIPAddress = value; }
    }
    /// <summary>
    /// 自身用户档案
    /// </summary>
    public UserProfile SelfProfile
    {
        get { return selfStatu.SelfProfile; }
        set { selfStatu.SelfProfile = value; }
    }
    public bool IsSync { get; set; }
    public float SyncTime { get; set; }

    public void SyncUpdate()
    {
        Pack pack = new(this, out int id);
        pack.ClientStatuPack(selfStatu);
        ClientStream.WriteAsync(pack.Bytes);
        SyncTime = 0f;
    }
    #endregion

    /// <summary>
    /// 标记自身是否为房主
    /// </summary>
    public bool IsHost { get; set; }
    /// <summary>
    /// 标记是否在对局中
    /// </summary>
    public bool IsPlaying { get; set; }
    public float BaseTime { get; set; }
    public float ThinkingTime { get; set; }

    // 测试用变量

    public string testName;
    public string testIPAddress;
    public KeyCode clientKeyCode;
    public TextMeshProUGUI textMeshPro;

    /// <summary>
    /// 主对局控制
    /// </summary>
    public MatchServer MatchServer { get; set; }
    /// <summary>
    /// 单局对局的游戏日志,用于心跳包同步和日志记录
    /// </summary>
    public List<PlayerActionData> PlayerActionRecordList { get; set; } = new();
    public int RecordIndex { get { return PlayerActionRecordList.Count; } }

    public void RecordUpdate(Pack pack)
    {
        if (pack.PackType == PackType.HeartBeatPack)
        {
            int previousRecordIndex = RecordIndex;
            bool isAdded = false;
            List<PlayerActionData> actionData = pack.PlayerActionPackDecode(out int startIndex, out int length);
            if (length != actionData.Count)
            {
                Debug.Log("传入记录包长度出现问题");
            }
            if (length == 0)
            {
                // 传入长度为0
            }
            if (RecordIndex < startIndex - 1)
            {
                // 当前记录序号小于实际序号-1,即传入记录和已有记录见存在缺失,返回同步请求
            }
            else if (RecordIndex == startIndex - 1)
            {
                // 传入序号正好为记录序号的下一位,直接将传入记录添加进已有记录之后
                isAdded = true;
                PlayerActionRecordList.AddRange(actionData);
            }
            else if (RecordIndex >= startIndex)
            {
                // 当前记录序号等于或大于实际序号,增加自身记录并返回收到请求
                int offset = startIndex + length - 1 - RecordIndex;
                if (offset <= 0)
                {
                    // 传入记录完全被已有记录包含,因此不做修改
                }
                else
                {
                    isAdded = true;
                    for (int i = length - offset; i < offset; i++)
                    {
                        // 按偏差长度,从第一个缺少的记录依次添加进当前记录中
                        PlayerActionRecordList.Add(actionData[i]);
                    }
                }
            }
            // 如果发送了记录修改,开始按步骤推演
            if (isAdded)
            {
                for (int j = previousRecordIndex + 1; j < RecordIndex; j++)
                {
                    // 按顺序,依次按记录进行推演
                    MatchControl.ActionDeduce(PlayerActionRecordList[j]);
                }
            }
        }
    }


    #region *方法*

    public void Connect()
    {

    }




    /// <summary>
    /// 任务ID
    /// </summary>
    private int taskSequenceID = 0;

    /// <summary>
    /// 任务ID的获取
    /// </summary>
    /// <returns></returns>
    public int GetTaskID()
    {
        return System.Threading.Interlocked.Increment(ref taskSequenceID);
    }


    #endregion

    private void OnDestroy()
    {
        MainClient.Close();
    }
}


/// <summary>
/// 信号枚举,包含所有信号的类型
/// </summary>
public enum SignalType : byte
{
    Error = 1,
    Ready,
    UnReady,
    AcceptRoom,
    Start,
    Init,
    ActionSend,
    Action,
    ReadyAck,
    UnReadyAck,
    StartAck,
    InitAck,
    ActionSendAck,
    ActionAck,
    NewRound,
}
[Serializable]
public enum NetworkStatu
{
    Empty = 0,
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
public enum TaskResult
{
    Finished,
    TimeOut,
}