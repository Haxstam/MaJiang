using MaJiangLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Unity.Profiling;
using UnityEngine;
/// <summary>
/// 所有玩家都拥有的网络控制类,每个玩家只能有一个此类的实例
/// </summary>
public class NetworkControl : MonoBehaviour, INetworkInformation
{
    /*
     * 1.目前包的发送如果需要Ack返回,则会通过TaskID来确定对应包确实返回了
     */


    // 单例不好调试,先不用了

    ///// <summary>
    ///// 单例
    ///// </summary>
    //public static NetworkControl Instance { get; private set; }
    private void Awake()
    {
        SelfIPAddress = IPAddress.Parse(testIPAddress);
        SelfProfile = new(testName);
        Debug.Log($"I'm {testName}");
    }
    // Start is called before the first frame update
    void Start()
    {

    }
    private static readonly ProfilerMarker MyMethodMarker = new ProfilerMarker("MyMethod");
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.N))
        {
            Debug.Log("OK");
        }
        // 用不同标记选择不同用户
        if (Input.GetKey(clientKeyCode))
        {
            if (Input.GetKeyDown(KeyCode.T))
            {
                Debug.Log($"{testName} here!");
            }
            if (Input.GetKeyDown(KeyCode.Z))
            {
                Debug.Log($"{testName} Connecting");
                MainClient.Connect(IPAddress.Parse("127.0.0.1"), 11111);
                ClientStream = MainClient.GetStream();
                CurrentNetworkStatu = NetworkStatu.InRoom;
            }
            if (Input.GetKeyDown(KeyCode.X))
            {
                Ready();
            }
            if (Input.GetKeyDown(KeyCode.C))
            {
                UnReady();
            }
            //if (Input.GetKeyDown(KeyCode.Y))
            //{
            //    using (MyMethodMarker.Auto())
            //    {

            //        GlobalFunction.MainTastTenhe();
            //    }
            //}
        }


        SignalDictionaryUpdate();
        NetworkReceiveUpdate();
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
    public bool IsConnect { get; set; }
    // 表示当前将要写入缓存的位置
    int writeIndex = 0;
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
            if (SignalDictionary.TryGetValue(taskID, out TaskData taskData))
            {
                taskData.TaskSolve(TaskResult.Finished);
                SignalDictionary.Remove(taskID);
            }
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
            // --- 1. 读取阶段：尽可能吸干 Socket 中的数据 ---
            // 只要流里有数据，且缓冲区没满，就一直读
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

            // --- 2. 解析阶段：循环处理缓冲区中的粘包 ---
            // 只有数据量达到 1024（最小包长）或发现包尾标识时才有必要处理
            Span<byte> bufferSpan = ClientBuffer.AsSpan();
            ReadOnlySpan<byte> endMarker = PackCoder.PackEndBytes;

            while (true)
            {
                // 搜索范围严格限制在 0 到 writeIndex 之间，避免扫到旧数据
                int matchIndex = bufferSpan.Slice(0, writeIndex).IndexOf(endMarker);

                // 如果找不到包尾，说明当前数据不足以构成一个完整的包
                if (matchIndex == -1)
                {
                    // 【缓冲区保护】
                    // 如果缓冲区已经填满（约 8KB）却依然找不到 8 字节的标识符，
                    // 说明流中全是垃圾数据或协议不匹配，直接重置防止溢出。
                    if (writeIndex >= 8100)
                    {
                        Debug.LogWarning("清理数据...");
                        writeIndex = 0;
                    }
                    break; // 跳出解析循环，等待下一帧读取更多数据
                }

                // 计算该段潜在数据的总长度（包含包尾 8 字节）
                int endPosition = matchIndex + 8;

                // 情况 A：找到标识符，且标识符前有足够的数据（>= 1024 字节）
                if (endPosition >= 1024)
                {
                    // 向前截取固定 1024 字节进行校验
                    int packStart = endPosition - 1024;
                    Span<byte> packCandidate = bufferSpan.Slice(packStart, 1024);

                    if (PackCoder.IsLegalPack(packCandidate))
                    {
                        // 校验成功，转交业务处理
                        PackSolve(packCandidate.ToArray());
                    }
                    else
                    {
                        Debug.LogWarning("Found end marker but packet CRC check failed.");
                    }
                }
                // 情况 B：找到标识符，但标识符太靠前（不足 1024 字节），判定为杂质

                // --- 3. 整理阶段：数据前移 ---
                // 无论刚才截取的包是否合法，直到 endPosition 为止的数据都已经处理过（或是垃圾）
                // 将 endPosition 之后的所有有效数据移动到缓冲区的最前面
                int remainingAfterProcess = writeIndex - endPosition;
                if (remainingAfterProcess > 0)
                {
                    // 使用 Span 的 CopyTo 进行高效内存拷贝
                    bufferSpan.Slice(endPosition, remainingAfterProcess).CopyTo(bufferSpan);
                }

                // 更新索引位置
                writeIndex = remainingAfterProcess;

                // 继续下一轮解析，处理可能存在的后续包（粘包）
            }
        }
        catch (Exception e)
        {
            // 如果这里捕获到异常，通常是底层 Socket 断开或资源释放
            Debug.LogError($"NetworkRead Error: {e.Message}");
        }
    }

    #endregion

    public NetworkStatu currentNetworkStatu;

    /// <summary>
    /// 本端当前网络状态
    /// </summary>
    public NetworkStatu CurrentNetworkStatu
    {
        get { return currentNetworkStatu; }
        set { currentNetworkStatu = value; }
    }
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
    /// <summary>
    /// 自身所在房间的房间号
    /// </summary>
    public int RoomNumber { get; set; }

    // 测试用变量

    public string testName;
    public string testIPAddress;
    public KeyCode clientKeyCode;

    /// <summary>
    /// 自身IP地址
    /// </summary>
    public IPAddress SelfIPAddress { get; set; }
    /// <summary>
    /// 自身用户档案
    /// </summary>
    public UserProfile SelfProfile { get; set; }
    /// <summary>
    /// 主对局控制
    /// </summary>
    public MatchServer MatchServer { get; set; }
    #region *方法*
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

    public Dictionary<int, TaskData> SignalDictionary { get; set; } = new();
    public class TaskData
    {
        public TaskData(Action<TaskResult> actionSolve, int timeOut = 20)
        {
            WaitTime = 0f;
            TimeOut = timeOut;
            TaskSolve = actionSolve;
        }
        /// <summary>
        /// 当前等待时间
        /// </summary>
        public float WaitTime { get; set; }
        /// <summary>
        /// 超时上限
        /// </summary>
        public int TimeOut { get; set; }
        /// <summary>
        /// 完成时的回调方法
        /// </summary>
        public Action<TaskResult> TaskSolve { get; set; }
    }
    /// <summary>
    /// 对信号记录的处理,每帧更新一次,用于判断方法超时时间
    /// </summary>
    public void SignalDictionaryUpdate()
    {
        List<int> keyList = SignalDictionary.Keys.ToList();
        foreach (int key in keyList)
        {
            if (SignalDictionary.TryGetValue(key, out TaskData taskData))
            {
                taskData.WaitTime += Time.deltaTime;
                if (taskData.WaitTime >= taskData.TimeOut)
                {
                    // 已超时,返回TimeOut并移除对应键值对
                    taskData.TaskSolve.Invoke(TaskResult.TimeOut);
                    SignalDictionary.Remove(key);
                }
            }
        }
    }
    /// <summary>
    /// TODO 等待实现
    /// </summary>
    /// <param name="pack"></param>
    public void PackSend(byte[] pack)
    {
        ClientStream.Write(pack, 0, pack.Length);
    }
    public void Connect(IPAddress iPAddress, int port)
    {
        Debug.Log($"{testName} Connecting");
        if (CurrentNetworkStatu == NetworkStatu.Offline)
        {
            var cts = new CancellationTokenSource();

            var a= MainClient.ConnectAsync(iPAddress, port);
            // TODO 2026/2/6
        }
        
        ClientStream = MainClient.GetStream();
        CurrentNetworkStatu = NetworkStatu.InRoom;
    }
    public void Ready()
    {
        if (CurrentNetworkStatu == NetworkStatu.InRoom)
        {
            Pack pack = new(this);
            pack.SignalPack(SignalType.Ready);
            SignalDictionary[pack.TaskID] = new(ReadySolve);
            CurrentNetworkStatu = NetworkStatu.ReadyWait;
            PackSend(pack.Bytes);
        }
        else
        {
            Debug.Log("必须在房间中且未准备");
        }
    }
    public void ReadySolve(TaskResult result)
    {
        if (result == TaskResult.Finished)
        {
            CurrentNetworkStatu = NetworkStatu.Ready;
        }
        else if (result == TaskResult.TimeOut)
        {
            CurrentNetworkStatu = NetworkStatu.InRoom;
        }
    }

    public void UnReady()
    {
        if (CurrentNetworkStatu == NetworkStatu.Ready)
        {
            Pack pack = new(this);
            pack.SignalPack(SignalType.UnReady);
            SignalDictionary[pack.TaskID] = new(UnReadySolve);
            CurrentNetworkStatu = NetworkStatu.UnReadyWait;
            PackSend(pack.Bytes);
        }
        else
        {
            Debug.Log("必须在房间中且未准备");
        }
    }
    public void UnReadySolve(TaskResult result)
    {
        if (result == TaskResult.Finished)
        {
            CurrentNetworkStatu = NetworkStatu.InRoom;
        }
        else if (result == TaskResult.TimeOut)
        {
            CurrentNetworkStatu = NetworkStatu.Ready;
        }
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
public enum TaskResult
{
    Finished,
    TimeOut,
}