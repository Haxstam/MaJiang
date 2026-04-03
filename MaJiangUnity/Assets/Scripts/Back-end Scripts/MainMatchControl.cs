using MaJiangLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MaJiangLib.GlobalFunction;
using static MatchServer;
/// <summary>
/// 主对局控制类,存储系统操作方法和对局相关信息
/// </summary>
public class MainMatchControl : MonoBehaviour, IMatchInformation
{
    /*
     *   1.对于麻将王牌的特殊性:
     *  杠牌后从王牌中摸一张并从牌山末取一张补足王牌,宝牌堆(10张)固定不变
     *  为代码实现方便,用一个标记标明下一张待摸岭上牌,王牌列表只增加不减少
     *  
     *   2.对于对局信息,目前设定是对于MainMatchControl,其有一个完全转换为Byte[]的方法,但避免使用此方法
     *     对局信息的更新是按照单次行为在本地的演算来获取的,服务器和用户端之间的对局信息同步则尽可能避免
     *     
     */
    private void Start()
    {

    }
    private void Update()
    {

    }

    ///// <summary>
    ///// 主对局控制类自东一开局的构造器,需要指定是否有红宝牌,是否有字牌
    ///// </summary>
    ///// <param name="matchType"></param>
    ///// <param name="haveRedDora"></param>
    ///// <param name="haveHonor"></param>
    //public MainMatchControl(List<Player> playerList, MatchType matchType, bool haveRedDora, bool haveHonor)
    //{
    //    MatchType = matchType;
    //    if (MatchType == MatchType.FourMahjongEast || MatchType == MatchType.FourMahjongSouth)
    //    {
    //        PlayerList = playerList;
    //        FourInit();
    //        // 各列表/数值初始化为东一 0本场的开局情况,且未开始发牌和掀开宝牌
    //        Wind = WindType.East;
    //        Round = 1;
    //        Honba = 0;
    //        PlayerPoint = new() { 25000, 25000, 25000, 25000 };
    //        CurrentBankerIndex = 0;
    //        CurrentPlayerIndex = CurrentBankerIndex;
    //    }
    //    else
    //    {

    //    }
    //}
    //public MainMatchControl(List<Player> playerList, MatchSettingData matchSettingData)
    //{
    //    MatchType = matchSettingData.MatchType;
    //    PlayerList = playerList;
    //    if (matchSettingData.MatchType == MatchType.FourMahjongEast || MatchType == MatchType.FourMahjongSouth)
    //    {

