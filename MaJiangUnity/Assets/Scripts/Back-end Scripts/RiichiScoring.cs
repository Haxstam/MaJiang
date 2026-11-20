using System;
using System.Collections.Generic;
using System.Linq;

namespace MaJiangLib
{
    /// <summary>役种结果；Han用于普通役，Yakuman用于实际役满倍数，二者不混用。</summary>
    public sealed class ScoredYaku
    {
        public YakuType Yaku { get; }
        public int Han { get; }
        public int Yakuman { get; }
        public ScoredYaku(YakuType yaku, int han = 0, int yakuman = 0)
        {
            Yaku = yaku;
            Han = han;
            Yakuman = yakuman;
        }
    }

    /// <summary>一个合法拆分的最高得点结果；BasePoints不含本场及供托。</summary>
    public sealed class ScoredHand
    {
        public int Han { get; }
        public int Fu { get; }
        public int BasePoints { get; }
        public int Yakuman { get; }
        public IReadOnlyList<ScoredYaku> Yaku { get; }
        public ScoredHand(int han, int fu, int basePoints, int yakuman, IEnumerable<ScoredYaku> yaku)
        {
            Han = han;
            Fu = fu;
            BasePoints = basePoints;
            Yakuman = yakuman;
            Yaku = Array.AsReadOnly(yaku.OrderBy(item => item.Yaku).ToArray());
        }
    }

    /// <summary>显式和牌上下文；不使用全局当前对局，暗手列表不包含单独和牌张。</summary>
    public sealed class WinContext
    {
        public IMatchInformation State;
        public HandTile Hand;
        public Tile WinningTile;
        public bool Tsumo;
        public bool Chankan;
        public bool FirstDraw;
        public IReadOnlyList<Tile> UraIndicators = Array.Empty<Tile>();
        internal bool RonEstimate;
        internal bool DeclaresRiichi;
    }

    /// <summary>纯四人立直麻将计分。穷举分解及和牌归属，先算符再算基本点，排除无役宝牌。</summary>
    public static class RiichiScoring
    {
        /// <summary>预估普通荣和中由当前牌型、风位、立直及已翻开的表宝牌确定的番数。</summary>
        public static ScoredHand EstimateRon(IMatchInformation state, HandTile hand, Tile winningTile, bool declaresRiichi)
            => Evaluate(new WinContext { State = state, Hand = hand, WinningTile = winningTile, RonEstimate = true, DeclaresRiichi = declaresRiichi });

        private readonly struct Meld
        {
            public readonly int Face;
            public readonly bool Triple;
            public readonly bool Open;
            public readonly bool Kan;
            public Meld(int face, bool triple, bool open, bool kan = false)
            {
                Face = face;
                Triple = triple;
                Open = open;
                Kan = kan;
            }
        }

        /// <summary>返回最高实际支付对应的合法结果；无牌型或无役返回null。</summary>
        public static ScoredHand Evaluate(WinContext context)
        {
            if (context?.State == null || context.Hand == null || context.WinningTile == null) { throw new ArgumentException("A complete winning context is required."); }
            if (!HandAnalysis.HasWinningShape(context.Hand, context.WinningTile)) { return null; }
            List<ScoredHand> candidates = new();
            int[] faces = context.Hand.HandTileList.Concat(new[] { context.WinningTile }).Concat(context.Hand.OpenTileList.SelectMany(group => group.Tiles)).Select(HandAnalysis.Face).ToArray();
            if (HandAnalysis.IsKokushi(context.Hand, context.WinningTile))
            {
                bool thirteen = context.Hand.HandTileList.Select(HandAnalysis.Face).Distinct().Count() == 13;
                List<ScoredYaku> yaku = CommonYakuman(context, faces);
                yaku.Add(new(thirteen ? YakuType.KokushiMusouThirteenOrphans : YakuType.KokushiMusou, yakuman: thirteen ? 2 : 1));
                candidates.Add(Finish(context, yaku, 0));
            }
            if (context.Hand.OpenTileList.Count == 0 && faces.GroupBy(face => face).Count(group => group.Count() == 2) == 7)
            {
                List<ScoredYaku> yaku = CommonYakuman(context, faces);
                if (yaku.Count == 0)
                {
                    yaku = CommonYaku(context, faces);
                    yaku.Add(new(YakuType.Chiitoitsu, 2));
                    if (faces.All(Terminal)) { yaku.Add(new(YakuType.Honroutou, 2)); }
                }
                candidates.Add(Finish(context, yaku, 25));
            }
            foreach (HandAnalysis.Shape shape in HandAnalysis.Shapes(context.Hand, context.WinningTile))
            {
                int winningFace = HandAnalysis.Face(context.WinningTile);
                if (shape.Pair == winningFace) { ScoreShape(context, faces, shape, -1, candidates); }
                for (int meld = 0; meld < shape.Melds.Count; meld++)
                {
                    (int face, bool triple) = shape.Melds[meld];
                    if (triple ? face == winningFace : winningFace >= face && winningFace <= face + 2)
                    {
                        ScoreShape(context, faces, shape, meld, candidates);
                    }
                }
            }
            bool dealer = context.Hand.PlayerNumber == context.State.CurrentDealerIndex;
            return candidates.Where(candidate => candidate != null)
                .OrderByDescending(candidate => WinningPayment(candidate.BasePoints, dealer, context.Tsumo))
                .ThenByDescending(candidate => candidate.BasePoints).ThenByDescending(candidate => candidate.Han).ThenByDescending(candidate => candidate.Fu).FirstOrDefault();
        }

