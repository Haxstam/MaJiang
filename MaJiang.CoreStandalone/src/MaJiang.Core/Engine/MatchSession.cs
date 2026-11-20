using System;
using System.Linq;

namespace MaJiangLib
{
    /// <summary>结算是已执行操作的结果；下一局只能通过显式调用开始。</summary>
    internal sealed class MatchSession
    {
        private readonly MatchJournal journal;
        public AuthoritativeMatchMachine Machine { get; private set; }

        public MatchSession(MatchJournal journal)
        {
            this.journal = journal;
            Machine = new AuthoritativeMatchMachine(journal);
        }

        public void SettleIfEnded()
        {
            if (Machine.Stage != MatchOperationStage.RoundEnded || journal.Settlement?.RoundId == journal.RoundId) { return; }
            MainMatchControl core = journal.AuthoritativeState;
            bool win = journal.Outcome == RoundOutcomeKind.Ron || journal.Outcome == RoundOutcomeKind.Tsumo;
            if (win && Enumerable.Range(0, 4).Any(seat => (journal.WinnerMask & (1 << seat)) != 0 && core.IsRiichi[seat]))
            {
                core.PrepareUraIndicators(journal.WinnerMask, journal.ResponseTile);
            }
            RoundSettlementResult result = RoundSettlement.Calculate(core, journal.Outcome, journal.WinnerMask,
                journal.ResponseSourceSeat, journal.ResponseTile, journal.WinByRobbingKan);
            result.RoundId = journal.RoundId; result.RoundGeneration = journal.RoundGeneration;
            if (result.PointsBefore.Sum(point => (long)point) + result.SticksBefore * 1000L !=
                result.PointsAfter.Sum(point => (long)point) + result.SticksAfter * 1000L ||
                Enumerable.Range(0, 4).Any(seat => (long)result.PointsBefore[seat] + result.Deltas[seat] != result.PointsAfter[seat]))
            {
                throw new InvalidOperationException("Settlement ledger mismatch.");
            }
            core.PlayerPoint = result.PointsAfter.ToList(); core.RiichiStickCount = result.SticksAfter;
            journal.Settlement = StateCopy.Settlement(result); journal.RecordSettlement();
        }

        public string StartNextRound()
        {
            if (Machine.Stage != MatchOperationStage.RoundEnded || journal.Settlement?.RoundId != journal.RoundId) { return "RoundNotEnded"; }
            RoundSettlementResult previous = journal.Settlement;
            if (previous.MatchEnded) { return "MatchEnded"; }
            MainMatchControl core = journal.AuthoritativeState;
            core.Wind = previous.NextWind; core.Round = previous.NextRound;
            core.Honba = previous.NextHonba; core.CurrentDealerIndex = previous.NextDealer;
            core.ServerFourHonbaInit(); core.CurrentPlayerIndex = core.CurrentDealerIndex;
            core.RevealDoraIndicator();
            journal.BeginRound(core); Machine = new AuthoritativeMatchMachine(journal);
            return "Ok";
        }
    }
}
