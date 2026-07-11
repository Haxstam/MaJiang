using MaJiangLib;
using System;
using System.Collections.Generic;
using System.Linq;
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
                ClientStream = MainClient.GetStream();
            }
            if (Input.GetKeyDown(KeyCode.X))
            {
                SendReadySignal();
            }
        }
        NetworkReceiveUpdate();

        // 对局外心跳:定期发送ClientStatuPack
        if (MainClient.Connected && ClientStream != null)
        {
            clientStatuTimer += Time.deltaTime;
            if (clientStatuTimer >= ClientStatuInterval)
            {
                clientStatuTimer -= ClientStatuInterval;
                SendClientStatu();
            }
        }

        // 如果超过10秒未收到服务端任何包,请求同步
        LastReceiveTime += Time.deltaTime;
        if (LastReceiveTime > 10f && MainClient.Connected)
        {
            Debug.LogWarning("No packet received for 10s. Requesting sync...");
            RequestSync();
            LastReceiveTime = 0f;
        }
        textMeshPro.text = 
            $"IP:{selfStatu.SelfIPAddress}\n" +
            $"NetworkStatu:{selfStatu.CurrentNetworkStatu}\n" +
            $"RecordIndex:{RecordIndex}\n" +
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
        LastReceiveTime = 0f;
        Pack pack = new(packBytes);
        int taskID = pack.TaskID;
        switch (pack.PackType)
        {
            case PackType.Acknowledge:
                HandleAcknowledge(pack);
                break;
            case PackType.Init:
                HandleInitPack(pack);
                break;
            case PackType.PlayerAction:
                HandlePlayerActionPack(pack);
                SendHeartbeatAck();
                break;
            case PackType.AvailableAction:
                HandleAvailableActionPack(pack);
                break;
            case PackType.Signal:
                HandleSignalPack(pack);
                break;
            case PackType.HeartBeatPack:
                RecordUpdate(pack);
                SendHeartbeatAck();
                break;
            case PackType.SyncResponse:
                HandleSyncResponse(pack);
                break;
            case PackType.MatchData:
                HandleMatchDataPack(pack);
                break;
            case PackType.ClientStatuPack:
                ServerClientStatu = pack.ClientStatuPackDecode();
                IsSync = selfStatu == ServerClientStatu;
                if (ServerClientStatu.PlayerNumber >= 0)
                {
                    SelfPlayerNumber = ServerClientStatu.PlayerNumber;
                    Debug.Log($"SelfPlayerNumber set to {SelfPlayerNumber}");
                }
                break;
            default:
                break;
        }
    }
    /// <summary>
    /// 处理确认包
    /// </summary>
    public void HandleAcknowledge(Pack pack)
    {
        SignalType ackType = pack.SignalPackDecode();
        switch (ackType)
        {
            case SignalType.AcceptRoom:
                CurrentClientStage = NetworkStatu.InRoom;
                break;
            case SignalType.ReadyAck:
                CurrentClientStage = NetworkStatu.Ready;
                break;
            case SignalType.UnReadyAck:
                CurrentClientStage = NetworkStatu.InRoom;
                break;
            case SignalType.StartAck:
                CurrentClientStage = NetworkStatu.StartWait;
                break;
            case SignalType.ActionAck:
                IsWaitingServerResponse = false;
                break;
        }
    }
    /// <summary>
    /// 处理初始化包(收到手牌)
    /// </summary>
    public void HandleInitPack(Pack pack)
    {
        List<Tile> handTiles = pack.HonbaInitPackDecode(out HonbaEndMark honbaEndMark);
        if (MatchControl == null)
        {
            MatchControl = new MainMatchControl();
        }
        // 确保PlayerList已初始化
        if (MatchControl.PlayerList == null || MatchControl.PlayerList.Count == 0)
        {
            MatchControl.PlayerList = new List<Player>() { new(), new(), new(), new() };
        }
        for (int i = 0; i < 4; i++)
        {
            if (MatchControl.PlayerList[i].PlayerHandTile == null)
                MatchControl.PlayerList[i].PlayerHandTile = new HandTile(i);
        }
        // 初始化本地对局状态
        MatchControl.FourNewHonba(honbaEndMark);
        // 设置自己的手牌
        if (SelfPlayerNumber >= 0 && SelfPlayerNumber < 4)
        {
            MatchControl.PlayerList[SelfPlayerNumber].PlayerHandTile.HandTileList = handTiles;
        }
        else
        {
            // 如果还未知道序号,暂存手牌到0号位(后续收到ClientStatuPack后修正)
            Debug.LogWarning("Init pack received but SelfPlayerNumber not set yet. Storing temporarily.");
            MatchControl.PlayerList[0].PlayerHandTile.HandTileList = handTiles;
        }
        CurrentClientStage = NetworkStatu.StartWait;
        // 发送InitAck
        Pack ackPack = new(this, out int ackID);
        ackPack.SignalPack(SignalType.InitAck);
        ClientStream.WriteAsync(ackPack.Bytes);
    }
    /// <summary>
    /// 处理玩家操作广播包
    /// </summary>
    public void HandlePlayerActionPack(Pack pack)
    {
        List<PlayerActionData> actionList = pack.PlayerActionPackDecode(out int startIndex, out int length);
        if (actionList.Count == 0) return;
        // 检查序号连续性
        if (startIndex > RecordIndex)
        {
            Debug.LogWarning($"Action gap detected: local {RecordIndex}, server start {startIndex}. Requesting sync...");
            RequestSync();
            return;
        }
        if (startIndex < RecordIndex)
        {
            // 已经包含这些记录,忽略重复
            Debug.Log($"Received outdated actions (start {startIndex} < local {RecordIndex}), ignoring.");
            return;
        }
        // 将操作加入本地日志并推演
        int previousRecordIndex = RecordIndex;
        PlayerActionRecordList.AddRange(actionList);
        LastConfirmedRecordIndex = RecordIndex - 1;
        for (int j = previousRecordIndex; j < RecordIndex; j++)
        {
            MatchControl.ActionDeduce(PlayerActionRecordList[j]);
        }
        // 根据最新操作更新客户端状态
        PlayerActionData latestAction = actionList[actionList.Count - 1];
        switch (latestAction.PlayerAction)
        {
            case PlayerAction.Take:
                // 有人摸牌(通常是当前回合玩家)
                CurrentDrawTile = latestAction.TargetTile;
                break;
            case PlayerAction.HandCut:
            case PlayerAction.TakeCut:
            case PlayerAction.Riichi:
            case PlayerAction.AddedKan:
                // 切牌/立直/加杠后,如果没有可响应操作,客户端进入等待
                CurrentClientStage = NetworkStatu.RoundWait;
                break;
            case PlayerAction.Chi:
            case PlayerAction.Peng:
            case PlayerAction.OpenKan:
                // 有人鸣牌,新的当前玩家产生
                CurrentClientStage = NetworkStatu.RoundWait;
                break;
            case PlayerAction.ClosedKan:
            case PlayerAction.Kita:
                // 暗杠/拔北后当前玩家继续
                if (latestAction.SelfNumber == SelfPlayerNumber)
                    CurrentClientStage = NetworkStatu.RoundWait;
                break;
            case PlayerAction.Tsumo:
            case PlayerAction.Ron:
            case PlayerAction.Draw:
                CurrentClientStage = NetworkStatu.EndStage;
                break;
        }
        IsWaitingServerResponse = false;
    }
    /// <summary>
    /// 处理可行操作包(轮到自己做决定或鸣牌机会)
    /// </summary>
    public void HandleAvailableActionPack(Pack pack)
    {
        var actionDict = pack.AvailableActionPackDecode(out int playerIndex, out Tile singleTile);
        if (playerIndex != SelfPlayerNumber) return;

        CurrentAvailableActions = actionDict;
        CurrentDrawTile = singleTile;

        // 同步MatchControl中的摸牌信息(仅当singleTile有效时)
        if (singleTile != null && MatchControl != null && MatchControl.PlayerList != null
            && playerIndex < MatchControl.PlayerList.Count)
        {
            if (MatchControl.PlayerList[playerIndex].PlayerHandTile == null)
                MatchControl.PlayerList[playerIndex].PlayerHandTile = new HandTile(playerIndex);
            MatchControl.PlayerList[playerIndex].PlayerHandTile.SingleTile = singleTile;
        }

        // 根据可行操作类型区分阶段:
        // 包含吃/碰/明杠/荣和 → 鸣牌机会 → ActionStage
        // 否则(手切/摸切/自摸/暗杠/立直等) → 自己回合 → DiscardStage
        bool isClaimPhase = actionDict.Keys.Any(a =>
            a == PlayerAction.Chi || a == PlayerAction.Peng
            || a == PlayerAction.OpenKan || a == PlayerAction.Ron);

        if (isClaimPhase)
        {
            CurrentClientStage = NetworkStatu.ActionStage;
            Debug.Log($"Received claim actions. Count: {actionDict.Count}");
        }
        else
        {
            CurrentClientStage = NetworkStatu.DiscardStage;
            Debug.Log($"Received available actions. Count: {actionDict.Count}");
        }
    }
    /// <summary>
    /// 处理信号包
    /// </summary>
    public void HandleSignalPack(Pack pack)
    {
        SignalType signal = pack.SignalPackDecode();
        switch (signal)
        {
            case SignalType.NewRound:
                // 新回合开始(别人摸牌/操作),自己进入等待
                CurrentClientStage = NetworkStatu.RoundWait;
                break;
            case SignalType.Start:
                CurrentClientStage = NetworkStatu.StartWait;
                break;
            case SignalType.ActionSendAck:
                IsWaitingServerResponse = false;
                break;
            case SignalType.ActionAck:
                // 本场结束信号,进入结束阶段
                CurrentClientStage = NetworkStatu.EndStage;
                break;
        }
    }
    /// <summary>
    /// 处理同步响应包(断线重连)
    /// </summary>
    public void HandleSyncResponse(Pack pack)
    {
        List<PlayerActionData> actionList = pack.SyncResponsePackDecode(out int startIndex);
        if (actionList.Count > 0)
        {
            // 清空本地状态并从头推演
            // TODO: 如果已经有部分状态,可能需要更精细的重置
            if (MatchControl != null)
            {
                // 重新初始化对局状态(保留基本设定)
                var matchType = MatchControl.MatchType;
                MatchControl.FourNewHonba(HonbaEndMark.NewMatch);
                MatchControl.MatchType = matchType;
            }
            PlayerActionRecordList.Clear();
            PlayerActionRecordList.AddRange(actionList);
            LastConfirmedRecordIndex = RecordIndex - 1;
            foreach (var action in actionList)
            {
                MatchControl.ActionDeduce(action);
            }
            Debug.Log($"Synced {actionList.Count} actions from index {startIndex}");
        }
    }
    /// <summary>
    /// 处理对局数据包(公共信息同步)
    /// </summary>
    public void HandleMatchDataPack(Pack pack)
    {
        byte[] publicBytes = pack.Bytes; // MatchDataPack的数据在偏移104处
        // 从104偏移读取512字节公共信息
        byte[] extractedPublic = new byte[512];
        Array.Copy(pack.Bytes, 104, extractedPublic, 0, 512);
        MatchControl = MainMatchControl.PublicBytesTo(extractedPublic);
        // 尝试从对局数据推断自身序号(如果没有设置)
        if (SelfPlayerNumber < 0 && MatchControl.PlayerList != null)
        {
            // 根据PlayerProfile匹配
            for (int i = 0; i < MatchControl.PlayerList.Count; i++)
            {
                if (MatchControl.PlayerList[i].PlayerProfile == SelfProfile)
                {
                    SelfPlayerNumber = i;
                    break;
                }
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
    /// <summary>
    /// 客户端自身在对局中的玩家序号
    /// </summary>
    public int SelfPlayerNumber { get; set; } = -1;
    /// <summary>
    /// 客户端当前网络阶段
    /// </summary>
    public NetworkStatu CurrentClientStage { get; set; } = NetworkStatu.Free;
    /// <summary>
    /// 是否正在等待服务端响应(用于防止重复发送)
    /// </summary>
    public bool IsWaitingServerResponse { get; set; } = false;
    /// <summary>
    /// 当前收到的可行操作字典(用于本地UI显示)
    /// </summary>
    public Dictionary<PlayerAction, List<PlayerActionData>> CurrentAvailableActions { get; set; } = new();
    /// <summary>
    /// 当前收到的摸牌
    /// </summary>
    public Tile CurrentDrawTile { get; set; }
    /// <summary>
    /// 最后确认的操作日志序号(用于断线重连)
    /// </summary>
    public int LastConfirmedRecordIndex { get; set; } = -1;
    /// <summary>
    /// 最后一次收到服务端包的时间(用于断线检测)
    /// </summary>
    public float LastReceiveTime { get; set; } = 0f;
    /// <summary>
    /// 对局外ClientStatu心跳计时器
    /// </summary>
    private float clientStatuTimer = 0f;
    private const float ClientStatuInterval = 1f;

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
                // 当前记录序号小于实际序号-1,即传入记录和已有记录见存在缺失,请求同步
                Debug.LogWarning($"Record gap detected: local {RecordIndex}, server start {startIndex}. Requesting sync...");
                RequestSync();
                return;
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
                for (int j = previousRecordIndex; j < RecordIndex; j++)
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
    /// 发送玩家操作至服务端
    /// </summary>
    public void SendPlayerAction(PlayerActionData actionData)
    {
        if (ClientStream == null || !MainClient.Connected) return;
        actionData.SelfNumber = SelfPlayerNumber;
        Pack pack = new(this, out int taskID);
        List<PlayerActionData> list = new() { actionData };
        pack.PlayerActionPack(list, RecordIndex, 1);
        try
        {
            ClientStream.WriteAsync(pack.Bytes);
            IsWaitingServerResponse = true;
            // 不本地推演,等待服务端广播权威操作后再统一推演,避免状态分歧
        }
        catch (Exception e)
        {
            Debug.LogError($"SendPlayerAction failed: {e.Message}");
        }
    }
    /// <summary>
    /// 发送准备信号
    /// </summary>
    public void SendReadySignal()
    {
        if (ClientStream == null || !MainClient.Connected) return;
        Pack pack = new(this, out int taskID);
        pack.SignalPack(SignalType.Ready);
        ClientStream.WriteAsync(pack.Bytes);
        CurrentClientStage = NetworkStatu.ReadyWait;
    }
    /// <summary>
    /// 发送取消准备信号
    /// </summary>
    public void SendUnReadySignal()
    {
        if (ClientStream == null || !MainClient.Connected) return;
        Pack pack = new(this, out int taskID);
        pack.SignalPack(SignalType.UnReady);
        ClientStream.WriteAsync(pack.Bytes);
        CurrentClientStage = NetworkStatu.UnReadyWait;
    }
    /// <summary>
    /// 请求同步操作日志(断线重连后调用)
    /// </summary>
    public void RequestSync()
    {
        if (ClientStream == null || !MainClient.Connected) return;
        Pack pack = new(this, out int taskID);
        pack.SyncRequestPack(LastConfirmedRecordIndex);
        try
        {
            ClientStream.WriteAsync(pack.Bytes);
        }
        catch (Exception e)
        {
            Debug.LogError($"RequestSync failed: {e.Message}");
        }
    }
    /// <summary>
    /// 发送ClientStatu包(对局外心跳)
    /// </summary>
    public void SendClientStatu()
    {
        if (ClientStream == null || !MainClient.Connected) return;
        SelfStatu.CurrentNetworkStatu = CurrentClientStage;
        SelfStatu.PlayerNumber = SelfPlayerNumber;
        Pack pack = new(this, out int taskID);
        pack.ClientStatuPack(SelfStatu);
        try
        {
            ClientStream.WriteAsync(pack.Bytes);
        }
        catch (Exception e)
        {
            Debug.LogError($"SendClientStatu failed: {e.Message}");
        }
    }
    /// <summary>
    /// 发送心跳Ack,告知服务端已确认的日志序号
    /// </summary>
    public void SendHeartbeatAck()
    {
        if (ClientStream == null || !MainClient.Connected) return;
        Pack pack = new(this, out int taskID);
        pack.HeartbeatAckPack(RecordIndex - 1);
        try
        {
            ClientStream.WriteAsync(pack.Bytes);
        }
        catch (Exception e)
        {
            Debug.LogError($"SendHeartbeatAck failed: {e.Message}");
        }
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
    HeartbeatAck,
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