        private static void ScoreShape(WinContext context, int[] faces, HandAnalysis.Shape shape, int winningMeld, List<ScoredHand> candidates)
        {
            List<Meld> melds = shape.Melds.Select((meld, index) => new Meld(meld.Face, meld.Triple, !context.Tsumo && index == winningMeld && meld.Triple)).ToList();
            melds.AddRange(context.Hand.OpenTileList.Select(group => new Meld(group.Tiles.Min(HandAnalysis.Face), group.IsTriple, group.IsOpen,
                group.GroupType == GroupType.ClosedKan || group.GroupType == GroupType.OpenKan || group.GroupType == GroupType.AddedKan)));
            List<int> triples = melds.Where(meld => meld.Triple).Select(meld => meld.Face).ToList();
            List<int> straights = melds.Where(meld => !meld.Triple).Select(meld => meld.Face).ToList();
            int concealed = melds.Count(meld => meld.Triple && !meld.Open);
            int kans = melds.Count(meld => meld.Kan);
            List<ScoredYaku> yaku = CommonYakuman(context, faces);
            if (concealed == 4) { yaku.Add(new(winningMeld == -1 ? YakuType.SuuankouTanki : YakuType.Suuankou, yakuman: winningMeld == -1 ? 2 : 1)); }
            if (triples.Count(face => face >= 31) == 3) { yaku.Add(new(YakuType.DaiSanGen, yakuman: 1)); }
            int winds = triples.Count(face => face >= 27 && face <= 30);
            if (winds == 4) { yaku.Add(new(YakuType.DaiSuuShii, yakuman: 2)); }
            else if (winds == 3 && shape.Pair >= 27 && shape.Pair <= 30) { yaku.Add(new(YakuType.ShoSuuShii, yakuman: 1)); }
            if (kans == 4) { yaku.Add(new(YakuType.Suukantsu, yakuman: 1)); }
            if (yaku.Count > 0) { candidates.Add(Finish(context, yaku, 0)); return; }

            yaku = CommonYaku(context, faces);
            int seatWind = 27 + (context.Hand.PlayerNumber - context.State.CurrentDealerIndex + 4) % 4;
            int roundWind = 26 + (int)context.State.Wind;
            if (triples.Contains(seatWind)) { yaku.Add(new(YakuType.Jikaze, 1)); }
            if (triples.Contains(roundWind)) { yaku.Add(new(YakuType.Bakaze, 1)); }
            if (triples.Contains(31)) { yaku.Add(new(YakuType.Haku, 1)); }
            if (triples.Contains(32)) { yaku.Add(new(YakuType.Hatsu, 1)); }
            if (triples.Contains(33)) { yaku.Add(new(YakuType.Chun, 1)); }
            int pairFu = (shape.Pair >= 31 ? 2 : 0) + (shape.Pair == seatWind ? 2 : 0) + (shape.Pair == roundWind ? 2 : 0);
            int waitFu = 0;
            if (winningMeld == -1) { waitFu = 2; }
            else if (!shape.Melds[winningMeld].Triple)
            {
                int start = shape.Melds[winningMeld].Face;
                int win = HandAnalysis.Face(context.WinningTile);
                if (win == start + 1 || (start % 9 == 0 && win == start + 2) || (start % 9 == 6 && win == start)) { waitFu = 2; }
            }
            bool closed = context.Hand.IsClosedHand;
            bool pinfu = closed && straights.Count == 4 && pairFu == 0 && waitFu == 0;
            if (pinfu) { yaku.Add(new(YakuType.Pinfu, 1)); }
            if (closed)
            {
                int identicalPairs = straights.GroupBy(face => face).Sum(group => group.Count() / 2);
                if (identicalPairs >= 2) { yaku.Add(new(YakuType.Ryanpeiko, 3)); }
                else if (identicalPairs == 1) { yaku.Add(new(YakuType.Iipeikou, 1)); }
            }
            if (Enumerable.Range(0, 3).Any(suit => new[] { 0, 3, 6 }.All(start => straights.Contains(suit * 9 + start)))) { yaku.Add(new(YakuType.Ittsu, closed ? 2 : 1)); }
            if (Enumerable.Range(0, 7).Any(start => Enumerable.Range(0, 3).All(suit => straights.Contains(suit * 9 + start)))) { yaku.Add(new(YakuType.SanshokuDoujun, closed ? 2 : 1)); }
            if (Enumerable.Range(0, 9).Any(number => Enumerable.Range(0, 3).All(suit => triples.Contains(suit * 9 + number)))) { yaku.Add(new(YakuType.SanshokuDoukou, 2)); }
            if (triples.Count == 4) { yaku.Add(new(YakuType.Toitoi, 2)); }
            if (concealed >= 3) { yaku.Add(new(YakuType.Sananko, 2)); }
            if (kans == 3) { yaku.Add(new(YakuType.Sankantsu, 2)); }
            if (triples.Count(face => face >= 31) == 2 && shape.Pair >= 31) { yaku.Add(new(YakuType.ShoSanGen, 2)); }
            if (faces.All(Terminal)) { yaku.Add(new(YakuType.Honroutou, 2)); }
            if (straights.Count > 0 && Terminal(shape.Pair) && melds.All(meld => meld.Triple ? Terminal(meld.Face) : meld.Face % 9 == 0 || meld.Face % 9 == 6))
            {
                bool pure = faces.All(face => face < 27);
                yaku.Add(new(pure ? YakuType.Junchan : YakuType.Chanta, pure ? closed ? 3 : 2 : closed ? 2 : 1));
            }
            int fu = pinfu && context.Tsumo ? 20 : 20 + pairFu + waitFu + (closed && !context.Tsumo ? 10 : 0) + (context.Tsumo ? 2 : 0);
            foreach (Meld meld in melds.Where(meld => meld.Triple))
            {
                fu += (meld.Kan ? 8 : 2) * (meld.Open ? 1 : 2) * (Terminal(meld.Face) ? 2 : 1);
            }
            if (fu != 20 || !pinfu || !context.Tsumo) { fu = Math.Max(30, ((fu + 9) / 10) * 10); }
            candidates.Add(Finish(context, yaku, fu));
        }

