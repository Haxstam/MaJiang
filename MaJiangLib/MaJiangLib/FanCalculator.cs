using System;
using System.Collections.Generic;
using System.Linq;
using static MaJiangLib.GlobalFunction;

namespace MaJiangLib
{
    /// <summary>
    /// 番数计算的类,番数的计算相当繁琐且庞大,暂时考虑不和GlobalFunction放在一起
    /// </summary>
    public static class FanCalculator
    {
        /// <summary>
        /// 委托输出时的类,存储番和役种
        /// </summary>
        public class FanData
        {
            public FanData(int fan, YakuType yaku)
            {
                Fan = fan;
                Yaku = yaku;
            }
            public int Fan { get; set; }
            public YakuType Yaku { get; set; }
            public static FanData EmptyFanData { get; set; } = new FanData(0, YakuType.Empty);

            public static bool operator ==(FanData left, FanData right)
            {
                return (left.Fan == right.Fan && left.Yaku == right.Yaku);
            }
            public static bool operator !=(FanData left, FanData right)
            {
                return !(left.Fan == right.Fan && left.Yaku == right.Yaku);
            }
            public override bool Equals(object obj)
            {
                return this == (FanData)obj;
            }
        }
        /// <summary>
        /// 比赛信息
        /// </summary>
        public static IMatchInformation MatchInformation { get; set; }

