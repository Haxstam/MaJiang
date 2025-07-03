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
     * 5. 因为Group类也用于算法,考虑分离以合并Group内的成员 [目前已分离为GlobalFunction.GFSGroup 和 Group]
     * 6. 国士无双不好判断,目前如果为国士无双十三面,则和牌所对应牌为z8(字牌的第八种),从而使得听牌判断方便返回
     * 7. 对于多重牌型,比如三个相邻的刻子既可以当作三个顺子也可以当作三个刻子,目前在听牌判断中通过分别优先考虑顺子/刻子来满足所有可能
     */

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
    /// 立直麻将所有类型的役种,不包含流局满贯,括号内数字为副露时番数
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