    //    }
    //}
    ///// <summary>
    ///// 经由公共信息创建的当前比赛信息,用于用户端创建比赛信息实例
    ///// </summary>
    ///// <param name="playerInformation"></param>
    ///// <param name="matchInformation"></param>
    //public MainMatchControl(List<IPlayerInformation> playerInformation, IMatchInformation matchInformation)
    //{
    //    PlayerList = new();
    //    foreach (IPlayerInformation player in playerInformation)
    //    {
    //        PlayerList.Add(new(player));
    //    }
    //    MatchType = matchInformation.MatchType;
    //    DiscardTileList = matchInformation.DiscardTileList;
    //    DoraList = matchInformation.DoraList;
    //    UraDoraList = matchInformation.UraDoraList;
    //    KangCount = matchInformation.KangCount;
    //    KangMark = matchInformation.KangMark;
    //    Wind = matchInformation.Wind;
    //    Round = matchInformation.Round;
    //    Honba = matchInformation.Honba;
    //    PlayerPoint = matchInformation.PlayerPoint;
    //    IsRiichi = matchInformation.IsRiichi;
    //    IsDoubleRiichi = matchInformation.IsDoubleRiichi;
    //    RemainTileCount = matchInformation.RemainTileCount;
    //    HaveIppatsu = matchInformation.HaveIppatsu;
    //    FirstCycleIppatsu = matchInformation.FirstCycleIppatsu;
    //    IsKang = matchInformation.IsKang;
    //    CurrentPlayerIndex = matchInformation.CurrentPlayerIndex;
    //    CurrentStageType = matchInformation.CurrentStageType;
    //    CurrentBankerIndex = matchInformation.CurrentBankerIndex;
    //    PlayerFuluList = matchInformation.PlayerFuluList;
    //}
    public MainMatchControl()
    {

    }
    #region *公共信息,所有玩家都可见*
    /// <summary>
    /// 玩家列表,仅提取信息
    /// </summary>
    public List<Player> PlayerList { get; set; }
    /// <summary>
    /// 记录当前场的类型
    /// </summary>
    public MatchType MatchType { get; set; }
    /// <summary>
    /// 弃牌堆,存储所有人的弃牌,第0所对应的玩家为东一场的亲家
    /// </summary>
    public List<List<Tile>> DiscardTileList { get; set; }
    /// <summary>
    /// 宝牌指示牌列表,存储所有已被展示的宝牌指示牌
    /// </summary>
    public List<Tile> DoraPointerList { get; set; }
    /// <summary>
    /// 里宝牌列表,暂时放在这里
    /// </summary>
    public List<Tile> UraDoraPointerList { get; set; }
    /// <summary>
    /// 目前开杠的数量
    /// </summary>
    public int KangCount { get; set; }
    /// <summary>
    /// 开杠玩家的标记,通过该变量判断是否有多个玩家开杠,初始为6,第一位玩家开杠时设定此为玩家编号,若存在其他玩家开杠则设定为5,用于判断四杠流局
    /// </summary>
    public int KangMark { get; set; }
    /// <summary>
    /// 当前风场
    /// </summary>
    public WindType Wind { get; set; }
    /// <summary>
    /// 当前为第几局
    /// </summary>
    public int Round { get; set; }
    /// <summary>
    /// 当前为几本场
    /// </summary>
    public int Honba { get; set; }
    /// <summary>
    /// 玩家点数
    /// </summary>
    public List<int> PlayerPoint { get; set; }
    /// <summary>
    /// 剩余牌的数目,用于判断河底海底等
    /// </summary>
    public int RemainTileCount { get; set; }
    /// <summary>
    /// 当前正进行回合的玩家的序号,当前玩家结束阶段完毕后,转至下一玩家的舍牌阶段
    /// </summary>
    public int CurrentPlayerIndex { get; set; }
    /// <summary>
    /// 当前正进行回合的玩家的阶段类型
    /// </summary>
    public StageType CurrentStageType { get; set; }
    /// <summary>
    /// 当前局的庄家序号
    /// </summary>
    public int CurrentDealerIndex { get; set; }
    /// <summary>
    /// 所有玩家副露牌的列表,用于判断流局满贯/四杠散了等情况
    /// </summary>
    public List<List<Group>> PlayerOpenSetList { get; set; }
    public int RiichiStickCount { get; set; }
    /// <summary>
    /// 对局设置相关
    /// </summary>
    public MatchSettingData MatchSettingData { get; set; }
    #endregion

    #region *标记类变量*
    /// <summary>
    /// 第一巡的指示,用于判断两立直天地和,开局设定为True,有人鸣牌或庄家再摸牌后设定为False
    /// </summary>
    public bool FirstCycleIppatsu { get; set; }
    /// <summary>
    /// 记录是否立直,立直者对应序号的值为true
    /// </summary>
    public List<bool> IsRiichi { get; set; }
    /// <summary>
    /// 记录是否为两立直,此变量对应序号为True时,IsRiichi对应的序号也必须为True
    /// </summary>
    public List<bool> IsDoubleRiichi { get; set; }
    /// <summary>
    /// 一发的判断,某人立直后设定其序号对应的为True,有人鸣牌或其本人再打出一张后设定为False
    /// </summary>
    public List<bool> HaveIppatsu { get; set; }
    /// <summary>
    /// 刚摸完岭上牌的状态,拔北或开杠后设定为True,打出牌后设定为False
    /// </summary>
    public List<bool> IsKan { get; set; }
    public Tile CurrentTile { get; set; }
    public List<int> KitaCount { get; set; }
    #endregion

    #region *内部信息,不作为接口成员*
    /// <summary>
    /// 牌山下一张牌在牌山中的位置,当摸牌时获取对应牌山位置的牌
    /// </summary>
    public int CurrentTileIndex { get; set; }
    /// <summary>
    /// 牌山
    /// </summary>
    public List<Tile> MainTileList { get; set; }
    /// <summary>
    /// 王牌,恒定14张(四麻)/18张(三麻),前10张固定为宝牌/里宝牌指示牌,后4张/8张为岭上牌
    /// </summary>