        /*
         * [TODO]
         * 对所有可能的役种进行判断相当繁琐,目前考虑根据牌型的特点进行分类,尽可能剪枝
         * 目前问题:
         * 1. 枪杠的触发较为特殊,结合国士无双,先不考虑
         * 2. 不相兼容且相似的役种的判定放在一起
         * 3. 因为荣和时,所和之牌组成的刻子实际上为明刻,会影响三暗刻,四暗刻的判定,这一部分判定目前考虑先放在FanCalculator中单独用一个方法去实现
         * 4. 多面牌型,即既可以组成刻子也可以组成顺子的牌型,目前仅考虑一色三节高或四节高的情况,在计算番数的时候会先检测,然后判断有顺子和无顺子时的番数
         */
        /// <summary>
        /// 这一部分役种受和牌状态而非和牌面子影响,包含宝牌,立直,一发,自摸,岭上,河海底,天地和,一般情况下必须要判定
        /// </summary>
        internal static List<Func<HePaiData, FanData>> SpecialHePaiList = new()
        {
            Dora,
            AkaDora,
            UraDora,
            RiichiAndDoubleRiichi,
            Ippatsu,
            Tsumo,
            Rinshankaiho,
            HaiteiAndHotei,
            TenhoAndChiiho,
        };
        /// <summary>
        /// 这一部分役种需要顺子,比如平和,一杯口两杯口,一气通贯,三色同顺
        /// </summary>
        internal static List<Func<HePaiData, FanData>> StraightHePaiList = new()
        {
            Pinfu,
            IipeikouAndRyanpeiko,
            Ittsu,
            SanshokuDoujun,

        };
        /// <summary>
        /// 这一部分役种需要刻子,比如对对和,混老头清老头字一色,三元四喜,三杠子四杠子,三色同刻
        /// </summary>
        internal static List<Func<HePaiData, FanData>> TripleHePaiList = new()
        {
            Toitoi,
            HonroutouAndChinrotoAndTsuuiiso,
            SanankoAndSuuanKou,
            SanGenAndSuuShii,
            SankantsuAndSuukantsu,
            SanshokuDoukou,
        };
        /// <summary>
        /// 这一部分和牌不在乎面子类型或是牌型特殊,比如断幺九,七对子,清混一色,绿一色和九莲,纯全混全,国士无双,通常情况下必须判定
        /// </summary>
        internal static List<Func<HePaiData, FanData>> NormalHePaiList = new()
        {
            Tanyao,
            Chiitoitsu,
            HonitsuAndChinitsuAndRyuiishokuAndCHurenPoto,
            ChantaAndJunchan,
            KokushiMusou,
        };
        /// <summary>
        /// 和牌时番数计算的方法
        /// </summary>
        /// <param name="groups">和牌时的组</param>
        /// <param name="singlePai">和牌时手牌外的第十四张牌</param>
        /// <returns></returns>
        public static RonPoint RonPointCalculator(HePaiData hePaiData)
        {
            List<FanData> YakuList = new();
            List<FanData> YakuManList = new();
            bool haveStraight = false;
            bool haveTriple = false;
            int yakuManCount = 0;
            int fanCount = 0;
            Group pairGroup = new(GroupType.Pair, Color.Wans, new() { });  // 一定会被赋值,暂当引用
            if (hePaiData.Groups[0].Color == Color.Honor && hePaiData.Groups[0].Pais[0].Number == 8)
            {   // 国士无双,跳过正常的判定
                FanData fanData = KokushiMusou(hePaiData);
                if (fanData != FanData.EmptyFanData)
                {
                    if (fanData.Fan == 13 || fanData.Fan == 26)
                    {
                        YakuManList.Add(fanData);
                        yakuManCount += fanData.Fan / 13;
                    }
                    else
                    {
                        YakuList.Add(fanData);
                        fanCount += fanData.Fan;
                    }
                }
            }
            else
            {
                foreach (Group group in hePaiData.Groups)
                {
                    if (group.IsTriple)
                    {
                        haveTriple = true;
                    }
                    else if (group.GroupType == GroupType.Straight)
                    {
                        haveStraight = true;
                    }
                    else if (group.GroupType == GroupType.Pair)
                    {   // 在此处存储雀头的类便于后续计算
                        pairGroup = group;
                    }
                }
                if (haveStraight)
                {
                    foreach (var straightYaku in StraightHePaiList)
                    {
                        FanData fanData = straightYaku.Invoke(hePaiData);
                        if (fanData != FanData.EmptyFanData)
                        {
                            if (fanData.Fan == 13 || fanData.Fan == 26)
                            {
                                YakuManList.Add(fanData);
                                yakuManCount += fanData.Fan / 13;
                            }
                            else
                            {
                                YakuList.Add(fanData);
                                fanCount += fanData.Fan;
                            }
                        }
                    }
                }
                if (haveTriple)
                {
                    List<FanData> yakuFanData = YakuPai(hePaiData);
                    if (yakuFanData.Count != 0)
                    {
                        fanCount += yakuFanData.Count;
                        YakuList.AddRange(yakuFanData);
                    }
                    foreach (var straightYaku in TripleHePaiList)
                    {
                        FanData fanData = straightYaku.Invoke(hePaiData);
                        if (fanData != FanData.EmptyFanData)
                        {
                            if (fanData.Fan == 13 || fanData.Fan == 26)
                            {
                                YakuManList.Add(fanData);
                                yakuManCount += fanData.Fan / 13;
                            }
                            else
                            {
                                YakuList.Add(fanData);
                                fanCount += fanData.Fan;
                            }
                        }
                    }
                }
                foreach (var straightYaku in SpecialHePaiList)
                {
                    FanData fanData = straightYaku.Invoke(hePaiData);
                    if (fanData != FanData.EmptyFanData)
                    {
                        if (fanData.Fan == 13 || fanData.Fan == 26)
                        {
                            YakuManList.Add(fanData);
                            yakuManCount += fanData.Fan / 13;
                        }
                        else
                        {
                            YakuList.Add(fanData);
                            fanCount += fanData.Fan;
                        }
                    }
                }
                foreach (var straightYaku in NormalHePaiList)
                {
                    FanData fanData = straightYaku.Invoke(hePaiData);
                    if (fanData != FanData.EmptyFanData)
                    {
                        if (fanData.Fan == 13 || fanData.Fan == 26)
                        {
                            YakuManList.Add(fanData);
                            yakuManCount += fanData.Fan / 13;
                        }
                        else
                        {
                            YakuList.Add(fanData);
                            fanCount += fanData.Fan;
                        }
                    }
                }
            }

            int fu = 0;
            int basePoint = 0;
            RonPoint ronPoint;
            if (YakuManList.Count != 0)
            {   // n倍役满为 32000 * n 
                basePoint = yakuManCount * 8000;
                // 如果为役满,不考虑符数
                ronPoint = new(yakuManCount * 13, fu, basePoint, YakuManList);
            }
            else
            {   // 没有役满
                switch (fanCount)
                {
                    case 13:
                        basePoint = 8000;
                        break;
                    case 11:
                    case 12:
                        basePoint = 6000;
                        break;
                    case >= 8 and <= 10:
                        basePoint = 4000;
                        break;
                    case 6:
                    case 7:
                        basePoint = 3000;
                        break;
                    case 5:
                        basePoint = 2000;
                        break;
                    case >= 1 and <= 4:
                        basePoint = fu * (int)Math.Pow(2, fanCount + 2);
                        if (basePoint >= 2000)
                        {
                            basePoint = 2000;
                        }
                        break;
                    case 0: // 一般不会有
                        basePoint = 0;
                        break;
                }
                // 符的计算
                bool isFuSkip = false;
                foreach (FanData fanData in YakuList)
                {
                    if (fanData.Yaku == YakuType.Chiitoitsu)
                    {   // 七对子锁定25符
                        fu = 25;
                        isFuSkip = true;
                        break;
                    }
                    else if (fanData.Yaku == YakuType.Pinfu && hePaiData.IsIsumo)
                    {   // 平和自摸锁定20符
                        fu = 20;
                        isFuSkip = true;
                    }
                    else if (!haveTriple && !hePaiData.IsIsumo && !hePaiData.IsClosedHand)
                    {   // 无刻子,副露平和型荣和,锁定30符
                        fu = 30;
                        isFuSkip = true;
                    }
                }
                if (!isFuSkip)
                {
                    // 底符20符
                    fu += 20;
                    foreach (Group group in hePaiData.Groups)
                    {
                        // 面子符的计算:首先,根据刻杠增加次方数,明刻暗刻明杠暗杠分别为+1到+4,若为幺九则再+1
                        // 根据次方数计算该面子的符:2/4/8/16/32,顺子不加符
                        int tempFuCount = 0;
                        if (group.GroupType == GroupType.MingTriple)
                        {
                            tempFuCount += 1;
                        }
                        else if (group.GroupType == GroupType.Triple)
                        {
                            tempFuCount += 2;
                        }
                        else if (group.GroupType == GroupType.MingKang || group.GroupType == GroupType.JiaKang)
                        {
                            tempFuCount += 3;
                        }
                        else if (group.GroupType == GroupType.AnKang)
                        {
                            tempFuCount += 4;
                        }
                        if (group.IsTriple || group.Pais[0].Number == 1 || group.Pais[0].Number == 9 || group.Color == Color.Honor)
                        {
                            tempFuCount += 1;
                        }
                        if (tempFuCount != 0)
                        {
                            fu += (int)Math.Pow(2, tempFuCount);
                        }
                    }
                    if (IsYiPai(MatchInformation, pairGroup.Pais[0], hePaiData.Player))
                    {   // 如果雀头是役牌,记2符,连风也为2符
                        fu += 2;
                    }
                    if (hePaiData.IsClosedHand && !hePaiData.IsIsumo)
                    {   //门前清荣和加10符
                        fu += 10;
                    }
                    if (hePaiData.IsIsumo)
                    {   // 自摸加2符
                        fu += 2;
                    }
                    if (pairGroup.Pais[0] == hePaiData.SinglePai)
                    {   // 单骑听雀头加2符
                        fu += 2;
                    }
                    foreach (Group group1 in hePaiData.Groups)
                    {   // 所和牌为边坎张加2符
                        if (group1.GroupType == GroupType.Straight)
                        {
                            if (group1.Pais[1] == hePaiData.SinglePai)
                            {
                                fu += 2;
                                break;
                            }
                            else if ((group1.Pais[0].Number == 7 && group1.Pais[0] == hePaiData.SinglePai) || (group1.Pais[2].Number == 3 && group1.Pais[2] == hePaiData.SinglePai))
                            {
                                fu += 2;
                                break;
                            }
                        }
                    }
                    if (fu % 10 > 0)
                    {   // 最后符数向上进位到十位
                        fu += 10 - fu % 10;
                    }
                }
                ronPoint = new(fanCount, fu, basePoint, YakuList);
            }
            return ronPoint;
        }
        /// <summary>
        /// 提供宝牌指示牌计算获得宝牌列表
        /// </summary>
        /// <param name="doraPointers"></param>
        /// <returns></returns>
        public static List<Pai> DoraListCalculator(List<Pai> doraPointers)
        {
            List<Pai> doraList = new();
            foreach (Pai doraPointer in doraPointers)
            {
                if (doraPointer.Color == Color.Honor)
                {
                    if (doraPointer.Number == 7)
                    {
                        doraList.Add(new(Color.Honor, 1));
                    }
                    else
                    {
                        doraList.Add(new(Color.Honor, doraPointer.Number + 1));
                    }
                }
                else
                {
                    if (doraPointer.Number == 9)
                    {
                        doraList.Add(new(doraPointer.Color, 1));
                    }
                    else
                    {
                        doraList.Add(new(doraPointer.Color, doraPointer.Number + 1));
                    }
                }
            }
            return doraList;
        }
        /// <summary>
        /// 空判定
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData Empty(HePaiData data)
        {
            return new(0, YakuType.Empty);
        }
        /// <summary>
        /// 宝牌判定,按照刻子还是杠子设定宝牌个数
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData Dora(HePaiData data)
        {
            int doraCount = 0;
            List<Pai> doraList = DoraListCalculator(MatchInformation.DoraList);
            foreach (Group group in data.Groups)
            {
                if (group.IsTriple)
                {
                    if (doraList.Contains(group.Pais[0]))
                    {
                        if (group.GroupType == GroupType.Triple || group.GroupType == GroupType.MingTriple)
                        {
                            doraCount += 3;
                        }
                        else
                        {
                            doraCount += 4;
                        }
                    }
                }
                else
                {
                    foreach (Pai pai in group.Pais)
                    {
                        // 寻找符合数字和花色的牌
                        if (doraList.Contains(pai))
                        {
                            doraCount++;
                        }
                    }
                }
            }
            if (doraCount != 0)
            {
                return new(doraCount, YakuType.Dora);
            }
            else
            {
                return new(0, YakuType.Empty);
            }
        }
        /// <summary>
        /// 红宝牌判定
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData AkaDora(HePaiData data)
        {
            int doraCount = 0;
            foreach (Group group in data.Groups)
            {
                foreach (Pai pai in group.Pais)
                {
                    if (pai.IsRedDora)
                    {
                        doraCount++;
                    }
                }
            }
            if (doraCount != 0)
            {
                return new(doraCount, YakuType.AkaDora);
            }
            else
            {
                return new(0, YakuType.Empty);
            }

        }
        /// <summary>
        /// 里宝牌判定,和宝牌相同,要求立直
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData UraDora(HePaiData data)
        {
            // 必须立直
            if (MatchInformation.IsRiichi[data.Player] == true)
            {
                int doraCount = 0;
                List<Pai> doraList = DoraListCalculator(MatchInformation.UraDoraList);
                foreach (Group group in data.Groups)
                {
                    if (group.IsTriple)
                    {
                        if (doraList.Contains(group.Pais[0]))
                        {
                            if (group.GroupType == GroupType.Triple || group.GroupType == GroupType.MingTriple)
                            {
                                doraCount += 3;
                            }
                            else
                            {
                                doraCount += 4;
                            }
                        }
                    }
                    else
                    {
                        foreach (Pai pai in group.Pais)
                        {
                            // 寻找符合数字和花色的牌
                            if (doraList.Contains(pai))
                            {
                                doraCount++;
                            }
                        }
                    }
                }
                // 里宝牌较为特殊,即使没有一张也要提示
                return new(doraCount, YakuType.UraDora);
            }
            else
            {
                return new(0, YakuType.Empty);
            }
        }
        /// <summary>
        /// 役牌统合判定,返回役种列表,因而需要单独判断
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static List<FanData> YakuPai(HePaiData data)
        {
            List<FanData> fanList = new List<FanData>();
            foreach (Group group in data.Groups)
            {
                if (group.Color == Color.Honor && group.IsTriple)
                {
                    if (group.Pais[0].Number >= 5)
                    {
                        if (group.Pais[0].Number == 5)
                        {
                            fanList.Add(new(1, YakuType.Haku));
                        }
                        else if (group.Pais[0].Number == 6)
                        {
                            fanList.Add(new(1, YakuType.Hatsu));
                        }
                        else if (group.Pais[0].Number == 7)
                        {
                            fanList.Add(new(1, YakuType.Chun));
                        }
                    }
                    if (group.Pais[0].Number == (data.Player - MatchInformation.Round + 1))
                    {
                        fanList.Add(new(1, YakuType.Jikaze));
                    }
                    else if (group.Pais[0].Number == (int)MatchInformation.Wind)
                    {
                        fanList.Add(new(1, YakuType.Bakaze));
                    }
                }
            }
            return fanList;
        }
        /// <summary>
        /// 立直和两立直的判定
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData RiichiAndDoubleRiichi(HePaiData data)
        {
            if (MatchInformation.IsDoubleRiichi[data.Player] == true)
            {
                return new(2, YakuType.DoubleRiichi);
            }
            else if (MatchInformation.IsRiichi[data.Player] == true)
            {
                return new(1, YakuType.Riichi);
            }
            else
            {
                return new(0, YakuType.Empty);
            }
        }
        /// <summary>
        /// 一发的判定
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData Ippatsu(HePaiData data)
        {
            if (MatchInformation.HaveIppatsu[data.Player] == true && MatchInformation.IsRiichi[data.Player] == true)
            {
                return new(1, YakuType.Ippatsu);
            }
            else
            {
                return new(0, YakuType.Empty);
            }
        }
        /// <summary>
        /// 门前清自摸和的判定
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData Tsumo(HePaiData data)
        {
            if (data.IsClosedHand && MatchInformation.CurrentPlayer == data.Player)
            {
                return new(1, YakuType.Tsumo);
            }
            else
            {
                return new(0, YakuType.Empty);
            }
        }
        /// <summary>
        /// 断幺九的判定
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData Tanyao(HePaiData data)
        {
            if (data.SinglePai.Color == Color.Honor || data.SinglePai.Number == 1 || data.SinglePai.Number == 9)
            {
                return new(0, YakuType.Empty);
            }
            foreach (Pai pai in data.ShouPai.ShouPaiList)
            {
                if (pai.Color == Color.Honor || pai.Number == 1 || pai.Number == 9)
                {
                    return new(0, YakuType.Empty);
                }
            }
            return new(1, YakuType.Tanyao);
        }
        /// <summary>
        /// 平和的判定
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData Pinfu(HePaiData data)
        {
            List<Group> straightGroup = new();
            foreach (Group group in data.Groups)  // 判断面子中没有刻子且雀头非役牌
            {
                if (group.GroupType == GroupType.Pair)
                {
                    if (GlobalFunction.IsYiPai(MatchInformation, group.Pais[0], data.Player))
                    {
                        return new(0, YakuType.Empty);
                    }
                }
                else if (group.IsTriple)
                {
                    return new(0, YakuType.Empty);
                }
                else if (group.GroupType == GroupType.Straight)
                {
                    // 判断所和的牌是否在所属的顺子中
                    if (group.Color == data.SinglePai.Color && (data.SinglePai.Number - group.Pais[0].Number) <= 2)
                    {
                        straightGroup.Add(group);
                    }
                }
            }
            foreach (Group group in straightGroup)  // 判断包含所和的牌的顺子中,所和牌在顺子的首尾,且顺子不是边张(即另一端不是幺九)
            {
                if ((group.Pais[0].Number == data.SinglePai.Number && group.Pais[2].Number != 9) || (group.Pais[2].Number == data.SinglePai.Number && group.Pais[0].Number != 1))
                {
                    return new(1, YakuType.Pinfu);
                }
            }
            return new(0, YakuType.Empty);
        }
        /// <summary>
        /// 岭上开花的判定
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData Rinshankaiho(HePaiData data)
        {
            if (MatchInformation.IsKan[data.Player])
            {
                return new(1, YakuType.Rinshankaiho);
            }
            else
            {
                return new(0, YakuType.Empty);
            }
        }
        /// <summary>
        /// 河海底的判定
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData HaiteiAndHotei(HePaiData data)
        {
            if (MatchInformation.RemainPaiCount == 0)
            {
                if (data.IsIsumo)
                {
                    return new(1, YakuType.HaiteiRaoyui);
                }
                else
                {
                    return new(1, YakuType.HoteiRaoyui);
                }
            }
            else
            {
                return new(0, YakuType.Empty);
            }
        }
        /// <summary>
        /// 一杯口和两杯口的判定
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData IipeikouAndRyanpeiko(HePaiData data)
        {
            if (data.IsClosedHand == false)
            {
                return new(0, YakuType.Empty);
            }
            else
            {
                int count = 0;
                List<Pai> tempPaiList = new();
                // 将面子中所有顺子的第一张牌作为判断依据
                foreach (Group group in data.Groups)
                {
                    if (group.GroupType == GroupType.Straight)
                    {
                        tempPaiList.Add(group.Pais[0]);
                    }
                }
                tempPaiList.Sort();
                for (int i = 0; i < tempPaiList.Count - 1; i++)
                {
                    if (tempPaiList[i] == tempPaiList[i + 1])
                    {
                        // 存在一组合适的牌,计数+1,跳过下一组比较(防止一色三节高等牌型被反复计算)
                        count++;
                        i++;
                    }
                }
                if (count == 1)
                {
                    return new(1, YakuType.Iipeikou);
                }
                else if (count == 2)
                {
                    return new(3, YakuType.Ryanpeiko);
                }
                else
                {
                    return new(0, YakuType.Empty);
                }
            }
        }
        /// <summary>
        /// 一气通贯的判定
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData Ittsu(HePaiData data)
        {
            // 直接筛选所有顺子的第一个牌序号并排序,存在连续三张牌的序号==1,4,7且花色相同则返回True
            List<Pai> pais = new();
            foreach (Group group in data.Groups)
            {
                if (group.GroupType == GroupType.Straight)
                {
                    pais.Add(group.Pais[0]);
                }
            }
            pais.Sort();
            if (pais.Count >= 3)
            {
                for (int i = 0; i < pais.Count - 2; i++)
                {
                    if (pais[i].Number == 1 && pais[i + 1].Number == 4 && pais[i + 2].Number == 7 && pais[i].Color == pais[i + 2].Color)
                    {
                        if (data.IsClosedHand)
                        {
                            return new(2, YakuType.Ittsu);
                        }
                        else
                        {
                            return new(1, YakuType.Ittsu);
                        }
                    }
                }
            }
            return new(0, YakuType.Empty);
        }
        /// <summary>
        /// 三色同顺的判定
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData SanshokuDoujun(HePaiData data)
        {
            // 筛选所有顺子并记录花色出现次数,标记存在两个同花色顺子的花色,仅当各种花色都出现一次后再判断
            List<Pai> pais = new();
            Dictionary<Color, int> colorCount = new()
            {
                { Color.Wans, 0 },
                { Color.Tungs, 0 },
                { Color.Bamboo, 0 },
                { Color.Honor, 0 },
            };
            Color? majorColor = null;
            foreach (Group group in data.Groups)
            {
                if (group.GroupType == GroupType.Straight && group.GroupType == GroupType.MingStraight)
                {
                    pais.Add(group.Pais[0]);
                    colorCount[group.Color]++;
                    if (colorCount[group.Color] >= 2)
                    {
                        majorColor = group.Color;
                    }
                }
            }
            if (colorCount[Color.Wans] >= 1 && colorCount[Color.Tungs] >= 1 && colorCount[Color.Bamboo] >= 1)
            {
                pais.Sort();
                if (majorColor == null && pais[0].Number == pais[1].Number && pais[0].Number == pais[2].Number)
                {
                    if (data.IsClosedHand)
                    {
                        return new(2, YakuType.SanshokuDoujun);
                    }
                    else
                    {
                        return new(1, YakuType.SanshokuDoujun);
                    }
                }
                // 如果存在同一花色两个顺子的情况,先判断是万的情况,再判断不是万的
                int count = 0;
                if (majorColor == Color.Wans)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (pais[i].Number == pais[3].Number)
                        {
                            count++;
                        }
                    }
                }
                else if (majorColor != Color.Wans)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (pais[i].Number == pais[0].Number)
                        {
                            count++;
                        }
                    }
                }
                if (count >= 3)
                {
                    if (data.IsClosedHand)
                    {
                        return new(2, YakuType.SanshokuDoujun);
                    }
                    else
                    {
                        return new(1, YakuType.SanshokuDoujun);
                    }
                }
            }
            return new(0, YakuType.Empty);
        }
        /// <summary>
        /// 三色同刻的判定
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData SanshokuDoukou(HePaiData data)
        {
            // 整体实现思路和三色同顺相同
            // 筛选所有刻子并记录花色出现次数,标记存在两个同花色刻子的花色,仅当各种花色都出现一次后再判断
            List<Pai> pais = new();
            Dictionary<Color, int> colorCount = new()
            {
                { Color.Wans, 0 },
                { Color.Tungs, 0 },
                { Color.Bamboo, 0 },
                { Color.Honor, 0 },

            };
            Color? majorColor = null;
            foreach (Group group in data.Groups)
            {
                if (group.IsTriple)
                {
                    pais.Add(group.Pais[0]);
                    colorCount[group.Color]++;
                    if (colorCount[group.Color] >= 2)
                    {
                        majorColor = group.Color;
                    }
                }
            }
            if (colorCount[Color.Wans] >= 1 && colorCount[Color.Tungs] >= 1 && colorCount[Color.Bamboo] >= 1)
            {
                pais.Sort();
                if (majorColor == null && pais[0].Number == pais[1].Number && pais[0].Number == pais[2].Number)
                {
                    return new(2, YakuType.SanshokuDoukou);
                }
                // 如果存在同一花色两个刻子的情况,先判断是万的情况,再判断不是万的
                int count = 0;
                if (majorColor == Color.Wans)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (pais[i].Number == pais[3].Number)
                        {
                            count++;
                        }
                    }
                }
                else if (majorColor != Color.Wans)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (pais[i].Number == pais[0].Number)
                        {
                            count++;
                        }
                    }
                }
                if (count >= 3)
                {
                    return new(2, YakuType.SanshokuDoukou);
                }
            }
            return new(0, YakuType.Empty);
        }
        /// <summary>
        /// 三暗刻,四暗刻,四暗刻单骑的判定
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData SanankoAndSuuanKou(HePaiData data)
        {
            int AnTripleCount = 0;
            Pai? pairPai = new(Color.Wans, 1);  // 一定会被赋值,填写一万为占位符
            foreach (Group group in data.Groups)
            {
                if (group.GroupType == GroupType.Triple || group.GroupType == GroupType.AnKang)
                {
                    AnTripleCount++;
                }
                if (group.GroupType == GroupType.Pair)
                {
                    pairPai = group.Pais[0];
                }
            }
            if (AnTripleCount == 3)
            {
                return new(2, YakuType.Sananko);
            }
            else if (AnTripleCount == 4)
            {
                if (data.SinglePai == pairPai)
                {
                    return new(26, YakuType.SuuankouTanki);
                }
                else
                {
                    return new(13, YakuType.Suuankou);
                }
            }
            return new(0, YakuType.Empty);
        }
        /// <summary>
        /// 七对子的判定,如果和牌面子全为对子则为七对子
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData Chiitoitsu(HePaiData data)
        {
            if (data.Groups.All(group => group.GroupType == GroupType.Pair))
            {
                return new(2, YakuType.Chiitoitsu);
            }
            else
            {
                return new(0, YakuType.Empty);
            }
        }
        /// <summary>
        /// 对对和的判定
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData Toitoi(HePaiData data)
        {
            int TripleCount = 0;
            foreach (Group group in data.Groups)
            {
                if (group.IsTriple)
                {
                    TripleCount++;
                }
            }
            if (TripleCount == 4)
            {
                return new(2, YakuType.Toitoi);
            }
            return new(0, YakuType.Empty);
        }
        /// <summary>
        /// 纯全带和混全带的判定
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData ChantaAndJunchan(HePaiData data)
        {
            bool isChanta = true;
            bool isJunchan = true;
            foreach (Group group in data.Groups)
            {
                // 顺子两端必须是幺九
                if ((group.GroupType == GroupType.Straight || group.GroupType == GroupType.MingStraight) && (group.Pais[0].Number != 1 && group.Pais[2].Number != 9))
                {
                    isChanta = false;
                    isJunchan = false;
                    break;
                }
                else if ((group.IsTriple || group.GroupType == GroupType.Pair) && (group.Pais[0].Number != 1 && group.Pais[0].Number != 9 && group.Color != Color.Honor))
                {   // 刻子和雀头必须是幺九或字牌
                    isChanta = false;
                    isJunchan = false;
                    break;
                }
                else if ((group.IsTriple || group.GroupType == GroupType.Pair) && group.Color == Color.Honor)
                {   // 再划分,存在字牌雀头刻子则不为纯全带
                    isJunchan = false;
                }
            }
            if (isJunchan)  // 是纯全
            {
                if (data.IsClosedHand)
                {
                    return new(3, YakuType.Junchan);
                }
                else
                {
                    return new(2, YakuType.Junchan);
                }
            }
            else if (isChanta && !isJunchan)  // 不是纯全是混全
            {
                if (data.IsClosedHand)
                {
                    return new(2, YakuType.Chanta);
                }
                else
                {
                    return new(1, YakuType.Chanta);
                }
            }
            else  // 都不是
            {
                return new(0, YakuType.Empty);
            }

        }
        /// <summary>
        /// 判断面子是否符合绿一色的方法,因为实在太过繁琐不好放在if()里维护,故单开方法
        /// </summary>
        /// <param name="group"></param>
        /// <returns></returns>
        internal static bool RyuiishokuPaiJudge(Group group)
        {
            if (group.Color == Color.Bamboo)  // 是否为条子
            {
                if (group.IsTriple || group.GroupType == GroupType.Pair)  // 是条子 && 是刻杠子或雀头
                {
                    if (new int[] { 2, 3, 4, 6, 8 }.Contains(group.Pais[0].Number))  // 是条子 && 是刻杠子或雀头 && 序号在 2 3 4 6 8 里
                    {
                        return true;
                    }
                }
                else if (group.GroupType == GroupType.Straight)  // 是条子 && 是顺子
                {
                    if (group.Pais[0].Number == 2)  // 是条子 && 是顺子 && 第一张序号是2
                    {
                        return true;
                    }
                }
            }
            else if ((group.IsTriple || group.GroupType == GroupType.Pair) && group.Color == Color.Honor)  // 是刻杠子或雀头 && 是字牌
            {
                if (group.Pais[0].Number == 6)  // 是刻杠子或雀头 && 是字牌 && 是发
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 清一色,混一色,绿一色和九莲宝灯的判定,九莲宝灯要求是清一色
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData HonitsuAndChinitsuAndRyuiishokuAndCHurenPoto(HePaiData data)
        {
            bool isHonitsu = true;
            bool isChinitsu = true;
            bool isRyuiishoku = true;
            int[] numberCount = new int[10];
            Color color = Color.Honor;  // 一般情况下该变量一定会被赋值,如果没有改变赋值则判定为字一色,不在该方法范围内
            foreach (Group group in data.Groups)
            {
                if (group.Color != Color.Honor)
                {
                    color = group.Color;
                    break;
                }
            }
            if (color == Color.Honor)
            {
                return new(0, YakuType.Empty);
            }
            foreach (Group group1 in data.Groups)
            {
                ////合在一起的判断
                //if (!(((group1.IsTriple || group1.GroupType == GroupType.Pair) && ((group1.Color == Color.Bamboo && new int[] { 2, 3, 4, 6, 8 }.Contains(group1.Pais[0].Number)) || (group1.Color == Color.Honor && group1.Pais[0].Number == 4)))
                //|| ((group1.GroupType == GroupType.Straight) && group1.Pais[0].Number == 2)))
                //{
                //    isRyuiishoku = false;
                //}
                if (!RyuiishokuPaiJudge(group1))
                {
                    isRyuiishoku = false;
                }
                if (group1.Color != Color.Honor && group1.Color != color)
                {   // 既不是字牌也不是所定花色,直接跳出
                    isHonitsu = false;
                    isChinitsu = false;
                    break;
                }
                else if (group1.Color == Color.Honor)
                {   // 存在为字牌的面子,不是清一色
                    isChinitsu = false;
                }
                else if (group1.Color == color)
                {
                    foreach (Pai pai in group1.Pais)
                    {
                        numberCount[pai.Number]++;
                    }
                }
            }
            if (isRyuiishoku)
            {
                return new(13, YakuType.Ryuiishoku);
            }
            if (isChinitsu)  // 是清一色
            {
                if (data.IsClosedHand)
                {
                    bool isSkipPoto = false;
                    int extraPai = 0;
                    bool haveExtraPai = false;
                    for (int i = 1; i < 10; i++)
                    {   // 九莲宝灯判定方法:先找到多出的那一张(幺九即为第四张,中张即为第二张),如果同时有多个符合条件的张,跳过
                        if ((i == 1 && numberCount[i] == 4) || (i >= 2 && i <= 8 && numberCount[i] == 2) || (i == 9 && numberCount[i] == 4))
                        {
                            if (haveExtraPai == true)
                            {
                                extraPai = 0;
                                break;
                            }
                            else
                            {
                                haveExtraPai = true;
                                extraPai = i;
                            }

                        }
                        if ((i == 1 && numberCount[i] <= 2) || (i >= 2 && i <= 8 && numberCount[i] >= 3) || (i == 9 && numberCount[i] <= 2))
                        {   // 如果幺九少于3张,2到8大于2张,则不是九莲宝灯
                            isSkipPoto = true;
                            break;
                        }
                    }
                    if (haveExtraPai && extraPai != 0 && !isSkipPoto)
                    {   // 如果存在多出的牌且仅一张,也即除去这张牌后,手牌符合1112345678999,若所和牌为此牌,则为纯正九莲宝灯,反之为九莲宝灯
                        if (data.SinglePai.Number == extraPai)
                        {
                            return new(26, YakuType.ChuurenPoutou);
                        }
                        else
                        {
                            return new(13, YakuType.CHurenPoto);
                        }
                    }
                    else
                    {
                        return new(6, YakuType.Chinitsu);
                    }
                }
                else
                {
                    return new(5, YakuType.Chinitsu);
                }
            }
            else if (isHonitsu && !isChinitsu)  // 不是清一色是混一色
            {
                if (data.IsClosedHand)
                {
                    return new(3, YakuType.Honitsu);
                }
                else
                {
                    return new(2, YakuType.Honitsu);
                }
            }
            else
            {
                return new(0, YakuType.Empty);
            }
        }
        /// <summary>
        /// 混老头,清老头和字一色的判定,三种牌型都要求全是刻子且没有中张
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData HonroutouAndChinrotoAndTsuuiiso(HePaiData data)
        {
            bool isHonroutou = true;
            bool isChinroto = true;
            bool isTsuuiiso = true;
            foreach (Group group in data.Groups)
            {   // 排除不是刻子的面子,或是面子牌既不是幺九也不是字牌的和牌
                if ((!group.IsTriple && !(group.GroupType == GroupType.Pair))|| (group.Pais[0].Number != 1 && group.Pais[0].Number != 9 && group.Color != Color.Honor))
                {
                    return new(0, YakuType.Empty);
                }
                else if (group.Color == Color.Honor)
                {   // 存在字牌,则排除清老头
                    isChinroto = false;
                }
                else if (group.Color != Color.Honor)
                {   // 存在幺九牌,则排除字一色
                    isTsuuiiso = false;
                }
            }
            if (isTsuuiiso)
            {
                return new(13, YakuType.Tsuuiiso);
            }
            else if (isChinroto)
            {
                return new(13, YakuType.Chinroto);
            }
            else if (!isTsuuiiso && !isChinroto && isHonroutou)
            {   // 既不是清老头,也不是字一色,那就是混老头
                return new(2, YakuType.Honroutou);
            }
            return new(0, YakuType.Empty);
        }
        /// <summary>
        /// 小三元,大三元,小四喜和大四喜的判定,三种牌型都是字牌且不相互兼容
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData SanGenAndSuuShii(HePaiData data)
        {
            int[] HonorCount = new int[8];
            foreach (Group group in data.Groups)
            {   // 如果面子和雀头是字牌,就增加对应字牌数量
                if (group.Color == Color.Honor)
                {
                    if (group.IsTriple)
                    {
                        HonorCount[group.Pais[0].Number] += 3;
                    }
                    else if (group.GroupType == GroupType.Pair)
                    {
                        HonorCount[group.Pais[0].Number] += 2;
                    }
                }
            }
            // 三元牌总和为8是小三元,总和为9是大三元,四风牌总和为11是小四喜,总和为12是大四喜
            if (HonorCount[5] + HonorCount[6] + HonorCount[7] == 8)
            {
                return new(2, YakuType.ShoSanGen);
            }
            else if (HonorCount[5] + HonorCount[6] + HonorCount[7] == 9)
            {
                return new(13, YakuType.DaiSanGen);
            }
            else if (HonorCount[1] + HonorCount[2] + HonorCount[3] + HonorCount[4] == 11)
            {
                return new(13, YakuType.ShoSuuShii);
            }
            else if (HonorCount[1] + HonorCount[2] + HonorCount[3] + HonorCount[4] == 12)
            {
                return new(26, YakuType.DaiSuuShii);
            }
            else
            {
                return new(0, YakuType.Empty);
            }
        }
        /// <summary>
        /// 天地和的判定
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData TenhoAndChiiho(HePaiData data)
        {
            if (MatchInformation.FirstCycleIppatsu && data.IsIsumo)
            {
                if (data.Player == MatchInformation.CurrentBanker)
                {
                    return new(13, YakuType.Tenho);
                }
                else
                {
                    return new(13, YakuType.Chiiho);
                }
            }
            return new(0, YakuType.Empty);
        }
        /// <summary>
        /// 三杠子和四杠子的判定
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData SankantsuAndSuukantsu(HePaiData data)
        {   // 记录手牌中杠子的个数(或许可以将判断放在MatchInformation中)
            int KangCount = 0;
            foreach (Group group in data.Groups)
            {
                if (group.GroupType == GroupType.AnKang || group.GroupType == GroupType.MingKang || group.GroupType == GroupType.JiaKang)
                {
                    KangCount++;
                }
            }
            if (KangCount == 3)
            {
                return new(2, YakuType.Sankantsu);
            }
            else if (KangCount == 4)
            {
                return new(13, YakuType.Suukantsu);
            }
            else
            {
                return new(0, YakuType.Empty);
            }
        }
        /// <summary>
        /// 国士无双十三面的判定,根据z8判定,如果听牌为z8则为十三面,反之为国士无双
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData KokushiMusou(HePaiData data)
        {
            if (data.Groups[0].Pais[0] == new Pai(Color.Honor, 8))
            {
                if (data.SinglePai == new Pai(Color.Honor, 8))
                {
                    return new(26, YakuType.KokushiMusouThirteenOrphans);
                }
                else
                {
                    return new(13, YakuType.KokushiMusou);
                }
            }
            else
            {
                return new(0, YakuType.Empty);
            }
        }
    }
}
