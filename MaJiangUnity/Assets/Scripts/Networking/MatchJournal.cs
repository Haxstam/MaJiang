using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MaJiangLib
{
    /// <summary>每名玩家自己的可见状态，既是初始化基线，也是按序日志推演的结果。</summary>
    public sealed class VisibleMatchState
    {
        public Guid MatchId;
        public Guid RoundId;
        public int RoundGeneration;
        public long Sequence = -1;
        public int SelfSeat;
        public MainMatchControl State;
        public int[] HandCounts = new int[4];
        public long PhaseId;
        public long DeadlineMilliseconds;
        public long ServerMilliseconds;
        public int ThinkingMilliseconds;
        public byte FuritenFlags;
        public bool HasResponded;
        public int PendingDoraCount;
        public MatchOperationStage OperationStage;
        public int ResponseSourceSeat = -1;
        public Tile ResponseTile;
        public int PendingRiichiSeat = -1;
        public List<Tile> ForbiddenDiscards = new();
        public RoundOutcomeKind Outcome;
        public byte WinnerMask;
        /// <summary>最近一个已完成本场的公开结算；新局仍保留，便于重连查询。</summary>
        public RoundSettlementResult Settlement;
        public long NextRoundMilliseconds;

        public IReadOnlyList<PlayerActionData> GetAvailableActions() => MatchActionRules.GetActions(State, State.PlayerList[SelfSeat].PlayerHandTile,
            new MatchActionContext { Stage = OperationStage, SourceSeat = ResponseSourceSeat, ResponseTile = ResponseTile, HasResponded = HasResponded,
                FuritenFlags = FuritenFlags, PendingRiichiSeat = PendingRiichiSeat, ForbiddenDiscards = ForbiddenDiscards });

        public long RemainingMilliseconds(long millisecondsSinceReceipt)
            => RemainingUntil(DeadlineMilliseconds, millisecondsSinceReceipt);
        /// <summary>结算展示距自动换局的剩余时间，终局为0。</summary>
        public long RemainingResultMilliseconds(long millisecondsSinceReceipt)
            => RemainingUntil(NextRoundMilliseconds, millisecondsSinceReceipt);
        private long RemainingUntil(long deadline, long millisecondsSinceReceipt)
        {
            if (deadline <= ServerMilliseconds) { return 0; }
            long remaining = deadline - ServerMilliseconds;
            return remaining - Math.Min(remaining, Math.Max(0, millisecondsSinceReceipt));
        }
        public VisibleMatchState Copy() => MatchSyncCodec.DecodeSnapshot(MatchSyncCodec.EncodeSnapshot(this));
    }

    /// <summary>裁定后的动作与状态增量。Update仅包含该接收者可见的数据。</summary>
    public sealed class VisibleMatchRecord
    {
        private readonly byte[] update;
        private readonly PlayerActionData action;
        public long Sequence { get; }
        public PlayerActionData Action => MatchSyncCodec.CopyAction(action);
        public byte[] Update => (byte[])update.Clone();
        public VisibleMatchRecord(long sequence, PlayerActionData action, byte[] update)
        {
            Sequence = sequence; this.action = MatchSyncCodec.CopyAction(action); this.update = (byte[])update.Clone();
        }
    }

    /// <summary>日志批次独立标识本局；禁止用上局序号覆盖新局。</summary>
    public sealed class MatchLogBatch
    {
        public Guid MatchId;
        public Guid RoundId;
        public int RoundGeneration;
        public long LatestSequence;
        public IReadOnlyList<VisibleMatchRecord> Records;
    }

    /// <summary>权威状态的本局日志。记录发生在规则裁定/状态变更之后，不接受网络直接写入。</summary>
    public sealed class MatchJournal
    {
        private readonly List<VisibleMatchRecord[]> records = new();
        private VisibleMatchState[] baseline;
        private VisibleMatchState[] latest;
        private readonly Func<long> clock;
        private bool currentTileIsPublic;
        private Tile publicContextTile;
        private MatchSettingData fixedSettings;
        public MainMatchControl AuthoritativeState { get; private set; }
        public Guid MatchId { get; } = Guid.NewGuid();
        public Guid RoundId { get; private set; }
        public int RoundGeneration { get; private set; }
        public long LatestSequence => records.Count - 1;
        public long NowMilliseconds => clock();
        public int RetentionWindow { get; }
        public long PhaseId { get; set; } = 1;
        public long DeadlineMilliseconds { get; set; }
        public int[] ThinkingMilliseconds { get; } = new int[4];
        public byte[] FuritenFlags { get; } = new byte[4];
        public bool[] HasResponded { get; } = new bool[4];
        public int PendingDoraCount { get; set; }
        public MatchOperationStage OperationStage { get; set; }
        public int ResponseSourceSeat { get; set; } = -1;
        public Tile ResponseTile { get; set; }
        public int PendingRiichiSeat { get; set; } = -1;
        public List<Tile>[] ForbiddenDiscards { get; } = new List<Tile>[4] { new(), new(), new(), new() };
        public long[] PlayerDeadlines { get; } = new long[4];
        public Func<int, int> ThinkingProvider { get; set; }
        public RoundOutcomeKind Outcome { get; set; }
        public byte WinnerMask { get; set; }
        public bool WinByRobbingKan { get; set; }
        public RoundSettlementResult Settlement { get; set; }
        public long NextRoundMilliseconds { get; set; }

        public MatchJournal(MainMatchControl state, Func<long> clock = null, int retentionWindow = 512)
        {
            if (retentionWindow < 1 || retentionWindow > 4096) { throw new ArgumentOutOfRangeException(nameof(retentionWindow)); }
            Stopwatch stopwatch = Stopwatch.StartNew();
            this.clock = clock ?? (() => stopwatch.ElapsedMilliseconds);
            RetentionWindow = retentionWindow;
            BeginRound(state);
        }

        /// <summary>服务端本局初始化完成后建立新基线，新的局代数和ID隔离旧包。</summary>
        public void BeginRound(MainMatchControl state)
        {
            if (state?.PlayerList?.Count != 4 || state.MatchSettingData == null) { throw new ArgumentException("An initialized four-player match is required.", nameof(state)); }
            AuthoritativeState = state;
            fixedSettings = MatchSyncCodec.CopySettings(state.MatchSettingData);
            RoundGeneration++;
            RoundId = Guid.NewGuid();
            records.Clear();
            currentTileIsPublic = false;
            publicContextTile = null;
            PhaseId = 1;
            DeadlineMilliseconds = clock() + (state.MatchSettingData.BaseTime + state.MatchSettingData.ThinkingTime) * 1000L;
            PendingDoraCount = 0;
            OperationStage = MatchOperationStage.AwaitingDraw; ResponseSourceSeat = -1; ResponseTile = null; PendingRiichiSeat = -1;
            Outcome = RoundOutcomeKind.None; WinnerMask = 0; ThinkingProvider = null;
            WinByRobbingKan = false; NextRoundMilliseconds = 0;
            for (int seat = 0; seat < 4; seat++)
            {
                ThinkingMilliseconds[seat] = state.MatchSettingData.ThinkingTime * 1000;
                FuritenFlags[seat] = 0; HasResponded[seat] = false;
                ForbiddenDiscards[seat] = new(); PlayerDeadlines[seat] = -1;
            }
            baseline = Enumerable.Range(0, 4).Select(Capture).ToArray();
            latest = baseline.Select(view => view.Copy()).ToArray();
        }

        public VisibleMatchState GetBaseline(int seat) => baseline[CheckSeat(seat)].Copy();
        public VisibleMatchState GetSnapshot(int seat)
        {
            VisibleMatchState snapshot = latest[CheckSeat(seat)].Copy();
            snapshot.ServerMilliseconds = clock();
            if (ThinkingProvider != null) { snapshot.ThinkingMilliseconds = ThinkingProvider(seat); }
            return snapshot;
        }

        /// <summary>控制状态变化也应记录，action为null表示阶段/私有裁定事件，不表示跳过。</summary>
        public void RecordAppliedAction(PlayerActionData action)
        {
            if (records.Count >= 4096) { throw new InvalidOperationException("Round journal limit exceeded."); }
            action = MatchSyncCodec.CopyAction(action);
            if (action != null)
            {
                if (action.SelfNumber < 0 || action.SelfNumber > 3) { throw new ArgumentException("Invalid action actor.", nameof(action)); }
                if (action.PlayerAction == PlayerAction.Take) { currentTileIsPublic = false; publicContextTile = null; }
                else if (action.PlayerAction != PlayerAction.Skip)
                {
                    currentTileIsPublic = true;
                    publicContextTile = action.TargetTile ?? action.Tiles?.FirstOrDefault();
                }
            }
            VisibleMatchRecord[] row = new VisibleMatchRecord[4];
            VisibleMatchState[] next = new VisibleMatchState[4];
            for (int seat = 0; seat < 4; seat++)
            {
                next[seat] = Capture(seat);
                next[seat].Sequence = records.Count;
                PlayerActionData visibleAction = MatchSyncCodec.CopyAction(action);
                if (visibleAction != null && visibleAction.PlayerAction == PlayerAction.Take && visibleAction.SelfNumber != seat)
                {
                    visibleAction.TargetTile = null; visibleAction.Tiles = new();
                }
                if (visibleAction != null && visibleAction.PlayerAction == PlayerAction.Skip) { visibleAction.TargetTile = null; visibleAction.Tiles = new(); }
                row[seat] = new VisibleMatchRecord(records.Count, visibleAction, MatchSyncCodec.EncodeUpdate(latest[seat], next[seat]));
            }
            records.Add(row);
            latest = next;
        }

        /// <summary>完整本局日志仍在服务端，网络只开放最近窗口；落后过多返回null以发送检查点。</summary>
        public MatchLogBatch GetDelta(int seat, long lastApplied, int maximumRecords = 16)
        {
            CheckSeat(seat);
            if (lastApplied < -1 || lastApplied > LatestSequence || lastApplied < LatestSequence - RetentionWindow || maximumRecords < 1 || maximumRecords > 16) { return null; }
            return new MatchLogBatch
            {
                MatchId = MatchId, RoundId = RoundId, RoundGeneration = RoundGeneration, LatestSequence = LatestSequence,
                Records = records.Skip((int)(lastApplied + 1)).Take(maximumRecords).Select(row => row[seat]).ToArray()
            };
        }

        private VisibleMatchState Capture(int seat)
        {
            MainMatchControl source = AuthoritativeState;
            MainMatchControl view = new()
            {
                MatchType = fixedSettings.MatchType, MatchSettingData = MatchSyncCodec.CopySettings(fixedSettings),
                PlayerList = new(), DiscardTileList = source.DiscardTileList.Select(list => list.ToList()).ToList(),
                PlayerOpenSetList = source.PlayerOpenSetList.Select(groups => groups.Select(MatchSyncCodec.CopyGroup).ToList()).ToList(),
                DoraPointerList = source.DoraPointerList.ToList(),
                Wind = source.Wind, Round = source.Round, Honba = source.Honba, KangCount = source.KangCount, KangMark = source.KangMark,
                PlayerPoint = source.PlayerPoint.ToList(), RemainTileCount = source.RemainTileCount, CurrentPlayerIndex = source.CurrentPlayerIndex,
                CurrentDealerIndex = source.CurrentDealerIndex, CurrentStageType = source.CurrentStageType, RiichiStickCount = source.RiichiStickCount,
                FirstCycleIppatsu = source.FirstCycleIppatsu, IsRiichi = source.IsRiichi.ToList(), IsDoubleRiichi = source.IsDoubleRiichi.ToList(),
                HaveIppatsu = source.HaveIppatsu.ToList(), IsKan = source.IsKan.ToList(),
                CurrentTile = currentTileIsPublic ? publicContextTile : source.CurrentPlayerIndex == seat ? source.PlayerList[seat].PlayerHandTile.SingleTile : null,
                IsServer = false, Initialized = true
            };
            for (int player = 0; player < 4; player++)
            {
                HandTile hand = new(player) { OpenTileList = view.PlayerOpenSetList[player] };
                if (player == seat)
                {
                    hand.HandTileList = source.PlayerList[player].PlayerHandTile.HandTileList.ToList();
                    hand.SingleTile = source.PlayerList[player].PlayerHandTile.SingleTile;
                }
                view.PlayerList.Add(new Player(new UserProfile(source.PlayerList[player].PlayerProfile.Name), player, hand, source.CurrentStageType));
            }
            byte furiten = FuritenFlags[seat];
            return new VisibleMatchState
            {
                MatchId = MatchId, RoundId = RoundId, RoundGeneration = RoundGeneration, Sequence = LatestSequence, SelfSeat = seat,
                State = view, HandCounts = source.PlayerList.Select(player => player.PlayerHandTile.HandTileList.Count + (player.PlayerHandTile.SingleTile != null ? 1 : 0)).ToArray(),
                PhaseId = PhaseId, DeadlineMilliseconds = PlayerDeadlines[seat] >= 0 ? PlayerDeadlines[seat] : DeadlineMilliseconds, ServerMilliseconds = clock(),
                ThinkingMilliseconds = ThinkingProvider != null ? ThinkingProvider(seat) : ThinkingMilliseconds[seat], FuritenFlags = furiten, HasResponded = HasResponded[seat], PendingDoraCount = PendingDoraCount,
                OperationStage = OperationStage, ResponseSourceSeat = ResponseSourceSeat, ResponseTile = ResponseTile, PendingRiichiSeat = PendingRiichiSeat,
                ForbiddenDiscards = ForbiddenDiscards[seat].ToList(), Outcome = Outcome, WinnerMask = WinnerMask,
                Settlement = Settlement == null ? null : RoundSettlementCodec.Copy(Settlement), NextRoundMilliseconds = NextRoundMilliseconds
            };
        }
        private static int CheckSeat(int seat)
        {
            if (seat < 0 || seat > 3) { throw new ArgumentOutOfRangeException(nameof(seat)); }
            return seat;
        }
    }

    /// <summary>客户端只安装基线与顺序应用裁定增量，不推测隐藏信息或再次执行服务端规则。</summary>
    public sealed class ClientMatchProjection
    {
        public VisibleMatchState Current { get; private set; }
        public bool NeedsCheckpoint { get; private set; }
        public void Clear() { Current = null; NeedsCheckpoint = false; }
        public void ReanchorClock(long serverMilliseconds)
        {
            if (Current != null) { Current.ServerMilliseconds = Math.Max(Current.ServerMilliseconds, serverMilliseconds); }
        }
        public bool Install(VisibleMatchState snapshot, bool allowSequenceCorrection = false)
        {
            MatchSyncCodec.Validate(snapshot);
            if (Current != null && Current.MatchId == snapshot.MatchId
                && (snapshot.RoundGeneration < Current.RoundGeneration || (snapshot.RoundGeneration == Current.RoundGeneration && snapshot.Sequence < Current.Sequence && !allowSequenceCorrection))) { return false; }
            if (Current != null && Current.MatchId == snapshot.MatchId && snapshot.RoundGeneration == Current.RoundGeneration && snapshot.RoundId != Current.RoundId)
            {
                throw new InvalidDataException("Conflicting round identity.");
            }
            Current = snapshot.Copy(); NeedsCheckpoint = false; return true;
        }

        public bool Apply(MatchLogBatch batch)
        {
            NeedsCheckpoint = false;
            if (Current == null) { NeedsCheckpoint = true; return false; }
            if (batch.MatchId != Current.MatchId || batch.RoundGeneration != Current.RoundGeneration || batch.RoundId != Current.RoundId)
            {
                return false;
            }
            if (batch.LatestSequence < Current.Sequence) { return true; }
            try
            {
                VisibleMatchState next = Current.Copy();
                foreach (VisibleMatchRecord record in batch.Records)
                {
                    if (record.Sequence <= next.Sequence) { continue; }
                    if (record.Sequence != next.Sequence + 1) { return false; }
                    MatchSyncCodec.ApplyUpdate(next, record.Update);
                    next.Sequence = record.Sequence;
                    MatchSyncCodec.Validate(next);
                }
                Current = next;
                return batch.Records.Count != 0 || Current.Sequence >= batch.LatestSequence;
            }
            catch (Exception exception) when (exception is InvalidDataException || exception is IOException || exception is ArgumentException || exception is NullReferenceException)
            {
                NeedsCheckpoint = true;
                return false;
            }
        }
    }
}