    // 理论上只要所摸牌是非公开的,这张牌在哪其实无所谓,没开的宝牌/牌山随便一张/海底作为被摸的牌是完全等价的
    // 日麻四麻下王牌7墩,靠近海底的那侧为前,前5墩为宝牌指示牌,后2墩岭上牌,第一张宝牌从靠近岭上的那侧开始掀开,开杠后摸岭上牌并依次指定与王牌相邻的海底牌为王牌,最后不摸
    public List<Tile> PrimeTileList { get; set; }
    /// <summary>
    /// 下一张岭上牌的标记
    /// </summary>
    public int PrimeTileIndex { get; set; }
    /// <summary>
    /// 标记是否已初始化
    /// </summary>
    public bool Initialized { get; set; } = false;
    public Xoshiro256StarStar Random { get; set; }
    public bool IsServer { get; set; }
    #endregion

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

    #region *服务端方法/属性*
    /*
     * TODO 考虑将网络相关内容和对局完全分开,使得对局方法不参与网络交互
     * 
     * 1. 对局初始化顺序:
     *    服务端:新游戏:服务端全局初始化(确定座次,初始随机数)->服务端本场初始化(生成随机牌山,分发王牌手牌)->新本场(确定庄位/本场/局数)
     *          新本场:服务端本场初始化(生成随机牌山,分发王牌手牌)->新本场(确定庄位/本场/局数)
     * 2. 目前客户端和服务端的操作完全由单次玩家操作推演而前进,从而将所有操作分解成零碎的元操作
     *    如A摸牌后手切,B吃牌再切出一张就是4个操作,(A的回合)A摸牌->A手切->(B的回合)B吃牌->B手切->(下一位玩家)
     */


