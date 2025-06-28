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
        internal static FanData Empty(HePaiData data)
        {
            return new(0, YakuType.Empty);
        }
        internal static FanData Riichi(HePaiData data)
        {
            if (MatchInformation.IsRiichi[data.Player] == true)
            {
                return new(1, YakuType.Riichi);
            }
            else
            {
                return new(0, YakuType.Empty);
            }
        }
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
        internal static FanData Tsumo(HePaiData data)
        {
            return new(0, YakuType.Empty);
        }
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
        internal static FanData Pinfu(HePaiData data)
        {
            List<Group> straightGroup = new();
            foreach (Group group in data.Groups)  // 判断面子中没有刻子且雀头非役牌
            {
                if (group.GroupType == GroupType.Pair)
                {
                    if (GlobalFunction.IsYiPai(MatchInformation, new Pai(group.Color, group.Numbers[0]), data.Player))
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
                    if (group.Color == data.SinglePai.Color && (data.SinglePai.Number - group.Numbers[0]) <= 2)
                    {
                        straightGroup.Add(group);
                    }
                }
            }
            foreach (Group group in straightGroup)  // 判断包含所和的牌的顺子中,所和牌在顺子的首尾,且顺子不是边张(即另一端不是幺九)
            {
                if (group.Numbers[0] == data.SinglePai.Number && group.Numbers[2] != 9 || group.Numbers[2] == data.SinglePai.Number && group.Numbers[0] != 1)
                {
                    return new(1, YakuType.Pinfu);
                }
            }
            return new(0, YakuType.Empty);
        }
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
                        tempPaiList.Add(new(group.Color, group.Numbers[0]));
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
        internal static FanData Ittsu(HePaiData data)
        {
            // 直接筛选所有顺子的第一个牌序号并排序,存在连续三张牌的序号==1,4,7且花色相同则返回True
            List<Pai> pais = new();
            foreach (Group group in data.Groups)
            {
                if (group.GroupType == GroupType.Straight)
                {
                    pais.Add(new(group.Color, group.Numbers[0]));
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
                    pais.Add(new(group.Color, group.Numbers[0]));
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
                    pais.Add(new(group.Color, group.Numbers[0]));
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
        /// 对对和的判定方法
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
        internal static FanData Chanta(HePaiData data)
        {
            foreach (Group group in data.Groups)
            {
                // 顺子两端必须是幺九,雀头和刻子必须是字牌或幺九
                if (group.GroupType == GroupType.Straight && group.GroupType == GroupType.MingStraight && (group.Numbers[0] != 1 || group.Numbers[2] != 9))
                {
                    break;
                }
                else if (group.GroupType == GroupType.Triple && group.GroupType == GroupType.MingTriple && (group.Numbers[0] != 1 || group.Numbers[2] != 9))
                {

                }
            }
            return new(0, YakuType.Empty);
        }
    }
}
