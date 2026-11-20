using System;
using System.Collections.Generic;
using System.Linq;

namespace MaJiangLib
{
    /// <summary>结算时按规则公开的手牌；独立于下一局的私有手牌存储。</summary>
    public sealed class RevealedHand
    {
        public int Seat;
        public List<Tile> Concealed = new();
        public Tile Drawn;
        public List<Group> Groups = new();
    }

    /// <summary>单名和牌者或流满者的计分与逐座位支付，正数为收入。</summary>
    public sealed class WinnerSettlement
    {
        public int Seat;
        public int SourceSeat;
        public ScoredHand Score;
        public Tile WinningTile;
        public int PaoSeat = -1;
        public int[] Payments = new int[4];
    }

    /// <summary>完整本局结算；点数前后含本次供托分配，下一局参数在展示期间保持独立。</summary>
    public sealed class RoundSettlementResult
    {
        public Guid RoundId;
        public int RoundGeneration;
        public RoundOutcomeKind Outcome;
        public byte WinnerMask;
        public byte TenpaiMask;
        public byte NagashiMask;
        public int[] PointsBefore = new int[4];
        public int[] PointsAfter = new int[4];
        public int[] Deltas = new int[4];
        public int SticksBefore;
        public int SticksAfter;
        public List<WinnerSettlement> Winners = new();
        public List<RevealedHand> Revealed = new();
        public List<Tile> UraIndicators = new();
        public bool DealerRepeats;
        public bool MatchEnded;
        public WindType NextWind;
        public int NextRound;
        public int NextDealer;
        public int NextHonba;
        public int[] Ranking = new int[4];
    }

    /// <summary>纯本局支付及终局判定；不修改传入核心，可先验证总账再一次提交。</summary>
    public static class RoundSettlement
    {
        /// <summary>根据最终权威牌局裁定全部和牌、听牌、流满与责任支付。</summary>
        public static RoundSettlementResult Calculate(MainMatchControl core, RoundOutcomeKind outcome, byte winnerMask, int source, Tile winningTile, bool chankan = false)
        {
            if (core == null || outcome == RoundOutcomeKind.None || source < 0 || source > 3 || winnerMask > 15) { throw new ArgumentException("Invalid round outcome."); }
            bool win = outcome == RoundOutcomeKind.Ron || outcome == RoundOutcomeKind.Tsumo;
            if (win != (winnerMask != 0) || (win && winningTile == null) || (outcome == RoundOutcomeKind.Tsumo && BitCount(winnerMask) != 1)
                || (outcome == RoundOutcomeKind.Ron && (winnerMask & (1 << source)) != 0)) { throw new ArgumentException("Winning seats do not match outcome."); }
            RoundSettlementResult result = new()
            {
                Outcome = outcome, WinnerMask = winnerMask, PointsBefore = core.PlayerPoint.ToArray(), PointsAfter = core.PlayerPoint.ToArray(),
                SticksBefore = core.RiichiStickCount, SticksAfter = core.RiichiStickCount,
                NextWind = core.Wind, NextRound = core.Round, NextDealer = core.CurrentDealerIndex
            };
            if (win)
            {
                int[] winners = Enumerable.Range(0, 4).Where(seat => (winnerMask & (1 << seat)) != 0).OrderBy(seat => (seat - source + 4) % 4).ToArray();
                if (winners.Any(seat => core.IsRiichi[seat]))
                {
                    result.UraIndicators = Enumerable.Range(0, core.DoraPointerList.Count).Select(index => core.PrimeTileList[index * 2 + 1]).ToList();
                }
                foreach (int seat in winners)
                {
                    ScoredHand score = RiichiScoring.Evaluate(new WinContext
                    {
                        State = core, Hand = core.PlayerList[seat].PlayerHandTile, WinningTile = winningTile, Tsumo = outcome == RoundOutcomeKind.Tsumo,
                        Chankan = chankan, FirstDraw = core.FirstCycleIppatsu && core.DiscardTileList[seat].Count == 0,
                        UraIndicators = result.UraIndicators
                    });
                    if (score == null) { throw new InvalidOperationException("Winning outcome has no legal scored hand."); }
                    WinnerSettlement winner = new() { Seat = seat, SourceSeat = source, Score = score, WinningTile = winningTile };
                    int paoBase = 0;
                    foreach (ScoredYaku yaku in score.Yaku.Where(yaku => yaku.Yakuman > 0 && (yaku.Yaku == YakuType.DaiSanGen || yaku.Yaku == YakuType.DaiSuuShii)))
                    {
                        int pao = PaoSeat(core.PlayerOpenSetList[seat], yaku.Yaku == YakuType.DaiSanGen);
                        if (pao >= 0) { winner.PaoSeat = pao; paoBase += yaku.Yakuman * 8000; }
                    }
                    PayWin(core, winner, paoBase, outcome == RoundOutcomeKind.Tsumo, seat == winners[0]);
                    if (seat == winners[0])
                    {
                        winner.Payments[seat] += result.SticksAfter * 1000;
                        result.SticksAfter = 0;
                    }
                    result.Winners.Add(winner);
                    Reveal(result, core, seat);
                }
                result.DealerRepeats = winners.Contains(core.CurrentDealerIndex);
                result.NextHonba = result.DealerRepeats ? core.Honba + 1 : 0;
            }
            else if (outcome == RoundOutcomeKind.ExhaustiveDraw)
            {
                int[] tenpai = Enumerable.Range(0, 4).Where(seat => HandAnalysis.Waits(core.PlayerList[seat].PlayerHandTile).Count > 0).ToArray();
                result.TenpaiMask = Mask(tenpai);
                foreach (int seat in tenpai) { Reveal(result, core, seat); }
                int[] nagashi = Enumerable.Range(0, 4).Where(seat => IsNagashi(core, seat)).ToArray();
                result.NagashiMask = Mask(nagashi);
                foreach (int seat in nagashi)
                {
                    WinnerSettlement winner = new()
                    {
                        Seat = seat, SourceSeat = seat, Score = new ScoredHand(5, 0, 2000, 0, new[] { new ScoredYaku(YakuType.NagashiMangan, 5) })
                    };
                    PayWin(core, winner, 0, true, true);
                    result.Winners.Add(winner);
                }
                if (nagashi.Length == 0 && tenpai.Length > 0 && tenpai.Length < 4)
                {
                    for (int seat = 0; seat < 4; seat++) { result.Deltas[seat] += tenpai.Contains(seat) ? 3000 / tenpai.Length : -3000 / (4 - tenpai.Length); }
                }
                result.DealerRepeats = tenpai.Contains(core.CurrentDealerIndex);
                result.NextHonba = core.Honba + 1;
            }
            else
            {
                result.DealerRepeats = true;
                result.NextHonba = core.Honba + 1;
                if (outcome == RoundOutcomeKind.NineTerminals) { Reveal(result, core, source); }
            }
            foreach (WinnerSettlement winner in result.Winners)
            {
                for (int seat = 0; seat < 4; seat++) { result.Deltas[seat] += winner.Payments[seat]; }
            }
            for (int seat = 0; seat < 4; seat++) { result.PointsAfter[seat] += result.Deltas[seat]; }
            DecideContinuation(core, result);
            if (result.PointsBefore.Sum() + result.SticksBefore * 1000 != result.PointsAfter.Sum() + result.SticksAfter * 1000)
            {
                throw new InvalidOperationException("Settlement ledger does not conserve points and deposits.");
            }
            return result;
        }

