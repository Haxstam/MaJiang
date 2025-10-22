using MaJiangLib;
/// <summary>
/// ָ����ǰ�Ծ����õ���,�����������������Ծ�����,����ʱ�������
/// </summary>
public class MatchSettingData
{
    /// <summary>
    /// Ĭ�ϵĶԾ�,��20+5,��ʼ25000��,һ����,�г౦,��ʳ��,�޹���
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
    /// ��ȫ�Զ���
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
    /// �Ծ�����
    /// </summary>
    public MatchType MatchType { get; set; }
    /// <summary>
    /// ��ʱ,�����������õĳ���ʱ��,Ĭ��20s
    /// </summary>
    public int ThinkingTime { get; set; }
    /// <summary>
    /// ��ʱ,��������������ʱ˼��ʱ��,Ĭ��5s
    /// </summary>
    public int BaseTime { get; set; }
    /// <summary>
    /// ����,����ȥ���ƺ�,�����������С����,��������½��������ֲ��������,Ĭ��Ϊ1,��һ����(һ��Ҳ��������)
    /// </summary>
    public int MinimumYakuFan { get; set; }
    /// <summary>
    /// �Ծֳ�ʼ����,���Ծֿ�ʼʱ�����˵ĳ�ʼ����,Ĭ��Ϊ25000
    /// </summary>
    public int BasePoint { get; set; }
    /// <summary>
    /// �ܷ�ʳ��,�����۾��ܷ��ڸ�¶�³���,��Ϊfalse,����۾������޶�,Ĭ��Ϊtrue
    /// </summary>
    public bool OpenTanyao { get; set; }
    /// <summary>
    /// �Ƿ��к�����,����Ͳ,�����������ų౦��,��Ϊ��,�����Ƹ���ɫ����Ϊ5��һ�����ö�Ӧ�챦�ƴ���,Ĭ��Ϊtrue
    /// </summary>
    public bool HaveRedDora { get; set; }
    /// <summary>
    /// �Ƿ��������,����׼���ֵ�����,Ĭ��Ϊfalse
    /// </summary>
    public bool HaveLocalYaku { get; set; }
}
