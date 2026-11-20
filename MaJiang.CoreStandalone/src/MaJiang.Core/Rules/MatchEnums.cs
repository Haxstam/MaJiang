namespace MaJiangLib
{
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
        /// 未开始的玩家/牌局初始标记。
        /// </summary>
        InitStage,
        /// <summary>
        /// 本场重建后的粗阶段，首次摸牌由操作状态机执行。
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
        /// 本局操作已结束，结算和换局由MatchSession管理。
        /// </summary>
        EndStage,
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
        OpenTriple,
        OpenStraight,
        OpenKan,
        ClosedKan,
        AddedKan,
        Pair,
        Kita,
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
    /// 玩家所能进行的操作,包含切牌,吃,碰,杠(明杠,加杠,暗杠),立直,拔北,荣和,自摸,流局
    /// </summary>
    public enum PlayerAction : byte
    {
        // 用于鸣牌优先级判断的显示转换,必须吃<碰<荣和

        /// <summary>
        /// 跳过
        /// </summary>
        Skip = 1,
        /// <summary>
        /// 摸牌
        /// </summary>
        Take = 2,
        /// <summary>
        /// 手切
        /// </summary>
        HandCut = 3,
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
        /// 明杠
        /// </summary>
        OpenKan,
        /// <summary>
        /// 加杠
        /// </summary>
        AddedKan,
        /// <summary>
        /// 暗杠
        /// </summary>
        ClosedKan,
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
        KitaDora,  // 拔北宝牌
        UraDora,  // 里宝牌
    }
    #endregion

}