        private static List<ScoredYaku> CommonYakuman(WinContext context, int[] faces)
        {
            List<ScoredYaku> yaku = new();
            if (context.FirstDraw && context.Tsumo) { yaku.Add(new(context.Hand.PlayerNumber == context.State.CurrentDealerIndex ? YakuType.Tenho : YakuType.Chiiho, yakuman: 1)); }
            if (faces.All(face => face >= 27)) { yaku.Add(new(YakuType.Tsuuiiso, yakuman: 1)); }
            if (faces.All(face => face < 27 && (face % 9 == 0 || face % 9 == 8))) { yaku.Add(new(YakuType.Chinroto, yakuman: 1)); }
            if (faces.All(face => new[] { 19, 20, 21, 23, 25, 32 }.Contains(face))) { yaku.Add(new(YakuType.Ryuiishoku, yakuman: 1)); }
            if (context.Hand.IsClosedHand && context.Hand.OpenTileList.Count == 0 && faces.All(face => face < 27) && faces.Select(face => face / 9).Distinct().Count() == 1)
            {
                int[] numbers = Enumerable.Range(0, 9).Select(number => faces.Count(face => face % 9 == number)).ToArray();
                int[] baseNineGates = { 3, 1, 1, 1, 1, 1, 1, 1, 3 };
                if (Enumerable.Range(0, 9).All(number => numbers[number] >= baseNineGates[number]))
                {
                    numbers[context.WinningTile.Number - 1]--;
                    bool pure = numbers.SequenceEqual(baseNineGates);
                    yaku.Add(new(pure ? YakuType.ChuurenPoutou : YakuType.ChurenPoto, yakuman: pure ? 2 : 1));
                }
            }
            return yaku;
        }

