using System;
using System.Linq;

namespace MaJiangLib
{
    /// <summary>整场生命周期：操作机结束后仅结算一次，展示结束后换局，终局保留排名供查看与重连。</summary>
    public sealed class MatchSession
    {
        private readonly MatchJournal journal;
        private readonly Func<long> clock;
        private readonly int resultDisplayMilliseconds;
        public AuthoritativeMatchMachine Machine { get; private set; }

        public MatchSession(MatchJournal journal, Func<long> clock = null, int resultDisplayMilliseconds = 5000, AuthoritativeMatchMachine machine = null)
        {
            if (resultDisplayMilliseconds < 0 || resultDisplayMilliseconds > 60000) { throw new ArgumentOutOfRangeException(nameof(resultDisplayMilliseconds)); }
            this.journal = journal ?? throw new ArgumentNullException(nameof(journal));
            this.clock = clock ?? (() => journal.NowMilliseconds);
            this.resultDisplayMilliseconds = resultDisplayMilliseconds;
            if (machine != null && !ReferenceEquals(machine.Journal, journal)) { throw new ArgumentException("Machine belongs to a different journal.", nameof(machine)); }
            Machine = machine ?? new AuthoritativeMatchMachine(journal, this.clock);
        }

        /// <summary>调用者串行驱动；结果不等待客户端确认，因此断线不阻止换局。</summary>
        public void Tick()
        {
            Machine.Tick();
            if (Machine.Stage != MatchOperationStage.RoundEnded) { return; }
            if (journal.Settlement?.RoundId != journal.RoundId)
            {
                RoundSettlementResult result = RoundSettlement.Calculate(journal.AuthoritativeState, journal.Outcome, journal.WinnerMask,
                    journal.ResponseSourceSeat, journal.ResponseTile, journal.WinByRobbingKan);
                result.RoundId = journal.RoundId; result.RoundGeneration = journal.RoundGeneration;
                RoundSettlementCodec.Validate(result);
                journal.AuthoritativeState.PlayerPoint = result.PointsAfter.ToList();
                journal.AuthoritativeState.RiichiStickCount = result.SticksAfter;
                journal.Settlement = RoundSettlementCodec.Copy(result);
                journal.NextRoundMilliseconds = result.MatchEnded ? 0 : clock() + resultDisplayMilliseconds;
                journal.RecordAppliedAction(null);
                return;
            }
            if (!journal.Settlement.MatchEnded && clock() >= journal.NextRoundMilliseconds) { AdvanceRound(); }
        }

        private void AdvanceRound()
        {
            RoundSettlementResult previous = journal.Settlement;
            MainMatchControl core = journal.AuthoritativeState;
            core.Wind = previous.NextWind; core.Round = previous.NextRound; core.Honba = previous.NextHonba; core.CurrentDealerIndex = previous.NextDealer;
            core.ServerFourHonbaInit();
            core.CurrentPlayerIndex = core.CurrentDealerIndex;
            core.DoraPointerList.Add(core.PrimeTileList[0]);
            journal.BeginRound(core);
            Machine = new AuthoritativeMatchMachine(journal, clock);
        }
    }
}
