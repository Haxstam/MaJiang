using MaJiangLib;
using System;
using System.Collections.Specialized;
/// <summary>
/// 指定当前对局设置的类,包含各类基本属性如对局类型,长考时间的设置
/// </summary>
public class MatchSettingData : IByteable<MatchSettingData>
{
    /// <summary>
    /// 默认的对局,即20+5,初始25000点,一番缚,有赤宝,有食断,无古役
    /// </summary>
    public MatchSettingData(MatchType matchType)
    {
        MatchType = matchType;
        ThinkingTime = 20;
        BaseTime = 5;
        BasePoint = 25000;
        FinishPoint = 30000;
        MinimumYakuFan = 1;
        OpenTanyao = true;
        HaveRedDora = true;
        HaveBankrupt = true;
        HaveLocalYaku = false;
    }
    /// <summary>
    /// 完全自定义
    /// </summary>
    /// <param name="matchType"></param>
    /// <param name="thinkTime"></param>
    /// <param name="baseTime"></param>
    /// <param name="basePoint"></param>
    /// <param name="minimumFan"></param>
    /// <param name="openTanyao"></param>
    /// <param name="haveRedDora"></param>
    /// <param name="haveLocalYaku"></param>
    public MatchSettingData(MatchType matchType, int thinkTime, int baseTime, int basePoint, int finishPoint, int minimumFan, bool openTanyao, bool haveRedDora, bool haveBankrupt, bool haveLocalYaku)
    {
        MatchType = matchType;
        ThinkingTime = thinkTime;
        BaseTime = baseTime;
        BasePoint = basePoint;
        FinishPoint = finishPoint;
        MinimumYakuFan = minimumFan;
        OpenTanyao = openTanyao;
        HaveRedDora = haveRedDora;
        HaveBankrupt = haveBankrupt;
        HaveLocalYaku = haveLocalYaku;
    }
    /// <summary>
    /// 对局类型
    /// </summary>
    public MatchType MatchType { get; set; }
    /// <summary>
    /// 局时,即单本场共用的长考时间,默认20s
    /// </summary>
    public int ThinkingTime { get; set; }
    /// <summary>
    /// 步时,即单步操作的临时思考时间,默认5s
    /// </summary>
    public int BaseTime { get; set; }
    /// <summary>
    /// 番缚,即除去宝牌后,和牌所需的最小番数,如二番缚下仅断幺役种不允许和牌,默认为1,即一番缚(一发也算作役种)
    /// </summary>
    public int MinimumYakuFan { get; set; }
    /// <summary>
    /// 对局初始点数,即对局开始时所有人的初始点数,默认为25000
    /// </summary>
    public int BasePoint { get; set; }
    /// <summary>
    /// 终局所需点数,若最后一本场下庄且存在至少一名玩家达到此点数,则终局,若没有玩家达到则南入/西入
    /// </summary>
    public int FinishPoint { get; set; }
    /// <summary>
    /// 能否食断,即断幺九能否在副露下成立,若为false,则断幺九门清限定,默认为true
    /// </summary>
    public bool OpenTanyao { get; set; }
    /// <summary>
    /// 是否有红五万,红五筒,红五条共三张赤宝牌,若为是,则数牌各花色序数为5的一张牌用对应红宝牌代替,默认为true
    /// </summary>
    public bool HaveRedDora { get; set; }
    /// <summary>
    /// 是否有击飞,即当某玩家结算点数后为负数时是否立刻终止对局
    /// </summary>
    public bool HaveBankrupt { get; set; }
    /// <summary>
    /// 是否包含古役,即标准役种的役种,默认为false
    /// </summary>
    public bool HaveLocalYaku { get; set; }
    public const int byteSize = 16;

    public int ByteSize
    {
        get { return byteSize; }
    }
    public byte[] GetBytes()
    {
        Span<byte> mainBytes = new byte[16];
        mainBytes[0] = (byte)MatchType;
        mainBytes[1] = (byte)ThinkingTime;
        mainBytes[2] = (byte)BaseTime;
        mainBytes[3] = (byte)MinimumYakuFan;
        mainBytes[4] = BitConverter.GetBytes(OpenTanyao)[0];
        mainBytes[5] = BitConverter.GetBytes(HaveRedDora)[0];
        mainBytes[6] = BitConverter.GetBytes(HaveBankrupt)[0];
        mainBytes[7] = BitConverter.GetBytes(HaveLocalYaku)[0];
        GlobalFunction.ReplaceBytes(mainBytes, BitConverter.GetBytes(BasePoint), 8);
        GlobalFunction.ReplaceBytes(mainBytes, BitConverter.GetBytes(FinishPoint), 12);
        return mainBytes.ToArray();
    }

    public MatchSettingData BytesTo(byte[] bytes, int index)
    {
        MatchType matchType = (MatchType)bytes[index];
        int thinkTime = bytes[index + 1];
        int baseTime = bytes[index + 2];
        int minimumYakuFan = bytes[index + 3];
        bool openTanyao = BitConverter.ToBoolean(bytes, index + 4);
        bool haveRedDora = BitConverter.ToBoolean(bytes, index + 5);
        bool haveBankrupt = BitConverter.ToBoolean(bytes,index + 6);
        bool haveLocalYaku = BitConverter.ToBoolean(bytes, index + 7);
        int basePoint = BitConverter.ToInt32(bytes, index + 8);
        int finishPoint = BitConverter.ToInt32(bytes,index + 12);
        return new(matchType, thinkTime, baseTime, basePoint, finishPoint, minimumYakuFan, openTanyao, haveRedDora, haveBankrupt, haveLocalYaku);
    }
}