    /// <summary>
    /// 玩家可行鸣牌列表,存储所有玩家对于某张牌所可行的鸣牌操作
    /// </summary>
    public Dictionary<PlayerAction, List<PlayerActionData>>[] PlayerClaimingAvailableList { get; set; } = new Dictionary<PlayerAction, List<PlayerActionData>>[4] { new(), new(), new(), new() };
    /// <summary>
    /// 玩家可行回合内操作列表,存储当前玩家在摸牌后可进行的操作
    /// </summary>
    public Dictionary<PlayerAction, List<PlayerActionData>> PlayerSelfAvailableDict { get; set; } = new();
    /// <summary>
    /// 振听标记,若玩家振听则标记为true,无法通过荣和和牌
    /// </summary>
    public bool[] IsPlayerFuriten { get; set; } = new bool[4];
    /// <summary>
    /// 玩家能否鸣牌和是否返回鸣牌的记录,所有可鸣牌玩家鸣牌则鸣牌结束,顺序依次对应玩家
    /// </summary>
    public ActionMark[] PlayerWaitActionList { get; set; } = new ActionMark[4] { new(), new(), new(), new() };
    /// <summary>
    /// 玩家若听牌,其听牌的列表.考虑到摸切(即不改变听牌)占切牌的很大一部分,因此单独存储从而减少运算
    /// </summary>
    public Dictionary<Tile, List<Group>>[] AvailableTenpaiDict { get; private set; } = new Dictionary<Tile, List<Group>>[4];
    public void ServerUpdate()
    {
        PlayerActionUpdate();
    }
    /// <summary>
    /// 四麻通用对局开始初始化:随机排序座位,初始化点数,仅供服务端调用
    /// </summary>
    public void ServerFourMatchInit(MatchSettingData matchSettingData, Dictionary<ClientData, byte[]> clientBufferDictionary)
    {
        MatchSettingData = matchSettingData;
        PlayerPoint = Enumerable.Repeat(MatchSettingData.BasePoint, 4).ToList();
        // 生成坐席位次
        int[] playerOrder = new int[4] { 0, 1, 2, 3 };
        GlobalFunction.Shuffle(playerOrder, Random.NextInt32());
        int i = 0;
        // 对连接的各个用户分配其座次,并依次提取它们的信息生成Player
        foreach (var item in clientBufferDictionary)
        {
            ClientData clientData = item.Key;
            clientData.RelativePlayerNumber = playerOrder[i];
            i++;
            // 添加对应座次玩家的信息,初始化手牌和状态
            PlayerList.Add(new(clientData.UserProfile, playerOrder[i], new(clientData.RelativePlayerNumber), StageType.InitStage));
        }
        ServerFourHonbaInit();
    }
    /// <summary>
    /// 四麻新本场通用初始化,仅供服务端调用
    /// </summary>
    public void ServerFourHonbaInit()
    {
        Random = new();
        // 牌山设定:通过随机数生成牌山,取前122张作为手牌,后14张作为王牌
        List<Tile> rawTileList = RandomCardGenerator(out int randomNumber, true, true, true, Random);
        PrimeTileIndex = 0;
        PrimeTileList = rawTileList.GetRange(122, 14);
        rawTileList.RemoveRange(122, 14);
        MainTileList = rawTileList;
        FourRegularInit();
        for (int j = 0; j < 4; j++)
        {
            // 从牌山的第0处开始,依次取13张牌作为玩家手牌并将其从牌山中移除
            PlayerList[j].PlayerHandTile.HandTileList = rawTileList.GetRange(0, 13);
            rawTileList.RemoveRange(0, 13);
        }
    }
    /// <summary>
    /// 从牌山摸一张牌,区分岭上牌和牌山
    /// </summary>
    /// <param name="isRinshan"></param>
    /// <returns></returns>
    public Tile GetNextTile(bool isRinshan)
    {
        Tile nextTile;
        if (isRinshan)
        {
            nextTile = PrimeTileList[PrimeTileIndex];
            PrimeTileIndex++;
        }
        else
        {
            nextTile = MainTileList[CurrentTileIndex];
            CurrentTileIndex++;
        }
        RemainTileCount--;
        return nextTile;
    }
    /// <summary>
    /// 根据玩家返回的操作进行返回
    /// </summary>
    /// <param name="currentActionData"></param>
    public void ServerActionSolve(PlayerActionData currentActionData)
    {
        if (CurrentStageType == StageType.ClaimStage)
        {
            if (PlayerWaitActionList[currentActionData.SelfNumber].AvailableActions.Contains(currentActionData.PlayerAction))
            {
                PlayerWaitActionList[currentActionData.SelfNumber].ChoosedAction = currentActionData.PlayerAction;
                PlayerWaitActionList[currentActionData.SelfNumber].IsSendAction = true;
            }
        }
        ActionDeduce(currentActionData);
        // 用于判断是否要进入鸣牌阶段
        bool isClaimingAvailable = false;
        int actionPlayerNumber = currentActionData.SelfNumber;
        switch (currentActionData.PlayerAction)
        {
            case PlayerAction.Skip:
                break;
            case PlayerAction.Take:
                PlayerSelfAvailableDict = SelfAvailableJudge(PlayerList[actionPlayerNumber].PlayerHandTile, currentActionData.TargetTile, this);
                break;
            case PlayerAction.HandCut:
            case PlayerAction.TakeCut:
                PlayerClaimingActionUpdate(currentActionData, out isClaimingAvailable);
                break;
            case PlayerAction.Chi:
                break;
            case PlayerAction.Peng:
                break;
            case PlayerAction.OpenKan:
                break;
            case PlayerAction.AddedKan:
                break;
            case PlayerAction.ClosedKan:
                break;
            case PlayerAction.Riichi:
                break;
            case PlayerAction.Kita:
                break;
            case PlayerAction.Ron:
                break;
            case PlayerAction.Tsumo:
                break;
            case PlayerAction.Draw:
                break;
            default:
                break;
        }
    }
    public void StageUpdate()
    {
        switch (CurrentStageType)
        {
            case StageType.InitStage:
                break;
            case StageType.StartStage:
                PlayerActionData playerActionData = new(PlayerAction.Take)
                {
                    TargetTile = GetNextTile(IsKan[CurrentPlayerIndex])
                };
                break;
            case StageType.DiscardStage:
                break;
            case StageType.ClaimStage:
                break;
            case StageType.EndStage:
                break;
            default:
                break;
        }
    }
    /// <summary>
    /// 循环玩家操作刷新,当处于待鸣牌状态时调用
    /// </summary>
    /// <returns>-2为存在更高优先操作未做出,-1为无可操作玩家,0~3为对应操作玩家</returns>
    public int PlayerActionUpdate()
    {
        // [MARK] 这块显式转换太多了,类型不重要,考虑单独列一个变量去做比较
        // 整体是为了解决鸣牌优先级的问题,和>(杠>碰)>吃>跳过,仅当某个操作被作出且没有大于它的优先级的操作有被作出的可能时终止

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
    public void PlayerClaimingActionUpdate(PlayerActionData playerActionData, out bool isClaimingAvailable)
    {
        isClaimingAvailable = false;
        foreach (var player in PlayerList)
        {
            PlayerClaimingAvailableList[player.PlayerNumber] = SingleClaimingAvailableJudge(player.PlayerHandTile, playerActionData.TargetTile, this);
            if (PlayerClaimingAvailableList[player.PlayerNumber].Count != 0)
            {
                isClaimingAvailable = true;
                // 将所有可行操作的类型添加到鸣牌等待列表中的可行操作里
                PlayerWaitActionList[player.PlayerNumber].AvailableActions.AddRange(PlayerClaimingAvailableList[player.PlayerNumber].Keys.ToList());
            }

            if (AvailableTenpaiDict[player.PlayerNumber].ContainsKey(playerActionData.TargetTile))
            {
                // 如果当前打出的牌包含在某玩家的听牌列表中,开始判断该玩家能否荣和此牌
                RonData ronData = new(player.PlayerNumber, this, AvailableTenpaiDict[player.PlayerNumber][playerActionData.TargetTile], false);
                RonPoint ronPoint = FanCalculator.RonPointCalculator(ronData);
                if (ronPoint.Fan < MatchSettingData.MinimumYakuFan)
                {
                    // 因为无役或番缚,不允许和牌
                    IsPlayerFuriten[player.PlayerNumber] = true;
                }
                else
                {
                    // 如果可以和牌,将和牌操作添加进列表中
                    PlayerWaitActionList[player.PlayerNumber].AvailableActions.Add(PlayerAction.Ron);
                }
            }
        }
    }
    /// <summary>
    /// 玩家行为解决方法,接收进行操作的玩家序号和其操作,并判断是否合理
    /// </summary>
    /// <param name="playerActionData"></param>
    /// <param name="player"></param>
    /// <returns></returns>
    public bool PlayerActionJudge(PlayerActionData playerActionData, int player, bool isClaim)
    {
        if (isClaim)
        {
            if (PlayerClaimingAvailableList != null)
            {
                Dictionary<PlayerAction, List<PlayerActionData>> playerActionDict = PlayerClaimingAvailableList[player];
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
        else
        {
            if (PlayerSelfAvailableDict != null)
            {
                List<PlayerActionData> actionList = PlayerSelfAvailableDict[playerActionData.PlayerAction];
                if (actionList.Count == 0)
                {
                    return false;
                }
                foreach (PlayerActionData availableData in actionList)
                {
                    // 所有回合内操作都是针对单张牌的,因此若存在操作的单张牌相同,则返回true
                    if (availableData.TargetTile == playerActionData.TargetTile)
                    {
                        return true;
                    }
                }
                // 没有对应操作,返回false
                return false;
            }
            else
            {
                throw new System.Exception("意料外的操作判断");
            }
        }
    }

    #endregion

    #region *客户端方法*
    public void ClientFourMatchInit(Pack pack)
    {
        PlayerList = new List<Player>() { new(), new(), new(), new() };
        DoraPointerList = new();
        UraDoraPointerList = new();

    }
    /// <summary>
    /// 四麻客户端本场初始化,根据传入数据获取自己的手牌和本场初始化参数,仅供客户端调用,本身被动调用
    /// </summary>
    public void ClientFourHonbaInit(Pack pack, int selfPlayerNumber)
    {
        List<Tile> tileList = pack.HonbaInitPackDecode(out HonbaEndMark honbaEndMark);
        PlayerList[selfPlayerNumber].PlayerHandTile.HandTileList = tileList;
        FourNewHonba(honbaEndMark);
    }
    #endregion

    #region *方法*
    /// <summary>
    /// 通用初始化,客户端和用户端都要进行的初始化
    /// </summary>
    public void FourRegularInit()
    {
        DiscardTileList = new() { new(), new(), new(), new() };
        DoraPointerList = new();
        UraDoraPointerList = new();
        IsRiichi = new() { false, false, false, false };
        IsDoubleRiichi = new() { false, false, false, false };
        IsKan = new() { false, false, false, false };
        HaveIppatsu = new() { false, false, false, false };
        PlayerOpenSetList = new() { new(), new(), new(), new() };
        FirstCycleIppatsu = true;
        CurrentStageType = StageType.StartStage;
        CurrentTileIndex = 0;
        RemainTileCount = 122;
        KangCount = 0;
        KangMark = 6;
    }

    /// <summary>
    /// 四麻新局的初始化
    /// </summary>
    /// <param name="honbaEndMark"></param>
    /// <exception cref="Exception"></exception>
    public void FourNewHonba(HonbaEndMark honbaEndMark)
    {
        FourRegularInit();
        switch (honbaEndMark)
        {
            case HonbaEndMark.DealerRon:
                // 庄家和牌,本场加一,立直棒归零,连庄
                Honba++;
                RiichiStickCount = 0;
                break;
            case HonbaEndMark.NonDealerRon:
                // 子家和牌,本场归零,立直棒归零,连庄
                Honba = 0;
                RiichiStickCount = 0;
                if (Round == 4)
                {
                    // 为当前风场的第4局,切换风场
                    Wind++;
                    Round = 1;
                    CurrentDealerIndex = 0;
                    CurrentPlayerIndex = CurrentDealerIndex;
                }
                else
                {
                    // 局数加一
                    Round++;
                    CurrentDealerIndex++;
                    CurrentPlayerIndex = CurrentDealerIndex;
                }
                break;
            case HonbaEndMark.DealerTenpaiDraw:
                // 庄家听牌流局,本场加一,立直棒不变,连庄
                Honba++;
                break;
            case HonbaEndMark.DealerNotenDraw:
                // 庄家未听流局,本场加一,立直棒不变,下庄
                Honba = 0;
                if (Round == 4)
                {
                    Wind++;
                    Round = 1;
                    CurrentDealerIndex = 0;
                    CurrentPlayerIndex = CurrentDealerIndex;
                }
                else
                {
                    Round++;
                    CurrentDealerIndex++;
                    CurrentPlayerIndex = CurrentDealerIndex;
                }
                break;
            case HonbaEndMark.AbortiveDraw:
                // 途中流局,本场加一,立直棒不变,连庄
                Honba++;
                break;
            case HonbaEndMark.NewMatch:
                // 新对局,立直棒归零,东一0本场指定庄位开始,本场归零
                Wind = WindType.East;
                Round = 1;
                Honba = 0;
                CurrentDealerIndex = 0;
                RiichiStickCount = 0;
                break;
            default:
                // 一般无法触及
                throw new Exception("意料外输入");
        }
    }

    /// <summary>
    /// 破除一发,同时也破除第一巡"一发"
    /// </summary>
    public void ClearIppatsu()
    {
        for (int i = 0; i < HaveIppatsu.Count; i++)
        {
            HaveIppatsu[i] = false;
            FirstCycleIppatsu = false;
        }
    }
    /// <summary>
    /// 接收单个玩家操作,根据操作和现有情况进行推断
    /// </summary>
    /// <param name="currentActionData"></param>
    public void ActionDeduce(PlayerActionData currentActionData)
    {
        int actionPlayerNumber = currentActionData.SelfNumber;
        switch (currentActionData.PlayerAction)
        {
            case PlayerAction.Skip:
                Debug.Log("错误:一般不会传入该动作");
                break;
            case PlayerAction.Take:
                // 摸牌,从牌山头部摸一张牌,或是摸岭上牌
                // 目前,如果摸牌操作的数据中包含牌,则设定此牌为下一张宝牌,当庄家第一次摸牌和开杠后玩家摸牌时,通过此方式发送宝牌信息
                RemainTileCount--;

                if (FirstCycleIppatsu && actionPlayerNumber == CurrentDealerIndex && DiscardTileList[CurrentDealerIndex].Any())
                {
                    // 存在首巡标记,且目前为庄家摸牌,其牌河存在牌,即庄家第二次摸牌,破除首巡一发
                    FirstCycleIppatsu = false;
                }
                break;
            case PlayerAction.HandCut:
                // 手切,从其手牌中打出一张牌
                DiscardTileList[actionPlayerNumber].Add(currentActionData.TargetTile);
                break;
            case PlayerAction.TakeCut:
                // 摸切,打出其刚摸到的牌
                DiscardTileList[actionPlayerNumber].Add(currentActionData.TargetTile);

                // 如果此玩家立直,当其摸切时破除其一发
                HaveIppatsu[actionPlayerNumber] = false;
                break;
            case PlayerAction.Chi:
                // 吃
                PlayerOpenSetList[actionPlayerNumber].Add(
                    new(
                    groupType: GroupType.OpenStraight,
                    color: currentActionData.TargetTile.Color,
                    tiles: currentActionData.Tiles,
                    player: currentActionData.TargetPlayerNumber,
                    singleTile: currentActionData.TargetTile
                    ));

                CurrentPlayerIndex = actionPlayerNumber;
                ClearIppatsu();
                break;
            case PlayerAction.Peng:
                // 碰
                PlayerOpenSetList[actionPlayerNumber].Add(
                new(
                groupType: GroupType.OpenTriple,
                color: currentActionData.TargetTile.Color,
                tiles: currentActionData.Tiles,
                player: currentActionData.TargetPlayerNumber,
                singleTile: currentActionData.TargetTile
                ));

                CurrentPlayerIndex = actionPlayerNumber;
                ClearIppatsu();
                break;
            case PlayerAction.OpenKan:
                // 明杠

                PlayerOpenSetList[actionPlayerNumber].Add(
                new(
                groupType: GroupType.OpenKan,
                color: currentActionData.TargetTile.Color,
                tiles: currentActionData.Tiles,
                player: currentActionData.TargetPlayerNumber,
                singleTile: currentActionData.TargetTile
                ));

                CurrentPlayerIndex = actionPlayerNumber;
                ClearIppatsu();
                break;
            case PlayerAction.AddedKan:
                // 加杠,遍历该玩家所有副露面子,获取到与加杠牌相同的牌组成的明刻,修改该面子为加杠
                foreach (var group in PlayerOpenSetList[actionPlayerNumber])
                {
                    if (group.Tiles[0] == currentActionData.TargetTile && group.GroupType == GroupType.OpenTriple)
                    {
                        group.GroupType = GroupType.AddedKan;
                        group.Tiles.Add(currentActionData.TargetTile);
                    }
                }

                ClearIppatsu();
                break;
            case PlayerAction.ClosedKan:
                // 暗杠,在该玩家的副露组中添加一个暗杠面子,记录时面子内没有指定牌
                PlayerOpenSetList[actionPlayerNumber].Add(new(
                    groupType: GroupType.ClosedKan,
                    color: currentActionData.Tiles[0].Color,
                    tiles: currentActionData.Tiles
                    ));

                ClearIppatsu();
                break;
            case PlayerAction.Riichi:
                DiscardTileList[actionPlayerNumber].Add(currentActionData.TargetTile);
                IsRiichi[actionPlayerNumber] = true;
                if (FirstCycleIppatsu)
                {
                    IsDoubleRiichi[actionPlayerNumber] = true;
                }
                RiichiStickCount++;
                break;
            case PlayerAction.Kita:
                KitaCount[actionPlayerNumber]++;

                ClearIppatsu();
                break;
            //  实际上这几种都是终结对局的情况,只是体现不同
            case PlayerAction.Ron:
                break;
            case PlayerAction.Tsumo:
                break;
            case PlayerAction.Draw:
                break;
            default:
                Debug.Log("预料外输入");
                break;
        }
    }
    #endregion


    #region *序列化部分*
    /// <summary>
    /// 对于比赛信息中公共信息的序列化,返回不包含用户名的512Bytes字节串
    /// </summary>
    /// <returns></returns>
    public byte[] GetPublicBytes()
    {
        // 对局信息,使用 512 bytes 空间,不包含牌山,也即仅传输用户端会用到的信息
        // 前16位,为各种短变量
        Span<byte> MainBytes = new byte[512];
        MainBytes[0] = (byte)MatchType;
        MainBytes[1] = (byte)Wind;
        MainBytes[2] = (byte)Round;
        MainBytes[3] = (byte)Honba;
        MainBytes[4] = (byte)KangCount;
        MainBytes[5] = (byte)KangMark;
        MainBytes[6] = (byte)CurrentPlayerIndex;
        MainBytes[7] = (byte)CurrentStageType;
        MainBytes[8] = (byte)CurrentDealerIndex;
        MainBytes[9] = (byte)RemainTileCount;
        MainBytes[10] = BitConverter.GetBytes(FirstCycleIppatsu)[0];
        // 标记列表 16~47
        ReplaceBytes(MainBytes, ListToBytes(PlayerPoint), 16);  // +16
        ReplaceBytes(MainBytes, ListToBytes(IsRiichi), 32); // +4
        ReplaceBytes(MainBytes, ListToBytes(IsDoubleRiichi), 36); // +4
        ReplaceBytes(MainBytes, ListToBytes(HaveIppatsu), 40); // +4
        ReplaceBytes(MainBytes, ListToBytes(IsKan), 44); // +4
        // 弃牌堆 48~239
        // 弃牌堆目前设定单个玩家的弃牌堆大小为24张牌,弃牌堆总大小96张牌,也即 192 bytes,单个玩家的弃牌堆为 48 bytes
        // 理论来讲,四人麻将庄家如果单局被吃碰鸣牌12次(三个子家各4次)需要24次过牌(四家总共),剩余48张牌庄家最多摸到12张,也即最多24张过牌
        // 实际弃牌大于20张就足够罕见
        for (int i = 0; i < DiscardTileList.Count; i++)
        {
            ReplaceBytes(MainBytes, ListToBytes(DiscardTileList[i]), 48 + i * 48);
        }
        // 宝牌 240~259
        // 表里宝牌共10张 20 bytes
        ReplaceBytes(MainBytes, ListToBytes(DoraPointerList), 240);
        ReplaceBytes(MainBytes, ListToBytes(UraDoraPointerList), 250);
        // 副露列表 260~483
        // 存储四家共16个Group,也即 224 bytes,单个玩家的副露列表为 56 bytes
        for (int i = 0; i < PlayerOpenSetList.Count; i++)
        {
            ReplaceBytes(MainBytes, ListToBytes(PlayerOpenSetList[i]), 260 + i * 56);
        }
        // 484 立直棒计数
        MainBytes[484] = (byte)RiichiStickCount;

        // 目前485~511 留空
        return MainBytes.ToArray();
    }
    public static MainMatchControl PublicBytesTo(byte[] bytes, int index = 0)
    {
        byte[] shortBytes = new byte[512];
        Array.Copy(bytes, index, shortBytes, 0, 512);
        MainMatchControl mainMatchControl = new();
        // 0-15
        mainMatchControl.MatchType = (MatchType)shortBytes[0];
        mainMatchControl.Wind = (WindType)shortBytes[1];
        mainMatchControl.Round = shortBytes[2];
        mainMatchControl.Honba = shortBytes[3];
        mainMatchControl.KangCount = shortBytes[4];
        mainMatchControl.KangMark = shortBytes[5];
        mainMatchControl.CurrentPlayerIndex = shortBytes[6];
        mainMatchControl.CurrentStageType = (StageType)shortBytes[7];
        mainMatchControl.CurrentDealerIndex = shortBytes[8];
        mainMatchControl.RemainTileCount = shortBytes[9];
        mainMatchControl.FirstCycleIppatsu = BitConverter.ToBoolean(shortBytes, 10);
        // 16-47
        mainMatchControl.PlayerPoint = BytesToIntList(shortBytes, 16, 4);
        mainMatchControl.IsRiichi = BytesToBoolList(shortBytes, 32, 4);
        mainMatchControl.IsDoubleRiichi = BytesToBoolList(shortBytes, 36, 4);
        mainMatchControl.HaveIppatsu = BytesToBoolList(shortBytes, 40, 4);
        mainMatchControl.IsKan = BytesToBoolList(shortBytes, 44, 4);
        // 48-239,260-483

        mainMatchControl.DiscardTileList = new();
        mainMatchControl.PlayerOpenSetList = new();
        if (mainMatchControl.MatchType == MatchType.FourMahjongEast || mainMatchControl.MatchType == MatchType.FourMahjongSouth)
        {
            for (int i = 0; i < 4; i++)
            {
                mainMatchControl.DiscardTileList.Add(BytesToList<Tile>(shortBytes, 48 + i * 48, 24));
                mainMatchControl.PlayerOpenSetList.Add(BytesToList<Group>(shortBytes, 260 + i * 56, 4));
            }
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                mainMatchControl.DiscardTileList.Add(BytesToList<Tile>(shortBytes, 48 + i * 48, 24));
                mainMatchControl.PlayerOpenSetList.Add(BytesToList<Group>(shortBytes, 260 + i * 56, 4));
            }
        }
        mainMatchControl.DoraPointerList = BytesToList<Tile>(shortBytes, 240, 5);
        mainMatchControl.UraDoraPointerList = BytesToList<Tile>(shortBytes, 250, 5);

        // 484 立直棒计数
        mainMatchControl.RiichiStickCount = shortBytes[484];

        return mainMatchControl;
    }
    #endregion
}

/// <summary>
/// 本场结束的状态
/// </summary>
public enum HonbaEndMark
{
    /// <summary>
    /// 庄家和牌,立直棒归零,连庄,本场加一
    /// </summary>
    DealerRon,
    /// <summary>
    /// 子家和牌,立直棒归零,下庄,本场归零
    /// </summary>
    NonDealerRon,
    /// <summary>
    /// 庄家流听,立直棒不变,连庄,本场加一
    /// </summary>
    DealerTenpaiDraw,
    /// <summary>
    /// 庄家流局未听,立直棒不变,下庄,本场加一
    /// </summary>
    DealerNotenDraw,
    /// <summary>
    /// 途中流局,立直棒不变,连庄,本场加一
    /// </summary>
    AbortiveDraw,
    /// <summary>
    /// 新对局,立直棒归零,指定庄开始,本场归零
    /// </summary>
    NewMatch,
}