        private static void PayWin(MainMatchControl core, WinnerSettlement winner, int paoBase, bool tsumo, bool receivesHonba)
        {
            bool dealer = winner.Seat == core.CurrentDealerIndex;
            int normalBase = winner.Score.BasePoints - paoBase;
            if (tsumo)
            {
                for (int payer = 0; payer < 4; payer++)
                {
                    if (payer == winner.Seat) { continue; }
                    Transfer(winner.Payments, payer, winner.Seat, RiichiScoring.RoundHundred(normalBase * (dealer || payer == core.CurrentDealerIndex ? 2 : 1)));
                }
                if (paoBase > 0) { Transfer(winner.Payments, winner.PaoSeat, winner.Seat, paoBase * (dealer ? 6 : 4)); }
                if (receivesHonba)
                {
                    if (winner.PaoSeat >= 0) { Transfer(winner.Payments, winner.PaoSeat, winner.Seat, core.Honba * 300); }
                    else { for (int payer = 0; payer < 4; payer++) { if (payer != winner.Seat) { Transfer(winner.Payments, payer, winner.Seat, core.Honba * 100); } } }
                }
            }
            else
            {
                Transfer(winner.Payments, winner.SourceSeat, winner.Seat, RiichiScoring.RoundHundred(normalBase * (dealer ? 6 : 4)));
                if (paoBase > 0)
                {
                    int amount = paoBase * (dealer ? 6 : 4);
                    if (winner.SourceSeat == winner.PaoSeat) { Transfer(winner.Payments, winner.SourceSeat, winner.Seat, amount); }
                    else
                    {
                        Transfer(winner.Payments, winner.SourceSeat, winner.Seat, amount / 2);
                        Transfer(winner.Payments, winner.PaoSeat, winner.Seat, amount / 2);
                    }
                }
                if (receivesHonba) { Transfer(winner.Payments, winner.PaoSeat >= 0 ? winner.PaoSeat : winner.SourceSeat, winner.Seat, core.Honba * 300); }
            }
        }

