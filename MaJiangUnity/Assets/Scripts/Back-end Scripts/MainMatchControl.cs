using MaJiangLib;
using System;
using System.Collections.Generic;
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

    /// <summary>
    /// 主对局控制类自东一开局的构造器,需要指定是否有红宝牌,是否有字牌
    /// </summary>
    /// <param name="matchType"></param>
    /// <param name="haveRedDora"></param>
    /// <param name="haveHonor"></param>
    public MainMatchControl(List<Player> playerList, MatchType matchType, bool haveRedDora, bool haveHonor)
    {
        MatchType = matchType;
        if (MatchType == MatchType.FourMahjongEast || MatchType == MatchType.FourMahjongSouth)
        {
            PlayerList = playerList;
            // 牌山设定:通过随机数生成牌山,取前122张作为手牌,后14张作为王牌
            List<Pai> rawPaiList = RandomCardGenerator(out int randomNumber, true, true, true);
            PrimePaiIndex = 0;
            PrimePaiList = rawPaiList.GetRange(122, 14);
            rawPaiList.RemoveRange(122, 14);
            MainPaiList = rawPaiList;
            // 各列表/数值初始化为东一 0本场的开局情况
            QiPaiList = new();
            DoraList = new();
            UraDoraList = new();
            KangCount = 0;
            KangMark = 0;
            Wind = WindType.East;
            Round = 1;
            Honba = 0;
            PlayerPoint = new();
            IsRiichi = new() { false, false, false, false };
            IsDoubleRiichi = new() { false, false, false, false };
            IsKang = new() { false, false, false, false };
            HaveIppatsu = new() { false, false, false, false };
            FirstCycleIppatsu = true;
            CurrentBankerIndex = 0;
            CurrentPlayerIndex = 0;
            CurrentStageType = StageType.StartStage;
            CurrentPaiIndex = 0;
            RemainPaiCount = 122;
        }
        else
        {

        }
    }
    /// <summary>
    /// 经由公共信息创建的当前比赛信息,用于用户端创建比赛信息实例
    /// </summary>
    /// <param name="playerInformation"></param>
    /// <param name="matchInformation"></param>
    public MainMatchControl(List<IPlayerInformation> playerInformation, IMatchInformation matchInformation)
    {
        PlayerList = new();
        foreach (IPlayerInformation player in playerInformation)
        {
            PlayerList.Add(new(player));
        }
        MatchType = matchInformation.MatchType;
        QiPaiList = matchInformation.QiPaiList;
        DoraList = matchInformation.DoraList;
        UraDoraList = matchInformation.UraDoraList;
        KangCount = matchInformation.KangCount;
        KangMark = matchInformation.KangMark;
        Wind = matchInformation.Wind;
        Round = matchInformation.Round;
        Honba = matchInformation.Honba;
        PlayerPoint = matchInformation.PlayerPoint;
        IsRiichi = matchInformation.IsRiichi;
        IsDoubleRiichi = matchInformation.IsDoubleRiichi;
        RemainPaiCount = matchInformation.RemainPaiCount;
        HaveIppatsu = matchInformation.HaveIppatsu;
        FirstCycleIppatsu = matchInformation.FirstCycleIppatsu;
        IsKang = matchInformation.IsKang;
        CurrentPlayerIndex = matchInformation.CurrentPlayerIndex;
        CurrentStageType = matchInformation.CurrentStageType;
        CurrentBankerIndex = matchInformation.CurrentBankerIndex;
        PlayerFuluList = matchInformation.PlayerFuluList;
    }

    /// <summary>
    /// 玩家列表
    /// </summary>
    public List<Player> PlayerList { get; set; }
    /// <summary>
    /// 记录当前场的类型
    /// </summary>
    public MatchType MatchType { get; set; }
    /// <summary>
    /// 弃牌堆,存储所有人的弃牌,第0所对应的玩家为东一场的亲家
    /// </summary>
    public List<List<Pai>> QiPaiList { get; set; }
    /// <summary>
    /// 当前已展示的宝牌数
    /// </summary>
    public int DoraCount { get; set; }
    /// <summary>
    /// 宝牌指示牌列表,存储所有已被展示的宝牌指示牌
    /// </summary>
    public List<Pai> DoraList { get; set; }
    /// <summary>
    /// 里宝牌列表,暂时放在这里
    /// </summary>
    public List<Pai> UraDoraList { get; set; }
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
    /// 记录是否立直,立直者对应序号的值为true
    /// </summary>
    public List<bool> IsRiichi { get; set; }
    /// <summary>
    /// 记录是否为两立直,此变量对应序号为True时,IsRiichi对应的序号也必须为True
    /// </summary>
    public List<bool> IsDoubleRiichi { get; set; }
    /// <summary>
    /// 牌山下一张牌在牌山中的位置,当摸牌时获取对应牌山位置的牌
    /// </summary>
    public int CurrentPaiIndex { get; set; }
    /// <summary>
    /// 剩余牌的数目,用于判断河底海底等
    /// </summary>
    public int RemainPaiCount { get; set; }
    /// <summary>
    /// 一发的判断,某人立直后设定其序号对应的为True,有人鸣牌或其本人再打出一张后设定为False
    /// </summary>
    public List<bool> HaveIppatsu { get; set; }
    /// <summary>
    /// 第一巡的指示,用于判断两立直天地和,开局设定为True,有人鸣牌或庄家再摸牌后设定为False
    /// </summary>
    public bool FirstCycleIppatsu { get; set; }
    /// <summary>
    /// 刚摸完岭上牌的状态,拔北或开杠后设定为True,打出牌后设定为False
    /// </summary>
    public List<bool> IsKang { get; set; }
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
    public Dictionary<int, List<Group>> PlayerFuluList { get; set; }
    /// <summary>
    /// 牌山
    /// </summary>
    public List<Pai> MainPaiList { get; set; }
    /// <summary>
    /// 王牌,恒定14张(四麻)/18张(三麻),前10张固定为宝牌/里宝牌墩,后4张/8张为岭上牌
    /// </summary>
    public List<Pai> PrimePaiList { get; set; }
    /// <summary>
    /// 下一张岭上牌的标记
    /// </summary>
    public int PrimePaiIndex { get; set; }
    /// <summary>
    /// 添加宝牌的方法,会根据当前宝牌数按照王牌添加新宝牌,最多五张
    /// </summary>
    /// <returns>是否成功添加</returns>
    public bool AddDora()
    {
        if (DoraCount < 5)
        {
            // 仅在小于5时添加
            DoraList.Add(PrimePaiList[2 * DoraCount]);
            UraDoraList.Add(PrimePaiList[2 * DoraCount + 1]);
            DoraCount++;
            return true;
        }
        else
        {
            return false;
        }
    }
    /// <summary>
    /// 杠牌时对王牌的处理,岭上相关判断,返回所摸岭上牌
    /// </summary>
    /// <param name="player">开杠的玩家编号</param>
    /// <returns>返回岭上牌</returns>
    public Pai KangSolve(int player)
    {
        if (KangCount == 4)
        {
            throw new Exception("错误:在已有4杠的情况下又进行开杠操作");
        }
        KangCount++;
        IsKang[player] = true;
        Pai endPai = MainPaiList[-1];
        PrimePaiList.Add(endPai);
        MainPaiList.RemoveAt(-1);
        PrimePaiIndex++;

        bool isAdd = AddDora();

        if (isAdd)
        {
            UnityEngine.Debug.Log("已添加宝牌");
        }
        else
        {
            UnityEngine.Debug.Log("添加宝牌失败");
        }
        return PrimePaiList[PrimePaiIndex + 9];
    }
    /// <summary>
    /// 拔北处理,除了不调用AddDora()外和KangSolve()相同
    /// </summary>
    /// <param name="player">拔北的玩家编号</param>
    /// <returns>返回岭上牌</returns>
    public Pai BaBeiSolve(int player)
    {
        IsKang[player] = true;
        Pai endPai = MainPaiList[-1];
        PrimePaiList.Add(endPai);
        MainPaiList.RemoveAt(-1);
        PrimePaiIndex++;
        return PrimePaiList[PrimePaiIndex + 9];
    }
    /// <summary>
    /// 从牌山摸牌的方法,仅当剩余牌大于0时可行
    /// </summary>
    /// <returns></returns>
    public Pai TakePai()
    {
        if (RemainPaiCount == 0)
        {
            throw new Exception("错误:尝试在剩余0张牌时从牌山摸牌");
        }
        else
        {
            Pai pai = MainPaiList[CurrentPaiIndex];
            CurrentPaiIndex++;
            return pai;
        }
    }
    /// <summary>
    /// 当处于起始阶段时的推进行为
    /// </summary>
    /// <returns></returns>
    public bool StartStageAction()
    {
        Pai singlePai = TakePai();
        Player currentPlayer = PlayerList[CurrentPlayerIndex];
        currentPlayer.ShouPai.SinglePai = singlePai;

        return true;
    }
    /// <summary>
    /// 舍牌阶段推进行为,获取玩家可进行的操作并发送至玩家
    /// </summary>
    /// <returns></returns>
    public bool DiscardStageAction()
    {
        Player currentPlayer = PlayerList[CurrentPlayerIndex];
        // 同步设定玩家和本局当前环节为舍牌环节
        CurrentStageType = StageType.DiscardStage;
        currentPlayer.CurrentStageType = StageType.DiscardStage;
        Dictionary<PlayerAction, List<PlayerActionData>> playerActionDict = SelfAvailableJudge(currentPlayer.ShouPai, currentPlayer.ShouPai.SinglePai, this);

        return false;
    }
    public bool ClaimStageAction()
    {
        return false;
    }

    /// <summary>
    /// 对于比赛信息中公共信息的序列化,返回不包含用户名的864Bytes字节串
    /// </summary>
    /// <returns></returns>
    public byte[] GetPublicBytes()
    {
        // 对局信息,使用 864 bytes 空间,不包含牌山,也即仅传输用户端会用到的信息
        // 前16位,为各种短变量
        byte[] MainBytes = new byte[864];
        MainBytes[0] = (byte)MatchType;
        MainBytes[1] = (byte)Wind;
        MainBytes[2] = (byte)Round;
        MainBytes[3] = (byte)Honba;
        MainBytes[4] = (byte)KangCount;
        MainBytes[5] = (byte)KangMark;
        MainBytes[6] = (byte)CurrentPlayerIndex;
        MainBytes[7] = (byte)CurrentStageType;
        MainBytes[8] = (byte)CurrentBankerIndex;
        MainBytes[9] = (byte)RemainPaiCount;
        MainBytes[10] = BitConverter.GetBytes(FirstCycleIppatsu)[0];
        // 标记列表 16~47
        ReplaceBytes(MainBytes, ListToBytes(PlayerPoint), 16);  // +16
        ReplaceBytes(MainBytes, ListToBytes(IsRiichi), 32); // +4
        ReplaceBytes(MainBytes, ListToBytes(IsDoubleRiichi), 36); // +4
        ReplaceBytes(MainBytes, ListToBytes(HaveIppatsu), 40); // +4
        ReplaceBytes(MainBytes, ListToBytes(IsKang), 44); // +4
        // 弃牌堆 48~431
        // 弃牌堆目前设定单个玩家的弃牌堆大小为24张牌,弃牌堆总大小96张牌,也即 384 bytes,单个玩家的弃牌堆为 96 bytes
        // 理论来讲,四人麻将庄家如果单局被吃碰鸣牌12次(三个子家各4次)需要24次过牌(四家总共),剩余48张牌庄家最多摸到12张,也即最多24张过牌
        // 实际弃牌大于20张就足够罕见
        for (int i = 0; i < QiPaiList.Count; i++)
        {
            ReplaceBytes(MainBytes, ListToBytes(QiPaiList[i]), 48 + i * 96);
        }
        // 宝牌 432~471
        // 表里宝牌共10张 40 bytes
        ReplaceBytes(MainBytes, ListToBytes(DoraList), 432);
        ReplaceBytes(MainBytes, ListToBytes(UraDoraList), 452);
        // 副露列表 472~855
        // 存储四家共16个Group,也即 384 bytes,单个玩家的副露列表为 96 bytes
        for (int i = 0; i < PlayerFuluList.Count; i++)
        {
            ReplaceBytes(MainBytes, ListToBytes(PlayerFuluList[i]), 472 + i * 96);
        }
        // 目前856~863 留空
        return new byte[864];
    }

    //public static implicit operator byte[](MainMatchControl mainMatchControl)
    //{
    //    // 对局信息,使用 864 bytes 空间,不包含牌山,也即仅传输用户端会用到的信息
    //    // 前16位,为各种短变量
    //    byte[] MainBytes = new byte[864];
    //    MainBytes[0] = (byte)mainMatchControl.MatchType;
    //    MainBytes[1] = (byte)mainMatchControl.Wind;
    //    MainBytes[2] = (byte)mainMatchControl.Round;
    //    MainBytes[3] = (byte)mainMatchControl.Honba;
    //    MainBytes[4] = (byte)mainMatchControl.KangCount;
    //    MainBytes[5] = (byte)mainMatchControl.KangMark;
    //    MainBytes[6] = (byte)mainMatchControl.CurrentPlayerIndex;
    //    MainBytes[7] = (byte)mainMatchControl.CurrentStageType;
    //    MainBytes[8] = (byte)mainMatchControl.CurrentBankerIndex;
    //    MainBytes[9] = (byte)mainMatchControl.RemainPaiCount;
    //    MainBytes[10] = BitConverter.GetBytes(mainMatchControl.FirstCycleIppatsu)[0];
    //    // 标记列表 16~47
    //    ReplaceBytes(MainBytes, ListToBytes(mainMatchControl.PlayerPoint), 16);  // +16
    //    ReplaceBytes(MainBytes, ListToBytes(mainMatchControl.IsRiichi), 32); // +4
    //    ReplaceBytes(MainBytes, ListToBytes(mainMatchControl.IsDoubleRiichi), 36); // +4
    //    ReplaceBytes(MainBytes, ListToBytes(mainMatchControl.HaveIppatsu), 40); // +4
    //    ReplaceBytes(MainBytes, ListToBytes(mainMatchControl.IsKang), 44); // +4
    //    // 弃牌堆 48~431
    //    // 弃牌堆目前设定单个玩家的弃牌堆大小为24张牌,弃牌堆总大小96张牌,也即 384 bytes,单个玩家的弃牌堆为 96 bytes
    //    // 理论来讲,四人麻将庄家如果单局被吃碰鸣牌12次(三个子家各4次)需要24次过牌(四家总共),剩余48张牌庄家最多摸到12张,也即最多24张过牌
    //    // 实际弃牌大于20张就足够罕见
    //    for (int i = 0; i < mainMatchControl.QiPaiList.Count; i++)
    //    {
    //        ReplaceBytes(MainBytes, ListToBytes(mainMatchControl.QiPaiList[i]), 48 + i * 96);
    //    }
    //    // 宝牌 432~471
    //    // 表里宝牌共10张 40 bytes
    //    ReplaceBytes(MainBytes, ListToBytes(mainMatchControl.DoraList), 432);
    //    ReplaceBytes(MainBytes, ListToBytes(mainMatchControl.UraDoraList), 452);
    //    // 副露列表 472~855
    //    // 存储四家共16个Group,也即 384 bytes,单个玩家的副露列表为 96 bytes
    //    for (int i = 0; i < mainMatchControl.PlayerFuluList.Count; i++)
    //    {
    //        ReplaceBytes(MainBytes, ListToBytes(mainMatchControl.PlayerFuluList[i]), 472 + i * 96);
    //    }
    //    // 目前856~863 留空
    //    return new byte[864];
    //}
}