        private static List<ScoredYaku> CommonYaku(WinContext context, int[] faces)
        {
            List<ScoredYaku> yaku = new();
            int seat = context.Hand.PlayerNumber;
            bool closed = context.Hand.IsClosedHand;
            if (context.State.IsRiichi[seat] || context.DeclaresRiichi)
            {
                bool doubleRiichi = context.DeclaresRiichi
                    ? MatchActionRules.IsDoubleRiichiDeclaration(context.State, seat)
                    : context.State.IsDoubleRiichi[seat];
                yaku.Add(new(doubleRiichi ? YakuType.DoubleRiichi : YakuType.Riichi, doubleRiichi ? 2 : 1));
                if (!context.RonEstimate && context.State.HaveIppatsu[seat]) { yaku.Add(new(YakuType.Ippatsu, 1)); }
            }
            if (context.Tsumo && closed) { yaku.Add(new(YakuType.Tsumo, 1)); }
            if (context.Tsumo && context.State.IsKan[seat]) { yaku.Add(new(YakuType.Rinshankaiho, 1)); }
            if (context.Chankan) { yaku.Add(new(YakuType.Chankan, 1)); }
            if (!context.RonEstimate && context.State.RemainTileCount == 0 && (!context.Tsumo || !context.State.IsKan[seat]))
            {
                yaku.Add(new(context.Tsumo ? YakuType.HaiteiRaoyui : YakuType.HoteiRaoyui, 1));
            }
            if ((closed || context.State.MatchSettingData.OpenTanyao) && faces.All(face => !Terminal(face))) { yaku.Add(new(YakuType.Tanyao, 1)); }
            if (faces.Where(face => face < 27).Select(face => face / 9).Distinct().Count() == 1)
            {
                bool honors = faces.Any(face => face >= 27);
                yaku.Add(new(honors ? YakuType.Honitsu : YakuType.Chinitsu, honors ? closed ? 3 : 2 : closed ? 6 : 5));
            }
            return yaku;
        }

        private static ScoredHand Finish(WinContext context, List<ScoredYaku> yaku, int fu)
        {
            int yakuman = yaku.Sum(item => item.Yakuman);
            if (yakuman > 0) { return new(0, 0, 8000 * yakuman, yakuman, yaku.Where(item => item.Yakuman > 0)); }
            if (yaku.Sum(item => item.Han) < context.State.MatchSettingData.MinimumYakuFan) { return null; }
            List<Tile> tiles = context.Hand.HandTileList.Concat(new[] { context.WinningTile }).Concat(context.Hand.OpenTileList.SelectMany(group => group.Tiles)).ToList();
            int CountDora(IEnumerable<Tile> indicators) => indicators.Sum(indicator => tiles.Count(tile => HandAnalysis.Face(tile) == NextDora(HandAnalysis.Face(indicator))));
            int dora = CountDora(context.State.DoraPointerList);
            if (dora > 0) { yaku.Add(new(YakuType.Dora, dora)); }
            if (!context.RonEstimate)
            {
                int red = tiles.Count(tile => tile.IsRedDora);
                int ura = context.State.IsRiichi[context.Hand.PlayerNumber] ? CountDora(context.UraIndicators) : 0;
                if (red > 0) { yaku.Add(new(YakuType.AkaDora, red)); }
                if (ura > 0) { yaku.Add(new(YakuType.UraDora, ura)); }
            }
            int han = yaku.Sum(item => item.Han);
            return new(han, fu, BasicPoints(han, fu), 0, yaku);
        }

        /// <summary>番符转基本点；不切上满贯，累计役满封顶单倍，实际役满另计。</summary>
        public static int BasicPoints(int han, int fu)
        {
            if (han < 1 || fu < 20) { throw new ArgumentOutOfRangeException(); }
            if (han >= 13) { return 8000; }
            if (han >= 11) { return 6000; }
            if (han >= 8) { return 4000; }
            if (han >= 6) { return 3000; }
            if (han == 5) { return 2000; }
            return Math.Min(2000, fu * (1 << (han + 2)));
        }

        /// <summary>单笔支付向上进位至百点。</summary>
        public static int RoundHundred(int points) => ((points + 99) / 100) * 100;
        /// <summary>不含本场供托的和牌总收入，用于比较各合法拆分。</summary>
        public static int WinningPayment(int basic, bool dealer, bool tsumo) => tsumo
            ? dealer ? 3 * RoundHundred(basic * 2) : RoundHundred(basic * 2) + 2 * RoundHundred(basic)
            : RoundHundred(basic * (dealer ? 6 : 4));
        /// <summary>34牌种编号上的宝牌循环，风牌和三元牌分别循环。</summary>
        public static int NextDora(int face) => face < 27 ? face / 9 * 9 + (face + 1) % 9 : face <= 30 ? 27 + (face - 26) % 4 : 31 + (face - 30) % 3;
        private static bool Terminal(int face) => face >= 27 || face % 9 == 0 || face % 9 == 8;
    }
}
