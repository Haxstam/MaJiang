using System;
using System.Collections.Generic;

namespace MaJiangLib
{
    /// <summary>
    /// 番数计算的类,番数的计算相当繁琐且庞大,暂时考虑不和GlobalFunction放在一起
    /// </summary>
    public static class FanCalculator
    {
        /// <summary>
        /// 委托输出时的小结构体,仅限在该类内部使用
        /// </summary>
        internal struct FanData
        {
            public FanData(int fan, YakuType yaku)
            {
                Fan = fan;
                Yaku = yaku;
            }
            internal int Fan { get; set; }
            internal YakuType Yaku { get; set; }
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
         * 2. 考虑将对对和,三暗刻,四暗刻,混老头这类需求刻子的役种判定放在一起
         * 3. 不相兼容且相似的役种的判定放在一起
         * 4. 因为荣和时,所和之牌组成的刻子实际上为明刻,会影响三暗刻,四暗刻的判定,这一部分判定目前考虑先放在FanCalculator中单独用一个方法去实现
         */

        /// <summary>
        /// 存储所有仅当门清时才会触发的番的判断
        /// </summary>
        internal static List<Func<HePaiData, FanData>> ClosedHandFanList { get; set; } = new List<Func<HePaiData, FanData>>()
        {

        };
        /// <summary>
        /// 特殊牌型番数判断,比如不符合四个面子一个雀头的七对子,一些役满
        /// </summary>
        internal static List<Func<HePaiData, FanData>> SpecialHandFanList { get; set; } = new List<Func<HePaiData, FanData>>()
        {

        };
        /// <summary>
        /// 普遍牌型番数判断,存储绝大多数的函数
        /// </summary>
        internal static List<Func<HePaiData, FanData>> NromalHandFanList { get; set; } = new List<Func<HePaiData, FanData>>()
        {

        };
        /// <summary>
        /// 和牌时番数计算的主方法
        /// </summary>
        /// <param name="groups">和牌时的组</param>
        /// <param name="singlePai">和牌时手牌外的第十四张牌</param>
        /// <returns></returns>
        public static int MainCalculator(HePaiData hePaiData)
        {

            return -1;
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
            Dictionary<Color, int> colorCount = new();
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
            Dictionary<Color, int> colorCount = new();
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
            Pai? pairPai = null;
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
        /// 清一色和混一色的判定
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal static FanData HonitsuAndChinitsu(HePaiData data)
        {
            bool isHonitsu = true;
            bool isChinitsu = true;
            Color color = Color.Honor;  // 一般情况下该变量一定会被赋值,如果没有改变赋值则判定为字一色,不在该方法范围内
            foreach (Group group in data.Groups)
            {
                if (group.Color != Color.Honor)
                {
                    color = group.Color;
                    break;
                }
            }
            foreach (Group group1 in data.Groups)
            {   // 既不是字牌也不是所定花色,直接跳出
                if (group1.Color != Color.Honor && group1.Color != color)
                {
                    isHonitsu = false;
                    isChinitsu = false;
                    break;
                }
                else if (group1.Color == Color.Honor)
                {   // 存在为字牌的面子,不是清一色
                    isChinitsu = false;
                }
            }
            if (isChinitsu)  // 是清一色
            {
                if (data.IsClosedHand)
                {
                    return new(6, YakuType.Chinitsu);
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
                if (!group.IsTriple || (group.Pais[0].Number != 1 && group.Pais[0].Number != 9 && group.Color != Color.Honor))
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
    }
}
