using MaJiangLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using static MaJiangLib.GlobalFunction;
/// <summary>
/// 对局服务器类,在进行新对局时创建并初始化
/// </summary>
public class MatchServer : MonoBehaviour, INetworkInformation
{
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
        if (Input.GetKeyDown(KeyCode.G))
        {
            NetworkStatu[] networkStatus = new NetworkStatu[4];
            int i = 0;
            foreach (ClientData data in ClientBufferDictionary.Keys)
            {
                networkStatus[i] = data.ClientNetworkStatu;
                i++;
            }
            Debug.Log($"{networkStatus[0]},{networkStatus[1]},{networkStatus[2]},{networkStatus[3]}");
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
    /// 玩家每步都有的等待时间,可以进行操作时重置为5s(默认)
    /// </summary>
    public float[] PlayerBaseTimeList { get; private set; } = new float[4];
    /// <summary>
    /// 玩家在一本场内所通用的总思考时间,仅在开始新的本场时重置为20s(默认),若为零则强制跳过操作
    /// </summary>
    public float[] PlayerThinkingTimeCountList { get; private set; } = new float[4];
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

    public IPAddress SelfIPAddress { get; set; }
    public UserProfile SelfProfile { get; set; } = new("TestServer");
    /// <summary>
    /// 听牌列表刷新,当有玩家手牌发生改变,即手切/拔北/鸣牌/开杠时调用,更新此玩家的听牌列表
    /// </summary>
    /// <param name="playerNumber">改变自己手牌的玩家序号</param>
    /// <returns></returns>
    public bool TenpaiRefresh(int playerNumber)
    {
        if (GlobalFunction.TenpaiJudge(MatchControl.PlayerList[playerNumber].PlayerHandTile, out Dictionary<Tile, List<Group>> groups))
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
    /// <summary>
    /// 长考时间的刷新,仅在新本场开始时调用
    /// </summary>
    public void ThinkingTimeReset()
    {
        PlayerThinkingTimeCountList = new float[4] { 20f, 20f, 20f, 20f };
    }

    #region *方法*
    // 创建新对局

    public void StartMatch(MatchType matchType)
    {
        MatchControl = new();
        if (matchType == MatchType.FourMahjongSouth)
        {
            ServerNetworkStatu = NetworkStatu.ServerInit;

            FourHonbaInit();

        }
    }
    /// <summary>
    /// 四麻通用对局开始初始化:随机排序座位,初始化点数
    /// </summary>
    public void FourMatchInit(MatchSettingData matchSettingData)
    {
        MatchControl.MatchSettingData = matchSettingData;
        MatchControl.PlayerPoint = Enumerable.Repeat(MatchControl.MatchSettingData.BasePoint, 4).ToList();
        // 生成坐席位次
        int[] playerOrder = new int[4] { 0, 1, 2, 3 };
        GlobalFunction.Shuffle(playerOrder, MatchControl.Random.NextInt32());
        int i = 0;
        // 对连接的各个用户分配其座次,并依次提取它们的信息生成Player
        foreach (var item in ClientBufferDictionary)
        {
            ClientData clientData = item.Key;
            clientData.RelativePlayerNumber = playerOrder[i];
            i++;
            // 添加对应座次玩家的信息,初始化手牌和状态
            MatchControl.PlayerList.Add(new(clientData.UserProfile, playerOrder[i], matchSettingData.BasePoint, new(), StageType.InitStage));
        }
        FourHonbaInit();
    }
    /// <summary>
    /// 四麻通用本场初始化:生成新牌山,分配王牌,重置所有标记,但还未掀开宝牌和分发起始手牌
    /// </summary>
    public void FourHonbaInit()
    {
        MatchControl.Random = new();
        // 牌山设定:通过随机数生成牌山,取前122张作为手牌,后14张作为王牌
        List<Tile> rawTileList = RandomCardGenerator(out int randomNumber, true, true, true, MatchControl.Random);
        MatchControl.PrimeTileIndex = 0;
        MatchControl.PrimeTileList = rawTileList.GetRange(122, 14);
        rawTileList.RemoveRange(122, 14);
        MatchControl.MainTileList = rawTileList;
        MatchControl.DiscardTileList = new();
        MatchControl.DoraList = new();
        MatchControl.UraDoraList = new();
        MatchControl.IsRiichi = new() { false, false, false, false };
        MatchControl.IsDoubleRiichi = new() { false, false, false, false };
        MatchControl.IsKang = new() { false, false, false, false };
        MatchControl.HaveIppatsu = new() { false, false, false, false };
        MatchControl.PlayerOpenSetList = new();
        MatchControl.FirstCycleIppatsu = true;
        MatchControl.CurrentStageType = StageType.StartStage;
        MatchControl.CurrentTileIndex = 0;
        MatchControl.RemainTileCount = 122;
        MatchControl.KangCount = 0;
        MatchControl.KangMark = 0;

    }
    /// <summary>
    /// 四麻庄家连庄状态下的场况更新
    /// </summary>
    public void FourNewHonba()
    {
        FourHonbaInit();
        MatchControl.Honba++;
    }
    /// <summary>
    /// 四麻庄家下庄时的场况更新
    /// </summary>
    public void FourNewRound()
    {
        FourHonbaInit();
        MatchControl.Honba = 0;
        if (MatchControl.Round == 4)
        {
            MatchControl.Wind++;
            MatchControl.Round = 0;
            MatchControl.CurrentBankerIndex = 0;
            MatchControl.CurrentPlayerIndex = MatchControl.CurrentBankerIndex;
        }
        else
        {
            MatchControl.Round++;
            MatchControl.CurrentBankerIndex++;
            MatchControl.CurrentPlayerIndex = MatchControl.CurrentBankerIndex;
        }
    }
    #endregion

    #region *操作方法*
    /// <summary>
    /// 玩家行为解决方法,接收进行操作的玩家序号和其操作,并判断是否合理
    /// </summary>
    /// <param name="playerActionData"></param>
    /// <param name="player"></param>
    /// <returns></returns>
    public bool PlayerActionSolve(PlayerActionData playerActionData, int player)
    {
        if (AvailableActionDict != null)
        {
            Dictionary<PlayerAction, List<PlayerActionData>> playerActionDict = AvailableActionDict[player];
            // 列表化
            List<PlayerActionData> actionList = playerActionDict[playerActionData.PlayerAction];
            if (actionList.Count == 0)
            {
                // 根本没有对应操作,返回false
                return false;
            }
            else if (actionList.Contains(playerActionData) || playerActionData.PlayerAction == PlayerAction.Skip)
            {
                // 如果包括所选操作或是选择跳过,返回true
                PlayerWaitActionList[player].ChoosedAction = playerActionData.PlayerAction;
                PlayerWaitActionList[player].IsSendAction = true;
                return true;
            }
            return false;
        }
        else
        {
            throw new System.Exception("意料外的操作判断");
        }
    }
    /// <summary>
    /// 当有人打出牌时调用,获取所有可行的响应并发送
    /// </summary>
    /// <returns></returns>
    public bool AvailableActionSend()
    {
        for (int i = 0; i < MatchControl.PlayerList.Count; i++)
        {
            HandTile handTile = MatchControl.PlayerList[i].PlayerHandTile;
            // 根据场况依次获取所有玩家的可行操作并存储

            // 鸣牌(吃/碰/杠)的可行操作
            AvailableActionDict[i] = SingleClaimingAvailableJudge(handTile, MatchControl);
            // 荣和的可行操作
            if (AvailableTenpaiDict[i].ContainsKey(MatchControl.CurrentTile))
            {
                // 因为只要能和,就是固定的,因此仅标记可以荣和
                AvailableActionDict[i].Add(PlayerAction.Ron, new());
            }
        }

        // [TODO] Send
        throw new System.Exception("未完成");

        return false;
    }
    /// <summary>
    /// 当有人打出牌时调用,获取所有可行的响应并发送且存储对应操作
    /// </summary>
    /// <returns></returns>
    public bool AvailableActionSolve()
    {
        PlayerWaitActionList = new ActionMark[4] { new(), new(), new(), new() };
        for (int i = 0; i < MatchControl.PlayerList.Count; i++)
        {
            HandTile handTile = MatchControl.PlayerList[i].PlayerHandTile;
            // 根据场况依次获取所有玩家的可行操作并存储

            // 鸣牌(吃/碰/杠)的可行操作
            AvailableActionDict[i] = SingleClaimingAvailableJudge(handTile, MatchControl);
            if (AvailableActionDict[i][PlayerAction.Peng].Count > 0)
            {
                PlayerWaitActionList[i].AvailableActions.Add(PlayerAction.Peng);
            }
            if (AvailableActionDict[i][PlayerAction.Chi].Count > 0)
            {
                PlayerWaitActionList[i].AvailableActions.Add(PlayerAction.Chi);
            }
            // 荣和的可行操作
            if (AvailableTenpaiDict[i].ContainsKey(MatchControl.CurrentTile))
            {
                // 因为只要能和,就是固定的,因此仅标记可以荣和
                AvailableActionDict[i].Add(PlayerAction.Ron, new());
                RonPoint ronPoint = FanCalculator.RonPointCalculator(new(i, MatchControl, AvailableTenpaiDict[i][MatchControl.CurrentTile]));
                if (ronPoint.YakuFan() >= MatchControl.MatchSettingData.MinimumYakuFan)
                {
                    PlayerWaitActionList[i].AvailableActions.Add(PlayerAction.Ron);
                }
                else
                {
                    // 番数不够,不让和牌且标记为振听
                    IsPlayerFuriten[i] = true;
                }
            }
            // 所有玩家都可以跳过,即便他们没得选
            PlayerWaitActionList[i].AvailableActions.Add(PlayerAction.Skip);
        }

        // [TODO] Send
        throw new System.Exception("未完成");
        return false;
    }
    /// <summary>
    /// 循环玩家操作刷新,当处于待鸣牌状态时调用
    /// </summary>
    /// <returns>-2为存在更高优先操作未做出,-1为无可操作玩家,0~3为对应操作玩家</returns>
    public int PlayerActionRefresh()
    {
        // [MARK] 这块显式转换太多了,类型不重要,考虑单独列一个变量去做比较

        // 当前还未被做出的最优先操作
        PlayerAction priorityAction = PlayerAction.Skip;
        // 当前已经被做出的最优先操作
        PlayerAction currentPriorityAction = PlayerAction.Skip;
        int priorityPlayer = -1;
        for (int i = 0; i < 4; i++)
        {
            ActionMark mark = PlayerWaitActionList[i];
            if (mark.AvailableActions.Count != 0)
            {
                if (mark.IsSendAction)
                {
                    if ((int)mark.ChoosedAction > (int)currentPriorityAction)
                    {
                        // 该玩家已选择,其所进行的操作比现有操作大
                        currentPriorityAction = mark.ChoosedAction;
                        priorityPlayer = i;
                    }
                }
                else
                {
                    PlayerAction maxPriorityAction = (PlayerAction)mark.AvailableActions.Max(p => (int)p);
                    if ((int)maxPriorityAction > (int)priorityAction)
                    {   // 该玩家还未选择,其所能进行的操作比现有操作大
                        priorityAction = maxPriorityAction;
                    }
                }
            }
        }
        if (priorityAction == PlayerAction.Skip && currentPriorityAction == PlayerAction.Skip)
        {   // 所有人什么都不能做
            return -1;
        }
        else if (currentPriorityAction > priorityAction)
        {   // 已经做出的操作比还未做出的操作大,终止
            return priorityPlayer;
        }
        else
        {   // 存在有更高优先级但未操作的玩家,返回false,等待完成
            return -2;
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
            Ping = 0;
            Closing = false;
            PersonalNetworkStatu = NetworkStatu.ConnectWait;
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
    public void StopConnection()
    {

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
            int endIndex = MatchBytes(buffer, PackCoder.PackEndBytes);
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
                if (PackCoder.IsLegalPack(rawPack))
                {
                    Debug.Log("Server receive pack*1!");
                    // 判断成功,标记返回True和所提取的包
                    byte[] packBytes = rawPack.ToArray();
                    PackSolve(clientData, packBytes);
                    // 记录延迟,仅此处可以更改连接延迟
                    clientData.Ping = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - BitConverter.ToDouble(rawPack.Slice(8, 8));

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

    #endregion

    #region *包信息处理*
    /// <summary>
    /// 处理完整包后的处理方法
    /// </summary>
    /// <param name="clientData"></param>
    /// <param name="packBytes"></param>
    public void PackSolve(ClientData clientData, byte[] packBytes)
    {
        Pack pack = new Pack(packBytes);
        if (pack.PackType == PackType.Signal)
        {
            // 传入包类型是信号包,返回确定包
            Pack ackPack = new(this, pack.TaskID);
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

                case SignalType.Error:
                    break;
                default:
                    break;
            }
            if (ackPack.PackType != PackType.Empty)
            {
                clientData.ClientStream.WriteAsync(ackPack.Bytes);
            }
        }
    }
    #endregion
}
