using System;
using System.Collections.Generic;
using System.Diagnostics;
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
     * 10.目前将门清/副露听牌的判断定义在CutTenpaiJudge()中,调用时会根据当前手牌返回切哪张牌可以进入听牌的阶段,对于是否能立直等情况在外层判断
     * 11.流满判断考虑在matchInformation中记录,当某家被鸣牌时设定其为false,从而避免流局时判定
     * 
     * 12.单巡阶段的基准是客户端和服务端之间的交互点
     *    开始->服务端,客户端连接
     *    开始阶段: 服务端向客户端发送初始化信息,自身进入开始阶段
     *              
     *              客户端接收后发送响应信息,自身进入开始阶段
     *              
     *    舍牌阶段: 服务端确认客户端已初始化,向客户端发送摸牌信息和可进行的操作信息,自身进入舍牌阶段
     *              
     *              客户端接收摸牌后返回自身操作,自身进入舍牌阶段
     *              
     *    分支-1:   客户端确认自摸/流局,服务端接收后广播,自身标记对局结束
     *              
     *              客户端接收后处理,自身标记对局结束[END]
     *              
     *    响应阶段: 服务端确认客户端的操作,将玩家操作广播给所有玩家,自身进入响应阶段
     *              
     *              客户端接收信息后处理并返回,自身进入响应阶段
     *              
     *    分支-1:   服务端处理发现存在鸣牌,将玩家操作广播给所有玩家,如开杠/拔北返回岭上牌,自身进入舍牌阶段
     *              
     *              客户端接收鸣牌信息,做出处理后返回,自身进入舍牌阶段-->进入响应阶段/舍牌阶段分支-1
     *              
     *    分支-2:   服务端处理发现存在和牌,将玩家操作广播给所有玩家,自身标记对局结束
     *              
     *              客户端接收消息后,自身标记对局结束[END]
     *              
     *    结束阶段: 服务端处理发现无人鸣牌,判断流局,没有流局,广播结果,自身进入结束阶段
     *              
     *              客户端接收后处理并返回,自身进入结束阶段-->进入舍牌阶段分支
     *              
     *    分支-1:   服务端处理后发现存在流局,进行广播,自身标记对局结束
     *              
     *              客户端接收后处理,自身标记对局结束[END]
     *    
     * 13.流局优先级:
     *    四杠散了>四家立直>四风连打,以上途中流局均优先于荒牌流局
     *    第一巡四家均立直且宣言牌都为同一种风牌,北家立直并支付1000点后如无人荣和按四家立直流局
     *    牌山剩余最后一张,当前已有两人及以上开三杠,玩家开四杠后舍牌时无人荣和,按四杠散了流局,不计算罚符
     *    
     * 14.因为架构问题,目前考虑将类重新划分,将公共类放在MaJiangLib中,从而方便运行服务器端
     * 
     * 15.为了便于传输,尽量所有使得涉及到传输的数据避免使用负数作为标记
     * 
     * 16.用json实现序列化会使得数据传输量太大,而用自动化反射序列化节省的操作不算多,暂时先为每个类手动实现序列化,将来也许会改
     * 
     * 17.因为C# 9.0 的限制,像BytesTo()这样的静态方法无法在接口中声明,因此对于BytesToList(),其限制where T : IByteable并不保证T可以从字节串转化而来
     *    对于任何实现了该接口的类型,其必须在GlobalFunction.ByteableInstanceDict里注册,类自身用属性包装常量byteSize字段
     *    
     * 18.List<>用太多了..考虑优化成数组
     * 19.玩家序号从0开始
     */

    #region *枚举定义*
    /// <summary>
    /// 性别:男,女,保密..
    /// </summary>
    public enum Gender
    {
        Male = 1,
        Female,
        Other,
    }

    /// <summary>
    /// 玩家切牌时的阶段类型,分为舍牌阶段,响应阶段和结束阶段
    /// </summary>
    public enum StageType
    {
        /// <summary>
        /// 初始化阶段,仅开局时设定
        /// </summary>
        InitStage,
        /// <summary>
        /// 开始阶段,仅用于触发摸牌行为/标记,一般立刻结束进入舍牌阶段
        /// </summary>
        StartStage,
        /// <summary>
        /// 玩家摸牌或响应别家牌时进入舍牌阶段,可加杠/拔北/暗杠/立直/自摸/九种九牌流局
        /// </summary>
        DiscardStage,
        /// <summary>
        /// 打出牌/开杠/拔北后进入响应阶段,可被其他家吃碰杠荣和
        /// </summary>
        ClaimStage,
        /// <summary>
        /// 如果拔北/开杠后响应阶段无人响应,再进入摸牌阶段并添加岭上标记;如果打出牌后响应阶段无人响应,进入结束阶段,判断流局
        /// </summary>
        EndStage,
    }
    /// <summary>
    /// 流局类型,包含五种-荒牌流局,九种九牌,四杠散了,四家立直,四风连打
    /// </summary>
    public enum DrawType
    {
        /// <summary>
        /// 占位符
        /// </summary>
        Void = 31,
        /// <summary>
        /// 荒牌流局:当剩余牌为0时最后一位玩家打出牌后无人和牌时触发,需要判断听牌罚符和流局满贯
        /// </summary>
        DeadWallDraw = 1,
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
        /// 四风连打:第一巡无人鸣牌时,庄家和所有子家都打出同一种风牌时触发,三麻无此规则
        /// </summary>
        FourWindsDraw,
    }
    /// <summary>
    /// 比赛类型,标记本场比赛是三麻还是四麻,是东场还是半庄
    /// </summary>
    public enum MatchType
    {
        ThreeMahjongEast = 1,
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
        Straight = 1,
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
        Chi = 1,
        Peng,
        AnKang,
        MingKang,
        JiaKang,
    }
    /// <summary>
    /// 玩家所能进行的操作,包含切牌,吃,碰,杠,立直,拔北,荣和,自摸,流局
    /// </summary>
    public enum PlayerAction : byte
    {// 必须吃<碰<荣和
        /// <summary>
        /// 跳过
        /// </summary>
        Skip = 1,
        /// <summary>
        /// 手切
        /// </summary>
        HandCut = 2,
        /// <summary>
        /// 摸切
        /// </summary>
        TakeCut,
        /// <summary>
        /// 吃
        /// </summary>
        Chi,
        /// <summary>
        /// 碰
        /// </summary>
        Peng,
        /// <summary>
        /// 杠
        /// </summary>
        Kang,
        /// <summary>
        /// 立直
        /// </summary>
        Riichi,
        /// <summary>
        /// 拔北
        /// </summary>
        Kita,
        /// <summary>
        /// 荣和
        /// </summary>
        Ron,
        /// <summary>
        /// 自摸
        /// </summary>
        Tsumo,
        /// <summary>
        /// 流局
        /// </summary>
        Draw,
    }
    /// <summary>
    /// 立直麻将所有类型的役种,括号内数字为副露时番数
    /// </summary>
    public enum YakuType : byte
    {
        // 因为也用于番种排序,此处声明位置顺序有意义,相似番种定义相近

        Empty = 1,  // 占位符
        Riichi,  // 立直-1
        DoubleRiichi,  // 两立直-2
        Ippatsu,  // 一发-1
        Tsumo,  // 门前清自摸和-1

        Rinshankaiho,  // 岭上开花-1
        HaiteiRaoyui,  // 海底捞月-1
        HoteiRaoyui,  // 河底捞鱼-1
        Chankan,  // 枪杠-1

        Jikaze,  // 自风牌-1
        Bakaze,  // 场风牌-1
        Haku,  // 役牌 白-1
        Hatsu,  // 役牌 发-1
        Chun,  // 役牌 中-1

        Pinfu,  // 平和-1
        Tanyao,  // 断幺-1
        Iipeikou,  // 一杯口-1
        Ryanpeiko,  // 两杯口-3
        Ittsu,  // 一气通贯-2(1)
        SanshokuDoujun,  // 三色同顺-2(1)

        Chanta,  // 混全带幺九-2(1)
        Junchan,  // 纯全带幺九-3(2)

        SanshokuDoukou,  // 三色同刻-2
        Chiitoitsu,  // 七对子-2
        Toitoi,  // 对对和-2
        Honroutou,  // 混老头-2
        Sananko,  // 三暗刻-2
        Sankantsu,  // 三杠子-2
        ShoSanGen,  // 小三元-2

        Honitsu,  // 混一色-3(2)
        Chinitsu,  // 清一色-6(5)

        Suuankou,  // 四暗刻-13
        SuuankouTanki,  // 四暗刻单骑-26

        Tsuuiiso,  // 字一色-13
        DaiSanGen,  // 大三元-13
        ShoSuuShii,  // 小四喜-13
        DaiSuuShii,  // 大四喜-26

        Ryuiishoku,  // 绿一色-13
        Chinroto,  // 清老头-13
        Suukantsu,  // 四杠子-13

        Tenho,  // 天和-13
        Chiiho,  // 地和-13

        ChurenPoto,  // 九莲宝灯-13
        ChuurenPoutou,  // 纯正九莲宝灯-26

        KokushiMusou,  // 国士无双-13
        KokushiMusouThirteenOrphans,  // 国士无双十三面-26

        NagashiMangan,  // 流局满贯-5

        Dora,  // 宝牌
        AkaDora, // 红宝牌
        NorthAkaDora,  // 拔北宝牌
        UraDora,  // 里宝牌
    }
    #endregion

    /// <summary>
    /// 公用的方法类,这些方法应当可以被所有用户访问,且保持静态
    /// </summary>
    public static class GlobalFunction
    {
        
        /// <summary>
        /// 洗牌方法,提供列表和随机数种,返回随机打乱后的数组
        /// </summary>
        /// <typeparam name="T">列表中元素的类型</typeparam>
        /// <param name="list">要被打乱列表</param>
        /// <param name="seed">随机数种,通过输入相同的随机数种和列表来获取相同结果</param>
        public static void Shuffle<T>(this IList<T> list, int seed)
        {
            Random random = new(seed);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// 随机牌山生成器,可根据设定生成不同组成的随机牌山
        /// </summary>
        /// <param name="randomNumber">所生成牌山的随机数</param>
        /// <param name="isFourMahJong">标记生成的牌山是不是四人牌山,若为三人牌山,将会移除2w到8w</param>
        /// <param name="haveRedDora">标记生成的牌山是否包含0w,0p,0s三张红宝牌</param>
        /// <param name="haveHonor">标记生成的牌山是否包含字牌</param>
        /// <param name="random">随机数产生器</param>
        /// <returns></returns>
        public static List<Tile> RandomCardGenerator(out int randomNumber, bool isFourMahJong, bool haveRedDora, bool haveHonor, Xoshiro256StarStar random)
        {
            // [TODO]暂时先实现四人有赤宝情况
            // 每次生成牌山都要这样生成一个列表,考虑将其作为一个静态变量放在类中跳过生成
            // 对于四人麻将,总共牌数136张,其中王牌14张
            List<Tile> tileList = new List<Tile>();
            for (int i = 1; i < 10; i++)
            {   // 添加1w~9w,1p~9p,1s~9s,其中5w,5p,5s中有一张牌分别被一张0w,0p,0s替代
                foreach (Color color in new List<Color>() { Color.Wans, Color.Tungs, Color.Bamboo })
                {
                    if (i == 5)
                    {
                        for (int j = 0; j < 3; j++)
                        {
                            tileList.Add(new(color, i, false));
                        }
                        tileList.Add(new(color, i, true));
                    }
                    else
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            tileList.Add(new(color, i));
                        }
                    }
                }
            }
            for (int i = 1; i < 8; i++)
            {   // 添加1z~7z,分别代表东南西北白发中
                for (int j = 0; j < 4; j++)
                {
                    tileList.Add(new(Color.Honor, i, false));
                }
            }
            randomNumber = random.NextInt32();
            Shuffle(tileList, randomNumber);
            return tileList;
        }
        // 暂时用一个列表去存储国士无双的牌来判断国士,流满和九种九牌
        public static List<Tile> kokushiList = new()
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
        /// 为实现实例方法调用的注册字典,任何实现IByteable的类都要注册于此
        /// </summary>
        public static Dictionary<Type, object> ByteableInstanceDict { get; } = new Dictionary<Type, object>()
        {
            // 仅用于调用接口方法,具体实现不影响
            {typeof(Tile), new Tile(Color.Wans, 1) },
            {typeof(Group), new Group(GroupType.Triple, Color.Wans, new())},
            {typeof(Player), new Player() },
            {typeof(PlayerActionData), new PlayerActionData(new(),new(Color.Wans,1 ),PlayerAction.Chi) },
            {typeof(HandTile),new HandTile() },
            {typeof(UserProfile),new UserProfile() },
        };

        /// <summary>
        /// 流局判断,判断荒牌流局,四风连打,四杠散了,四家立直,九种九牌属于玩家的主动操作,不在此处判断
        /// </summary>
        /// <param name="drawType">流局类型</param>
        /// <param name="matchInformation">比赛信息</param>
        /// <returns>返回是否流局</returns>
        public static bool DrawJudge(out DrawType drawType, IMatchInformation matchInformation)
        {
            if (matchInformation.CurrentStageType == StageType.EndStage)
            {   // 以上流局判断都必须在结束阶段内进行
                List<List<Tile>> discardTileList = matchInformation.DiscardTileList;

                if (matchInformation.KangCount == 4 && matchInformation.KangMark == 5)
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
                    matchInformation.CurrentPlayerIndex == 3
                    ) // 无人鸣牌,四人麻将,是北家的结束阶段,判断四风连打
                {
                    if (
                        discardTileList[0][0].Color == Color.Honor && discardTileList[0][0].Number >= 1 && discardTileList[0][0].Number <= 4 &&
                        discardTileList[1][0] == discardTileList[0][0] &&
                        discardTileList[2][0] == discardTileList[0][0] &&
                        discardTileList[3][0] == discardTileList[0][0]
                        ) // 庄家第一张舍牌为风牌,且子家的第一张舍牌与其相同,分开以避免在第一巡访问到不存在的子家舍牌
                    {
                        drawType = DrawType.FourWindsDraw;
                        return true;
                    }
                }
                else if (matchInformation.RemainTileCount == 0) // 进入结束阶段且剩余牌为0,为荒牌流局,优先级最低
                {
                    drawType = DrawType.DeadWallDraw;
                    return true;
                }
            }
            // 不符合任何一种流局情况,返回Void
            drawType = DrawType.Void;
            return false;
        }
        /// <summary>
        /// 用于判断其他玩家打出牌时,当前玩家所能进行的操作,需要当前手牌信息,每当玩家切换自己的手牌时更新
        /// </summary>
        /// <param name="handTile"></param>
        /// <param name="matchInformation"></param>
        /// <returns>返回一个字典,以操作为键,可进行的操作的list为值</returns>
        [Obsolete]
        public static Dictionary<PlayerAction, List<PlayerActionData>> ClaimingAvailableJudge(HandTile handTile, IMatchInformation matchInformation)
        {   // 返回结果是字典-列表-列表的嵌套,考虑优化
            Dictionary<PlayerAction, List<PlayerActionData>> playerActions = new()
            {   // 鸣牌操作仅限吃碰杠
                { PlayerAction.Chi, new()},
                { PlayerAction.Peng, new()},
                { PlayerAction.Kang, new()},
            };
            // 排序[可能会破坏原有顺序]
            List<Tile> handTileList = handTile.HandTileList;
            handTileList.Sort();
            for (int i = 0; i < handTileList.Count - 1; i++)
            {   // 从手牌中每张牌进行遍历,按照每两张进行判断
                if (handTileList[i] == handTileList[i + 1])
                {   // 即雀头/刻子,第一张和第二张牌相同,若有三张相同则为刻子
                    if (i < handTileList.Count - 2)
                    {   // 至少其后有两张牌
                        if (handTileList[i] == handTileList[i + 2])
                        {   //是刻子,可以碰/杠
                            if (matchInformation.RemainTileCount >= 1)
                            {   // 杠和拔北要摸岭上牌,因此当剩余牌数小于1时不允许开杠和拔北
                                if (matchInformation.KangCount <= 3)
                                {   // 开杠数大于3则不允许继续开杠
                                    playerActions[PlayerAction.Kang].Add(new(
                                        new() { handTileList[i], handTileList[i + 1], handTileList[i + 2] },
                                        handTileList[i],
                                        PlayerAction.Kang
                                        ));
                                }
                            }
                            playerActions[PlayerAction.Peng].Add(new(
                                new() { handTileList[i], handTileList[i + 1] },
                                handTileList[i],
                                PlayerAction.Peng
                                ));
                        }
                        else
                        {   //是雀头,可以碰
                            playerActions[PlayerAction.Peng].Add(new(
                                new() { handTileList[i], handTileList[i + 1] },
                                handTileList[i],
                                PlayerAction.Peng
                                ));
                        }
                    }
                }
                // 因为同一张牌可以同时实现吃碰杠,这里不能使用else
                if ((matchInformation.MatchType == MatchType.FourMahjongEast || matchInformation.MatchType == MatchType.FourMahjongSouth)
                    && (matchInformation.CurrentPlayerIndex == handTile.PlayerNumber - 1 || matchInformation.CurrentPlayerIndex == handTile.PlayerNumber + 3))
                {   // 只有四人麻将可以吃牌,且只能吃上家(即座位序号比自身小1或比自身大3)的牌
                    if ((handTileList[i].Color == handTileList[i + 1].Color && handTileList[i].Number == handTileList[i + 1].Number + 1) && handTileList[i].Color != Color.Honor)
                    {   // 即两面/边张,第一张牌和第二张牌相邻且同色且都不是字牌
                        if (handTileList[i].Number == 1)
                        {   // 是1 2边张
                            playerActions[PlayerAction.Chi].Add(new(
                                new() { handTileList[i], handTileList[i + 1] },
                                new(handTileList[i].Color, handTileList[i].Number + 2),
                                PlayerAction.Chi
                                ));
                        }
                        else if (handTileList[i].Number == 8)
                        {
                            // 是8 9边张   
                            playerActions[PlayerAction.Chi].Add(new(
                                new() { handTileList[i], handTileList[i + 1] },
                                new(handTileList[i].Color, handTileList[i].Number - 1),
                                PlayerAction.Chi
                                ));
                        }
                        else
                        {   // 即中张两面
                            // 单一牌型能鸣两张则分别添加鸣牌操作
                            playerActions[PlayerAction.Chi].Add(new(
                                new() { handTileList[i], handTileList[i + 1] },
                                new(handTileList[i].Color, handTileList[i].Number - 1),
                                PlayerAction.Chi
                                ));
                            playerActions[PlayerAction.Chi].Add(new(
                                new() { handTileList[i], handTileList[i + 1] },
                                new(handTileList[i].Color, handTileList[i].Number + 2),
                                PlayerAction.Chi
                                ));
                        }
                    }
                    if ((handTileList[i].Color == handTileList[i + 1].Color && handTileList[i].Number == handTileList[i + 1].Number + 2) && handTileList[i].Color != Color.Honor)
                    {   // 即坎张,第二张牌比第一张牌点数大2且同色且都不是字牌
                        playerActions[PlayerAction.Chi].Add(new(
                            new() { handTileList[i], handTileList[i + 1] },
                            new(handTileList[i].Color, handTileList[i].Number + 1),
                            PlayerAction.Chi
                            ));
                    }
                }
            }
            return playerActions;
        }
        /// <summary>
        /// 对特定单张牌的响应计算,需要手牌,所鸣单牌和对局信息
        /// </summary>
        /// <param name="handTile"></param>
        /// <param name="matchInformation"></param>
        /// <returns></returns>
        public static Dictionary<PlayerAction, List<PlayerActionData>> SingleClaimingAvailableJudge(HandTile handTile, Tile currentTile, IMatchInformation matchInformation)
        {
            Dictionary<PlayerAction, List<PlayerActionData>> playerActions = new()
            {   // 鸣牌操作仅限吃碰杠
                { PlayerAction.Chi, new()},
                { PlayerAction.Peng, new()},
                { PlayerAction.Kang, new()},

            };
            // 对手牌中所有牌进行统计,根据统计进行可行鸣牌判断
            // 每个数列第0位为红宝牌标记,1~9为对应序数牌标记,字牌第0位暂空,1~7为对应字牌顺序
            Dictionary<Color, int[]> tileCountDict = new Dictionary<Color, int[]>()
            {
                {Color.Wans, new int[10] },
                {Color.Tungs, new int [10] },
                {Color.Bamboo, new int [10] },
                {Color.Honor, new int [8] },
            };
            foreach (Tile tile in handTile.HandTileList)
            {
                if (tile.IsRedDora)
                {
                    // 目前仅考虑红五宝牌的情况,如果有红宝牌则在0上+1
                    tileCountDict[tile.Color][0]++;
                }
                else
                {
                    tileCountDict[tile.Color][tile.Number]++;
                }
            }
            Color color = currentTile.Color;
            int num = currentTile.Number;

            // 判断逻辑,考虑到此方法主要用于数据传输,因此新建牌实例

            // 碰杠的逻辑
            int count = tileCountDict[color][num];
            if (num == 5 && color != Color.Honor && count != 0)
            {   // 如果是5万筒索,额外判断有红宝牌时的情况
                if (count == 1)
                {
                    playerActions[PlayerAction.Peng].Add(new(
                                new() { new(color, num), new(color, num, true) },
                                currentTile,
                                PlayerAction.Peng
                                ));
                }
                if (count == 2)
                {
                    playerActions[PlayerAction.Kang].Add(new(
                                new() { new(color, num), new(color, num), new(color, num, true) },
                                currentTile,
                                PlayerAction.Kang
                                ));
                }
            }
            if (count == 2)
            {
                playerActions[PlayerAction.Peng].Add(new(
                                new() { new(color, num), new(color, num) },
                                currentTile,
                                PlayerAction.Peng
                                ));
            }
            if (count == 3)
            {
                playerActions[PlayerAction.Peng].Add(new(
                                new() { new(color, num), new(color, num) },
                                currentTile,
                                PlayerAction.Peng
                                ));
                playerActions[PlayerAction.Kang].Add(new(
                                new() { new(color, num), new(color, num), new(color, num) },
                                currentTile,
                                PlayerAction.Kang
                                ));
            }
            // 吃的逻辑
            // 结合红宝牌,理论上最多可以有5种鸣牌方式(如234506鸣4,有23,35,30,56,06)
            if (color != Color.Honor)
            {
                // 牌的左侧
                if (num >= 3 && tileCountDict[color][num - 1] != 0 && tileCountDict[color][num - 2] != 0)
                {
                    playerActions[PlayerAction.Chi].Add(new(
                                new() { new(color, num - 1), new(color, num - 2) },
                                currentTile,
                                PlayerAction.Chi
                                ));
                }
                // 牌的右侧
                if (num <= 7 && tileCountDict[color][num + 1] != 0 && tileCountDict[color][num + 2] != 0)
                {
                    playerActions[PlayerAction.Chi].Add(new(
                                new() { new(color, num + 1), new(color, num + 2) },
                                currentTile,
                                PlayerAction.Chi
                                ));
                }
                // 坎张
                if (num >= 2 && num <= 8 && tileCountDict[color][num - 1] != 0 && tileCountDict[color][num + 1] != 0)
                {
                    playerActions[PlayerAction.Chi].Add(new(
                                new() { new(color, num - 1), new(color, num + 1) },
                                currentTile,
                                PlayerAction.Chi
                                ));
                }
                // 如果存在对应花色的红宝牌,进行额外判断
                if (tileCountDict[color][0] != 0)
                {
                    // 暂时用挨个判断添加的方式处理红宝牌相关的吃牌问题
                    // 考虑修改为当红宝牌存在,对应序号牌才存在并再判断的方式来实现即使不是红5也可判断吃的鸣牌
                    if (num == 3 && tileCountDict[color][4] != 0)
                    {
                        // 为3,右侧
                        playerActions[PlayerAction.Chi].Add(new(
                                new() { new(color, 4), new(color, 5, true) },
                                currentTile,
                                PlayerAction.Chi
                                ));
                    }
                    if (num == 4 && tileCountDict[color][6] != 0)
                    {
                        // 为4,右侧
                        playerActions[PlayerAction.Chi].Add(new(
                                new() { new(color, 5, true), new(color, 6) },
                                currentTile,
                                PlayerAction.Chi
                                ));
                    }
                    if (num == 4 && tileCountDict[color][3] != 0)
                    {
                        // 为4,坎张
                        playerActions[PlayerAction.Chi].Add(new(
                                new() { new(color, 3), new(color, 5, true) },
                                currentTile,
                                PlayerAction.Chi
                                ));
                    }
                    if (num == 6 && tileCountDict[color][4] != 0)
                    {
                        // 为6,左侧
                        playerActions[PlayerAction.Chi].Add(new(
                                new() { new(color, 4), new(color, 5, true) },
                                currentTile,
                                PlayerAction.Chi
                                ));
                    }
                    if (num == 6 && tileCountDict[color][7] != 0)
                    {
                        // 为6,坎张
                        playerActions[PlayerAction.Chi].Add(new(
                                new() { new(color, 5, true), new(color, 7) },
                                currentTile,
                                PlayerAction.Chi
                                ));
                    }
                    if (num == 7 && tileCountDict[color][6] != 0)
                    {
                        // 为7,左侧
                        playerActions[PlayerAction.Chi].Add(new(
                                new() { new(color, 5, true), new(color, 6) },
                                currentTile,
                                PlayerAction.Chi
                                ));
                    }
                }
            }

            return playerActions;
        }
        /// <summary>
        /// 省略了单张牌,仅根据手牌和对局信息判断
        /// </summary>
        /// <param name="handTile"></param>
        /// <param name="matchInformation"></param>
        /// <returns></returns>
        public static Dictionary<PlayerAction, List<PlayerActionData>> SingleClaimingAvailableJudge(HandTile handTile, IMatchInformation matchInformation)
            => SingleClaimingAvailableJudge(handTile, matchInformation.CurrentTile, matchInformation);
        /// <summary>
        /// 用于判断当前玩家手牌所能进行的自身操作,需要玩家手牌,当前所摸的牌,判断能否加杠/暗杠/拔北/流局
        /// </summary>
        /// <param name="handTile"></param>
        /// <returns>返回一个字典,以操作为键,可进行的操作(SelfActionData)的list为值</returns>
        public static Dictionary<PlayerAction, List<PlayerActionData>> SelfAvailableJudge(HandTile handTile, Tile singleTile, IMatchInformation matchInformation)
        {

            // 将刚摸的牌添加进去,并判断
            List<Tile> tileList = handTile.HandTileList.ToList();
            tileList.Add(singleTile);
            tileList.Sort();
            Dictionary<PlayerAction, List<PlayerActionData>> playerActions = new()
            {
                 { PlayerAction.Kang, new()},
                 { PlayerAction.Kita, new()},
                 { PlayerAction.Draw, new()},
            };
            if (matchInformation.FirstCycleIppatsu)
            {   // 在第一巡时,判断能否九种九牌流局
                List<Tile> HonorTile = new();
                foreach (Tile tile in tileList)
                {
                    if (kokushiList.Contains(tile) && !HonorTile.Contains(tile))
                    {
                        HonorTile.Add(tile);
                    }
                }
                if (HonorTile.Count >= 9)
                {
                    // 流局不需要选定手牌,设定可以流局时,流局操作对应列表不为空
                    playerActions[PlayerAction.Draw] = new List<PlayerActionData>() { new(new() { HonorTile[0] }, PlayerAction.Draw) };
                }
            }
            if (matchInformation.RemainTileCount >= 1)
            {   // 杠和拔北要摸岭上牌,因此当剩余牌数小于1时不允许开杠和拔北
                if (matchInformation.KangCount <= 3)
                {   // 开杠数大于3则不允许继续开杠
                    List<Tile> KangTileList = new();
                    foreach (Group openGroup in handTile.OpenTileList)
                    {   // 获取所有副露的明刻,从而获取那几张牌可以用来加杠
                        if (openGroup.GroupType == GroupType.Triple)
                        {
                            KangTileList.Add(openGroup.Tiles[0]);
                        }
                    }
                    for (int i = 0; i < tileList.Count; i++)
                    {
                        if (i < tileList.Count - 3)
                        {   // 暗杠,如果存在四张相同的牌,那么可以进行暗杠
                            if (tileList[i] == tileList[i + 1] && tileList[i] == tileList[i + 2] && tileList[i] == tileList[i + 3])
                            {
                                playerActions[PlayerAction.Kang].Add(new(
                                    new() { tileList[i], tileList[i + 1], tileList[i + 2], tileList[i + 3], },
                                    PlayerAction.Kang
                                    ));
                            }
                        }
                        if (KangTileList.Contains(tileList[i]))
                        {   // 加杠,如果存在和刻子相同的牌,则可以进行加杠
                            playerActions[PlayerAction.Kang].Add(new(
                                 new() { tileList[i], },
                                 PlayerAction.Kang
                                 ));
                        }
                    }
                }
                if (matchInformation.MatchType == MatchType.ThreeMahjongEast || matchInformation.MatchType == MatchType.ThreeMahjongSouth)
                {   // 三人场才允许拔北
                    for (int i = 0; i < tileList.Count; i++)
                    {
                        if (tileList[i].Color == Color.Honor && tileList[i].Number == 4)
                        {   // 所有北牌等效
                            playerActions[PlayerAction.Kita].Add(new(
                                 new() { tileList[i], },
                                 PlayerAction.Kita
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
        /// <param name="handTile">手牌</param>
        /// <param name="matchInformation">比赛信息</param>
        /// <returns>返回能否立直</returns>
        public static bool RiichiJudge(HandTile handTile, IMatchInformation matchInformation)
        {
            return matchInformation.IsRiichi[handTile.PlayerNumber] == false && matchInformation.RemainTileCount >= 4 && matchInformation.PlayerPoint[handTile.PlayerNumber] >= 1000 && handTile.IsClosedHand;
        }
        /// <summary>
        /// 流局满贯判定,即在荒牌流局的情况下存在玩家,其牌河仅为幺九牌且未被鸣牌
        /// </summary>
        /// <param name="matchInformation"></param>
        /// <returns></returns>
        public static bool NagashiManganJudge(IMatchInformation matchInformation, int player)
        {
            foreach (Tile tile in matchInformation.DiscardTileList[player])
            {
                if (!(tile.Color == Color.Honor || tile.Number == 1 || tile.Number == 9))
                {   // 弃牌全为幺九牌
                    return false;
                }
            }
            foreach (List<Group> openGroups in matchInformation.PlayerOpenSetList)
            {   // 没有别家鸣牌来源自该玩家
                foreach (Group group in openGroups)
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
        /// <param name="handTile">手中的手牌</param>
        /// <param name="singleTile">单牌,也即刚摸到的牌</param>
        /// <param name="cutTiles">存储所切的牌->对应的和牌列表->对应的面子</param>
        /// <returns>返回存在牌,切出该牌后可听牌</returns>
        public static bool CutTenpaiJudge(HandTile handTile, Tile singleTile, out Dictionary<Tile, Dictionary<Tile, List<Group>>> cutTiles)
        {
            cutTiles = new();
            // 将输入的手牌再赋值并添加单牌
            HandTile fullHandTile = (HandTile)handTile.Clone();
            fullHandTile.HandTileList.Add(singleTile);
            for (int i = 0; i < fullHandTile.HandTileList.Count; i++)
            {   // 具体的计算过程是,依次删除每一张手牌并判断剩余手牌是否听牌,如果能听牌将其标记为可被切的牌
                HandTile calculateHandTile = (HandTile)fullHandTile.Clone();
                Tile deletedTile = calculateHandTile.HandTileList[i];
                calculateHandTile.HandTileList.RemoveAt(i);
                if (TenpaiJudge(calculateHandTile, out Dictionary<Tile, List<Group>> successTiles))
                {   // 将可行的切牌和可听的牌列表存储进字典中
                    cutTiles[deletedTile] = successTiles;
                }
            }
            if (cutTiles.Count != 0)
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
        /// <param name="tile">所判断的字牌</param>
        /// <param name="player">玩家的序号</param>
        /// <returns></returns>
        public static bool IsYakuTile(IMatchInformation matchInformation, Tile tile, int player)
        {
            if (tile.Color == Color.Honor)
            {
                if (tile.Number >= 5)  // 判断是不是白发中
                {
                    return true;
                }
                else if (tile.Number == (player - matchInformation.Round + 1))  // 判断是否为自风
                {
                    return true;
                }
                else if (tile.Number == (int)matchInformation.Wind)  // 判断是否为场风
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 振听判断方法,通过列表(暂时)
        /// </summary>
        /// <param name="TenpaiList">玩家所听的牌</param>
        /// <param name="DiscardTileList">玩家的弃牌堆</param>
        /// <returns></returns>
        public static bool FuritenJudge(List<Tile> TenpaiList, List<Tile> DiscardTileList)
        {
            foreach (Tile tenpai in TenpaiList)
            {
                foreach (Tile discardTile in DiscardTileList)
                {
                    if (tenpai == discardTile)
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
        /// <param name="handTile"></param>
        /// <param name="successTiles"></param>
        /// <returns></returns>
        public static bool TenpaiJudge(HandTile handTile, out Dictionary<Tile, List<Group>> successTiles)
        {
            List<Tile> HandTileList = handTile.HandTileList;
            HandTileList.Sort();
            successTiles = new();
            // 对四副露分开看待
            if (handTile.OpenTileList.Count == 4)
            {
                List<Group> groups = new();
                foreach (Group openTile in handTile.OpenTileList)
                {
                    groups.Add(openTile);
                }
                // 直接返回所听牌和手牌相同
                groups.Add(new(GroupType.Pair, HandTileList[0].Color, new() { HandTileList[0] }));
                successTiles[handTile.HandTileList[0]] = groups;
                return true;
            }
            else
            {
                // 先对牌按花色分类
                List<Tile> mainTileList = handTile.HandTileList;
                List<List<int>> coloredTileList = new() { new(), new(), new(), new() };

                foreach (Tile tile in mainTileList)
                {
                    // 按照牌的花色去分类
                    switch (tile.Color)
                    {
                        case Color.Wans:
                            coloredTileList[0].Add(tile.Number);
                            break;
                        case Color.Tungs:
                            coloredTileList[1].Add(tile.Number);
                            break;
                        case Color.Bamboo:
                            coloredTileList[2].Add(tile.Number);
                            break;
                        case Color.Honor:
                            coloredTileList[3].Add(tile.Number);
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
                            List<List<int>> tempList = coloredTileList.Select(inner => inner.ToList()).ToList();
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
                                    int[] tileCount = new int[8];
                                    foreach (int num in tempList[j])
                                    {
                                        tileCount[num]++;
                                    }
                                    for (int k = 1; k < 8; k++)
                                    {
                                        // 两张字牌,即为雀头,三张字牌,即为暗刻,其余字牌数认为不听牌
                                        if (tileCount[k] == 2)
                                        {
                                            pair++;
                                            tempGroups.Add(new(GroupType.Pair, Color.Honor, new() { new(Color.Honor, k), new(Color.Honor, k) }));
                                        }
                                        else if (tileCount[k] == 3)
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
                            if (major + handTile.OpenTileList.Count == 4 && pair == 1)
                            {
                                // 添加副露中的面子
                                foreach (Group fulutile in handTile.OpenTileList)
                                {
                                    tempGroups.Add(fulutile);
                                }
                                successTiles[new(color, i)] = tempGroups;
                            }
                            else
                            {

                            }
                        }
                    }
                }

            }
            if (successTiles.Count != 0)
            {
                return true;
            }
            else
            {
                // 国士无双的判断
                if (KokushiJudge(HandTileList, out Dictionary<Tile, List<Group>> kokushiTiles))
                {
                    successTiles = kokushiTiles;
                    return true;
                }
                // 最后判断七对子,避免两杯口被判断为七对子
                if (SevenPairJudge(HandTileList, out Tile sevenPairTile, out List<Group> sevenPairGroups))
                {
                    successTiles[sevenPairTile] = sevenPairGroups;
                    return true;
                }
                return false;
            }
        }
        /// <summary>
        /// 国士无双的判定,先判断是否所有牌都是幺九牌,再判断是否最多只有两张重复的牌,没有重复的牌则为十三面
        /// </summary>
        /// <param name="calTileList"></param>
        /// <param name="successTiles"></param>
        /// <returns></returns>
        public static bool KokushiJudge(List<Tile> calTileList, out Dictionary<Tile, List<Group>> successTiles)
        {
            successTiles = new();
            if (calTileList.All(n => (n.Color == Color.Honor) || (n.Number == 9 || n.Number == 1)))
            {
                bool isKokushi = true;
                bool haveExtraTile = false;
                for (int i = 0; i < calTileList.Count - 1; i++)
                {
                    if (calTileList[i] == calTileList[i + 1] && haveExtraTile)
                    {
                        isKokushi = false;
                        break;
                    }
                    else if (calTileList[i] == calTileList[i + 1] && !haveExtraTile)
                    {
                        haveExtraTile = true;
                        i++;
                    }
                }
                if (isKokushi)
                {
                    if (haveExtraTile)
                    {
                        for (int i = 0; i < calTileList.Count; i++)
                        {
                            if (calTileList[i] != kokushiList[i])
                            {
                                successTiles[kokushiList[i]] = new()
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
                        successTiles[new(Color.Honor, 8)] = new()
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
        /// <param name="calTileList"></param>
        /// <param name="tile"></param>
        /// <returns></returns>
        public static bool SevenPairJudge(List<Tile> calTileList, out Tile tile, out List<Group> groups)
        {
            int pairCount = 0;
            bool haveSingleTile = false;
            tile = calTileList[0];  // 一定会被赋值,仅用于占位
            groups = new();
            for (int i = 0; i < calTileList.Count - 1; i++)
            {
                if (i < calTileList.Count - 2)
                {
                    if (calTileList[i] == calTileList[i + 1] && calTileList[i] == calTileList[i + 2])
                    {
                        // 如果存在三张相同的牌,直接退出,避免龙七对
                        return false;
                    }
                }
                // 存在前后相同的牌,对子数+1且序号+1
                if (calTileList[i] == calTileList[i + 1])
                {
                    groups.Add(new(GroupType.Pair, calTileList[i].Color, new() { calTileList[i], calTileList[i + 1] }));
                    pairCount++;
                    i++;
                }
                else if (!haveSingleTile)
                {
                    // 如果下一张牌和当前牌不一样且目前没有存储的单张,存储当前牌为待听牌
                    tile = calTileList[i];
                    haveSingleTile = true;
                }
                else if (haveSingleTile)
                {
                    // 在已有一张孤张的情况下又出现一张,直接退出
                    return false;
                }
            }
            if (pairCount == 6)
            {
                if (!haveSingleTile)
                {
                    // 如果遍历完毕后仍没有单张,则说明单张为最后一张
                    tile = calTileList[12];
                }
                groups.Add(new(GroupType.Pair, tile.Color, new() { tile, tile }));
                // 如果haveSingleTile为True,一定会在上述循环中赋值
                return true;
            }
            else  // 兜底,正常情况下不会走到这里
            {
                Console.WriteLine($"警告:七对子判断进行到不可能的范围,位置:{nameof(SevenPairJudge)}");
                return false;
            }

        }
        #region *DFS算法块*
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
        #endregion

        #region *序列化方法*
        /// <summary>
        /// 字节替换,需要目标字节,短字节和索引
        /// </summary>
        /// <param name="targetBytes">要被替换的较长字节</param>
        /// <param name="shortBytes">用于替换的较短字节</param>
        /// <param name="index">长字节被替换的起始索引</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void ReplaceBytes(byte[] targetBytes, byte[] shortBytes, int index)
        {
            if (index < 0 || index + shortBytes.Length > targetBytes.Length)
            {
                UnityEngine.Debug.Log(targetBytes.Length);
                UnityEngine.Debug.Log(shortBytes.Length);
                UnityEngine.Debug.Log(index);
                throw new ArgumentOutOfRangeException("替换范围超出目标数组长度");
            }
            Span<byte> spanBytes = targetBytes;
            Span<byte> sliceBytes = spanBytes.Slice(index, shortBytes.Length);
            spanBytes.CopyTo(sliceBytes);
        }
        /// <summary>
        /// 使用Span<>的字节替换,需要目标Span<>,短字节和索引
        /// </summary>
        /// <param name="targetBytes">Span<byte>类型的字节段</param>
        /// <param name="shortBytes">短字节</param>
        /// <param name="index">索引</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void ReplaceBytes(Span<byte> targetBytes, byte[] shortBytes, int index)
        {
            if (index < 0 || index + shortBytes.Length > targetBytes.Length)
            {
                UnityEngine.Debug.Log(targetBytes.Length);
                UnityEngine.Debug.Log(shortBytes.Length);
                UnityEngine.Debug.Log(index);
                throw new ArgumentOutOfRangeException("替换范围超出目标数组长度");
            }
            Span<byte> sliceBytes = targetBytes.Slice(index, shortBytes.Length);
            shortBytes.CopyTo(sliceBytes);
        }
        /// <summary>
        /// 对T的列表进行序列化的方法,T必须实现接口IByteable
        /// </summary>
        /// <typeparam name="T">类型T必须实现IByteable</typeparam>
        /// <param name="list">包含T的列表</param>
        /// <returns>列表成员依次序列化后的字节串</returns>
        public static byte[] ListToBytes<T>(List<T> list) where T : IByteable<T>
        {
            if (list.Count == 0)
            {
                return new byte[0];
            }
            else
            {
                int byteSize = list[0].ByteSize;
                Span<byte> mainBytes = new byte[list.Count * byteSize];
                for (int i = 0; i < list.Count; i++)
                {
                    ReplaceBytes(mainBytes, list[i].GetBytes(), i * byteSize);
                }
                return mainBytes.ToArray();
            }

        }
        public static byte[] ListToBytes(List<bool> list)
        {
            Span<byte> mainBytes = new byte[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                mainBytes[i] = BitConverter.GetBytes(list[i])[0];
            }
            return mainBytes.ToArray();
        }
        public static byte[] ListToBytes(List<int> list)
        {
            Span<byte> mainBytes = new byte[list.Count * 4];

            for (int i = 0; i < list.Count; i++)
            {
                ReplaceBytes(mainBytes, BitConverter.GetBytes(list[i]), 4 * i);
            }
            return mainBytes.ToArray();
        }
        /// <summary>
        /// 从字节串到列表的转换方法,列表成员类型必须实现IByteable接口
        /// </summary>
        /// <typeparam name="T">类型T必须实现IByteable接口并于GlobalFunction.ByteableInstanceDict中注册</typeparam>
        /// <param name="bytes">待输入的字节串</param>
        /// <param name="startIndex">转换起始索引</param>
        /// <param name="count">要转换的最大数量,如果转换达到该值则返回,如果遇到首位为0也返回,-1为尽可能转换直到遇到0</param>
        /// <returns></returns>
        public static List<T> BytesToList<T>(byte[] bytes, int startIndex = 0, int count = -1) where T : IByteable<T>
        {
            // 因为C# 9.0 的限制,像BytesTo()这样的静态方法无法在接口中声明
            // 因此对于BytesToList(),其限制where T : IByteable并不保证T可以从字节串转化而来
            // 对于任何实现了该接口的类型,其必须在ByteableInstanceDict内注册
            IByteable<T> instance = (IByteable<T>)ByteableInstanceDict[typeof(T)];
            List<T> list = new List<T>();
            int listIndex = startIndex;
            if (count != -1)
            {

                for (int i = 0; i < count; i++)
                {
                    if (bytes[listIndex] == 0)
                    {
                        // 将要被转化的元素的首位为0,表示列表已结束,停止转化
                        break;
                    }
                    list.Add(instance.BytesTo(bytes, listIndex));
                    listIndex += instance.ByteSize;
                }
            }
            else
            {
                while (true)
                {
                    try
                    {
                        if (bytes[listIndex] == 0)
                        {
                            // 将要被转化的元素的首位为0,表示列表已结束,停止转化
                            break;
                        }
                        list.Add(instance.BytesTo(bytes, listIndex));
                        listIndex += instance.ByteSize;
                    }
                    catch
                    {
                        // 转化出现错误,停止转化
                        break;
                    }
                }
            }
            return list;
        }
        public static List<int> BytesToIntList(byte[] bytes, int startIndex = 0, int count = -1)
        {
            List<int> list = new();
            int listIndex = startIndex;
            if (count != -1)
            {

                for (int i = 0; i < count; i++)
                {
                    if (bytes[listIndex] == 0)
                    {
                        // 将要被转化的元素的首位为0,表示列表已结束,停止转化
                        break;
                    }
                    list.Add(BitConverter.ToInt32(bytes, listIndex));
                    listIndex += 4;
                }
            }
            else
            {
                while (true)
                {
                    try
                    {
                        if (bytes[listIndex] == 0)
                        {
                            // 将要被转化的元素的首位为0,表示列表已结束,停止转化
                            break;
                        }
                        list.Add(BitConverter.ToInt32(bytes, listIndex));
                        listIndex += 4;
                    }
                    catch
                    {
                        // 转化出现错误,停止转化
                        break;
                    }
                }
            }
            return list;
        }
        public static List<bool> BytesToBoolList(byte[] bytes, int startIndex = 0, int count = -1)
        {
            List<bool> list = new();
            int listIndex = startIndex;
            if (count != -1)
            {

                for (int i = 0; i < count; i++)
                {
                    if (bytes[listIndex] == 0)
                    {
                        // 将要被转化的元素的首位为0,表示列表已结束,停止转化
                        break;
                    }
                    list.Add(BitConverter.ToBoolean(bytes, listIndex));
                    listIndex += 4;
                }
            }
            else
            {
                while (true)
                {
                    try
                    {
                        if (bytes[listIndex] == 0)
                        {
                            // 将要被转化的元素的首位为0,表示列表已结束,停止转化
                            break;
                        }
                        list.Add(BitConverter.ToBoolean(bytes, listIndex));
                        listIndex += 4;
                    }
                    catch
                    {
                        // 转化出现错误,停止转化
                        break;
                    }
                }
            }
            return list;
        }
        #endregion

        #region *测试方法*
        public static bool TestTenhe()
        {
            List<Tile> tiles = RandomCardGenerator(out int rand, true, true, true,new());
            List<Tile> chooseTiles = tiles.GetRange(0, 14);
            HandTile handTile = new();
            handTile.HandTileList = chooseTiles;
            handTile.SingleTile = chooseTiles[13];
            handTile.HandTileList.RemoveAt(13);
            handTile.PlayerNumber = 1;
            
            if (TenpaiJudge(handTile, out var successTiles))
            {
                NetworkControl.WriteDebug(handTile.HandTileList);
                return true;
            }
            return false;
        }
        public static void MainTastTenhe()
        {
            int count = 0;
            for (int i = 0; i < 100; i++)
            {

                if (TestTenhe())
                {
                    count++;
                }
            }
            NetworkControl.WriteDebug(count);
        }
        #endregion
    }
}
