using MaJiangLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using TMPro;
using UnityEngine;
using static MaJiangLib.GlobalFunction;
/// <summary>
/// 对局服务器类,在进行新对局时创建并初始化
/// </summary>
public class MatchServer : MonoBehaviour, INetworkInformation
{
    /* 1. 想了一圈,最后决定用心跳包和日志同步的方式处理数据同步和断线的问题
     *    每秒服务端会向客户端发送心跳包,并附带上一秒中所有对局操作,客户端接收后返回ack
     *    服务端和客户端各自维护一个玩家操作列表,服务端每秒更新,客户端接收到包后更新
     *    如果服务端长时间未收到ack,或未收到的ack序号差太大,则进行差量同步,传输从ack处直到当前的所有包
     *    如果服务端差量同步超时,则判断为断联,不再发送包,等待用户端自身主动连接
     */


    private void Awake()
    {
        SelfIPAddress = IPAddress.Parse(testIPAddress);
        SelfProfile = new(testName);
        StartServer(SelfIPAddress, 11111);
        EnterState(NetworkStatu.RoomWait);
    }
    private void Start()
    {

    }
    private void Update()
    {
        NetworkStatu[] networkStatus = new NetworkStatu[4] { NetworkStatu.Empty, NetworkStatu.Empty, NetworkStatu.Empty, NetworkStatu.Empty};
        int i = 0;
        foreach (ClientData data in ClientBufferDictionary.Keys)
        {
            if (i < 4) networkStatus[i] = data.ClientNetworkStatu;
            i++;
        }
        if (TextMeshPro != null)
            TextMeshPro.text = $"networkStatus:\n{networkStatus[0]},{networkStatus[1]},{networkStatus[2]},{networkStatus[3]}\nStage:{ServerNetworkStatu}";

        foreach (ClientData data in ClientBufferDictionary.Keys.ToList())
        {
            data.Ping += Time.deltaTime;
            if (data.Ping > 30f && !data.Closing)
            {
                Debug.LogWarning($"Player {data.RelativePlayerNumber} heartbeat timeout. Marking as disconnected.");
                data.Closing = true;
                try { data.TcpClient.Close(); } catch { }
            }
        }

        ServerStateUpdate();
        SignalDictionaryUpdate();
    }
    /// <summary>
    /// 从连接到对局顺位的映射表
    /// </summary>
    public TcpClient[] tcpClientNumberList = new TcpClient[4];
    /// <summary>
    /// 对局控制类
    /// </summary>
    public MainMatchControl MatchControl { get; set; }
    /// <summary>
    /// 玩家可行操作的列表,每当有人打出时更新,响应/跳过后归零
    /// </summary>
    public Dictionary<PlayerAction, List<PlayerActionData>>[] AvailableActionDict { get; private set; } = new Dictionary<PlayerAction, List<PlayerActionData>>[4];
    /// <summary>
    /// 玩家若听牌,其听牌的列表.考虑到摸切(即不改变听牌)占切牌的很大一部分,因此单独存储从而减少运算
    /// </summary>
    public Dictionary<Tile, List<Group>>[] AvailableTenpaiDict { get; private set; } = new Dictionary<Tile, List<Group>>[4];
    /// <summary>
    /// 主服务器当前状态
    /// </summary>
    public NetworkStatu ServerNetworkStatu { get; set; } = NetworkStatu.Offline;

    /// <summary>
    /// 记录进入当前ServerNetworkStatu的时间,用于超时判断
    /// </summary>
    public float StageEnterTime { get; set; } = 0f;
    /// <summary>
    /// 鸣牌阶段已收集的响应操作,key为玩家序号
    /// </summary>
    public Dictionary<int, PlayerActionData> ClaimActionDict { get; set; } = new Dictionary<int, PlayerActionData>();
    /// <summary>
    /// 当前打出牌进入鸣牌阶段的源操作类型
    /// </summary>
    public PlayerAction CurrentPhaseSourceAction { get; set; } = PlayerAction.Skip;
    /// <summary>
    /// 当前进入鸣牌阶段的打出牌,仅作为操作上下文,非状态机状态
    /// </summary>
    public Tile CurrentDiscardTile { get; set; }
    /// <summary>
    /// 已发送初始化确认的玩家序号集合
    /// </summary>
    public HashSet<int> InitAckSet { get; set; } = new HashSet<int>();
    /// <summary>
    /// 心跳计时器,每秒触发一次心跳发送
    /// </summary>
    private float heartbeatTimer = 0f;
    private const float HeartbeatInterval = 1f;

    private int taskSequenceID = 0;
    public int GetTaskID()
    {
        System.Threading.Interlocked.Increment(ref taskSequenceID);
        return taskSequenceID;
    }

    // 测试用变量

    public string testName;
    public string testIPAddress;
    public TextMeshProUGUI TextMeshPro;
    public IPAddress SelfIPAddress { get; set; }
    public UserProfile SelfProfile { get; set; } = new("TestServer");
    /// <summary>
    /// 听牌列表刷新,当有玩家手牌发生改变,即手切/拔北/鸣牌/开杠时调用,更新此玩家的听牌列表
    /// </summary>
    /// <param name="playerNumber">改变自己手牌的玩家序号</param>
    /// <returns></returns>
    public bool TenpaiUpdate(int playerNumber)
    {
        if (TenpaiJudge(MatchControl.PlayerList[playerNumber].PlayerHandTile, out Dictionary<Tile, List<Group>> groups))
        {
            // 听牌(不考虑是否有役种)则修改字典
            AvailableTenpaiDict[playerNumber] = groups;
            return true;
        }
        else
        {
            // 判断后发现未听牌,置空
            AvailableTenpaiDict[playerNumber] = null;
            return true;
        }
    }
    #region *方法*
    // 创建新对局

    public void StartMatch(MatchType matchType)
    {
        MatchControl = new();
        if (matchType == MatchType.FourMahjongSouth)
        {
            ServerNetworkStatu = NetworkStatu.ServerInit;


        }
    }
    public class ActionMark
    {
        public ActionMark()
        {
            IsSendAction = false;
            AvailableActions = new List<PlayerAction>();
        }
        public List<PlayerAction> AvailableActions { get; set; }
        public PlayerAction ChoosedAction { get; set; }
        /// <summary>
        /// 对应玩家是否发送操作
        /// </summary>
        public bool IsSendAction { get; set; }
    }
    #endregion


    #region 游戏日志/心跳包
    /// <summary>
    /// 单局对局的游戏日志,用于心跳包同步和日志记录
    /// </summary>
    public List<PlayerActionData> PlayerActionRecordList { get; set; } = new();
    public int RecordIndex { get { return PlayerActionRecordList.Count; } }
    public void PlayerActionRecord(PlayerActionData playerActionData)
    {
        PlayerActionRecordList.Add(playerActionData);

    }

