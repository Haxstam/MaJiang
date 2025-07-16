using MaJiangLib;

namespace MaJIangNet
{
    /// <summary>
    /// 对局信息的类,实现IMatchInformation接口
    /// </summary>
    internal class MatchInformation : IMatchInformation
    {
        /// <summary>
        /// 测试用构造器,即无宝牌,东一 一本场 无立直 剩余0牌 无一发
        /// </summary>
        public MatchInformation()
        {
            QiPaiList = new();
            DoraList = new();
            UraDoraList = new();
            Wind = WindType.East;
            Round = 1;
            Honba = 1;
            PlayerPoint = new();
            IsRiichi = new() { false, false, false, false };
            IsDoubleRiichi = new() { false, false, false, false };
            RemainPaiCount = 0;
            HaveIppatsu = new() { false, false, false, false };
            FirstCycleIppatsu = false;
            IsKan = new() { false, false, false, false };
            CurrentBanker = 1;
            CurrentPlayer = 1;
            PlayerFuluList = new();
        }
        /// <summary>
        /// 暂不定义副露牌列表
        /// </summary>
        /// <param name="matchType"></param>
        /// <param name="qiPaiList"></param>
        /// <param name="doraList"></param>
        /// <param name="uraDoraList"></param>
        /// <param name="kangCount"></param>
        /// <param name="wind"></param>
        /// <param name="round"></param>
        /// <param name="honba"></param>
        /// <param name="playerPoint"></param>
        /// <param name="isRiichi"></param>
        /// <param name="isDoubleRiichi"></param>
        /// <param name="remainPaiCount"></param>
        /// <param name="haveIppatsu"></param>
        /// <param name="firstCycleIppatsu"></param>
        /// <param name="isKan"></param>
        /// <param name="currentPlayer"></param>
        /// <param name="currentBanker"></param>
        public MatchInformation(MaJiangLib.MatchType matchType,List<List<Pai>> qiPaiList, List<Pai> doraList, List<Pai> uraDoraList, int kangCount, WindType wind, int round, int honba, List<int> playerPoint, List<bool> isRiichi, List<bool> isDoubleRiichi, int remainPaiCount, List<bool> haveIppatsu, bool firstCycleIppatsu, List<bool> isKan, int currentPlayer, int currentBanker)
        {
            MatchType = matchType;
            QiPaiList = qiPaiList;
            DoraList = doraList;
            UraDoraList = uraDoraList;
            KangCount = kangCount;
            Wind = wind;
            Round = round;
            Honba = honba;
            PlayerPoint = playerPoint;
            IsRiichi = isRiichi;
            IsDoubleRiichi = isDoubleRiichi;
            RemainPaiCount = remainPaiCount;
            HaveIppatsu = haveIppatsu;
            FirstCycleIppatsu = firstCycleIppatsu;
            IsKan = isKan;
            CurrentPlayer = currentPlayer;
            CurrentBanker = currentBanker;
            PlayerFuluList = new();  // [TODO]
        }
        // MatchType存在二义性:MaJiang.MatchType和System.IO.MatchType

        /// <summary>
        /// 记录当前场的类型
        /// </summary>
        public MaJiangLib.MatchType MatchType { get; set; }
        /// <summary>
        /// 弃牌堆,存储所有人的弃牌,第0所对应的玩家为东一场的亲家
        /// </summary>
        public List<List<Pai>> QiPaiList { get; set; }
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
        public List<bool> IsKan { get; set; }
        /// <summary>
        /// 当前等待操作(打出一张牌)的玩家
        /// </summary>
        public int CurrentPlayer { get; set; }
        /// <summary>
        /// 当前局的庄家序号
        /// </summary>
        public int CurrentBanker { get; set; }
        /// <summary>
        /// 副露牌列表
        /// </summary>
        public Dictionary<int, List<Group>> PlayerFuluList { get; set; }
    }
}
