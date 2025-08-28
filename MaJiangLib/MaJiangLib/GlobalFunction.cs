using System;
using System.Collections.Generic;
using System.Linq;

namespace MaJiangLib
{
    /*
     * [TODO]
     * 1. 命名问题,修改变量名和方法名以切合相关术语
     * 2. 效率问题,优化听牌判断和番数的判断
     * 3. 限制域问题,修改修饰符减少成员暴露
     * 4. 变量类型问题,尽可能减少泛型List的使用
     * 
     * 5. 因为Group类也用于算法,考虑分离以合并Group内的成员 [目前已分离为GlobalFunction.GFSGroup 和 Group]
     * 
     * 6. 国士无双不好判断,目前如果为国士无双十三面,则和牌所对应牌为z8(字牌的第八种),从而使得听牌判断方便返回
     * 7. 对于多重牌型,比如三个相邻的刻子既可以当作三个顺子也可以当作三个刻子,目前在听牌判断中通过分别优先考虑顺子/刻子来满足所有可能
     * 8. 介于红宝牌的特殊性,需要在所有鸣牌操作中对其进行区分
     * 
     * 9. 鸣牌考虑禁止食替,和牌/立直的判断放在一个方法内,吃/碰/明杠作为副露鸣牌放在一个方法内,加杠/暗杠/拔北作为自身行为放在一个方法里
     * 10.目前将门清/副露听牌的判断定义在CutTingPaiJudge()中,调用时会根据当前手牌返回切哪张牌可以进入听牌的阶段,对于是否能立直等情况在外层判断
     * 11.流满判断考虑在matchInformation中记录,当某家被鸣牌时设定其为false,从而避免流局时判定
     * 
     * 12.目前暂定单个玩家切牌时分三个阶段->舍牌阶段,响应阶段和结束阶段
     *    玩家摸牌或响应别家牌时进入舍牌阶段,可加杠/拔北/暗杠/立直/自摸/九种九牌流局
     *    打出牌/开杠/拔北后进入响应阶段,可被其他家吃碰杠荣和
     *    如果拔北/开杠后响应阶段无人响应,再进入舍牌阶段并添加岭上标记
     *    如果打出牌后响应阶段无人响应,进入结束阶段,判断流局
     *    
     * 13.流局优先级:
     *    四杠散了>四家立直>四风连打,以上途中流局均优先于荒牌流局
     *    第一巡四家均立直且宣言牌都为同一种风牌,北家立直并支付1000点后按四家立直流局
     *    牌山剩余最后一张,当前已有两人及以上开三杠,玩家开四杠后舍牌时无人荣和,按四杠散了流局,不计算罚符
     */

