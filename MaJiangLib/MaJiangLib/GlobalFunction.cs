using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Threading;

namespace MaJiangLib
{
    public enum GroupType
    {
        Straight,
        Triple,
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
    /// 公用的方法类
    /// </summary>
    public static class GlobalFunction
    {
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
        public static bool TingPaiJudge(ShouPai shouPai, out Dictionary<Pai, List<Group>> successPais)
        {
            successPais = new();
            // 对四副露分开看待
            if (shouPai.FuluPaiList.Count == 4)
            {
                List<Group> groups = new();
                foreach (FuluPai fuluPai in shouPai.FuluPaiList)
                {
                    groups.Add(fuluPai.Group);
                }
                groups.Add(new(GroupType.Pair, shouPai.ShouPaiList[0].Color, shouPai.ShouPaiList[0].Number));
                successPais[shouPai.ShouPaiList[0]] = groups;
                return true;
            }
            else
            {
                // 先对牌按花色分类
                List<Pai> mainPaiList = shouPai.ShouPaiList;
                List<List<int>> coloredPaiList = new();

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
                // 遍历所有类型的牌并依次进行牌型判断
                foreach (Color color in new List<Color>() { Color.Wans, Color.Tungs, Color.Bamboo, Color.Honor })
                {
                    for (int i = 1; i < 10; i++)
                    {
                        // [TODO]返回标签,考虑舍弃
                        Start:
                        // 跳过字牌序号为8,9的情况
                        if (color == Color.Honor && i >= 8)
                        {
                            break;
                        }
                        // [TODO] 等待优化
                        // 添加一张牌去判断
                        List<Group> tempGroups = new();
                        mainPaiList.Add(new(color, i));
                        coloredPaiList[(int)color].Add(i);
                        // major pair 分别存储面子和雀头的数量
                        int major = 0;
                        int pair = 0;
                        // 按照牌的花色进行DFS算法寻找面子和雀头
                        for (int j = 0; j < 4; j++)
                        {
                            // 介于字牌的特殊情况,采用更直接的方法
                            if (j == 3)
                            {
                                int[] paiCount = new int[7];
                                foreach (int num in coloredPaiList[j])
                                {
                                    paiCount[num]++;
                                }
                                for (int k = 0; k < 7; k++)
                                {
                                    // 两张字牌,即为雀头,三张字牌,即为暗刻,其余字牌数认为不听牌
                                    if (paiCount[k] == 2)
                                    {
                                        tempGroups.Add(new(GroupType.Pair, k));
                                    }
                                    else if (paiCount[k] == 3)
                                    {
                                        tempGroups.Add(new(GroupType.Triple, k));
                                    }
                                    else
                                    {
                                        // [TODO]考虑将goto改为其他方式
                                        goto Start;
                                    }
                                }
                            }
                            else
                            {
                                Node node = SingleColorJudge(coloredPaiList[j]);
                                major += node.MajorCount;
                                pair += node.PairCount;
                                foreach (Group group in node.Groups)
                                {
                                    group.Color = (Color)j;
                                    tempGroups.Add(group);
                                }
                            }
                        }
                        // 进行判断,仅当为四个面子和一个雀头时,才看做和牌
                        if (major == 4 && pair == 1)
                        {
                            successPais[new(color, i)] = tempGroups;
                        }
                        else
                        {

                        }
                    }
                }
                /*
                // 先对牌进行花色分类
                foreach (Pai pai in mainPaiList)
                {
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
                mainPaiList.Sort();

                List<Pai> tempPaiList = new();
                for (int i = 0; i < mainPaiList.Count - 1; i++)
                {
                    if (mainPaiList[i].Color != mainPaiList[i+1].Color)
                    {
                        continue;
                    }
                    else
                    {
                        if (mainPaiList[i].Number == mainPaiList[i+1].Number)
                        {
                            int major = 0;
                            foreach (List<int> ints in coloredPaiList)
                            {
                                major += SingleColorJudge(ints).MajorCount;
                            }
                            if (major == 3)
                            {

                            }
                        }
                    }
                }
                */
            }
            if (successPais.Count != 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        // 测试
        public static int Main()
        {
            ShouPai shouPai = new ShouPai();
            shouPai.ShouPaiList = new() 
            {
                new(Color.Wans,1),
                new(Color.Wans,2),
                new(Color.Wans,3),
                new(Color.Tungs,4),
                new(Color.Tungs,4),
                new(Color.Tungs,5),
                new(Color.Tungs,5),
                new(Color.Tungs,6),
                new(Color.Tungs,6),
                new(Color.Bamboo,1),
                new(Color.Bamboo,2),
                new(Color.Bamboo,3),
                new(Color.Bamboo,4),
            };
            bool isTingPai = TingPaiJudge(shouPai, out Dictionary<Pai, List<Group>> successPais);
            Console.WriteLine(isTingPai);
            foreach (KeyValuePair<Pai,List<Group>> keyValuePair in successPais)
            {
                Console.Write(keyValuePair.Key.ToString() + " ");
                foreach (Group group in keyValuePair.Value)
                {
                    Console.Write(group.ToString() + " ");
                }
            }
            return 0;
        }
        /// <summary>
        /// DFS算法,Node定义,实现 IComparable 从而可比较
        /// </summary>
        internal class Node : IComparable<Node>
        {
            public Node(int maj, int pair, List<Group> group)
            {
                MajorCount = maj;
                PairCount = pair;
                Groups = group;
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
            public List<Group> Groups { get; set; }
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
        internal static Dictionary<string, Node> Memory = new();
        internal static Node Dfs(int[] cnt)
        {
            // 通过手牌剩余序列化选项
            string key = string.Join(",", cnt.Skip(1));
            if (Memory.TryGetValue(key, out var cached))
            {
                return cached;
            }
            // 从第一张牌开始
            int i = 1;
            while (i <= 9 && cnt[i] == 0)
            {
                i++;
            }
            // 全处理完
            if (i > 9)
            {
                Node leaf = new Node(0, 0, new List<Group>());
                Memory[key] = leaf;
                return leaf;
            }

            Node best = new Node(0, 0, new List<Group>());

            // 1) 顺子
            if (i <= 7 && cnt[i] > 0 && cnt[i + 1] > 0 && cnt[i + 2] > 0)
            {
                cnt[i]--; cnt[i + 1]--; cnt[i + 2]--;
                Node sub = Dfs(cnt);
                List<Group> newGroups = new(sub.Groups);
                newGroups.Insert(0, new Group(GroupType.Straight, i, i + 1, i + 2));
                Node cand = new Node(sub.MajorCount + 1, sub.PairCount, newGroups);
                if (cand.CompareTo(best) > 0) best = cand;
                cnt[i]++;
                cnt[i + 1]++;
                cnt[i + 2]++;
            }

            // 2) 刻子
            if (cnt[i] >= 3)
            {
                cnt[i] -= 3;
                Node sub = Dfs(cnt);
                List<Group> newGroups = new(sub.Groups);
                newGroups.Insert(0, new Group(GroupType.Triple, i));
                Node cand = new Node(sub.MajorCount + 1, sub.PairCount, newGroups);
                if (cand.CompareTo(best) > 0) best = cand;
                cnt[i] += 3;
            }

            // 3) 对子
            if (cnt[i] >= 2)
            {
                cnt[i] -= 2;
                Node sub = Dfs(cnt);
                List<Group> newGroups = new(sub.Groups);
                newGroups.Insert(0, new Group(GroupType.Pair, i));
                Node cand = new Node(sub.MajorCount, sub.PairCount + 1, newGroups);
                if (cand.CompareTo(best) > 0) best = cand;
                cnt[i] += 2;
            }

            // 4) 跳过 i
            {
                int save = cnt[i];
                cnt[i] = 0;
                Node sub = Dfs(cnt);
                // 不加任何组
                if (sub.CompareTo(best) > 0) best = sub;
                cnt[i] = save;
            }

            Memory[key] = best;
            return best;
        }
        internal static Node SingleColorJudge(List<int> nums)
        {
            int[] countList = new int[10];
            foreach (int i in nums)
            {
                countList[i]++;
            }
            return Dfs(countList);
        }
    }
}
