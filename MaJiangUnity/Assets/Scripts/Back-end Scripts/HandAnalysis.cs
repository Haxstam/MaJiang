using System;
using System.Collections.Generic;
using System.Linq;

namespace MaJiangLib
{
    /// <summary>穷举真实牌种和牌型，提供听牌、立直暗杠分解与统一计分器的合法性入口。</summary>
    public static class HandAnalysis
    {
        internal sealed class Shape
        {
            public int Pair;
            public List<(int Face, bool Triple)> Melds = new();
        }

        /// <summary>赤牌与普通牌合并为0至33的真实牌种。</summary>
        public static int Face(Tile tile) => (int)tile.Color * 9 + tile.Number - 1;
        /// <summary>牌种编号还原为不带赤牌标记的实体牌值。</summary>
        public static Tile TileOf(int face) => new((Color)(face / 9), face % 9 + 1);
        private static bool Terminal(int face) => face >= 27 || face % 9 == 0 || face % 9 == 8;

        private static int[] Counts(IEnumerable<Tile> tiles)
        {
            int[] counts = new int[34];
            foreach (Tile tile in tiles)
            {
                if (tile is null || tile.Color > Color.Honor || tile.Number < 1 || tile.Number > (tile.Color == Color.Honor ? 7 : 9)) { throw new ArgumentException("Invalid physical tile."); }
                counts[Face(tile)]++;
            }
            return counts;
        }

        /// <summary>国士必须无副露，输入暗手不包含单独的和牌张。</summary>
        public static bool IsKokushi(HandTile hand, Tile winningTile)
        {
            if (hand.OpenTileList.Count != 0 || winningTile == null) { return false; }
            int[] counts = Counts(hand.HandTileList.Concat(new[] { winningTile }));
            return counts.Sum() == 14 && Enumerable.Range(0, 34).Where(Terminal).All(face => counts[face] >= 1)
                && Enumerable.Range(0, 34).Where(face => !Terminal(face)).All(face => counts[face] == 0);
        }

        /// <summary>检查普通、七对与国士牌型，拒绝本人手牌和副露组成的第五张同种牌。</summary>
        public static bool HasWinningShape(HandTile hand, Tile winningTile)
        {
            if (winningTile == null) { return false; }
            int[] own = Counts(hand.HandTileList.Concat(hand.OpenTileList.SelectMany(group => group.Tiles)));
            if (own[Face(winningTile)] >= 4) { return false; }
            int[] full = Counts(hand.HandTileList.Concat(new[] { winningTile }));
            if (full.Any(count => count > 4)) { return false; }
            return IsKokushi(hand, winningTile) || IsSevenPairs(hand, full) || Shapes(hand, winningTile).Any();
        }

        /// <summary>穷举34种真实牌，不返回旧国士算法的虚拟字牌标记。</summary>
        public static IReadOnlyList<Tile> Waits(HandTile hand)
        {
            List<Tile> waits = new();
            for (int face = 0; face < 34; face++)
            {
                Tile candidate = TileOf(face);
                if (HasWinningShape(hand, candidate)) { waits.Add(candidate); }
            }
            return waits;
        }

        private static bool IsSevenPairs(HandTile hand, int[] counts) => hand.OpenTileList.Count == 0 && counts.Count(count => count == 2) == 7;

        internal static List<Shape> Shapes(HandTile hand, Tile winningTile)
        {
            int[] counts = Counts(hand.HandTileList.Concat(new[] { winningTile }));
            List<Shape> shapes = new();
            int needed = 4 - hand.OpenTileList.Count;
            if (needed < 0 || counts.Sum() != needed * 3 + 2) { return shapes; }
            for (int pair = 0; pair < 34; pair++)
            {
                if (counts[pair] < 2) { continue; }
                counts[pair] -= 2;
                Search(counts, needed, new List<(int, bool)>(), pair, shapes);
                counts[pair] += 2;
            }
            return shapes;
        }

        private static void Search(int[] counts, int needed, List<(int Face, bool Triple)> melds, int pair, List<Shape> results)
        {
            int first = Array.FindIndex(counts, count => count > 0);
            if (first < 0)
            {
                if (needed == 0) { results.Add(new Shape { Pair = pair, Melds = melds.ToList() }); }
                return;
            }
            if (needed <= 0) { return; }
            if (counts[first] >= 3)
            {
                counts[first] -= 3; melds.Add((first, true));
                Search(counts, needed - 1, melds, pair, results);
                melds.RemoveAt(melds.Count - 1); counts[first] += 3;
            }
            if (first < 27 && first % 9 <= 6 && counts[first + 1] > 0 && counts[first + 2] > 0)
            {
                counts[first]--; counts[first + 1]--; counts[first + 2]--; melds.Add((first, false));
                Search(counts, needed - 1, melds, pair, results);
                melds.RemoveAt(melds.Count - 1); counts[first]++; counts[first + 1]++; counts[first + 2]++;
            }
        }

        /// <summary>一番缚的役合法性；宝牌不参与。该方法不返回番符或支付金额。</summary>
        public static bool HasYaku(IMatchInformation state, HandTile hand, Tile tile, bool tsumo, MatchOperationStage stage)
        {
            if (!HasWinningShape(hand, tile)) { return false; }
            if (stage == MatchOperationStage.ClosedKanResponse) { return IsKokushi(hand, tile); }
            return RiichiScoring.Evaluate(new WinContext { State = state, Hand = hand, WinningTile = tile, Tsumo = tsumo,
                Chankan = stage == MatchOperationStage.AddedKanResponse, FirstDraw = state.FirstCycleIppatsu && state.DiscardTileList[hand.PlayerNumber].Count == 0 }) != null;
        }

        /// <summary>立直后的暗杠必须是新摸第四张，所有原分解均固定包含该暗刻且等待不变。</summary>
        public static bool RiichiKanPreservesShape(HandTile hand, Tile drawn)
        {
            if (drawn == null || hand.HandTileList.Count(tile => tile == drawn) != 3) { return false; }
            IReadOnlyList<Tile> before = Waits(hand);
            if (before.Count == 0) { return false; }
            int face = Face(drawn);
            HandTile after = new(hand.PlayerNumber)
            {
                HandTileList = hand.HandTileList.Where(tile => tile != drawn).ToList(),
                OpenTileList = hand.OpenTileList.Concat(new[] { new Group(GroupType.ClosedKan, drawn.Color, hand.HandTileList.Where(tile => tile == drawn).Concat(new[] { drawn }).ToList()) }).ToList()
            };
            if (!before.Select(Face).OrderBy(value => value).SequenceEqual(Waits(after).Select(Face).OrderBy(value => value))) { return false; }
            foreach (Tile wait in before)
            {
                List<Shape> shapes = Shapes(hand, wait);
                if (shapes.Count == 0 || shapes.Any(shape => !shape.Melds.Contains((face, true)))) { return false; }
                string[] oldForms = shapes.Select(shape => Signature(shape, face)).Distinct().OrderBy(value => value).ToArray();
                string[] newForms = Shapes(after, wait).Select(shape => Signature(shape, -1)).Distinct().OrderBy(value => value).ToArray();
                if (!oldForms.SequenceEqual(newForms)) { return false; }
            }
            return true;
        }
        private static string Signature(Shape shape, int removedTriple) => shape.Pair + ":" + string.Join(",", shape.Melds.Where(meld => !(meld.Triple && meld.Face == removedTriple)).OrderBy(meld => meld.Face).ThenBy(meld => meld.Triple).Select(meld => $"{meld.Face}/{meld.Triple}"));
    }
}