        /// <summary>副露按成立顺序记录；加杠保留原碰的位置/来源，最后所需面子为暗杠时不包牌。</summary>
        public static int PaoSeat(IReadOnlyList<Group> groups, bool dragons)
        {
            Group[] required = groups.Where(group => group.IsTriple && group.Color == Color.Honor && (dragons ? group.Tiles[0].Number >= 5 : group.Tiles[0].Number <= 4)).ToArray();
            int count = dragons ? 3 : 4;
            if (required.Length != count || required.Select(group => group.Tiles[0].Number).Distinct().Count() != count) { return -1; }
            Group last = required[count - 1];
            return last.IsOpen ? last.FuluSource : -1;
        }

        /// <summary>保持作者原定义：河中全为幺九且从未被别家鸣取；自己的鸣牌不取消流满。</summary>
        public static bool IsNagashi(IMatchInformation state, int seat) => state.DiscardTileList[seat].Count > 0
            && state.DiscardTileList[seat].All(tile => tile.Color == Color.Honor || tile.Number == 1 || tile.Number == 9)
            && !state.PlayerOpenSetList.Where((groups, player) => player != seat).SelectMany(groups => groups).Any(group => group.IsOpen && group.FuluSource == seat);

        private static void DecideContinuation(MainMatchControl core, RoundSettlementResult result)
        {
            int[] Rank() => Enumerable.Range(0, 4).OrderByDescending(seat => result.PointsAfter[seat]).ThenBy(seat => seat).ToArray();
            result.Ranking = Rank();
            int scheduledWind = core.MatchType == MatchType.FourMahjongEast ? 1 : 2;
            bool reached = result.PointsAfter.Any(points => points >= core.MatchSettingData.FinishPoint);
            bool bankrupt = core.MatchSettingData.HaveBankrupt && result.PointsAfter.Any(points => points < 0);
            bool extra = (int)core.Wind > scheduledWind;
            bool dealerWonMulti = result.WinnerMask != 0 && BitCount(result.WinnerMask) > 1 && result.DealerRepeats;
            bool dealerStop = (int)core.Wind == scheduledWind && core.Round == 4 && result.DealerRepeats
                && (result.Outcome == RoundOutcomeKind.Ron || result.Outcome == RoundOutcomeKind.Tsumo || result.Outcome == RoundOutcomeKind.ExhaustiveDraw)
                && result.Ranking[0] == core.CurrentDealerIndex && result.PointsAfter[core.CurrentDealerIndex] >= core.MatchSettingData.FinishPoint;
            bool normalEnd = result.Outcome == RoundOutcomeKind.Ron || result.Outcome == RoundOutcomeKind.Tsumo || result.Outcome == RoundOutcomeKind.ExhaustiveDraw;
            result.MatchEnded = bankrupt || dealerStop || (extra && reached && !dealerWonMulti && normalEnd)
                || (!result.DealerRepeats && core.Round == 4 && ((int)core.Wind == scheduledWind && reached || extra));
            if (result.MatchEnded)
            {
                int first = result.Ranking[0];
                int tail = result.SticksAfter * 1000;
                result.Deltas[first] += tail;
                result.PointsAfter[first] += tail;
                result.SticksAfter = 0;
                result.Ranking = Rank();
                return;
            }
            if (!result.DealerRepeats)
            {
                result.NextDealer = (core.CurrentDealerIndex + 1) % 4;
                result.NextRound = core.Round % 4 + 1;
                if (core.Round == 4) { result.NextWind = core.Wind + 1; }
            }
        }

        private static void Transfer(int[] payments, int payer, int winner, int amount)
        {
            if (payer == winner || amount == 0) { return; }
            payments[payer] -= amount;
            payments[winner] += amount;
        }
        private static int BitCount(byte mask) => Enumerable.Range(0, 4).Count(seat => (mask & (1 << seat)) != 0);
        private static byte Mask(IEnumerable<int> seats) => (byte)seats.Aggregate(0, (mask, seat) => mask | (1 << seat));
        private static void Reveal(RoundSettlementResult result, MainMatchControl core, int seat)
        {
            HandTile hand = core.PlayerList[seat].PlayerHandTile;
            result.Revealed.Add(new RevealedHand { Seat = seat, Concealed = hand.HandTileList.ToList(), Drawn = hand.SingleTile,
                Groups = core.PlayerOpenSetList[seat].Select(group => new Group(group.GroupType, group.Color, group.Tiles) { FuluSource = group.FuluSource, SingleTile = group.SingleTile }).ToList() });
        }
    }
}
