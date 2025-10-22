using MaJiangLib;
/// <summary>
/// 指定当前对局设置的类,包含各类基本属性如对局类型,长考时间的设置
/// </summary>
public class MatchSettingData
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
        MinimumYakuFan = 1;
        OpenTanyao = true;
        HaveRedDora = true;
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
    public MatchSettingData(MatchType matchType, int thinkTime, int baseTime, int basePoint, int minimumFan, bool openTanyao, bool haveRedDora, bool haveLocalYaku)
    {
        MatchType = matchType;
        ThinkingTime = thinkTime;
        BaseTime = baseTime;
        BasePoint = basePoint;
        MinimumYakuFan = minimumFan;
        OpenTanyao = openTanyao;
        HaveRedDora = haveRedDora;
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
    /// 能否食断,即断幺九能否在副露下成立,若为false,则断幺九门清限定,默认为true
    /// </summary>
    public bool OpenTanyao { get; set; }
    /// <summary>
    /// 是否有红五万,红五筒,红五条共三张赤宝牌,若为是,则数牌各花色序数为5的一张牌用对应红宝牌代替,默认为true
    /// </summary>
    public bool HaveRedDora { get; set; }
    /// <summary>
    /// 是否包含古役,即标准役种的役种,默认为false
    /// </summary>
    public bool HaveLocalYaku { get; set; }
}