    /// <summary>
    /// 玩家切牌时的阶段类型,分为舍牌阶段,响应阶段和结束阶段
    /// </summary>
    public enum StageType
    {
        /// <summary>
        /// 玩家摸牌或响应别家牌时进入舍牌阶段,可加杠/拔北/暗杠/立直/自摸/九种九牌流局
        /// </summary>
        DiscardStage,
        /// <summary>
        /// 打出牌/开杠/拔北后进入响应阶段,可被其他家吃碰杠荣和
        /// </summary>
        ClaimStage,
        /// <summary>
        /// 如果拔北/开杠后响应阶段无人响应,再进入舍牌阶段并添加岭上标记;如果打出牌后响应阶段无人响应,进入结束阶段,判断流局
        /// </summary>
        EndStage,
    }
    /// <summary>
    /// 流局类型,包含五种-荒牌流局,九种九牌,四杠散了,四家立直,四风连打
    /// </summary>
    public enum DrawType
    {
        /// <summary>
        /// 荒牌流局:当剩余牌为0时最后一位玩家打出牌后无人和牌时触发,需要判断听牌罚符和流局满贯
        /// </summary>
        DeadWallDraw,
        /// <summary>
        /// 九种九牌:第一巡无人鸣牌情况下,有玩家手牌包含九种及以上的幺九牌且选择流局时触发
        /// </summary>
        NineTerminalsDraw,
        /// <summary>
        /// 四杠散了:当开杠人数大于1且开第4杠的玩家打出牌后无人和牌时触发,单个玩家开4杠不会触发
        /// </summary>
        FourKansDraw,
        /// <summary>
        /// 四家立直:当四家都宣布立直且最后立直玩家的立直宣言牌无人荣和时触发,三麻无此规则
        /// </summary>
        FourRiichiDraw,
        /// <summary>
        /// 四风连打:第一巡无人鸣牌时,庄家和所有子家都打出同一种风牌且北家打出后无人和牌时触发,三麻无此规则
        /// </summary>
        FourWindsDraw,
    }
    /// <summary>
    /// 比赛类型,标记本场比赛是三麻还是四麻,是东场还是半庄
    /// </summary>
    public enum MatchType
    {
        ThreeMahjongEast,
        ThreeMahjongSouth,
        FourMahjongEast,
        FourMahjongSouth,
    }
    /// <summary>
    /// 风场类型,东风设定枚举为1是为了切合牌的序号
    /// </summary>
    public enum WindType
    {
        East = 1,
        South,
        West,
        North,
    }
    /// <summary>
    /// 牌面类型,包含所有面子的类型
    /// </summary>
    public enum GroupType
    {
        Straight,
        Triple,
        MingTriple,
        MingStraight,
        MingKang,
        AnKang,
        JiaKang,
        Pair,
    }
    /// <summary>
    /// 副露类型,因为杠的属性不同,分开看待
    /// </summary>
    public enum FuluType
    {
        Chi,
        Peng,
        AnGang,
        MingGang,
        JiaGang,
    }
    /// <summary>
    /// 玩家所能进行的操作,包含吃,碰,杠,立直,拔北,和牌(包含自摸)
    /// </summary>
    public enum PlayerAction
    {
        Chi,
        Peng,
        Gang,
        Riichi,
        BaBei,
        He,
    }
    /// <summary>
    /// 立直麻将所有类型的役种,括号内数字为副露时番数
    /// </summary>
    public enum YakuType
    {
        Empty,  // 占位符
        Dora,  // 宝牌
        AkaDora, // 红宝牌
        UraDora,  // 里宝牌
        NorthAkaDora,  // 拔北宝牌
        Riichi,  // 立直-1
        Ippatsu,  // 一发-1
        Tsumo,  // 门前清自摸和-1
        Pinfu,  // 平和-1
        Tanyao,  // 断幺-1
        Iipeikou,  // 一杯口-1
        Jikaze,  // 自风牌-1
        Bakaze,  // 场风牌-1
        Haku,  // 役牌 白-1
        Hatsu,  // 役牌 发-1
        Chun,  // 役牌 中-1
        Rinshankaiho,  // 岭上开花-1
        HaiteiRaoyui,  // 海底捞月-1
        HoteiRaoyui,  // 河底捞鱼-1
        Chankan,  // 枪杠-1
        Ittsu,  // 一气通贯-2(1)
        Toitoi,  // 对对和-2
        SanshokuDoujun,  // 三色同顺-2(1)
        SanshokuDoukou,  // 三色同刻-2
        Chanta,  // 混全带幺九-2(1)
        Chiitoitsu,  // 七对子-2
        Honroutou,  // 混老头-2
        Sananko,  // 三暗刻-2
        Sankantsu,  // 三杠子-2
        ShoSanGen,  // 小三元-2
        DoubleRiichi,  // 两立直-2
        Honitsu,  // 混一色-3(2)
        Junchan,  // 纯全带幺九-3(2)
        Ryanpeiko,  // 两杯口-3
        Chinitsu,  // 清一色-6(5)
        Suuankou,  // 四暗刻-13
        KokushiMusou,  // 国士无双-13
        DaiSanGen,  // 大三元-13
        ShoSuuShii,  // 小四喜-13
        Tsuuiiso,  // 字一色-13
        Ryuiishoku,  // 绿一色-13
        Chinroto,  // 清老头-13
        CHurenPoto,  // 九莲宝灯-13
        Suukantsu,  // 四杠子-13
        Tenho,  // 天和-13
        Chiiho,  // 地和-13
        DaiSuuShii,  // 大四喜-26
        SuuankouTanki,  // 四暗刻单骑-26
        ChuurenPoutou,  // 纯正九莲宝灯-26
        KokushiMusouThirteenOrphans,  // 国士无双十三面-26
        NagashiMangan,  // 流局满贯-5
    }
    /// <summary>
    /// 公用的方法类
    /// </summary>
    public static class GlobalFunction
    {
        // 暂时用一个列表去存储国士无双的牌来判断
        public static List<Pai> kokuShiList = new()
            {
                new(Color.Wans,1),
                new(Color.Wans,9),
                new(Color.Tungs,1),
                new(Color.Tungs,9),
                new(Color.Bamboo,1),
                new(Color.Bamboo,9),
                new(Color.Honor,1),
                new(Color.Honor,2),
                new(Color.Honor,3),
                new(Color.Honor,4),
                new(Color.Honor,5),
                new(Color.Honor,6),
                new(Color.Honor,7),
            };
        /// <summary>
        /// 鸣牌数据,存储可进行吃碰杠时,对应的牌的列表和所要鸣的牌,对应的操作
        /// </summary>
        public class MingPaiData
        {
            /// <summary>
            /// 需要手牌中能响应的牌,目标牌和进行的操作
            /// </summary>
            /// <param name="pais">手牌中所能鸣牌的牌</param>
            /// <param name="targetPai">能被鸣牌响应的别家牌</param>
            /// <param name="playerAction">所进行的操作</param>
            public MingPaiData(List<Pai> pais, List<Pai> targetPai, PlayerAction playerAction)
            {
                Pais = pais;
                TargetPai = targetPai;
                PlayerAction = playerAction;
            }
            /// <summary>
            /// 存储在手牌中的,能和TargetPai中的牌组成顺刻杠的牌
            /// </summary>
            public List<Pai> Pais { get; set; }
            /// <summary>
            /// 目标牌,即能够被吃碰杠响应的牌
            /// </summary>
            public List<Pai> TargetPai { get; set; }
            /// <summary>
            /// 所对应的操作
            /// </summary>
            public PlayerAction PlayerAction { get; set; }
        }
        /// <summary>
        /// 玩家在自己切牌时的非副露行为,目前仅包含加杠/暗杠/拔北
        /// </summary>
        public class SelfActionData
        {
            public SelfActionData(List<Pai> pais, PlayerAction playerAction)
            {
                Pais = pais;
                PlayerAction = playerAction;
            }
            public List<Pai> Pais { get; set; }
            public PlayerAction PlayerAction { get; set; }
        }
        /// <summary>
        /// 流局判断,判断荒牌流局,四风连打,四杠散了,四家立直,九种九牌属于玩家的主动操作,不在此处判断
        /// </summary>
        /// <param name="drawType">流局类型</param>
        /// <param name="matchInformation">比赛信息</param>
        /// <returns>返回是否流局</returns>
        public static bool DrawJudge(out DrawType? drawType, IMatchInformation matchInformation)
        {
            if (matchInformation.CurrentStageType == StageType.EndStage)
            {   // 以上流局判断都必须在结束阶段内进行
                List<List<Pai>> qiPaiList = matchInformation.QiPaiList;

                if (matchInformation.KangCount == 4 && matchInformation.KangMark == -1)
                {   // 存在两名及以上玩家开四杠,按四杠散了流局,为最优先判断
                    drawType = DrawType.FourKansDraw;
                    return true;
                }
                else if (!matchInformation.IsRiichi.Exists(player => player = false))
                {   // 结束阶段时有四家均立直,按四家立直流局,优先于四风连打
                    drawType = DrawType.FourRiichiDraw;
                    return true;
                }
                else if (
                    matchInformation.FirstCycleIppatsu &&
                    matchInformation.MatchType == MatchType.FourMahjongEast || matchInformation.MatchType == MatchType.FourMahjongSouth &&
                    matchInformation.CurrentPlayer == 3
                    ) // 无人鸣牌,四人麻将,是北家的结束阶段,判断四风连打
                {
                    if (
                        qiPaiList[0][0].Color == Color.Honor && qiPaiList[0][0].Number >= 1 && qiPaiList[0][0].Number <= 4 &&
                        qiPaiList[1][0] == qiPaiList[0][0] &&
                        qiPaiList[2][0] == qiPaiList[0][0] &&
                        qiPaiList[3][0] == qiPaiList[0][0]
                        ) // 庄家第一张舍牌为风牌,且子家的第一张舍牌与其相同,分开以避免在第一巡访问到不存在的子家舍牌
                    {
                        drawType = DrawType.FourWindsDraw;
                        return true;
                    }
                }
                else if (matchInformation.RemainPaiCount == 0) // 进入结束阶段且剩余牌为0,为荒牌流局,优先级最低
                {
                    drawType = DrawType.DeadWallDraw;
                    return true;
                }
            }
            // 不符合任何一种流局情况,返回null
            drawType = null;
            return false;
        }
        /// <summary>
        /// 用于判断其他玩家打出牌时,当前玩家所能进行的操作,需要当前手牌信息,每当玩家切换自己的手牌时更新
        /// </summary>
        /// <param name="shouPai"></param>
        /// <returns>返回一个字典,以操作为键,可进行的操作(MingPaiData)的list为值</returns>
        public static Dictionary<PlayerAction, List<MingPaiData>> ClaimingAvailableJudge(ShouPai shouPai, IMatchInformation matchInformation)
        {   // 返回结果是字典-列表-列表的嵌套,考虑优化
            Dictionary<PlayerAction, List<MingPaiData>> playerActions = new()
            {   // 鸣牌操作仅限吃碰杠
                { PlayerAction.Chi, new()},
                { PlayerAction.Peng, new()},
                { PlayerAction.Gang, new()},
            };
            List<Pai> shouPaiList = shouPai.ShouPaiList;
            shouPaiList.Sort();
            for (int i = 0; i < shouPaiList.Count - 1; i++)
            {   // 从手牌中每张牌进行遍历,按照每两张进行判断
                if (shouPaiList[i] == shouPaiList[i + 1])
                {   // 即雀头/刻子,第一张和第二张牌相同,若有三张相同则为刻子
                    if (i < shouPaiList.Count - 2)
                    {   // 至少其后有两张牌
                        if (shouPaiList[i] == shouPaiList[i + 2])
                        {   //是刻子,可以碰/杠
                            if (matchInformation.RemainPaiCount >= 1)
                            {   // 杠和拔北要摸岭上牌,因此当剩余牌数小于1时不允许开杠和拔北
                                if (matchInformation.KangCount <= 3)
                                {   // 开杠数大于3则不允许继续开杠
                                    playerActions[PlayerAction.Gang].Add(new(
                                        new() { shouPaiList[i], shouPaiList[i + 1], shouPaiList[i + 2] },
                                        new() { shouPaiList[i] },
                                        PlayerAction.Gang
                                        ));
                                }
                            }
                            playerActions[PlayerAction.Peng].Add(new(
                                new() { shouPaiList[i], shouPaiList[i + 1] },
                                new() { shouPaiList[i] },
                                PlayerAction.Peng
                                ));
                        }
                        else
                        {   //是雀头,可以碰
                            playerActions[PlayerAction.Peng].Add(new(
                                new() { shouPaiList[i], shouPaiList[i + 1] },
                                new() { shouPaiList[i] },
                                PlayerAction.Peng
                                ));
                        }
                    }
                }
                // 因为同一张牌可以同时实现吃碰杠,这里不能使用else
                if ((matchInformation.MatchType == MatchType.FourMahjongEast || matchInformation.MatchType == MatchType.FourMahjongSouth)
                    && (matchInformation.CurrentPlayer == shouPai.Player - 1 || matchInformation.CurrentPlayer == shouPai.Player + 3))
                {   // 只有四人麻将可以吃牌,且只能吃上家(即座位序号比自身小1或比自身大3)的牌
                    if ((shouPaiList[i].Color == shouPaiList[i + 1].Color && shouPaiList[i].Number == shouPaiList[i + 1].Number + 1) && shouPaiList[i].Color != Color.Honor)
                    {   // 即两面/边张,第一张牌和第二张牌相邻且同色且都不是字牌
                        if (shouPaiList[i].Number == 1)
                        {   // 是1 2边张
                            playerActions[PlayerAction.Chi].Add(new(
                                new() { shouPaiList[i], shouPaiList[i + 1] },
                                new() { new(shouPaiList[i].Color, shouPaiList[i].Number + 2) },
                                PlayerAction.Chi
                                ));
                        }
                        else if (shouPaiList[i].Number == 8)
                        {
                            // 是8 9边张   
                            playerActions[PlayerAction.Chi].Add(new(
                                new() { shouPaiList[i], shouPaiList[i + 1] },
                                new() { new(shouPaiList[i].Color, shouPaiList[i].Number - 1) },
                                PlayerAction.Chi
                                ));
                        }
                        else
                        {   // 即中张两面
                            playerActions[PlayerAction.Chi].Add(new(
                                new() { shouPaiList[i], shouPaiList[i + 1] },
                                new() { new(shouPaiList[i].Color, shouPaiList[i].Number - 1), new(shouPaiList[i].Color, shouPaiList[i].Number + 2) },
                                PlayerAction.Chi
                                ));
                        }
                    }
                    if ((shouPaiList[i].Color == shouPaiList[i + 1].Color && shouPaiList[i].Number == shouPaiList[i + 1].Number + 2) && shouPaiList[i].Color != Color.Honor)
                    {   // 即坎张,第二张牌比第一张牌点数大2且同色且都不是字牌
                        playerActions[PlayerAction.Chi].Add(new(
                            new() { shouPaiList[i], shouPaiList[i + 1] },
                            new() { new(shouPaiList[i].Color, shouPaiList[i].Number + 1) },
                            PlayerAction.Chi
                            ));
                    }
                }
            }
            return playerActions;
        }
        /// <summary>
        /// 用于判断当前玩家手牌所能进行的自身操作,需要玩家手牌,当前所摸的牌,判断能否加杠/暗杠/拔北
        /// </summary>
        /// <param name="shouPai"></param>
        /// <returns>返回一个字典,以操作为键,可进行的操作(SelfActionData)的list为值</returns>
        public static Dictionary<PlayerAction, List<SelfActionData>> SelfAvailableJudge(ShouPai shouPai, Pai singlePai, IMatchInformation matchInformation)
        {

            // 将刚摸的牌添加进去,并判断
            List<Pai> paiList = shouPai.ShouPaiList.ToList();
            paiList.Add(singlePai);
            paiList.Sort();
            Dictionary<PlayerAction, List<SelfActionData>> playerActions = new()
            {
                 { PlayerAction.Gang, new()},
                 { PlayerAction.BaBei, new()},
            };
            if (matchInformation.RemainPaiCount >= 1)
            {   // 杠和拔北要摸岭上牌,因此当剩余牌数小于1时不允许开杠和拔北
                if (matchInformation.KangCount <= 3)
                {   // 开杠数大于3则不允许继续开杠
                    List<Pai> gangPaiList = new();
                    foreach (Group fuluGroup in shouPai.FuluPaiList)
                    {   // 获取所有副露的明刻,从而获取那几张牌可以用来加杠
                        if (fuluGroup.GroupType == GroupType.Triple)
                        {
                            gangPaiList.Add(fuluGroup.Pais[0]);
                        }
                    }
                    for (int i = 0; i < paiList.Count; i++)
                    {
                        if (i < paiList.Count - 3)
                        {   // 暗杠,如果存在四张相同的牌,那么可以进行暗杠
                            if (paiList[i] == paiList[i + 1] && paiList[i] == paiList[i + 2] && paiList[i] == paiList[i + 3])
                            {
                                playerActions[PlayerAction.Gang].Add(new(
                                    new() { paiList[i], paiList[i + 1], paiList[i + 2], paiList[i + 3], },
                                    PlayerAction.Gang
                                    ));
                            }
                        }
                        if (gangPaiList.Contains(paiList[i]))
                        {   // 加杠,如果存在和刻子相同的牌,则可以进行加杠
                            playerActions[PlayerAction.Gang].Add(new(
                                 new() { paiList[i], },
                                 PlayerAction.Gang
                                 ));
                        }
                    }
                }
                if (matchInformation.MatchType == MatchType.ThreeMahjongEast || matchInformation.MatchType == MatchType.ThreeMahjongSouth)
                {   // 三人场才允许拔北
                    for (int i = 0; i < paiList.Count; i++)
                    {
                        if (paiList[i].Color == Color.Honor && paiList[i].Number == 4)
                        {   // 所有北牌等效
                            playerActions[PlayerAction.BaBei].Add(new(
                                 new() { paiList[i], },
                                 PlayerAction.BaBei
                                 ));
                        }
                    }
                }
            }
            return playerActions;
        }
        /// <summary>
        /// 立直的判定,当前玩家未立直且有1000点且门清且牌山剩余牌数大于4时可以进行立直,此方法不考虑是否听牌,相关判断位于调用处
        /// </summary>
        /// <param name="shouPai">手牌</param>
        /// <param name="matchInformation">比赛信息</param>
        /// <returns>返回能否立直</returns>
        public static bool RiichiJudge(ShouPai shouPai, IMatchInformation matchInformation)
        {
            return matchInformation.IsRiichi[shouPai.Player] == false && matchInformation.RemainPaiCount >= 4 && matchInformation.PlayerPoint[shouPai.Player] >= 1000 && shouPai.IsClosedHand;
        }
        /// <summary>
        /// 流局满贯判定,即在荒牌流局的情况下存在玩家,其牌河仅为幺九牌且未被鸣牌
        /// </summary>
        /// <param name="matchInformation"></param>
        /// <returns></returns>
        public static bool NagashiManganJudge(IMatchInformation matchInformation, int player)
        {
            foreach (Pai pai in matchInformation.QiPaiList[player])
            {
                if (!(pai.Color == Color.Honor || pai.Number == 1 || pai.Number == 9))
                {   // 弃牌全为幺九牌
                    return false;
                }
            }
            foreach (KeyValuePair<int, List<Group>> keyValuePair in matchInformation.PlayerFuluList)
            {   // 没有别家鸣牌来源自该玩家
                foreach (Group group in keyValuePair.Value)
                {
                    if (group.GroupType != GroupType.AnKang && group.FuluSource == player)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        /// <summary>
        /// 切牌听牌的判定,用于判断当前手牌下,打出哪张牌可以进入听牌阶段,返回一个字典,存储所切的牌->对应的和牌列表->对应的面子并根据能否听牌返回bool
        /// </summary>
        /// <param name="shouPai">手中的手牌</param>
        /// <param name="singlePai">单牌,也即刚摸到的牌</param>
        /// <param name="cutPais">存储所切的牌->对应的和牌列表->对应的面子</param>
        /// <returns>返回存在牌,切出该牌后可听牌</returns>
        public static bool CutTingPaiJudge(ShouPai shouPai, Pai singlePai, out Dictionary<Pai, Dictionary<Pai, List<Group>>> cutPais)
        {
            cutPais = new();
            // 将输入的手牌再赋值并添加单牌
            ShouPai fullShouPai = (ShouPai)shouPai.Clone();
            fullShouPai.ShouPaiList.Add(singlePai);
            for (int i = 0; i < fullShouPai.ShouPaiList.Count; i++)
            {   // 具体的计算过程是,依次删除每一张手牌并判断剩余手牌是否听牌,如果能听牌将其标记为可被切的牌
                ShouPai calculateShouPai = (ShouPai)fullShouPai.Clone();
                Pai deletedPai = calculateShouPai.ShouPaiList[i];
                calculateShouPai.ShouPaiList.RemoveAt(i);
                if (TingPaiJudge(calculateShouPai, out Dictionary<Pai, List<Group>> successPais))
                {   // 将可行的切牌和可听的牌列表存储进字典中
                    cutPais[deletedPai] = successPais;
                }
            }
            if (cutPais.Count != 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 判断该牌对于该玩家是否为役牌,需要当前比赛信息,牌的信息和玩家序号
        /// </summary>
        /// <param name="matchInformation">当前比赛信息</param>
        /// <param name="pai">所判断的字牌</param>
        /// <param name="player">玩家的序号</param>
        /// <returns></returns>
        public static bool IsYiPai(IMatchInformation matchInformation, Pai pai, int player)
        {
            if (pai.Color == Color.Honor)
            {
                if (pai.Number >= 5)  // 判断是不是白发中
                {
                    return true;
                }
                else if (pai.Number == (player - matchInformation.Round + 1))  // 判断是否为自风
                {
                    return true;
                }
                else if (pai.Number == (int)matchInformation.Wind)  // 判断是否为场风
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 振听判断方法,通过列表(暂时)
        /// </summary>
        /// <param name="TingPaiList">玩家所听的牌</param>
        /// <param name="QiPaiDui">玩家的弃牌堆</param>
        /// <returns></returns>
        public static bool ZhenTingJudge(List<Pai> TingPaiList, List<Pai> QiPaiDui)
        {
            foreach (Pai tingPai in TingPaiList)
            {
                foreach (Pai qiPai in QiPaiDui)
                {
                    if (tingPai == qiPai)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        /// <summary>
        /// 听牌判断方法,输入手牌列表,返回是否听牌和所听牌及对应和牌牌型
        /// </summary>
        /// <param name="shouPai"></param>
        /// <param name="successPais"></param>
        /// <returns></returns>
        public static bool TingPaiJudge(ShouPai shouPai, out Dictionary<Pai, List<Group>> successPais)
        {
            List<Pai> ShouPaiList = shouPai.ShouPaiList;
            ShouPaiList.Sort();
            successPais = new();
            // 对四副露分开看待
            if (shouPai.FuluPaiList.Count == 4)
            {
                List<Group> groups = new();
                foreach (Group fuluPai in shouPai.FuluPaiList)
                {
                    groups.Add(fuluPai);
                }
                // 直接返回所听牌和手牌相同
                groups.Add(new(GroupType.Pair, ShouPaiList[0].Color, new() { ShouPaiList[0] }));
                successPais[shouPai.ShouPaiList[0]] = groups;
                return true;
            }
            else
            {
                // 先对牌按花色分类
                List<Pai> mainPaiList = shouPai.ShouPaiList;
                List<List<int>> coloredPaiList = new() { new(), new(), new(), new() };

                foreach (Pai pai in mainPaiList)
                {
                    // 按照牌的花色去分类
                    switch (pai.Color)
                    {
                        case Color.Wans:
                            coloredPaiList[0].Add(pai.Number);
                            break;
                        case Color.Tungs:
                            coloredPaiList[1].Add(pai.Number);
                            break;
                        case Color.Bamboo:
                            coloredPaiList[2].Add(pai.Number);
                            break;
                        case Color.Honor:
                            coloredPaiList[3].Add(pai.Number);
                            break;
                        default:
                            break;
                    }
                }
                // 分别优先考虑顺子和雀头
                foreach (bool preferStraight in new bool[] { true, false })
                {
                    // 遍历所有类型的牌并依次进行牌型判断
                    foreach (Color color in new List<Color>() { Color.Wans, Color.Tungs, Color.Bamboo, Color.Honor })
                    {
                        for (int i = 1; i < 10; i++)
                        {
                            // 跳过字牌序号为8,9的情况
                            if (color == Color.Honor && i >= 8)
                            {
                                break;
                            }
                            // [TODO] 等待优化
                            // 添加一张牌去判断
                            List<Group> tempGroups = new();
                            List<List<int>> tempList = coloredPaiList.Select(inner => inner.ToList()).ToList();
                            tempList[(int)color].Add(i);
                            // major pair 分别存储面子和雀头的数量
                            int major = 0;
                            int pair = 0;
                            // 按照牌的花色进行DFS算法寻找面子和雀头
                            for (int j = 0; j < 4; j++)
                            {
                                // 介于字牌的特殊情况,采用更直接的方法
                                if (j == 3)
                                {
                                    int[] paiCount = new int[8];
                                    foreach (int num in tempList[j])
                                    {
                                        paiCount[num]++;
                                    }
                                    for (int k = 1; k < 8; k++)
                                    {
                                        // 两张字牌,即为雀头,三张字牌,即为暗刻,其余字牌数认为不听牌
                                        if (paiCount[k] == 2)
                                        {
                                            pair++;
                                            tempGroups.Add(new(GroupType.Pair, Color.Honor, new() { new(Color.Honor, k), new(Color.Honor, k) }));
                                        }
                                        else if (paiCount[k] == 3)
                                        {
                                            major++;
                                            tempGroups.Add(new(GroupType.Triple, Color.Honor, new() { new(Color.Honor, k), new(Color.Honor, k), new(Color.Honor, k) }));
                                        }
                                        else
                                        {

                                        }
                                    }
                                }
                                else
                                {
                                    Node node = SingleColorJudge(tempList[j], preferStraight);
                                    major += node.MajorCount;
                                    pair += node.PairCount;
                                    foreach (DFSGroup DFSgroup in node.DFSGroups)
                                    {
                                        // 将算法中的DFSGroup转化为Group
                                        Group tempGroup;
                                        DFSgroup.Color = (Color)j;
                                        if (DFSgroup.GroupType == GroupType.Straight)
                                        {
                                            tempGroup = new(GroupType.Straight, DFSgroup.Color, new()
                                        {
                                            new(DFSgroup.Color, DFSgroup.Numbers[0]),
                                            new(DFSgroup.Color, DFSgroup.Numbers[1]),
                                            new(DFSgroup.Color, DFSgroup.Numbers[2]),
                                        });
                                        }
                                        else if (DFSgroup.GroupType == GroupType.Triple)
                                        {
                                            tempGroup = new(GroupType.Triple, DFSgroup.Color, new()
                                        {
                                            new(DFSgroup.Color, DFSgroup.Numbers[0]),
                                            new(DFSgroup.Color, DFSgroup.Numbers[0]),
                                            new(DFSgroup.Color, DFSgroup.Numbers[0]),
                                        });
                                        }
                                        else
                                        {
                                            tempGroup = new(GroupType.Pair, DFSgroup.Color, new()
                                        {
                                            new(DFSgroup.Color, DFSgroup.Numbers[0]),
                                            new(DFSgroup.Color, DFSgroup.Numbers[0]),
                                        });
                                        }
                                        tempGroups.Add(tempGroup);
                                    }
                                }
                            }
                            // 进行判断,当为四个面子和一个雀头时,才看做和牌,考虑副露
                            if (major + shouPai.FuluPaiList.Count == 4 && pair == 1)
                            {
                                // 添加副露中的面子
                                foreach (Group fulupai in shouPai.FuluPaiList)
                                {
                                    tempGroups.Add(fulupai);
                                }
                                successPais[new(color, i)] = tempGroups;
                            }
                            else
                            {

                            }
                        }
                    }
                }

            }
            if (successPais.Count != 0)
            {
                return true;
            }
            else
            {
                // 国士无双的判断
                if (KokuShiJudge(ShouPaiList, out Dictionary<Pai, List<Group>> kokushiPais))
                {
                    successPais = kokushiPais;
                    return true;
                }
                // 最后判断七对子,避免两杯口被判断为七对子
                if (SevenPairJudge(ShouPaiList, out Pai sevenPairPai, out List<Group> sevenPairGroups))
                {
                    successPais[sevenPairPai] = sevenPairGroups;
                    return true;
                }
                return false;
            }
        }
        /// <summary>
        /// 国士无双的判定,先判断是否所有牌都是幺九牌,再判断是否最多只有两张重复的牌,没有重复的牌则为十三面
        /// </summary>
        /// <param name="calPaiList"></param>
        /// <param name="successPais"></param>
        /// <returns></returns>
        public static bool KokuShiJudge(List<Pai> calPaiList, out Dictionary<Pai, List<Group>> successPais)
        {
            successPais = new();
            if (calPaiList.All(n => (n.Color == Color.Honor) || (n.Number == 9 || n.Number == 1)))
            {
                bool isKokushi = true;
                bool haveExtraPai = false;
                for (int i = 0; i < calPaiList.Count - 1; i++)
                {
                    if (calPaiList[i] == calPaiList[i + 1] && haveExtraPai)
                    {
                        isKokushi = false;
                        break;
                    }
                    else if (calPaiList[i] == calPaiList[i + 1] && !haveExtraPai)
                    {
                        haveExtraPai = true;
                        i++;
                    }
                }
                if (isKokushi)
                {
                    if (haveExtraPai)
                    {
                        for (int i = 0; i < calPaiList.Count; i++)
                        {
                            if (calPaiList[i] != kokuShiList[i])
                            {
                                successPais[kokuShiList[i]] = new()
                                {
                                    new Group(GroupType.Triple, Color.Honor, new()
                                    {
                                        new(Color.Honor, 8),
                                        new(Color.Honor, 8),
                                        new(Color.Honor, 8),
                                    })
                                };
                                return true;
                            }
                        }
                    }
                    else
                    {   // 目前设定十三面的听牌为z8,标志其听牌为所有幺九牌
                        successPais[new(Color.Honor, 8)] = new()
                        {
                            new Group(GroupType.Triple, Color.Honor, new()
                            {
                                new(Color.Honor, 8),
                                new(Color.Honor, 8),
                                new(Color.Honor, 8),
                            })
                        };
                        return true;
                    }
                }
            }
            return false;
        }
        /// <summary>
        /// 用于判断七对子的单独方法,由于算法的设计优先考虑面子而非雀头,七对子若包含一杯口则会被看做四个对子和两个顺子,因此分开讨论
        /// </summary>
        /// <param name="calPaiList"></param>
        /// <param name="pai"></param>
        /// <returns></returns>
        public static bool SevenPairJudge(List<Pai> calPaiList, out Pai pai, out List<Group> groups)
        {
            int pairCount = 0;
            bool haveSinglePai = false;
            pai = calPaiList[0];  // 一定会被赋值,仅用于占位
            groups = new();
            for (int i = 0; i < calPaiList.Count - 1; i++)
            {
                if (i < calPaiList.Count - 2)
                {
                    if (calPaiList[i] == calPaiList[i + 1] && calPaiList[i] == calPaiList[i + 2])
                    {
                        // 如果存在三张相同的牌,直接退出,避免龙七对
                        return false;
                    }
                }
                // 存在前后相同的牌,对子数+1且序号+1
                if (calPaiList[i] == calPaiList[i + 1])
                {
                    groups.Add(new(GroupType.Pair, calPaiList[i].Color, new() { calPaiList[i], calPaiList[i + 1] }));
                    pairCount++;
                    i++;
                }
                else if (!haveSinglePai)
                {
                    // 如果下一张牌和当前牌不一样且目前没有存储的单张,存储当前牌为待听牌
                    pai = calPaiList[i];
                    haveSinglePai = true;
                }
                else if (haveSinglePai)
                {
                    // 在已有一张孤张的情况下又出现一张,直接退出
                    return false;
                }
            }
            if (pairCount == 6)
            {
                if (!haveSinglePai)
                {
                    // 如果遍历完毕后仍没有单张,则说明单张为最后一张
                    pai = calPaiList[12];
                }
                groups.Add(new(GroupType.Pair, pai.Color, new() { pai, pai }));
                // 如果haveSinglePai为True,一定会在上述循环中赋值
                return true;
            }
            else  // 兜底,正常情况下不会走到这里
            {
                Console.WriteLine($"警告:七对子判断进行到不可能的范围,位置:{nameof(SevenPairJudge)}");
                return false;
            }

        }
        /// <summary>
        /// DFS算法,Node定义,实现 IComparable 从而可比较
        /// </summary>
        internal class Node : IComparable<Node>
        {
            public Node(int maj, int pair, List<DFSGroup> DFSgroup)
            {
                MajorCount = maj;
                PairCount = pair;
                DFSGroups = DFSgroup;
            }
            /// <summary>
            /// 顺子/刻子的成组
            /// </summary>
            public int MajorCount { get; set; }
            /// <summary>
            /// 对子的成组
            /// </summary>
            public int PairCount { get; set; }
            /// <summary>
            /// 所有组合的列表
            /// </summary>
            public List<DFSGroup> DFSGroups { get; set; }
            /// <summary>
            /// 比较方法
            /// </summary>
            /// <param name="node">所比较的另一个结点</param>
            /// <returns></returns>
            public int CompareTo(Node node)
            {
                if (MajorCount != node.MajorCount)
                {
                    return MajorCount - node.MajorCount;
                }
                return PairCount - node.PairCount;
            }
        }
        /// <summary>
        /// 记忆字典,存储所有可能路径下的结果
        /// </summary>
        internal static Dictionary<string, Node> StraightMemory = new();
        internal static Dictionary<string, Node> TripleMemory = new();
        /// <summary>
        /// GlobalFunction 内部算法使用的类
        /// </summary>
        internal class DFSGroup
        {
            /// <summary>
            /// 不考虑花色的构造器,用于DFS算法,为内部构造器
            /// </summary>
            /// <param name="groupType">面子类型</param>
            /// <param name="nums">面子数字</param>
            internal DFSGroup(GroupType groupType, params int[] nums)
            {
                GroupType = groupType;
                Numbers = nums;
            }
            /// <summary>
            /// 考虑花色的构造器
            /// </summary>
            /// <param name="groupType">面子类型</param>
            /// <param name="color">面子花色</param>
            /// <param name="nums">面子数字</param>
            public DFSGroup(GroupType groupType, Color color, params int[] nums)
            {
                GroupType = groupType;
                Color = color;
                Numbers = nums;
            }
            public GroupType GroupType { get; set; }
            public int[] Numbers { get; set; }
            public Color Color { get; set; }
        }
        /// <summary>
        /// DFS 算法,优先考虑顺子,考虑多种面子情况,对顺子优先和刻子优先分别考虑
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        internal static Node StraightDFS(int[] count)
        {
            // 通过手牌剩余序列化选项
            string key = string.Join(",", count.Skip(1));
            if (StraightMemory.TryGetValue(key, out var cached))
            {
                return cached;
            }
            // 从第一张牌开始
            int i = 1;
            while (i <= 9 && count[i] == 0)
            {
                i++;
            }
            // 全处理完
            if (i > 9)
            {
                Node leaf = new Node(0, 0, new List<DFSGroup>());
                StraightMemory[key] = leaf;
                return leaf;
            }

            Node best = new Node(0, 0, new List<DFSGroup>());

            // 1) 顺子
            if (i <= 7 && count[i] > 0 && count[i + 1] > 0 && count[i + 2] > 0)
            {
                count[i]--; count[i + 1]--; count[i + 2]--;
                Node sub = StraightDFS(count);
                List<DFSGroup> newDFSGroups = new(sub.DFSGroups);
                newDFSGroups.Insert(0, new DFSGroup(GroupType.Straight, i, i + 1, i + 2));
                Node cand = new Node(sub.MajorCount + 1, sub.PairCount, newDFSGroups);
                if (cand.CompareTo(best) > 0) best = cand;
                count[i]++;
                count[i + 1]++;
                count[i + 2]++;
            }

            // 2) 刻子
            if (count[i] >= 3)
            {
                count[i] -= 3;
                Node sub = StraightDFS(count);
                List<DFSGroup> newDFSGroups = new(sub.DFSGroups);
                newDFSGroups.Insert(0, new DFSGroup(GroupType.Triple, i));
                Node cand = new Node(sub.MajorCount + 1, sub.PairCount, newDFSGroups);
                if (cand.CompareTo(best) > 0) best = cand;
                count[i] += 3;
            }

            // 3) 对子
            if (count[i] >= 2)
            {
                count[i] -= 2;
                Node sub = StraightDFS(count);
                List<DFSGroup> newDFSGroups = new(sub.DFSGroups);
                newDFSGroups.Insert(0, new DFSGroup(GroupType.Pair, i));
                Node cand = new Node(sub.MajorCount, sub.PairCount + 1, newDFSGroups);
                if (cand.CompareTo(best) > 0) best = cand;
                count[i] += 2;
            }

            // 4) 跳过 i
            {
                int save = count[i];
                count[i] = 0;
                Node sub = StraightDFS(count);
                // 不加任何组
                if (sub.CompareTo(best) > 0) best = sub;
                count[i] = save;
            }

            StraightMemory[key] = best;
            return best;
        }
        /// <summary>
        /// DFS 算法,优先考虑刻子
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        internal static Node TripleDFS(int[] count)
        {
            // 通过手牌剩余序列化选项
            string key = string.Join(",", count.Skip(1));
            if (TripleMemory.TryGetValue(key, out var cached))
            {
                return cached;
            }
            // 从第一张牌开始
            int i = 1;
            while (i <= 9 && count[i] == 0)
            {
                i++;
            }
            // 全处理完
            if (i > 9)
            {
                Node leaf = new Node(0, 0, new List<DFSGroup>());
                TripleMemory[key] = leaf;
                return leaf;
            }

            Node best = new Node(0, 0, new List<DFSGroup>());

            // 1) 刻子
            if (count[i] >= 3)
            {
                count[i] -= 3;
                Node sub = TripleDFS(count);
                List<DFSGroup> newDFSGroups = new(sub.DFSGroups);
                newDFSGroups.Insert(0, new DFSGroup(GroupType.Triple, i));
                Node cand = new Node(sub.MajorCount + 1, sub.PairCount, newDFSGroups);
                if (cand.CompareTo(best) > 0) best = cand;
                count[i] += 3;
            }

            // 2) 顺子
            if (i <= 7 && count[i] > 0 && count[i + 1] > 0 && count[i + 2] > 0)
            {
                count[i]--; count[i + 1]--; count[i + 2]--;
                Node sub = TripleDFS(count);
                List<DFSGroup> newDFSGroups = new(sub.DFSGroups);
                newDFSGroups.Insert(0, new DFSGroup(GroupType.Straight, i, i + 1, i + 2));
                Node cand = new Node(sub.MajorCount + 1, sub.PairCount, newDFSGroups);
                if (cand.CompareTo(best) > 0) best = cand;
                count[i]++;
                count[i + 1]++;
                count[i + 2]++;
            }

            // 3) 对子
            if (count[i] >= 2)
            {
                count[i] -= 2;
                Node sub = TripleDFS(count);
                List<DFSGroup> newDFSGroups = new(sub.DFSGroups);
                newDFSGroups.Insert(0, new DFSGroup(GroupType.Pair, i));
                Node cand = new Node(sub.MajorCount, sub.PairCount + 1, newDFSGroups);
                if (cand.CompareTo(best) > 0) best = cand;
                count[i] += 2;
            }

            // 4) 跳过 i
            {
                int save = count[i];
                count[i] = 0;
                Node sub = TripleDFS(count);
                // 不加任何组
                if (sub.CompareTo(best) > 0) best = sub;
                count[i] = save;
            }

            TripleMemory[key] = best;
            return best;
        }
        /// <summary>
        /// 单个花色下对面子的判断,根据preferStriaight选择优先考虑顺子还是面子
        /// </summary>
        /// <param name="nums"></param>
        /// <param name="preferStraight">若为True则优先考虑顺子,若为False则优先考虑刻子,默认为true</param>
        /// <returns></returns>
        internal static Node SingleColorJudge(List<int> nums, bool preferStraight)
        {
            int[] countList = new int[10];
            foreach (int i in nums)
            {
                countList[i]++;
            }
            if (preferStraight)
            {
                return StraightDFS(countList);
            }
            else
            {
                return TripleDFS(countList);
            }
        }
    }
}
