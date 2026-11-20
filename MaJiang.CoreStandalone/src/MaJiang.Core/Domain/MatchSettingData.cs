using MaJiangLib;

/// <summary>纯规则配置；计时和显示期限由宿主处理。</summary>
public sealed class MatchSettingData
{
    /// <summary>按指定场制创建四人日麻默认规则。</summary>
    public MatchSettingData(MatchType matchType) { MatchType = matchType; }
    /// <summary>四人东风或四人东南配置。</summary>
    public MatchType MatchType { get; set; }
    /// <summary>每名玩家的起始点数。</summary>
    public int BasePoint { get; set; } = 25000;
    /// <summary>正常终局的目标点数。</summary>
    public int FinishPoint { get; set; } = 30000;
    /// <summary>最低役番，公开引擎当前支持一番缚。</summary>
    public int MinimumYakuFan { get; set; } = 1;
    /// <summary>是否允许食断。</summary>
    public bool OpenTanyao { get; set; } = true;
    /// <summary>是否使用三张赤五。</summary>
    public bool HaveRedDora { get; set; } = true;
    /// <summary>是否在结算后负分时结束整场。</summary>
    public bool HaveBankrupt { get; set; } = true;
    /// <summary>是否使用古役，公开引擎当前不支持启用。</summary>
    public bool HaveLocalYaku { get; set; }
}