    /// <summary>
    /// 服务端主状态更新,每帧调用,处理心跳定时和状态机
    /// </summary>
    public void ServerStateUpdate()
    {
        // 心跳定时器
        heartbeatTimer += Time.deltaTime;
        if (heartbeatTimer >= HeartbeatInterval)
        {
            heartbeatTimer -= HeartbeatInterval;
            ServerHeartbeat();
        }
        // 状态机更新
        ServerStageUpdate();
    }
    /// <summary>
    /// 服务端心跳发送
    /// 对局外:发送ClientStatuPack同步状态
    /// 对局内:发送差值操作日志(HeartBeatPack),附带ClientStatu
    /// </summary>
    public void ServerHeartbeat()
    {
        // 对局内的服务端状态:从初始化到结束的所有状态
        bool isInGame = ServerNetworkStatu == NetworkStatu.ServerStartWait
                     || ServerNetworkStatu == NetworkStatu.ServerInit
                     || ServerNetworkStatu == NetworkStatu.ServerInitWait
                     || ServerNetworkStatu == NetworkStatu.ServerActionSendWait
                     || ServerNetworkStatu == NetworkStatu.ServerActionWait
                     || ServerNetworkStatu == NetworkStatu.ServerClaimWait
                     || ServerNetworkStatu == NetworkStatu.ServerEndWait;

        foreach (var clientData in ClientBufferDictionary.Keys.ToList())
        {
            if (clientData.Closing || clientData.ClientStream == null) continue;

            if (isInGame)
            {
                // 对局内:发送差值操作日志
                // RecordStamp是客户端最后确认的0-based索引(含), RecordIndex是总操作数
                int clientStamp = clientData.RecordStamp;
                int serverIndex = RecordIndex;
                int firstUnconfirmed = clientStamp + 1;  // 首个客户端缺失的0-based索引
                int count = serverIndex - firstUnconfirmed;
                if (count > 0)
                {
                    // 单包最多装 (912-8)/16 = 56 条操作,超过则只发最近一批
                    int maxPerPack = 56;
                    if (count > maxPerPack)
                    {
                        firstUnconfirmed = serverIndex - maxPerPack;
                        count = maxPerPack;
                    }
                    List<PlayerActionData> diffList = PlayerActionRecordList.GetRange(firstUnconfirmed, count);
                    Pack pack = new(this, out int taskID);
                    pack.PlayerActionPack(diffList, firstUnconfirmed, count);
                    pack.PackType = PackType.HeartBeatPack;
                    try { clientData.ClientStream.WriteAsync(pack.Bytes); }
                    catch { }
                }
                else
                {
                    // 客户端已同步,发送空心跳(仅ClientStatu)
                    ClientStatu statu = CreateClientStatu(clientData, clientData.ClientNetworkStatu, clientData.RelativePlayerNumber);
                    Pack statuPack = new(this, out int sid);
                    statuPack.ClientStatuPack(statu);
                    try { clientData.ClientStream.WriteAsync(statuPack.Bytes); }
                    catch { }
                }
            }
            else
            {
                // 对局外:发送ClientStatuPack同步状态
                ClientStatu statu = CreateClientStatu(clientData, clientData.ClientNetworkStatu, clientData.RelativePlayerNumber);
                Pack pack = new(this, out int taskID);
                pack.ClientStatuPack(statu);
                try { clientData.ClientStream.WriteAsync(pack.Bytes); }
                catch { }
            }
        }
    }
    /// <summary>
    /// 心跳包发送(已废弃,由ServerHeartbeat替代)
    /// </summary>
    [Obsolete]
    public void HeartBeatPackSend()
    {
        ServerHeartbeat();
    }
    #endregion


    #region *局内玩家记录*
    /// <summary>
    /// 振听标记,若玩家振听则标记为true,无法通过荣和和牌
    /// </summary>
    public bool[] IsPlayerFuriten { get; set; } = new bool[4];
    /// <summary>
    /// 玩家能否鸣牌和是否返回鸣牌的记录,所有可鸣牌玩家鸣牌则鸣牌结束,顺序依次对应玩家
    /// </summary>
    public ActionMark[] PlayerWaitActionList { get; set; } = new ActionMark[4] { new(), new(), new(), new() };

    #endregion

    #region *网络相关*
    /// <summary>
    /// 用户端连接数据类,包含用户端,延迟,对应玩家座次
    /// </summary>
    public class ClientData
    {
        public ClientData(TcpClient tcpClient)
        {
            TcpClient = tcpClient;
            ClientStream = TcpClient.GetStream();
            Ping = 0d;
            Closing = false;
            PersonalNetworkStatu = NetworkStatu.ConnectWait;
            ClientNetworkStatu = NetworkStatu.Offline;
            ClientStatu = new ClientStatu();
        }
        public TcpClient TcpClient { get; set; }
        public NetworkStream ClientStream { get; set; }
        public double Ping { get; set; }
        public int RelativePlayerNumber { get; set; }
        /// <summary>
        /// 连接将要被关闭时为true
        /// </summary>
        public bool Closing { get; set; }
        /// <summary>
        /// 服务器对于该客户端的状态
        /// </summary>
        public NetworkStatu PersonalNetworkStatu { get; set; }
        public NetworkStatu ClientNetworkStatu { get; set; }
        public UserProfile UserProfile { get; set; }
        /// <summary>
        /// 用户端对应时间戳, -1表示未确认任何记录
        /// </summary>
        public int RecordStamp { get; set; } = -1;
        public ClientStatu ClientStatu { get; set; }
    }
    public TcpListener MainServer { get; set; }
    /// <summary>
    /// 服务端连接缓存字典,当存在新建连接时,将添加连接信息和其对应的缓存,默认大小8KiB
    /// </summary>
    public Dictionary<ClientData, byte[]> ClientBufferDictionary { get; set; }
    public bool IsListening { get; set; }
    /// <summary>
    /// 连接限制,连接数最大为该值,大于该值会拒绝连接
    /// </summary>
    public int ConnectLimit { get; set; } = 4;
    public int ConnectCount { get { return ClientBufferDictionary.Count; } }
    /// <summary>
    /// 服务器启动,需要指定IP和端口
    /// </summary>
    /// <param name="IP"></param>
    /// <param name="Port"></param>
    public void StartServer(string IP, int Port) => StartServer(IPAddress.Parse(IP), Port);
    public void StartServer(IPAddress IP, int Port)
    {
        MainServer = new TcpListener(IP, Port);
        ClientBufferDictionary = new();
        MainServer.Start();
        Debug.Log("MainServer Started!");
        StartListen();
    }

