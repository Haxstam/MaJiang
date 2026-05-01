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
            networkStatus[i] = data.ClientNetworkStatu;
            i++;
        }
        TextMeshPro.text = $"networkStatus:\n{networkStatus[0]},{networkStatus[1]},{networkStatus[2]},{networkStatus[3]}";


        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("R");
        }

        foreach (ClientData data in ClientBufferDictionary.Keys)
        {
            data.Ping += Time.deltaTime;
        }
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
    /// 心跳包发送,同步一个列表中的操作
    /// </summary>
    public void HeartBeatPackSend()
    {
        foreach (var clientData in ClientBufferDictionary.Keys)
        {
            List<PlayerActionData> actionList = new();
            if (clientData.RecordStamp < RecordIndex)
            {
                actionList = PlayerActionRecordList.GetRange(clientData.RecordStamp + 1, RecordIndex - clientData.RecordStamp);
            }
            if (actionList.Count != 0)
            {
                Pack pack = new(this, out int taskID);
                pack.PlayerActionPack(actionList, clientData.RecordStamp + 1, actionList.Count);
                clientData.ClientStream.WriteAsync(pack.Bytes);
            }
        }
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
        /// 用户端对应时间戳
        /// </summary>
        public int RecordStamp { get; set; } = 0;
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
            clientData.ClientNetworkStatu = NetworkStatu.InRoom;
            ackPack.AcknowledgePack(SignalType.AcceptRoom);
        }
        else if (pack.PackType == PackType.ClientStatuPack)
        {
            clientData.ClientStatu = pack.ClientStatuPackDecode();
            clientData.Ping = 0d;
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
            PlayerActionData playerActionData = pack.PlayerActionPackDecode(out int startIndex, out int length)[0];
            MatchControl.ServerActionSolve(playerActionData);

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
        Tile currentTile = MatchControl.MainTileList[0];
        MatchControl.MainTileList.RemoveAt(0);
        Dictionary<PlayerAction, List<PlayerActionData>> actionData = SelfAvailableJudge(currentPlayer.PlayerHandTile, currentTile, MatchControl);
        if (AvailableTenpaiDict[playerIndex].ContainsKey(currentTile))
        {
            // 听牌列表包含此牌,可以自摸
            actionData[PlayerAction.Tsumo] = new();
        }
        else
        {
            if (!MatchControl.IsRiichi[playerIndex] && CutTenpaiJudge(currentPlayer.PlayerHandTile, currentTile, out var cutTiles))
            {
                // 如果未立直,判断切哪张牌可以立直
                foreach (var choose in cutTiles)
                {
                    // TODO 立直手切摸切修改
                    /* 
                     * 因为立直也要区分手切摸切,分开写有点奇怪,这里采用了比较取巧的办法
                     * 如果是手切立直,则tiles列表为空,如果是摸切立直,则tiles列表有一张1w
                     * 此外,这样判断时,如果摸切的牌手中也有,即便选择摸切相同牌也算手切
                     */
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
        Pack pack = new(this, out int taskID);
        pack.AvailableActionPack(actionData, playerIndex, currentTile);

        if (MatchControl.MatchType == MatchType.FourMahjongSouth)
        {
            // 提醒所有其他玩家,当前为新一轮
            for (int i = 0; i < 4; i++)
            {
                if (i != playerIndex)
                {
                    Pack signalPack = new(this, out int signalTaskID);
                    signalPack.SignalPack(SignalType.NewRound);
                }
            }
        }
    }

    #endregion
}
