using MaJiangLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using UnityEngine;
using static MaJiangLib.GlobalFunction;
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
    /// 当前已展示的宝牌数
    /// </summary>
    public int DoraCount { get; set; }
    /// <summary>
    /// 宝牌指示牌列表,存储所有已被展示的宝牌指示牌
    /// </summary>
    public List<Tile> DoraList { get; set; }
    /// <summary>
    /// 里宝牌列表,暂时放在这里
    /// </summary>
    public List<Tile> UraDoraList { get; set; }
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
    /// TODO 和PlayerList.Point重复存储了,考虑修改
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
    public int CurrentBankerIndex { get; set; }
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
    public List<bool> IsKang { get; set; }
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
        MainBytes[8] = (byte)CurrentBankerIndex;
        MainBytes[9] = (byte)RemainTileCount;
        MainBytes[10] = BitConverter.GetBytes(FirstCycleIppatsu)[0];
        // 标记列表 16~47
        ReplaceBytes(MainBytes, ListToBytes(PlayerPoint), 16);  // +16
        ReplaceBytes(MainBytes, ListToBytes(IsRiichi), 32); // +4
        ReplaceBytes(MainBytes, ListToBytes(IsDoubleRiichi), 36); // +4
        ReplaceBytes(MainBytes, ListToBytes(HaveIppatsu), 40); // +4
        ReplaceBytes(MainBytes, ListToBytes(IsKang), 44); // +4
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
        ReplaceBytes(MainBytes, ListToBytes(DoraList), 240);
        ReplaceBytes(MainBytes, ListToBytes(UraDoraList), 250);
        // 副露列表 260~483
        // 存储四家共16个Group,也即 224 bytes,单个玩家的副露列表为 56 bytes
        for (int i = 0; i < PlayerOpenSetList.Count; i++)
        {
            ReplaceBytes(MainBytes, ListToBytes(PlayerOpenSetList[i]), 260 + i * 56);
        }
        // 484 立直棒计数
        MainBytes[484] = (byte)RiichiStickCount;

        // 目前484~511 留空
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
        mainMatchControl.CurrentBankerIndex = shortBytes[8];
        mainMatchControl.RemainTileCount = shortBytes[9];
        mainMatchControl.FirstCycleIppatsu = BitConverter.ToBoolean(shortBytes, 10);
        // 16-47
        mainMatchControl.PlayerPoint = BytesToIntList(shortBytes, 16, 4);
        mainMatchControl.IsRiichi = BytesToBoolList(shortBytes, 32, 4);
        mainMatchControl.IsDoubleRiichi = BytesToBoolList(shortBytes, 36, 4);
        mainMatchControl.HaveIppatsu = BytesToBoolList(shortBytes, 40, 4);
        mainMatchControl.IsKang = BytesToBoolList(shortBytes, 44, 4);
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
        mainMatchControl.DoraList = BytesToList<Tile>(shortBytes, 240, 5);
        mainMatchControl.UraDoraList = BytesToList<Tile>(shortBytes, 250, 5);

        // 484 立直棒计数
        mainMatchControl.RiichiStickCount = shortBytes[484];
        
        return mainMatchControl;
    }
    #endregion
}
