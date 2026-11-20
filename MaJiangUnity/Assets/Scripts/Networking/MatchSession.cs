using System;
using System.Linq;

namespace MaJiangLib
{
    /// <summary>整场生命周期：一局只结算一次，四家确认或到期后换局。</summary>
    public sealed class MatchSession
    {
        public const byte AllSeatsConfirmed = 0b1111;
        public const int DefaultResultDisplayMilliseconds = 10000;
        private readonly MatchJournal journal;
        private readonly Func<long> clock;
        private readonly int resultDisplayMilliseconds;
        public AuthoritativeMatchMachine Machine { get; private set; }

        public MatchSession(MatchJournal journal, Func<long> clock = null, int resultDisplayMilliseconds = DefaultResultDisplayMilliseconds, AuthoritativeMatchMachine machine = null)
        {
            if (resultDisplayMilliseconds < 0 || resultDisplayMilliseconds > 60000) { throw new ArgumentOutOfRangeException(nameof(resultDisplayMilliseconds)); }
            this.journal = journal ?? throw new ArgumentNullException(nameof(journal));
            this.clock = clock ?? (() => journal.NowMilliseconds);
            this.resultDisplayMilliseconds = resultDisplayMilliseconds;
            if (machine != null && !ReferenceEquals(machine.Journal, journal)) { throw new ArgumentException("Machine belongs to a different journal.", nameof(machine)); }
            Machine = machine ?? new AuthoritativeMatchMachine(journal, this.clock);
        }

        /// <summary>调用者串行驱动；到期时补齐确认，断线不阻止换局。</summary>
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
                journal.NextRoundMilliseconds = clock() + resultDisplayMilliseconds;
                journal.RecordAppliedAction(null);
                return;
            }
            if (journal.SettlementConfirmations != AllSeatsConfirmed && clock() < journal.NextRoundMilliseconds) { return; }
            SetConfirmations(AllSeatsConfirmed);
            if (!journal.Settlement.MatchEnded) { AdvanceRound(); }
        }

        /// <summary>确认按已认证席位和本局身份去重，重连继续使用原进度。</summary>
        public bool ConfirmSettlement(int seat, Guid matchId, Guid roundId)
        {
            if (seat < 0 || seat >= 4 || matchId != journal.MatchId || roundId != journal.RoundId ||
                Machine.Stage != MatchOperationStage.RoundEnded || journal.Settlement?.RoundId != roundId) { return false; }
            SetConfirmations((byte)(journal.SettlementConfirmations | (1 << seat)));
            return true;
        }

        private void SetConfirmations(byte mask)
        {
            if (mask == journal.SettlementConfirmations) { return; }
            journal.SettlementConfirmations = mask;
            if (mask == AllSeatsConfirmed) { journal.NextRoundMilliseconds = 0; }
            journal.RecordAppliedAction(null);
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
