using MaJiangLib;
using System;
using System.Collections.Generic;
using System.Linq;
using static MaJiangLib.GlobalFunction;

/// <summary>完整牌局状态及发牌/取牌原语。操作由AuthoritativeMatchMachine裁定，结算换局由MatchSession管理。</summary>
public class MainMatchControl : IMatchInformation
{
    #region *牌局信息，客户端仅通过MatchJournal获取自己的可见投影*
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

    // 0至9为五对表/里指示牌，10至13为四张岭上，之后追加的海底保留牌不再摸取。
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
    /// <summary>隐藏配牌策略不复制进场况，也不交给玩家或模型。</summary>
    internal DemonCurseController CurseController { get; private set; }
    #endregion

    /// <summary>
    /// 四麻对局初始化：使用已分配座位的玩家初始化点数和首局，仅供服务端调用
    /// </summary>
    /// <param name="matchSettingData">对局设置。</param>
    /// <param name="playersBySeat">已由房间分配座位的四名玩家，列表下标即座位编号。</param>
    /// <param name="random">可选的随机数源，便于复现牌山和执行规则测试。</param>
    public void ServerFourMatchInit(MatchSettingData matchSettingData, IReadOnlyList<UserProfile> playersBySeat, Xoshiro256StarStar random = null, DemonCurseOptions curses = null)
    {
        if (matchSettingData == null)
        {
            throw new ArgumentNullException(nameof(matchSettingData));
        }
        if (matchSettingData.MatchType != MatchType.FourMahjongEast && matchSettingData.MatchType != MatchType.FourMahjongSouth)
        {
            throw new ArgumentException("此入口仅支持四人麻将。", nameof(matchSettingData));
        }
        if (playersBySeat == null || playersBySeat.Count != 4 || playersBySeat.Any(profile => profile is null))
        {
            throw new ArgumentException("必须提供按座位排列的四名玩家。", nameof(playersBySeat));
        }

        Random = random ?? new Xoshiro256StarStar();
        CurseController = curses == null ? null : new DemonCurseController(curses);
        IsServer = true;
        PlayerList = new() { null, null, null, null };
        MatchSettingData = matchSettingData;
        MatchType = matchSettingData.MatchType;
        Wind = WindType.East;
        Round = 1;
        Honba = 0;
        CurrentDealerIndex = 0;
        CurrentPlayerIndex = CurrentDealerIndex;
        RiichiStickCount = 0;
        PlayerPoint = Enumerable.Repeat(MatchSettingData.BasePoint, 4).ToList();
        for (int playerNumber = 0; playerNumber < 4; playerNumber++)
        {
            PlayerList[playerNumber] = new(new UserProfile(playersBySeat[playerNumber].Name), playerNumber, new(playerNumber), StageType.InitStage);
        }
        ServerFourHonbaInit();
    }
    /// <summary>
    /// 四麻新本场通用初始化,仅供服务端调用
    /// </summary>
    public void ServerFourHonbaInit()
    {
        Random ??= new Xoshiro256StarStar();
        // 牌山设定:通过随机数生成牌山,取前122张作为手牌,后14张作为王牌
        List<Tile> rawTileList = RandomCardGenerator(out int randomNumber, true, MatchSettingData.HaveRedDora, true, Random);
        PrimeTileIndex = 0;
        ResetRoundState();
        List<List<Tile>> hands = null;
        if (CurseController == null)
        {
            PrimeTileList = rawTileList.GetRange(122, 14);
            rawTileList.RemoveRange(122, 14);
            MainTileList = rawTileList;
        }
        else
        {
            hands = CurseController.Deal(rawTileList, Random, out List<Tile> live, out List<Tile> dead);
            MainTileList = live; PrimeTileList = dead;
        }
        for (int j = 0; j < 4; j++)
        {
            PlayerList[j].PlayerHandTile = new HandTile(j);
            PlayerList[j].PlayerHandTile.OpenTileList = PlayerOpenSetList[j];
            // 从牌山的第0处开始,依次取13张牌作为玩家手牌并将其从牌山中移除
            PlayerList[j].PlayerHandTile.HandTileList = hands == null ? rawTileList.GetRange(0, 13) : hands[j];
            if (hands == null) { rawTileList.RemoveRange(0, 13); }
        }
        RemainTileCount = MainTileList.Count;
        Initialized = true;
    }
    /// <summary>
    /// 消耗一张正常或岭上牌并唯一扣减余牌数；调用者只负责将返回牌放入玩家手牌。
    /// </summary>
    /// <param name="isRinshan"></param>
    /// <returns></returns>
    public Tile GetNextTile(bool isRinshan)
    {
        if (MainTileList == null || RemainTileCount <= 0 || CurrentTileIndex >= MainTileList.Count)
        {
            throw new InvalidOperationException("没有剩余可摸牌。");
        }
        Tile nextTile;
        if (isRinshan)
        {
            if (PrimeTileIndex >= 4)
            {
                throw new InvalidOperationException("四张岭上牌已经用尽。");
            }
            CurseController?.BeforeDraw(this, true);
            // 前十张为表里宝牌指示牌，后四张为岭上牌；每次岭上摸牌将海底补入王牌。
            nextTile = PrimeTileList[10 + PrimeTileIndex];
            PrimeTileIndex++;
            PrimeTileList.Add(MainTileList[MainTileList.Count - 1]);
            MainTileList.RemoveAt(MainTileList.Count - 1);
        }
        else
        {
            CurseController?.BeforeDraw(this, false);
            nextTile = MainTileList[CurrentTileIndex];
            CurrentTileIndex++;
        }
        RemainTileCount--;
        return nextTile;
    }

    /// <summary>实际揭示下一张表指示牌，先完成隐藏配牌约束，再固定该位置。</summary>
    public Tile RevealDoraIndicator()
    {
        int index = DoraPointerList.Count * 2;
        if (index >= 10) { throw new InvalidOperationException("Too many dora indicators."); }
        CurseController?.BeforeIndicator(this, index, false);
        Tile indicator = PrimeTileList[index]; DoraPointerList.Add(indicator); return indicator;
    }

    /// <summary>仅在真实和牌结算翻里牌前调用；纯计分/结算查询不会触发调牌。</summary>
    internal void PrepareUraIndicators(byte winners, Tile winningTile)
    {
        if (CurseController == null) { return; }
        for (int index = 0; index < DoraPointerList.Count; index++)
        {
            CurseController.BeforeIndicator(this, index * 2 + 1, true, winners, winningTile);
        }
    }
    /// <summary>
    /// 本场发牌前清空公共牌区和操作标记，窗口与私有裁定由MatchJournal重置。
    /// </summary>
    private void ResetRoundState()
    {
        DiscardTileList = new() { new(), new(), new(), new() };
        DoraPointerList = new();
        IsRiichi = new() { false, false, false, false };
        IsDoubleRiichi = new() { false, false, false, false };
        IsKan = new() { false, false, false, false };
        HaveIppatsu = new() { false, false, false, false };
        PlayerOpenSetList = new() { new(), new(), new(), new() };
        CurrentTile = null;
        FirstCycleIppatsu = true;
        CurrentStageType = StageType.StartStage;
        CurrentTileIndex = 0;
        RemainTileCount = 122;
        KangCount = 0;
        KangMark = 6;
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
}
