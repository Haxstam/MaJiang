using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MaJiangLib
{
    /// <summary>绑定具体本局和操作窗口的玩家选择；身份由连接会话另行提供。</summary>
    public sealed class MatchCommand
    {
        /// <summary>整场标识。</summary>
        public Guid MatchId;
        /// <summary>当前本场标识。</summary>
        public Guid RoundId;
        /// <summary>同一整场内的单调本场代数。</summary>
        public int RoundGeneration;
        /// <summary>选择产生时的操作窗口，不随其他玩家答复而改变。</summary>
        public long PhaseId;
        /// <summary>同座位本局内的幂等键，原请求重试时保持不变。</summary>
        public Guid CommandId;
        /// <summary>具体动作与所选实体牌，不能以此冒充其他座位。</summary>
        public PlayerActionData Action;
    }

    /// <summary>处理回执；实际牌局变化通过有序裁定日志同步。</summary>
    public sealed class MatchCommandResult
    {
        public Guid CommandId { get; }
        public bool Accepted { get; }
        public bool UsedDefault { get; }
        public string Reason { get; }
        public long PhaseId { get; }
        public long Sequence { get; }
        public MatchCommandResult(Guid commandId, bool accepted, bool usedDefault, string reason, long phaseId, long sequence)
        {
            CommandId = commandId; Accepted = accepted; UsedDefault = usedDefault; Reason = reason; PhaseId = phaseId; Sequence = sequence;
        }
    }

    /// <summary>唯一运行时对局推进入口。纯时钟注入，规则查询复用客户端代码，最终结果写入同一日志。</summary>
    public sealed class AuthoritativeMatchMachine
    {
        private sealed class CachedResult
        {
            public byte[] Request;
            public MatchCommandResult Result;
        }
        private readonly MatchJournal journal;
        private readonly Func<long> clock;
        private readonly Dictionary<(int Seat, Guid Id), CachedResult> results = new();
        private readonly Dictionary<int, PlayerActionData> responses = new();
        private readonly int[] banks = new int[4];
        private readonly Guid roundId;
        private long phaseStarted;
        private PlayerActionData pendingKan;
        private bool pendingDoubleRiichi;
        private MainMatchControl Core => journal.AuthoritativeState;
        public MatchJournal Journal => journal;
        public MatchOperationStage Stage => journal.OperationStage;

        /// <summary>接管已初始化的本局；注入时钟与日志必须使用相同毫秒时间基准。</summary>
        public AuthoritativeMatchMachine(MatchJournal journal, Func<long> clock = null, bool drawInitial = true)
        {
            this.journal = journal ?? throw new ArgumentNullException(nameof(journal));
            this.clock = clock ?? (() => journal.NowMilliseconds);
            if (Core.MatchSettingData.MinimumYakuFan != 1) { throw new NotSupportedException("The current preset supports one-yaku minimum."); }
            roundId = journal.RoundId;
            for (int seat = 0; seat < 4; seat++) { banks[seat] = Core.MatchSettingData.ThinkingTime * 1000; }
            journal.ThinkingProvider = RemainingBank;
            BeginTurn(Core.CurrentPlayerIndex, drawInitial, false, false, null);
        }

        /// <summary>纯查询，只把对应玩家暗手及共享上下文交给规则层。</summary>
        public IReadOnlyList<PlayerActionData> GetActions(int seat) => journal.RoundId == roundId
            ? MatchActionRules.GetActions(Core, Core.PlayerList[seat].PlayerHandTile, Context(seat)) : Array.Empty<PlayerActionData>();

        /// <summary>先处理已到期限，再校验身份、本局、阶段和幂等键；当前非法选择走默认路径。</summary>
        public MatchCommandResult Submit(int authenticatedSeat, MatchCommand command)
        {
            Tick();
            MatchCommandResult Reply(bool accepted, bool fallback, string reason) => new(command?.CommandId ?? Guid.Empty, accepted, fallback, reason, journal.PhaseId, journal.LatestSequence);
            if (journal.RoundId != roundId) { return Reply(false, false, "RetiredMachine"); }
            if (authenticatedSeat < 0 || authenticatedSeat > 3 || command == null || command.CommandId == Guid.Empty) { return Reply(false, false, "InvalidCommand"); }
            if (command.MatchId != journal.MatchId || command.RoundId != journal.RoundId || command.RoundGeneration != journal.RoundGeneration) { return Reply(false, false, "StaleRound"); }
            byte[] request;
            try { request = MatchCommandCodec.Encode(command); }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidDataException) { return Reply(false, false, "InvalidCommand"); }
            if (results.TryGetValue((authenticatedSeat, command.CommandId), out CachedResult cached))
            {
                return cached.Request.SequenceEqual(request) ? cached.Result : Reply(false, false, "CommandIdReused");
            }
            if (command.PhaseId != journal.PhaseId) { return Reply(false, false, "StalePhase"); }
            if (Stage == MatchOperationStage.RoundEnded) { return Reply(false, false, "RoundEnded"); }
            if (command.Action != null && command.Action.SelfNumber != authenticatedSeat) { return Reply(false, false, "ActorMismatch"); }
            if (journal.HasResponded[authenticatedSeat]) { return Reply(false, false, "AlreadyAnswered"); }
            if (results.Count >= 4096) { return Reply(false, false, "CommandLimit"); }
            bool response = IsResponse;
            if (!response && Core.CurrentPlayerIndex != authenticatedSeat) { return Reply(false, false, "NotYourTurn"); }
            PlayerActionData selected = GetActions(authenticatedSeat).FirstOrDefault(candidate => MatchActionRules.Matches(candidate, command.Action));
            bool valid = selected != null;
            Debit(authenticatedSeat);
            if (response)
            {
                selected ??= new PlayerActionData(authenticatedSeat, journal.ResponseSourceSeat, new(), null, PlayerAction.Skip);
                Respond(authenticatedSeat, selected);
            }
            else
            {
                selected ??= DefaultDiscard(authenticatedSeat);
                ExecuteTurn(selected);
            }
            MatchCommandResult result = Reply(valid, !valid, valid ? "Ok" : response ? "InvalidResponseSkipped" : "InvalidActionDefaulted");
            results[(authenticatedSeat, command.CommandId)] = new CachedResult { Request = request, Result = result };
            return result;
        }

        /// <summary>由服务器主线程调用；断线不暂停，响应超时跳过、出牌超时默认合法舍牌。</summary>
        public void Tick()
        {
            if (journal.RoundId != roundId || Stage == MatchOperationStage.RoundEnded) { return; }
            long now = clock();
            if (IsResponse)
            {
                for (int seat = 0; seat < 4; seat++)
                {
                    if (!journal.HasResponded[seat] && now >= journal.PlayerDeadlines[seat])
                    {
                        Debit(seat);
                        SaveResponse(seat, new PlayerActionData(seat, journal.ResponseSourceSeat, new(), null, PlayerAction.Skip));
                    }
                }
                if (journal.HasResponded.All(answered => answered)) { ResolveResponses(); }
            }
            else if (!journal.HasResponded[Core.CurrentPlayerIndex] && now >= journal.PlayerDeadlines[Core.CurrentPlayerIndex])
            {
                int seat = Core.CurrentPlayerIndex; Debit(seat); ExecuteTurn(DefaultDiscard(seat));
            }
        }

        private bool IsResponse => Stage == MatchOperationStage.DiscardResponse || Stage == MatchOperationStage.AddedKanResponse || Stage == MatchOperationStage.ClosedKanResponse;
        private MatchActionContext Context(int seat) => new()
        {
            Stage = Stage, SourceSeat = journal.ResponseSourceSeat, ResponseTile = journal.ResponseTile,
            HasResponded = journal.HasResponded[seat], FuritenFlags = journal.FuritenFlags[seat],
            PendingRiichiSeat = journal.PendingRiichiSeat, ForbiddenDiscards = journal.ForbiddenDiscards[seat]
        };

        private int RemainingBank(int seat)
        {
            if (journal.HasResponded[seat] || Stage == MatchOperationStage.RoundEnded) { return banks[seat]; }
            long consumed = Math.Max(0, clock() - phaseStarted - Core.MatchSettingData.BaseTime * 1000L);
            return (int)Math.Max(0, banks[seat] - consumed);
        }
        private void Debit(int seat)
        {
            banks[seat] = RemainingBank(seat);
            journal.ThinkingMilliseconds[seat] = banks[seat];
        }
        private void BeginClock(IEnumerable<int> eligible)
        {
            phaseStarted = clock(); journal.PhaseId++; journal.DeadlineMilliseconds = phaseStarted;
            HashSet<int> seats = new(eligible);
            for (int seat = 0; seat < 4; seat++)
            {
                journal.HasResponded[seat] = !seats.Contains(seat);
                journal.PlayerDeadlines[seat] = seats.Contains(seat) ? phaseStarted + Core.MatchSettingData.BaseTime * 1000L + banks[seat] : 0;
                journal.DeadlineMilliseconds = Math.Max(journal.DeadlineMilliseconds, journal.PlayerDeadlines[seat]);
            }
        }

        private void BeginTurn(int seat, bool draw, bool rinshan, bool afterCall, PlayerActionData call)
        {
            responses.Clear(); Core.CurrentPlayerIndex = seat; Core.CurrentStageType = StageType.DiscardStage;
            journal.OperationStage = afterCall ? MatchOperationStage.DiscardAfterCall : MatchOperationStage.Discard;
            journal.ResponseSourceSeat = -1; journal.ResponseTile = null;
            for (int player = 0; player < 4; player++) { if (!afterCall || player != seat) { journal.ForbiddenDiscards[player] = new(); } }
            PlayerActionData action = call;
            if (draw)
            {
                HandTile hand = Core.PlayerList[seat].PlayerHandTile;
                if (hand.SingleTile != null) { throw new InvalidOperationException("Cannot draw while a previous draw is held."); }
                Tile tile = Core.GetNextTile(rinshan); hand.SingleTile = tile; Core.CurrentTile = tile;
                if (Core.FirstCycleIppatsu && seat == Core.CurrentDealerIndex && Core.DiscardTileList[seat].Count > 0) { Core.FirstCycleIppatsu = false; }
                Core.IsKan[seat] = rinshan; journal.FuritenFlags[seat] &= 5;
                action = new PlayerActionData(seat, seat, new(), tile, PlayerAction.Take);
            }
            UpdateDiscardFuriten(seat); BeginClock(new[] { seat }); journal.RecordAppliedAction(action);
        }

        private PlayerActionData DefaultDiscard(int seat)
        {
            IReadOnlyList<PlayerActionData> actions = GetActions(seat);
            PlayerActionData cut = actions.FirstOrDefault(action => action.PlayerAction == PlayerAction.TakeCut)
                ?? actions.Where(action => action.PlayerAction == PlayerAction.HandCut).OrderBy(action => action.TargetTile).LastOrDefault();
            return cut ?? throw new InvalidOperationException("No legal default discard exists.");
        }

        private void ExecuteTurn(PlayerActionData action)
        {
            int seat = action.SelfNumber;
            if (action.PlayerAction == PlayerAction.Tsumo) { End(RoundOutcomeKind.Tsumo, new[] { seat }, seat, action.TargetTile); return; }
            if (action.PlayerAction == PlayerAction.Draw) { End(RoundOutcomeKind.NineTerminals, Array.Empty<int>(), seat, null); return; }
            if (action.PlayerAction == PlayerAction.ClosedKan || action.PlayerAction == PlayerAction.AddedKan)
            {
                RevealPendingDora(); pendingKan = MatchSyncCodec.CopyAction(action);
                BeginResponse(action.PlayerAction == PlayerAction.AddedKan ? MatchOperationStage.AddedKanResponse : MatchOperationStage.ClosedKanResponse, seat, action.TargetTile, null);
                return;
            }
            if (action.PlayerAction == PlayerAction.Riichi)
            {
                journal.PendingRiichiSeat = seat;
                pendingDoubleRiichi = Core.FirstCycleIppatsu && Core.DiscardTileList[seat].Count == 0;
            }
            HandTile hand = Core.PlayerList[seat].PlayerHandTile;
            if (action.PlayerAction == PlayerAction.HandCut || !Tile.ExactlyEquals(hand.SingleTile, action.TargetTile))
            {
                hand.HandTileList = MatchActionRules.RemoveCopy(hand.HandTileList, new[] { action.TargetTile });
                if (hand.SingleTile != null) { hand.HandTileList.Add(hand.SingleTile); }
            }
            hand.SingleTile = null;
            Core.DiscardTileList[seat].Add(action.TargetTile); Core.CurrentTile = action.TargetTile;
            if (Core.IsRiichi[seat]) { Core.HaveIppatsu[seat] = false; }
            Core.IsKan[seat] = false; UpdateDiscardFuriten(seat); RevealPendingDora();
            BeginResponse(MatchOperationStage.DiscardResponse, seat, action.TargetTile, action);
        }

        private void BeginResponse(MatchOperationStage stage, int source, Tile tile, PlayerActionData sourceAction)
        {
            responses.Clear(); journal.OperationStage = stage; journal.ResponseSourceSeat = source; journal.ResponseTile = tile;
            Core.CurrentStageType = StageType.ClaimStage; Core.CurrentPlayerIndex = source; Core.CurrentTile = tile;
            for (int seat = 0; seat < 4; seat++) { journal.HasResponded[seat] = seat == source; journal.ForbiddenDiscards[seat] = new(); }
            List<int> eligible = new();
            for (int seat = 0; seat < 4; seat++)
            {
                if (seat == source) { continue; }
                UpdateDiscardFuriten(seat);
                if (GetActions(seat).Any(action => action.PlayerAction != PlayerAction.Skip)) { eligible.Add(seat); }
                else { MarkMissedShape(seat); }
            }
            BeginClock(eligible); journal.RecordAppliedAction(sourceAction);
            if (eligible.Count == 0) { ResolveResponses(); }
        }

        private void Respond(int seat, PlayerActionData action)
        {
            SaveResponse(seat, action);
            if (journal.HasResponded.All(answered => answered)) { ResolveResponses(); }
        }
        private void SaveResponse(int seat, PlayerActionData action)
        {
            if (action.PlayerAction != PlayerAction.Ron) { MarkMissedShape(seat); }
            responses[seat] = MatchSyncCodec.CopyAction(action);
            journal.HasResponded[seat] = true; journal.PlayerDeadlines[seat] = 0;
            journal.RecordAppliedAction(null);
        }

        private void ResolveResponses()
        {
            int source = journal.ResponseSourceSeat; Tile tile = journal.ResponseTile;
            int[] winners = responses.Where(pair => pair.Value.PlayerAction == PlayerAction.Ron).Select(pair => pair.Key).OrderBy(seat => (seat - source + 4) % 4).ToArray();
            if (winners.Length > 0)
            {
                if (Stage == MatchOperationStage.AddedKanResponse || Stage == MatchOperationStage.ClosedKanResponse)
                {
                    HandTile robbed = Core.PlayerList[source].PlayerHandTile;
                    List<Tile> tiles = robbed.HandTileList.Concat(robbed.SingleTile == null ? Array.Empty<Tile>() : new[] { robbed.SingleTile }).ToList();
                    robbed.HandTileList = MatchActionRules.RemoveCopy(tiles, new[] { tile });
                    robbed.SingleTile = null;
                }
                journal.PendingRiichiSeat = -1; pendingKan = null;
                End(RoundOutcomeKind.Ron, winners, source, tile); return;
            }
            if (Stage == MatchOperationStage.AddedKanResponse || Stage == MatchOperationStage.ClosedKanResponse)
            {
                PlayerActionData kan = pendingKan; pendingKan = null; CommitKan(kan); return;
            }
            if (journal.PendingRiichiSeat >= 0)
            {
                int seat = journal.PendingRiichiSeat;
                Core.IsRiichi[seat] = true; Core.IsDoubleRiichi[seat] = pendingDoubleRiichi; Core.HaveIppatsu[seat] = true;
                Core.PlayerPoint[seat] -= 1000; Core.RiichiStickCount++; journal.PendingRiichiSeat = -1;
                journal.RecordAppliedAction(null);
            }
            RoundOutcomeKind abort = MatchActionRules.AbortAfterDiscard(Core, -1);
            if (abort != RoundOutcomeKind.None) { End(abort, Array.Empty<int>(), source, tile); return; }
            if (Core.RemainTileCount == 0) { End(RoundOutcomeKind.ExhaustiveDraw, Array.Empty<int>(), source, tile); return; }
            PlayerActionData call = responses.Values.Where(action => action.PlayerAction != PlayerAction.Skip)
                .OrderByDescending(action => action.PlayerAction == PlayerAction.Chi ? 1 : 2).ThenBy(action => (action.SelfNumber - source + 4) % 4).FirstOrDefault();
            if (call == null) { BeginTurn((source + 1) % 4, true, false, false, null); return; }
            if (call.PlayerAction == PlayerAction.OpenKan) { CommitKan(call); return; }
            ApplyCall(call);
        }

        private void ApplyCall(PlayerActionData action)
        {
            int seat = action.SelfNumber; HandTile hand = Core.PlayerList[seat].PlayerHandTile;
            hand.HandTileList = MatchActionRules.RemoveCopy(hand.HandTileList, action.Tiles);
            Core.PlayerOpenSetList[seat].Add(new Group(action.PlayerAction == PlayerAction.Chi ? GroupType.OpenStraight : GroupType.OpenTriple,
                action.TargetTile.Color, action.Tiles, action.TargetPlayerNumber, action.TargetTile));
            hand.OpenTileList = Core.PlayerOpenSetList[seat]; Core.ClearIppatsu(); Core.IsKan[seat] = false;
            journal.ForbiddenDiscards[seat] = MatchActionRules.ForbiddenAfterCall(action).ToList();
            BeginTurn(seat, false, false, true, action);
        }

        private void CommitKan(PlayerActionData action)
        {
            int seat = action.SelfNumber; HandTile hand = Core.PlayerList[seat].PlayerHandTile;
            List<Tile> full = hand.HandTileList.Concat(hand.SingleTile == null ? Array.Empty<Tile>() : new[] { hand.SingleTile }).ToList();
            hand.HandTileList = MatchActionRules.RemoveCopy(full, action.Tiles); hand.SingleTile = null;
            if (action.PlayerAction == PlayerAction.AddedKan)
            {
                Group pon = Core.PlayerOpenSetList[seat].Single(group => group.GroupType == GroupType.OpenTriple && group.Tiles[0] == action.TargetTile);
                int index = Core.PlayerOpenSetList[seat].IndexOf(pon);
                Core.PlayerOpenSetList[seat][index] = new Group(GroupType.AddedKan, pon.Color, pon.Tiles.Concat(action.Tiles).ToList()) { FuluSource = pon.FuluSource, SingleTile = pon.SingleTile };
            }
            else if (action.PlayerAction == PlayerAction.OpenKan)
            {
                Core.PlayerOpenSetList[seat].Add(new Group(GroupType.OpenKan, action.TargetTile.Color, action.Tiles, action.TargetPlayerNumber, action.TargetTile));
            }
            else { Core.PlayerOpenSetList[seat].Add(new Group(GroupType.ClosedKan, action.TargetTile.Color, action.Tiles) { FuluSource = seat }); }
            hand.OpenTileList = Core.PlayerOpenSetList[seat]; Core.ClearIppatsu(); Core.KangCount++;
            Core.KangMark = Core.KangMark == 6 ? seat : Core.KangMark == seat ? seat : 5;
            journal.PendingDoraCount++; journal.RecordAppliedAction(action);
            if (action.PlayerAction == PlayerAction.ClosedKan) { RevealPendingDora(); }
            BeginTurn(seat, true, true, false, null);
        }

        private void RevealPendingDora()
        {
            while (journal.PendingDoraCount > 0)
            {
                int index = Core.DoraPointerList.Count * 2;
                if (index >= 10) { throw new InvalidOperationException("Too many dora indicators."); }
                Core.DoraPointerList.Add(Core.PrimeTileList[index]); journal.PendingDoraCount--;
            }
        }
        private void UpdateDiscardFuriten(int seat)
        {
            bool discarded = HandAnalysis.Waits(Core.PlayerList[seat].PlayerHandTile).Any(wait => Core.DiscardTileList[seat].Any(tile => tile == wait));
            journal.FuritenFlags[seat] = (byte)((journal.FuritenFlags[seat] & 6) | (discarded ? 1 : 0));
        }
        private void MarkMissedShape(int seat)
        {
            HandTile hand = Core.PlayerList[seat].PlayerHandTile;
            bool completed = Stage == MatchOperationStage.ClosedKanResponse ? HandAnalysis.IsKokushi(hand, journal.ResponseTile) : HandAnalysis.HasWinningShape(hand, journal.ResponseTile);
            if (completed) { journal.FuritenFlags[seat] |= (byte)(Core.IsRiichi[seat] ? 4 : 2); }
        }
        private void End(RoundOutcomeKind outcome, int[] winners, int source, Tile tile)
        {
            journal.WinByRobbingKan = Stage == MatchOperationStage.AddedKanResponse || Stage == MatchOperationStage.ClosedKanResponse;
            journal.OperationStage = MatchOperationStage.RoundEnded; Core.CurrentStageType = StageType.EndStage;
            journal.Outcome = outcome; journal.WinnerMask = (byte)winners.Aggregate(0, (mask, seat) => mask | (1 << seat));
            journal.PendingRiichiSeat = -1; journal.ResponseSourceSeat = source; journal.ResponseTile = tile;
            journal.PhaseId++; journal.DeadlineMilliseconds = 0;
            for (int seat = 0; seat < 4; seat++) { journal.HasResponded[seat] = true; journal.PlayerDeadlines[seat] = 0; }
            if (winners.Length == 0) { journal.RecordAppliedAction(new PlayerActionData(source, source, new(), null, PlayerAction.Draw)); }
            else { foreach (int seat in winners) { journal.RecordAppliedAction(new PlayerActionData(seat, source, new(), tile, outcome == RoundOutcomeKind.Tsumo ? PlayerAction.Tsumo : PlayerAction.Ron)); } }
        }
    }

    /// <summary>业务命令使用独立GUID去重，不复用每次连接重置的物理消息序号。</summary>
    public static class MatchCommandCodec
    {
        /// <summary>编码本局身份、窗口、命令ID和可选动作。</summary>
        public static byte[] Encode(MatchCommand command)
        {
            using RoomPayloadWriter writer = new();
            writer.Guid(command.MatchId); writer.Guid(command.RoundId); writer.Int32(command.RoundGeneration); writer.Int64(command.PhaseId); writer.Guid(command.CommandId);
            MatchSyncCodec.WriteAction(writer, MatchSyncCodec.CopyAction(command.Action)); return writer.ToArray();
        }
        /// <summary>严格读取完整命令，拒绝尾随字节。</summary>
        public static MatchCommand Decode(byte[] bytes)
        {
            using RoomPayloadReader reader = new(bytes);
            MatchCommand command = new() { MatchId = reader.Guid(), RoundId = reader.Guid(), RoundGeneration = reader.Int32(), PhaseId = reader.Int64(), CommandId = reader.Guid(), Action = MatchSyncCodec.ReadAction(reader) };
            reader.Complete(); return command;
        }
        /// <summary>编码处理结果及裁定后阶段与日志位置。</summary>
        public static byte[] EncodeResult(MatchCommandResult result)
        {
            using RoomPayloadWriter writer = new();
            writer.Guid(result.CommandId); writer.Bool(result.Accepted); writer.Bool(result.UsedDefault); writer.Text(result.Reason, 96); writer.Int64(result.PhaseId); writer.Int64(result.Sequence);
            return writer.ToArray();
        }
        /// <summary>解码不含其他玩家私有选择的回执。</summary>
        public static MatchCommandResult DecodeResult(byte[] bytes)
        {
            using RoomPayloadReader reader = new(bytes);
            MatchCommandResult result = new(reader.Guid(), reader.Bool(), reader.Bool(), reader.Text(96), reader.Int64(), reader.Int64()); reader.Complete(); return result;
        }
    }
}
