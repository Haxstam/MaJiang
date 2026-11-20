using System;
using System.Collections.Generic;
using System.Linq;

namespace MaJiangLib
{
    /// <summary>保存最终执行事实；响应意向由操作机私有保存，不进入日志。</summary>
    internal sealed class MatchJournal
    {
        private readonly Dictionary<Guid, List<MatchRecord>> rounds = new();
        private List<MatchRecord> records;
        private bool currentTileIsPublic;
        private Tile publicContextTile;
        public MainMatchControl AuthoritativeState { get; private set; }
        public Guid MatchId { get; } = Guid.NewGuid();
        public Guid RoundId { get; private set; }
        public int RoundGeneration { get; private set; }
        public long LatestSequence => records.Count - 1;
        public long PhaseId { get; set; }
        public byte[] FuritenFlags { get; } = new byte[4];
        public bool[] HasResponded { get; } = new bool[4];
        public int PendingDoraCount { get; set; }
        public MatchOperationStage OperationStage { get; set; }
        public int ResponseSourceSeat { get; set; } = -1;
        public Tile ResponseTile { get; set; }
        public int PendingRiichiSeat { get; set; } = -1;
        public List<Tile>[] ForbiddenDiscards { get; } = new List<Tile>[4];
        public RoundOutcomeKind Outcome { get; set; }
        public byte WinnerMask { get; set; }
        public bool WinByRobbingKan { get; set; }
        public RoundSettlementResult Settlement { get; set; }

        public MatchJournal(MainMatchControl state) { BeginRound(state); }

        public void BeginRound(MainMatchControl state)
        {
            AuthoritativeState = state ?? throw new ArgumentNullException(nameof(state));
            RoundGeneration++; RoundId = Guid.NewGuid(); PhaseId = 1;
            records = new(); rounds.Add(RoundId, records);
            currentTileIsPublic = false; publicContextTile = null;
            PendingDoraCount = 0; PendingRiichiSeat = -1; ResponseSourceSeat = -1; ResponseTile = null;
            Outcome = RoundOutcomeKind.None; WinnerMask = 0; WinByRobbingKan = false;
            OperationStage = MatchOperationStage.AwaitingDraw;
            for (int seat = 0; seat < 4; seat++)
            {
                FuritenFlags[seat] = 0; HasResponded[seat] = false; ForbiddenDiscards[seat] = new();
            }
            Record(MatchRecordKind.RoundStarted);
            foreach (Tile indicator in state.DoraPointerList) { RecordDora(indicator); }
        }

        private void Record(MatchRecordKind kind, PlayerActionData action = null, Tile indicator = null, RoundSettlementResult settlement = null)
        {
            records.Add(new MatchRecord(MatchId, RoundId, RoundGeneration, records.Count, kind, action, indicator, Outcome, settlement));
        }

        public void RecordAppliedAction(PlayerActionData action)
        {
            if (action is null) { return; }
            if (action.PlayerAction == PlayerAction.Take) { currentTileIsPublic = false; publicContextTile = null; }
            else
            {
                currentTileIsPublic = true;
                publicContextTile = action.TargetTile ?? action.Tiles?.FirstOrDefault();
            }
            Record(MatchRecordKind.ActionApplied, action);
        }

        public void RecordDora(Tile indicator) => Record(MatchRecordKind.DoraRevealed, indicator: indicator);
        public void RecordSettlement() => Record(MatchRecordKind.RoundSettled, settlement: Settlement);

        public IReadOnlyList<Guid> RoundIds => Array.AsReadOnly(rounds.Keys.ToArray());
        public IReadOnlyList<MatchRecord> GetRecords(Guid roundId, long afterSequence = -1, int? seat = null)
        {
            if (afterSequence < -1) { throw new ArgumentOutOfRangeException(nameof(afterSequence)); }
            if (seat.HasValue) { CheckSeat(seat.Value); }
            if (!rounds.TryGetValue(roundId, out List<MatchRecord> selected)) { throw new ArgumentException("Unknown round.", nameof(roundId)); }
            return Array.AsReadOnly(selected.Where(record => record.Sequence > afterSequence)
                .Select(record => seat.HasValue ? record.ForSeat(seat.Value) : record).ToArray());
        }

        public VisibleMatchState GetSnapshot(int seat)
        {
            CheckSeat(seat);
            MainMatchControl source = AuthoritativeState;
            MainMatchControl view = StateCopy.Core(source);
            view.MainTileList = null; view.PrimeTileList = null; view.Random = null;
            view.CurrentTileIndex = 0; view.PrimeTileIndex = 0; view.IsServer = false;
            bool kanResponse = OperationStage == MatchOperationStage.AddedKanResponse || OperationStage == MatchOperationStage.ClosedKanResponse;
            view.CurrentTile = kanResponse ? ResponseTile : currentTileIsPublic ? publicContextTile :
                source.CurrentPlayerIndex == seat ? source.PlayerList[seat].PlayerHandTile.SingleTile : null;
            for (int player = 0; player < 4; player++)
            {
                if (player == seat) { continue; }
                view.PlayerList[player].PlayerHandTile.HandTileList.Clear();
                view.PlayerList[player].PlayerHandTile.SingleTile = null;
            }
            return new VisibleMatchState
            {
                MatchId = MatchId, RoundId = RoundId, RoundGeneration = RoundGeneration, Sequence = LatestSequence,
                PhaseId = PhaseId, SelfSeat = seat, State = view,
                HandCounts = source.PlayerList.Select(player => player.PlayerHandTile.HandTileList.Count + (player.PlayerHandTile.SingleTile != null ? 1 : 0)).ToArray(),
                FuritenFlags = FuritenFlags[seat], HasResponded = HasResponded[seat], PendingDoraCount = PendingDoraCount,
                OperationStage = OperationStage, ResponseSourceSeat = ResponseSourceSeat, ResponseTile = ResponseTile,
                PendingRiichiSeat = PendingRiichiSeat, ForbiddenDiscards = ForbiddenDiscards[seat].ToList(),
                Outcome = Outcome, WinnerMask = WinnerMask, Settlement = StateCopy.Settlement(Settlement)
            };
        }

        public static void CheckSeat(int seat)
        {
            if (seat < 0 || seat > 3) { throw new ArgumentOutOfRangeException(nameof(seat)); }
        }
    }
}