    public void StartListen()
    {
        IsListening = true;
        // 启动监听时调用方法
        ListenClientConnectAsync();

    }
    public void StopListen()
    {
        IsListening = false;
    }
    /// <summary>
    /// 通过Span匹配字节串的方法,需要主字节串和短字节串
    /// </summary>
    /// <param name="data"></param>
    /// <param name="shortData"></param>
    /// <returns></returns>
    public int MatchBytes(byte[] mainBytes, byte[] shortBytes)
    {
        Span<byte> SpanBytes = mainBytes;
        ReadOnlySpan<byte> ShortSpanBytes = shortBytes;
        return SpanBytes.IndexOf(ShortSpanBytes);
    }
    public async void ListenClientConnectAsync()
    {
        while (IsListening)
        {
            // 如果启动监听,将会一直运行并持续监听所有传入连接
            TcpClient connectedClient = await MainServer.AcceptTcpClientAsync();
            // [TODO] 因为异步的原因,即便已经指定停止监听也不能停止AcceptTcpClientAsync(),也不能直接关闭Server
            // [TODO] 因此当事实上已停止监听时,即便有接入连接也直接关闭,考虑添加处理
            if (!IsListening)
            {
                connectedClient.Close();
                break;
            }
            else
            {
                if (ConnectCount >= ConnectLimit)
                {
                    // [TODO] 连接数过多
                    connectedClient.Close();
                }
                if (connectedClient != null)
                {
                    // 连接创建后,分配数据缓存并激活读取方法
                    ClientData clientData = new(connectedClient);
                    ClientBufferDictionary[clientData] = new byte[8192];
                    clientData.PersonalNetworkStatu = NetworkStatu.ConnectFree;
                    clientData.ClientNetworkStatu = NetworkStatu.InRoom;
                    SubReadAsync(clientData);
                }
            }

        }
    }
    /// <summary>
    /// 单连接读取方法
    /// </summary>
    /// <param name="tcpClient"></param>
    public async void SubReadAsync(ClientData clientData)
    {
        // 因为Read读取的覆写性和读取时可能的不连续性,目前通过循环读取,拼接写入处理的方式获取数据
        byte[] clientBuffer = ClientBufferDictionary[clientData];

        // 表示当前将要写入缓存的位置
        int writeIndex = 0;
        // 读取循环,如果连接没有被关闭就保持读取
        while (!clientData.Closing)
        {
            // 读取循环
            // 每当ReadAsync从数据流中读取数据,都会读取最多1024字节的数据,如果写入指针接近上限,则缩小写入大小
            try
            {
                writeIndex += await clientData.ClientStream.ReadAsync(clientBuffer, writeIndex, Math.Min(1024, 8192 - writeIndex));
                if (writeIndex >= 1024)
                {   // 如果存在不少于1024字节的待处理数据,进行处理
                    bool isPackReceived = ReadByteSolve(clientBuffer, ref writeIndex, clientData);
                }
            }
            catch (ObjectDisposedException)
            {
                Debug.Log($"尝试读取已关闭的流:{((IPEndPoint)clientData.TcpClient.Client.RemoteEndPoint).Address}");
            }
            catch (Exception e)
            {
                throw e;
            }
        }
    }
    /// <summary>
    /// 数据缓存处理,在方法内部根据判断修改buffer和写入指针
    /// </summary>
    /// <param name="buffer">对应连接的接收缓存</param>
    /// <param name="packBytes">处理后的包</param>
    /// <param name="writeIndex">当前写入位置</param>
    /// <returns>当本次处理获取到包时,返回True,如果无数据或无合法包,返回False</returns>
    public bool ReadByteSolve(byte[] buffer, ref int writeIndex, ClientData clientData)
    {
        Span<byte> spanBuffer = buffer.AsSpan();
        bool isReceivePack = false;
        while (true)
        {
            // 处理循环
            // 循环判断直到没有任何合法包
            int endIndex = MatchBytes(buffer, Pack.PackEndBytes);
            if (endIndex == -1)
            {
                // 未获取到包尾,返回false
                return isReceivePack;
            }
            else if (endIndex >= 0 && endIndex < 1016)
            {
                // 获取到包尾,但位置小于1016,即为残缺的后半个包,修改该标记首位为0x00并将此包尾之后的数据前移
                buffer[endIndex] = 0x00;
                spanBuffer[(endIndex + 8)..].CopyTo(spanBuffer);
                writeIndex -= endIndex;
            }
            else
            {
                // 获取到包尾且位置不小于1016,进行包合法性判断
                Span<byte> rawPack = spanBuffer.Slice(endIndex - 1016, 1024);
                if (Pack.IsLegalPack(rawPack))
                {
                    Debug.Log("Server receive pack*1!");
                    // 判断成功,标记返回True和所提取的包
                    byte[] packBytes = rawPack.ToArray();
                    PackSolve(clientData, packBytes);

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

    //public void SubNetworkReceiveUpdate()
    //{
    //    // 基础安全检查
    //    if (MainClient == null || !MainClient.Connected || ClientStream == null)
    //    {
    //        return;
    //    }

    //    try
    //    {
    //        // 一直读取直到读满缓冲区
    //        while (ClientStream.DataAvailable && writeIndex < ClientBuffer.Length)
    //        {
    //            int spaceLeft = ClientBuffer.Length - writeIndex;
    //            // 读取数据并存放到 writeIndex 之后的位置
    //            int bytesRead = ClientStream.Read(ClientBuffer, writeIndex, spaceLeft);
    //            if (bytesRead == 0)
    //            {
    //                break;
    //            }
    //            writeIndex += bytesRead;
    //        }

    //        // 当包尾位置大于1024则分析
    //        Span<byte> bufferSpan = ClientBuffer.AsSpan();
    //        ReadOnlySpan<byte> endMarker = Pack.PackEndBytes;

    //        while (true)
    //        {
    //            // 在已写入数据的范围内进行读取
    //            int matchIndex = bufferSpan.Slice(0, writeIndex).IndexOf(endMarker);

    //            // 如果找不到包尾，说明当前数据不足以构成一个完整的包
    //            if (matchIndex == -1)
    //            {
    //                // 已写满但还没有找到,直接清除
    //                if (writeIndex >= 8100)
    //                {
    //                    Debug.LogWarning("清理数据...");
    //                    writeIndex = 0;
    //                }
    //                break;
    //            }

    //            // 计算长度
    //            int endPosition = matchIndex + 8;

    //            // 找到包尾且大于1024
    //            if (endPosition >= 1024)
    //            {
    //                // 校验
    //                int packStart = endPosition - 1024;
    //                Span<byte> packCandidate = bufferSpan.Slice(packStart, 1024);

    //                if (Pack.IsLegalPack(packCandidate))
    //                {
    //                    // 校验成功并处理
    //                    PackSolve(packCandidate.ToArray());
    //                }
    //                else
    //                {
    //                    Debug.LogWarning("Found end marker but packet CRC check failed.");
    //                }
    //            }
    //            // 将 endPosition 之后的所有有效数据移动到缓冲区的最前面
    //            int remainingAfterProcess = writeIndex - endPosition;
    //            if (remainingAfterProcess > 0)
    //            {
    //                bufferSpan.Slice(endPosition, remainingAfterProcess).CopyTo(bufferSpan);
    //            }

    //            // 更新索引位置
    //            writeIndex = remainingAfterProcess;
    //        }
    //    }
    //    catch (Exception e)
    //    {
    //        // 如果这里捕获到异常，通常是底层 Socket 断开或资源释放
    //        Debug.LogError($"NetworkRead Error: {e.Message}");
    //    }
    //}

    #endregion

    #region *包信息处理/发送*
    /// <summary>
    /// 广播包给所有已连接客户端,可排除指定玩家
    /// </summary>
    public void BroadcastPack(Pack pack, int exceptPlayerNumber = -1)
    {
        foreach (var clientData in ClientBufferDictionary.Keys)
        {
            if (clientData.RelativePlayerNumber == exceptPlayerNumber) continue;
            if (clientData.ClientStream != null)
            {
                try
                {
                    clientData.ClientStream.WriteAsync(pack.Bytes);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Broadcast to player {clientData.RelativePlayerNumber} failed: {e.Message}");
                }
            }
        }
    }
    /// <summary>
    /// 发送包给指定玩家
    /// </summary>
    public void SendPackToPlayer(Pack pack, int playerNumber)
    {
        foreach (var clientData in ClientBufferDictionary.Keys)
        {
            if (clientData.RelativePlayerNumber == playerNumber)
            {
                if (clientData.ClientStream != null)
                {
                    try
                    {
                        clientData.ClientStream.WriteAsync(pack.Bytes);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Send to player {playerNumber} failed: {e.Message}");
                    }
                }
                break;
            }
        }
    }
    /// <summary>
    /// 广播玩家操作日志给所有人
    /// </summary>
    public void BroadcastPlayerAction(PlayerActionData actionData)
    {
        int startIndex = RecordIndex; // 操作加入前的Count = 该操作的实际索引
        PlayerActionRecordList.Add(actionData);
        Pack pack = new(this, out int taskID);
        List<PlayerActionData> list = new() { actionData };
        pack.PlayerActionPack(list, startIndex, 1);
        BroadcastPack(pack);
    }
    /// <summary>
    /// 处理完整包后的处理方法
    /// </summary>
    /// <param name="clientData"></param>
    /// <param name="packBytes"></param>
    public void PackSolve(ClientData clientData, byte[] packBytes)
    {
        Pack pack = new Pack(packBytes);
        Pack ackPack = null;
        if (pack.PackType == PackType.Signal)
        {
            // 传入包类型是信号包,返回确定包
            ackPack = new(this, pack.TaskID);
            switch (pack.SignalPackDecode())
            {
                case SignalType.Ready:
                    // 标记为InRoom的客户端发送准备信号,接收标记并返回准备确定
                    clientData.ClientNetworkStatu = NetworkStatu.Ready;
                    ackPack.AcknowledgePack(SignalType.ReadyAck);
                    break;

                case SignalType.UnReady:
                    // 标记为Ready的客户端发送取消准备信号,接收标记并返回准备确定
                    clientData.ClientNetworkStatu = NetworkStatu.InRoom;
                    ackPack.AcknowledgePack(SignalType.UnReadyAck);
                    break;
                case SignalType.InitAck:
                    OnInitAckReceived(clientData);
                    break;
                case SignalType.Error:
                    break;
                default:
                    break;
            }
        }
        else if (pack.PackType == PackType.UserProfile)
        {
            ackPack = new(this, pack.TaskID);
            UserProfile userProfile = pack.ConnectRoomPackDecode(out int roomNumber);
            clientData.UserProfile = userProfile;
            // 检查是否是对局中的断线重连
            if (ServerNetworkStatu != NetworkStatu.RoomWait && ServerNetworkStatu != NetworkStatu.ServerInitWait)
            {
                bool isReconnected = false;
                foreach (var existing in ClientBufferDictionary.Keys.ToList())
                {
                    // 匹配用户名且原连接已关闭/超时
                    if (existing.UserProfile != null && existing.UserProfile.Name == userProfile.Name && existing.Closing)
                    {
                        int playerNum = existing.RelativePlayerNumber;
                        ClientBufferDictionary.Remove(existing);
                        clientData.RelativePlayerNumber = playerNum;
                        clientData.ClientNetworkStatu = NetworkStatu.Ready;
                        ClientBufferDictionary[clientData] = new byte[8192];
                        // 发送同步数据
                        HandleSyncRequest(clientData, -1);
                        // 发送当前公共状态
                        Pack dataPack = new(this, out int dpTaskID);
                        dataPack.MatchDataPack(MatchControl);
                        try { clientData.ClientStream.WriteAsync(dataPack.Bytes); } catch { }
                        isReconnected = true;
                        Debug.Log($"Player {playerNum} reconnected.");
                        break;
                    }
                }
                if (!isReconnected)
                {
                    // 对局已开始,新玩家无法加入
                    ackPack.AcknowledgePack(SignalType.Error);
                }
            }
            else
            {
                clientData.ClientNetworkStatu = NetworkStatu.InRoom;
                ackPack.AcknowledgePack(SignalType.AcceptRoom);
            }
        }
        else if (pack.PackType == PackType.ClientStatuPack)
        {
            clientData.ClientStatu = pack.ClientStatuPackDecode();
            clientData.Ping = 0d;
            // 客户端的ClientStatuPack也是其在线证明,等同于隐性心跳应答
        }
        else if (pack.PackType == PackType.Acknowledge && pack.Bytes[104] == (byte)SignalType.HeartbeatAck)
        {
            // 客户端心跳Ack,更新其已确认的日志序号
            int confirmedIndex = pack.HeartbeatAckPackDecode();
            if (confirmedIndex > clientData.RecordStamp)
            {
                clientData.RecordStamp = confirmedIndex;
            }
        }

        if (ackPack != null)
        {
            if (ackPack.PackType != PackType.Empty)
            {
                clientData.ClientStream.WriteAsync(ackPack.Bytes);
            }
        }

        if (pack.PackType == PackType.PlayerAction)
        {
            // 玩家操作处理
            List<PlayerActionData> decodedList = pack.PlayerActionPackDecode(out int startIndex, out int length);
            if (decodedList.Count > 0)
            {
                PlayerActionData playerActionData = decodedList[0];
                OnPlayerActionReceived(clientData, playerActionData);
            }
        }
        else if (pack.PackType == PackType.SyncRequest)
        {
            int lastIndex = pack.SyncRequestPackDecode();
            HandleSyncRequest(clientData, lastIndex);
        }
    }


    /// <summary>
    /// 触发器计时字典
    /// </summary>
    public Dictionary<int, FloatTriggerBase<TaskResult>> SignalDictionary { get; set; } = new();
    /// <summary>
    /// 对信号记录的处理,每帧更新一次,用于判断方法超时时间
    /// </summary>
    public void SignalDictionaryUpdate()
    {
        List<int> keyList = SignalDictionary.Keys.ToList();
        foreach (int key in keyList)
        {
            if (SignalDictionary.TryGetValue(key, out var taskTrigger) && taskTrigger.IsRunning)
            {
                // 更新时间,如果时间已达到,则触发方法
                taskTrigger.OnUpdate(Time.deltaTime);
                taskTrigger.OnDecide(out bool isActive);
                if (isActive && taskTrigger.IsSingle)
                {
                    SignalDictionary.Remove(key);
                }
            }
        }
    }


    /// <summary>
    /// 巡目更新,获取下一张牌,将这张牌和可行行动发送给玩家
    /// </summary>
    public void TurnUpdate()
    {
        int playerIndex = MatchControl.CurrentPlayerIndex;
        Player currentPlayer = MatchControl.PlayerList[playerIndex];
        Tile currentTile = currentPlayer.PlayerHandTile.SingleTile;
        Dictionary<PlayerAction, List<PlayerActionData>> actionData;
        if (currentTile != null)
        {
            // 正常摸牌后的巡目:计算自身操作和听牌相关操作
            actionData = SelfAvailableJudge(currentPlayer.PlayerHandTile, currentTile, MatchControl);
            // 始终可用的基础操作:手切(手中每张不同的牌)和摸切
            actionData[PlayerAction.HandCut] = new();
            var distinctHandTiles = currentPlayer.PlayerHandTile.HandTileList.Distinct().ToList();
            foreach (var tile in distinctHandTiles)
            {
                actionData[PlayerAction.HandCut].Add(new PlayerActionData(new(), tile, PlayerAction.HandCut));
            }
            actionData[PlayerAction.TakeCut] = new List<PlayerActionData>()
            {
                new PlayerActionData(new() { currentTile }, currentTile, PlayerAction.TakeCut)
            };
            if (AvailableTenpaiDict[playerIndex] != null && AvailableTenpaiDict[playerIndex].ContainsKey(currentTile))
            {
                // 听牌列表包含此牌,可以自摸(添加有效数据用于服务端校验)
                var tsumoData = new PlayerActionData(PlayerAction.Tsumo) { TargetTile = currentTile };
                actionData[PlayerAction.Tsumo] = new List<PlayerActionData>() { tsumoData };
            }
            else
            {
                if (!MatchControl.IsRiichi[playerIndex] && CutTenpaiJudge(currentPlayer.PlayerHandTile, currentTile, out var cutTiles))
                {
                    // 如果未立直,判断切哪张牌可以立直
                    if (!actionData.ContainsKey(PlayerAction.Riichi))
                        actionData[PlayerAction.Riichi] = new();
                    foreach (var choose in cutTiles)
                    {
                        if (currentPlayer.PlayerHandTile.HandTileList.Contains(choose.Key))
                        {
                            actionData[PlayerAction.Riichi].Add(new(
                                new(),
                                choose.Key,
                                PlayerAction.Riichi));
                        }
                        else
                        {
                            actionData[PlayerAction.Riichi].Add(new(
                                new() { new Tile(MaJiangLib.Color.Wans, 1) },
                                choose.Key,
                                PlayerAction.Riichi));
                        }
                    }
                }
            }
        }
        else
        {
            // 鸣牌后切牌:没有摸牌,只有手切选项
            actionData = new();
            actionData[PlayerAction.HandCut] = new();
            var distinctTiles = currentPlayer.PlayerHandTile.HandTileList.Distinct().ToList();
            foreach (var tile in distinctTiles)
            {
                actionData[PlayerAction.HandCut].Add(new PlayerActionData(new(), tile, PlayerAction.HandCut));
            }
        }
        // 同步可行操作到MatchControl用于服务端校验
        MatchControl.PlayerSelfAvailableDict = actionData;

        Pack pack = new(this, out int taskID);
        pack.AvailableActionPack(actionData, playerIndex, currentTile ?? new Tile(MaJiangLib.Color.Wans, 0));
        // 发送可行操作给当前玩家
        SendPackToPlayer(pack, playerIndex);

        // 广播新回合信号给其他玩家
        Pack signalPack = new(this, out int signalTaskID);
        signalPack.SignalPack(SignalType.NewRound);
        BroadcastPack(signalPack, playerIndex);
    }

    #region *服务端状态机*
    /// <summary>
    /// 主状态机更新,每帧调用
    /// </summary>
    public void ServerStageUpdate()
    {
        switch (ServerNetworkStatu)
        {
            case NetworkStatu.RoomWait:
                // 检查是否所有玩家都已准备
                if (CheckAllReady() && ClientBufferDictionary.Count == 4)
                {
                    StartMatchFlow(MatchType.FourMahjongSouth);
                }
                break;
            case NetworkStatu.ServerInitWait:
                if (Time.time - StageEnterTime > 10f)
                {
                    Debug.LogWarning("Init wait timeout, forcing start.");
                    MatchControl.CurrentStageType = StageType.StartStage;
                    MatchControl.CurrentPlayerIndex = MatchControl.CurrentDealerIndex;
                    EnterState(NetworkStatu.ServerActionWait);
                    EnterWaitingForPlayerAction();
                }
                break;
            case NetworkStatu.ServerActionWait:
                if (Time.time - StageEnterTime > MatchControl.MatchSettingData.ThinkingTime)
                {
                    ForceDefaultAction();
                }
                break;
            case NetworkStatu.ServerClaimWait:
                if (Time.time - StageEnterTime > MatchControl.MatchSettingData.BaseTime)
                {
                    ProcessClaimResult();
                }
                else
                {
                    int result = MatchControl.PlayerActionUpdate();
                    if (result >= 0 || result == -1)
                    {
                        ProcessClaimResult();
                    }
                }
                break;
        }
    }
    /// <summary>
    /// 进入新状态,同步到所有客户端
    /// </summary>
    public void EnterState(NetworkStatu newState)
    {
        if (ServerNetworkStatu == newState) return;
        ServerNetworkStatu = newState;
        StageEnterTime = Time.time;
        SyncClientStatuToAll();
        Debug.Log($"Server entered state: {newState}");
    }
    /// <summary>
    /// 同步当前服务端状态到所有客户端
    /// </summary>
    public void SyncClientStatuToAll()
    {
        foreach (var clientData in ClientBufferDictionary.Keys)
        {
            if (clientData.Closing) continue;
            int playerNum = clientData.RelativePlayerNumber;
            NetworkStatu targetStatu = GetTargetClientStatu(playerNum, clientData);
            ClientStatu statu = CreateClientStatu(clientData, targetStatu, playerNum);
            Pack pack = new(this, out int taskID);
            pack.ClientStatuPack(statu);
            try
            {
                clientData.ClientStream.WriteAsync(pack.Bytes);
            }
            catch (Exception e)
            {
                Debug.LogError($"SyncClientStatu to player {playerNum} failed: {e.Message}");
            }
        }
    }
    /// <summary>
    /// 根据服务端状态和玩家角色计算目标客户端状态
    /// </summary>
    public NetworkStatu GetTargetClientStatu(int playerNum, ClientData clientData)
    {
        switch (ServerNetworkStatu)
        {
            case NetworkStatu.ServerInitWait:
                return NetworkStatu.StartWait;
            case NetworkStatu.ServerActionWait:
                return playerNum == MatchControl.CurrentPlayerIndex ? NetworkStatu.DiscardStage : NetworkStatu.RoundWait;
            case NetworkStatu.ServerClaimWait:
                if (playerNum == MatchControl.CurrentPlayerIndex) return NetworkStatu.ActionStageWait;
                if (MatchControl.PlayerWaitActionList[playerNum].AvailableActions.Count > 0) return NetworkStatu.ActionStage;
                return NetworkStatu.RoundWait;
            case NetworkStatu.ServerEndWait:
                return NetworkStatu.EndStage;
            default:
                return clientData.ClientNetworkStatu;
        }
    }
    /// <summary>
    /// 创建/更新客户端状态对象
    /// </summary>
    public ClientStatu CreateClientStatu(ClientData clientData, NetworkStatu targetStatu, int playerNum)
    {
        if (clientData.ClientStatu == null)
        {
            clientData.ClientStatu = new ClientStatu(1, targetStatu, SelfIPAddress, clientData.UserProfile);
        }
        clientData.ClientStatu.CurrentNetworkStatu = targetStatu;
        clientData.ClientStatu.PlayerNumber = playerNum;
        return clientData.ClientStatu;
    }
    /// <summary>
    /// 检查所有连接玩家是否都已准备
    /// </summary>
    public bool CheckAllReady()
    {
        if (ClientBufferDictionary.Count == 0) return false;
        foreach (var clientData in ClientBufferDictionary.Keys)
        {
            if (clientData.ClientNetworkStatu != NetworkStatu.Ready)
                return false;
        }
        return true;
    }
    /// <summary>
    /// 开始匹配流程,从RoomWait进入ServerInit
    /// </summary>
    public void StartMatchFlow(MatchType matchType)
    {
        MatchControl = new MainMatchControl();
        // 新对局开始,清空操作日志和客户端确认状态
        PlayerActionRecordList.Clear();
        foreach (var clientData in ClientBufferDictionary.Keys)
        {
            clientData.RecordStamp = -1;
        }
        // 初始化对局
        MatchControl.ServerFourMatchInit(new MatchSettingData(matchType), ClientBufferDictionary);
        // 初始化听牌列表
        for (int p = 0; p < 4; p++)
        {
            TenpaiUpdate(p);
        }
        // 发送初始化包给每个玩家(包含各自的手牌和玩家序号)
        foreach (var clientData in ClientBufferDictionary.Keys)
        {
            int playerNum = clientData.RelativePlayerNumber;
            // 发送玩家状态(包含序号)
            ClientStatu clientStatu = CreateClientStatu(clientData, NetworkStatu.StartWait, playerNum);
            Pack statuPack = new(this, out int statuTaskID);
            statuPack.ClientStatuPack(clientStatu);
            try
            {
                clientData.ClientStream.WriteAsync(statuPack.Bytes);
            }
            catch (Exception e)
            {
                Debug.LogError($"Send statu to player {playerNum} failed: {e.Message}");
            }
            // 发送初始化手牌包
            Pack initPack = new(this, out int initTaskID);
            List<Tile> handTiles = MatchControl.PlayerList[playerNum].PlayerHandTile.HandTileList;
            initPack.HonbaInitPack(handTiles, HonbaEndMark.NewMatch);
            try
            {
                clientData.ClientStream.WriteAsync(initPack.Bytes);
            }
            catch (Exception e)
            {
                Debug.LogError($"Send init to player {playerNum} failed: {e.Message}");
            }
        }
        // 广播公共对局信息
        Pack publicPack = new(this, out int pubTaskID);
        publicPack.MatchDataPack(MatchControl);
        BroadcastPack(publicPack);
        // 进入等待初始化确认阶段
        EnterState(NetworkStatu.ServerInitWait);
    }
    /// <summary>
    /// 收到玩家初始化确认,由PackSolve调用
    /// </summary>
    public void OnInitAckReceived(ClientData clientData)
    {
        if (ServerNetworkStatu != NetworkStatu.ServerInitWait) return;
        int playerNum = clientData.RelativePlayerNumber;
        if (InitAckSet.Contains(playerNum)) return;
        InitAckSet.Add(playerNum);
        Debug.Log($"Player {playerNum} InitAck received. ({InitAckSet.Count}/4)");
        if (InitAckSet.Count >= ClientBufferDictionary.Count)
        {
            // 所有玩家确认完毕,开始第一回合
            MatchControl.CurrentStageType = StageType.StartStage;
            MatchControl.CurrentPlayerIndex = MatchControl.CurrentDealerIndex;
            EnterState(NetworkStatu.ServerActionWait);
            EnterWaitingForPlayerAction();
        }
    }
    /// <summary>
    /// 进入等待当前玩家操作状态
    /// </summary>
    /// <param name="needDraw">是否需要摸牌(吃碰明杠后不需要摸牌)</param>
    public void EnterWaitingForPlayerAction(bool needDraw = true)
    {
        int playerIndex = MatchControl.CurrentPlayerIndex;
        EnterState(NetworkStatu.ServerActionWait);
        if (needDraw)
        {
            // 摸牌
            Tile drawTile = MatchControl.GetNextTile(MatchControl.IsKan[playerIndex]);
            MatchControl.PlayerList[playerIndex].PlayerHandTile.SingleTile = drawTile;
            // 广播摸牌动作(所有人都能看到有人摸牌,但只有当前玩家知道摸到什么)
            PlayerActionData takeAction = new(PlayerAction.Take)
            {
                SelfNumber = playerIndex,
                TargetTile = drawTile
            };
            MatchControl.ActionDeduce(takeAction);
            BroadcastPlayerAction(takeAction);
            // 更新听牌
            TenpaiUpdate(playerIndex);
        }
        else
        {
            // 鸣牌后切牌:清空上次的SingleTile,确保TurnUpdate走post-claim分支
            MatchControl.PlayerList[playerIndex].PlayerHandTile.SingleTile = null;
        }
        // 发送可行操作
        TurnUpdate();
    }
    /// <summary>
    /// 进入等待鸣牌响应状态
    /// </summary>
    public void EnterWaitingForClaim(int discardPlayer, Tile discardTile, PlayerAction sourceAction = PlayerAction.Skip)
    {
        CurrentPhaseSourceAction = sourceAction;
        CurrentDiscardTile = discardTile;
        ClaimActionDict.Clear();
        EnterState(NetworkStatu.ServerClaimWait);
        // 计算其他玩家可进行的鸣牌操作
        bool hasClaim = false;
        for (int p = 0; p < 4; p++)
        {
            MatchControl.PlayerWaitActionList[p].AvailableActions = new List<PlayerAction>();
            MatchControl.PlayerWaitActionList[p].IsSendAction = false;
            if (p == discardPlayer) continue;
            var actions = SingleClaimingAvailableJudge(MatchControl.PlayerList[p].PlayerHandTile, discardTile, MatchControl);
            // 检查和牌
            if (AvailableTenpaiDict[p] != null && AvailableTenpaiDict[p].ContainsKey(discardTile))
            {
                var ronData = new RonData(p, MatchControl, AvailableTenpaiDict[p][discardTile], false);
                var ronPoint = FanCalculator.RonPointCalculator(ronData);
                if (ronPoint.Fan >= MatchControl.MatchSettingData.MinimumYakuFan)
                {
                    if (!actions.ContainsKey(PlayerAction.Ron))
                        actions[PlayerAction.Ron] = new List<PlayerActionData>();
                    actions[PlayerAction.Ron].Add(new PlayerActionData(p, discardPlayer, new(), discardTile, PlayerAction.Ron));
                }
            }
            if (actions.Count > 0)
            {
                hasClaim = true;
                MatchControl.PlayerClaimingAvailableList[p] = actions;
                MatchControl.PlayerWaitActionList[p].AvailableActions = actions.Keys.ToList();
                // 发送可行操作给该玩家
                Pack pack = new(this, out int taskID);
                pack.AvailableActionPack(actions, p, discardTile);
                SendPackToPlayer(pack, p);
            }
        }
        if (!hasClaim)
        {
            // 无人可以鸣牌,直接进入下一家
            ProcessClaimResult();
        }
    }
    /// <summary>
    /// 收到玩家操作,由PackSolve调用
    /// </summary>
    public void OnPlayerActionReceived(ClientData clientData, PlayerActionData actionData)
    {
        int playerNumber = clientData.RelativePlayerNumber;
        actionData.SelfNumber = playerNumber;
        if (ServerNetworkStatu == NetworkStatu.ServerActionWait)
        {
            if (playerNumber != MatchControl.CurrentPlayerIndex)
            {
                Debug.LogWarning($"Player {playerNumber} sent action during another player's turn. Ignored.");
                return;
            }
            // 校验操作
            bool isValid = MatchControl.PlayerActionJudge(actionData, playerNumber, false);
            if (!isValid)
            {
                Debug.LogWarning($"Player {playerNumber} sent invalid action {actionData.PlayerAction}. Forced skip.");
                // 强制摸切
                ForceDefaultAction();
                return;
            }
            ProcessPlayerAction(actionData);
        }
        else if (ServerNetworkStatu == NetworkStatu.ServerClaimWait)
        {
            // 鸣牌响应阶段
            if (actionData.PlayerAction == PlayerAction.Skip)
            {
                MatchControl.PlayerWaitActionList[playerNumber].ChoosedAction = PlayerAction.Skip;
                MatchControl.PlayerWaitActionList[playerNumber].IsSendAction = true;
            }
            else
            {
                bool isValid = MatchControl.PlayerActionJudge(actionData, playerNumber, true);
                if (isValid)
                {
                    ClaimActionDict[playerNumber] = actionData;
                }
                else
                {
                    MatchControl.PlayerWaitActionList[playerNumber].ChoosedAction = PlayerAction.Skip;
                    MatchControl.PlayerWaitActionList[playerNumber].IsSendAction = true;
                }
            }
            // 检查是否可以结束响应阶段
            int result = MatchControl.PlayerActionUpdate();
            if (result >= 0 || result == -1)
            {
                ProcessClaimResult();
            }
        }
    }
    /// <summary>
    /// 处理当前玩家的操作并推进状态
    /// </summary>
    public void ProcessPlayerAction(PlayerActionData actionData)
    {
        int playerNumber = actionData.SelfNumber;
        // 执行操作
        MatchControl.ActionDeduce(actionData);
        BroadcastPlayerAction(actionData);
        // 更新听牌(如果手牌发生变化)
        TenpaiUpdate(playerNumber);
        // 根据操作类型决定下一步
        switch (actionData.PlayerAction)
        {
            case PlayerAction.HandCut:
            case PlayerAction.TakeCut:
            case PlayerAction.Riichi:
                // 切牌/立直后进入响应阶段
                EnterWaitingForClaim(playerNumber, actionData.TargetTile, actionData.PlayerAction);
                break;
            case PlayerAction.Tsumo:
            case PlayerAction.Ron:
            case PlayerAction.Draw:
                // 和牌/流局,进入结束阶段
                EnterState(NetworkStatu.ServerEndWait);
                ProcessRoundEnd(actionData);
                break;
            case PlayerAction.ClosedKan:
            case PlayerAction.Kita:
                // 暗杠/拔北后需要重新摸牌(岭上),继续当前玩家回合
                // 简化处理:直接给当前玩家发新牌并等待操作
                EnterWaitingForPlayerAction();
                break;
            case PlayerAction.AddedKan:
                // 加杠后进入响应阶段(可被抢杠)
                EnterWaitingForClaim(playerNumber, actionData.TargetTile, PlayerAction.AddedKan);
                break;
            case PlayerAction.Chi:
            case PlayerAction.Peng:
            case PlayerAction.OpenKan:
                // 吃碰明杠后,响应者成为当前玩家并需要切牌(不摸牌)
                MatchControl.CurrentPlayerIndex = playerNumber;
                EnterWaitingForPlayerAction(false);
                break;
            default:
                break;
        }
    }
    /// <summary>
    /// 强制当前玩家执行默认操作(超时或非法操作)
    /// </summary>
    public void ForceDefaultAction()
    {
        if (ServerNetworkStatu != NetworkStatu.ServerActionWait) return;
        int playerNumber = MatchControl.CurrentPlayerIndex;
        Tile singleTile = MatchControl.PlayerList[playerNumber].PlayerHandTile.SingleTile;
        // 默认摸切
        PlayerActionData forcedAction = new(
            new() { singleTile },
            singleTile,
            PlayerAction.TakeCut)
        {
            SelfNumber = playerNumber
        };
        Debug.Log($"Player {playerNumber} timed out. Forced TakeCut.");
        MatchControl.ActionDeduce(forcedAction);
        BroadcastPlayerAction(forcedAction);
        EnterWaitingForClaim(playerNumber, singleTile);
    }
    /// <summary>
    /// 处理鸣牌响应结果
    /// </summary>
    public void ProcessClaimResult()
    {
        if (ServerNetworkStatu != NetworkStatu.ServerClaimWait) return;
        int result = MatchControl.PlayerActionUpdate();
        if (result >= 0)
        {
            // 有玩家进行了最高优先级操作
            PlayerActionData winningAction = ClaimActionDict.ContainsKey(result) ? ClaimActionDict[result] : null;
            if (winningAction == null)
            {
                // 从WaitActionList中重建
                var chosen = MatchControl.PlayerWaitActionList[result].ChoosedAction;
                winningAction = new PlayerActionData(result, MatchControl.CurrentPlayerIndex, new(), CurrentDiscardTile, chosen);
            }
            winningAction.TargetTile = CurrentDiscardTile;
            winningAction.TargetPlayerNumber = MatchControl.CurrentPlayerIndex;
            ProcessPlayerAction(winningAction);
        }
        else
        {
            // 无人响应或全部Skip
            if (CurrentPhaseSourceAction == PlayerAction.AddedKan)
            {
                // 加杠且无人抢杠,加杠玩家继续摸牌并切牌
                EnterWaitingForPlayerAction();
            }
            else
            {
                // 进入下一家
                MatchControl.CurrentPlayerIndex = (MatchControl.CurrentPlayerIndex + 1) % 4;
                EnterWaitingForPlayerAction();
            }
        }
    }
    /// <summary>
    /// 处理回合结束(和牌/流局)
    /// </summary>
    public void ProcessRoundEnd(PlayerActionData endAction)
    {
        Debug.Log($"Round ended by {endAction.PlayerAction} from player {endAction.SelfNumber}");
        int winner = endAction.SelfNumber;

        // 先广播结束信号
        Pack endPack = new(this, out int taskID);
        endPack.SignalPack(SignalType.ActionAck);
        BroadcastPack(endPack);

        if (endAction.PlayerAction == PlayerAction.Ron || endAction.PlayerAction == PlayerAction.Tsumo)
        {
            // 和牌结算
            bool isTsumo = endAction.PlayerAction == PlayerAction.Tsumo;
            Tile winningTile = isTsumo
                ? MatchControl.PlayerList[winner].PlayerHandTile.SingleTile
                : CurrentDiscardTile;

            // 获取和牌面子
            List<Group> winningGroups;
            if (AvailableTenpaiDict[winner] != null && AvailableTenpaiDict[winner].TryGetValue(winningTile, out var groups))
            {
                winningGroups = groups;
            }
            else
            {
                Debug.LogError($"Cannot find winning groups for player {winner} tile {winningTile}");
                StartNewHonba(HonbaEndMark.AbortiveDraw);
                return;
            }

            // 计算番数和点数
            FanCalculator.MatchInformation = MatchControl;
            RonData ronData = new(winner, MatchControl, winningGroups, isTsumo);
            RonPoint ronPoint = FanCalculator.RonPointCalculator(ronData);
            int basePoint = ronPoint.BasePoint;

            bool isDealer = winner == MatchControl.CurrentDealerIndex;
            int honbaBonus = MatchControl.Honba * 300;

            // 计算收入，记录变动
            Dictionary<int, int> pointChanges = new() { { 0, 0 }, { 1, 0 }, { 2, 0 }, { 3, 0 } };
            int winnerGain = 0;

            if (isTsumo)
            {
                if (isDealer)
                {
                    // 庄家自摸: 每家付 basePoint * 2
                    int perPayment = RoundUp100(basePoint * 2) + honbaBonus;
                    for (int p = 0; p < 4; p++)
                    {
                        if (p == winner) continue;
                        pointChanges[p] = -perPayment;
                        winnerGain += perPayment;
                    }
                }
                else
                {
                    // 子家自摸: 庄付 basePoint * 2, 子付 basePoint
                    for (int p = 0; p < 4; p++)
                    {
                        if (p == winner) continue;
                        int payment = (p == MatchControl.CurrentDealerIndex)
                            ? RoundUp100(basePoint * 2) + honbaBonus
                            : RoundUp100(basePoint) + honbaBonus;
                        pointChanges[p] = -payment;
                        winnerGain += payment;
                    }
                }
            }
            else
            {
                // 荣和: 放铳者全付
                int discarder = endAction.TargetPlayerNumber;
                int payment;
                if (isDealer)
                {
                    payment = RoundUp100(basePoint * 6) + honbaBonus;
                }
                else
                {
                    payment = RoundUp100(basePoint * 4) + honbaBonus;
                }
                pointChanges[discarder] = -payment;
                winnerGain = payment;
            }

            // 立直棒归赢家
            winnerGain += MatchControl.RiichiStickCount * 1000;

            // 应用点数变动
            pointChanges[winner] = winnerGain;
            for (int p = 0; p < 4; p++)
            {
                MatchControl.PlayerPoint[p] += pointChanges[p];
            }

            // 立直棒清零
            MatchControl.RiichiStickCount = 0;

            Debug.Log($"Round end: P{winner} wins by {(isTsumo ? "Tsumo" : "Ron")}. "
                + $"Fan={ronPoint.Fan}, Fu={ronPoint.Fu}, BasePoint={basePoint}. "
                + $"Points: [{string.Join(", ", MatchControl.PlayerPoint)}]");

            // 确定本场结束标记
            HonbaEndMark endMark = isDealer ? HonbaEndMark.DealerRon : HonbaEndMark.NonDealerRon;
            StartNewHonba(endMark);
        }
        else if (endAction.PlayerAction == PlayerAction.Draw)
        {
            // 流局结算
            DrawType drawType;
            GlobalFunction.DrawJudge(out drawType, MatchControl);

            if (drawType == DrawType.DeadWallDraw)
            {
                // 荒牌流局: 听牌罚符
                List<int> tenpaiPlayers = new();
                List<int> notenPlayers = new();
                for (int p = 0; p < 4; p++)
                {
                    if (AvailableTenpaiDict[p] != null && AvailableTenpaiDict[p].Count > 0)
                        tenpaiPlayers.Add(p);
                    else
                        notenPlayers.Add(p);
                }

                if (tenpaiPlayers.Count > 0 && notenPlayers.Count > 0)
                {
                    // 罚符总计 3000 点 (非听牌者付出), 听牌者平分
                    int totalPenalty = 3000 * notenPlayers.Count;
                    int perTenpaiGain = totalPenalty / tenpaiPlayers.Count;
                    foreach (int p in notenPlayers)
                        MatchControl.PlayerPoint[p] -= 3000;
                    foreach (int p in tenpaiPlayers)
                        MatchControl.PlayerPoint[p] += perTenpaiGain;
                    Debug.Log($"DeadWallDraw penalty: tenpai={{{string.Join(",", tenpaiPlayers)}}}, noten={{{string.Join(",", notenPlayers)}}}");
                }

                // 立直棒留在场上
                Debug.Log($"Riichi sticks remain: {MatchControl.RiichiStickCount}");

                // 判定本场结束标记
                bool dealerTenpai = tenpaiPlayers.Contains(MatchControl.CurrentDealerIndex);
                HonbaEndMark endMark = dealerTenpai ? HonbaEndMark.DealerTenpaiDraw : HonbaEndMark.DealerNotenDraw;
                StartNewHonba(endMark);
            }
            else
            {
                // 途中流局(四杠散了/四家立直/四风连打/九种九牌): 不罚符
                Debug.Log($"Abortive draw: {drawType}");
                MatchControl.RiichiStickCount = 0;
                StartNewHonba(HonbaEndMark.AbortiveDraw);
            }
        }
    }

    /// <summary>
    /// 点数向上取整到百位
    /// </summary>
    private static int RoundUp100(int points)
    {
        if (points % 100 == 0) return points;
        return points + (100 - points % 100);
    }

    /// <summary>
    /// 开始新本场
    /// </summary>
    public void StartNewHonba(HonbaEndMark endMark)
    {
        // 推进局数/风场
        MatchControl.FourNewHonba(endMark);

        // 检查是否终局(东风战打完东四, 半庄战打完南四)
        if ((MatchControl.MatchType == MatchType.FourMahjongEast && MatchControl.Wind > WindType.East)
            || (MatchControl.MatchType == MatchType.FourMahjongSouth && MatchControl.Wind > WindType.South))
        {
            Debug.Log("Match finished!");
            EnterState(NetworkStatu.RoomWait);
            return;
        }

        // 新本场初始化(重新洗牌发牌)
        MatchControl.ServerFourHonbaInit();

        // 清空操作日志和确认状态
        PlayerActionRecordList.Clear();
        foreach (var clientData in ClientBufferDictionary.Keys)
        {
            clientData.RecordStamp = -1;
        }

        // 更新听牌列表
        for (int p = 0; p < 4; p++)
        {
            TenpaiUpdate(p);
        }

        // 发送初始化包给每个玩家
        foreach (var clientData in ClientBufferDictionary.Keys)
        {
            int playerNum = clientData.RelativePlayerNumber;
            // 发送玩家状态(包含序号)
            ClientStatu clientStatu = CreateClientStatu(clientData, NetworkStatu.StartWait, playerNum);
            Pack statuPack = new(this, out int statuTaskID);
            statuPack.ClientStatuPack(clientStatu);
            try { clientData.ClientStream.WriteAsync(statuPack.Bytes); }
            catch (Exception e) { Debug.LogError($"Send statu failed: {e.Message}"); }

            // 发送初始化手牌包
            Pack initPack = new(this, out int initTaskID);
            List<Tile> handTiles = MatchControl.PlayerList[playerNum].PlayerHandTile.HandTileList;
            initPack.HonbaInitPack(handTiles, endMark);
            try { clientData.ClientStream.WriteAsync(initPack.Bytes); }
            catch (Exception e) { Debug.LogError($"Send init failed: {e.Message}"); }
        }

        // 广播公共对局信息
        Pack publicPack = new(this, out int pubTaskID);
        publicPack.MatchDataPack(MatchControl);
        BroadcastPack(publicPack);

        // 进入等待初始化确认阶段
        InitAckSet.Clear();
        EnterState(NetworkStatu.ServerInitWait);
    }
    /// <summary>
    /// 处理断线重连同步请求
    /// </summary>
    public void HandleSyncRequest(ClientData clientData, int lastConfirmedIndex)
    {
        // lastConfirmedIndex: 客户端最后确认的0-based索引, -1表示无记录
        if (lastConfirmedIndex < -1) lastConfirmedIndex = -1;
        int firstUnconfirmed = lastConfirmedIndex + 1;
        int availableCount = RecordIndex - firstUnconfirmed;
        if (availableCount > 0)
        {
            var syncList = PlayerActionRecordList.GetRange(lastConfirmedIndex + 1, availableCount);
            Pack syncPack = new(this, out int syncTaskID);
            syncPack.SyncResponsePack(syncList, lastConfirmedIndex + 1);
            try
            {
                clientData.ClientStream.WriteAsync(syncPack.Bytes);
            }
            catch (Exception e)
            {
                Debug.LogError($"Sync response to player {clientData.RelativePlayerNumber} failed: {e.Message}");
            }
        }
        else
        {
            // 没有新日志,发送空同步响应
            Pack syncPack = new(this, out int syncTaskID);
            syncPack.SyncResponsePack(new List<PlayerActionData>(), lastConfirmedIndex + 1);
            try
            {
                clientData.ClientStream.WriteAsync(syncPack.Bytes);
            }
            catch { }
        }
    }
    #endregion

    #endregion
}
