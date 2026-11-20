using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using MaJiangLib;

namespace MaJiang.Mortal
{
    /// <summary>模型建议；Command 只来自本地当前合法菜单，读取时复制。</summary>
    public sealed class MortalDecision
    {
        private readonly MatchCommand command;
        public int Seat { get; }
        public bool Available => command != null;
        public string Reason { get; }
        public string ReactionJson { get; }
        public string FollowupJson { get; }
        public MatchCommand Command => command == null ? null : MjaiActionMapper.Copy(command);

        internal MortalDecision(int seat, string reason, MatchCommand command = null, JsonObject reaction = null, JsonObject followup = null)
        {
            Seat = seat; Reason = reason; this.command = command == null ? null : MjaiActionMapper.Copy(command);
            ReactionJson = reaction?.ToJsonString(); FollowupJson = followup?.ToJsonString();
        }
    }

    /// <summary>实际操作反馈与模型同步错误分开：模型故障不能把已执行的操作伪装成未执行。</summary>
    public sealed class MortalStepResult
    {
        public OperationFeedback Feedback { get; internal set; }
        public IReadOnlyDictionary<int, string> ModelErrors { get; internal set; }
    }

    /// <summary>外部宿主适配层。核心仍是被动引擎，模型进程和协议历史都在本层。</summary>
    public sealed class MortalMatchAdapter : IAsyncDisposable
    {
        /// <summary>现有模型的国士抢暗杠兼容策略；由适配器提供合法跳过，仍需宿主提交。</summary>
        public const string KokushiRobClosedKanPolicy = "skip";
        private sealed class ModelSlot
        {
            public IMjaiClient Client;
            public JsonObject Reaction;
            public Guid RoundId;
            public long PhaseId;
            public string Error;
            public bool PreviewedReach;
            public MortalDecision Prepared;
        }

        private readonly MahjongEngine engine;
        private readonly MjaiEventTranslator translator = new();
        private readonly List<MjaiObservation> history = new();
        private readonly Dictionary<int, ModelSlot> models = new();
        private readonly SemaphoreSlim gate = new(1, 1);
        private string expected;
        private bool disposed;

        /// <summary>在本局任何玩家操作之前创建。其后的所有输入及换局必须经过此适配器。</summary>
        public MortalMatchAdapter(MahjongEngine engine)
        {
            this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
            MatchState current = engine.GetState();
            Add(MjaiEventTranslator.StartGame(current), current);
            foreach (JsonObject evt in translator.StartRound(current, engine.GetRecords())) { Add(evt, current); }
            expected = Token(current);
        }

        public MatchState GetState() => engine.GetState();
        public VisibleMatchState GetPlayerView(int seat) => engine.GetPlayerView(seat);
        public IReadOnlyList<MatchCommand> GetAvailableCommands(int seat) => engine.GetAvailableCommands(seat);
        public IReadOnlyList<MatchRecord> GetRecords(long afterSequence = -1) => engine.GetRecords(afterSequence);

        /// <summary>导出指定玩家可见的更新事件，不泄露其他暗手；协议历史与核心成功动作日志独立。</summary>
        public IReadOnlyList<string> GetProtocolHistory(int seat)
        {
            CheckSeat(seat);
            return Array.AsReadOnly(history.Select(evt => evt.ForSeat(seat, false).ToJsonString()).ToArray());
        }

        /// <summary>加载/替换一家模型，按已有事件恢复至当前局面。成功后适配器负责释放该客户端。</summary>
        public async Task AttachModelAsync(int seat, IMjaiClient client, CancellationToken cancellationToken = default)
        {
            CheckSeat(seat);
            if (client == null) { throw new ArgumentNullException(nameof(client)); }
            await gate.WaitAsync(cancellationToken);
            try
            {
                MatchState current = Verify();
                ModelSlot slot = new() { Client = client };
                try { await Replay(slot, seat, current, cancellationToken); }
                catch { await client.DisposeAsync(); throw; }
                if (models.TryGetValue(seat, out ModelSlot old) && !ReferenceEquals(old.Client, client)) { await old.Client.DisposeAsync(); }
                models[seat] = slot;
            }
            finally { gate.Release(); }
        }

        /// <summary>请求当前选择，不推进麻将核心。立直会在模型私有会话内完成 reach→dahai 握手。</summary>
        public async Task<MortalDecision> RecommendAsync(int seat, CancellationToken cancellationToken = default)
        {
            CheckSeat(seat); await gate.WaitAsync(cancellationToken);
            try
            {
                MatchState current = Verify();
                IReadOnlyList<MatchCommand> choices = engine.GetAvailableCommands(seat);
                if (choices.Count == 0) { return new(seat, "NoLegalDecision"); }
                if (!models.TryGetValue(seat, out ModelSlot slot)) { return new(seat, "NoModelAttached"); }
                if (slot.Error != null) { return new(seat, "ModelResetRequired: " + slot.Error); }
                if (slot.Prepared != null && slot.RoundId == current.RoundId && slot.PhaseId == current.PhaseId) { return slot.Prepared; }
                if (current.OperationStage == MatchOperationStage.ClosedKanResponse && choices.Any(choice => choice.Action.PlayerAction == PlayerAction.Ron))
                {
                    // 用户指定的兼容策略；不冒充模型返回，也不在查询建议时推进核心。
                    slot.Prepared = new(seat, "KokushiRobClosedKanSkippedByPolicy", choices.Single(choice => choice.Action.PlayerAction == PlayerAction.Skip));
                    return slot.Prepared;
                }
                if (slot.Reaction == null || slot.RoundId != current.RoundId || slot.PhaseId != current.PhaseId)
                {
                    return new(seat, "ModelProducedNoReaction");
                }
                JsonObject reaction = (JsonObject)slot.Reaction.DeepClone();
                JsonObject followup = null;
                try
                {
                    bool reach = reaction["type"]?.GetValue<string>() == "reach";
                    if (reach)
                    {
                        if (reaction["actor"]?.GetValue<int>() != seat || !choices.Any(choice => choice.Action.PlayerAction == PlayerAction.Riichi))
                        {
                            return new(seat, "ModelActionNotLegalLocally", reaction: reaction);
                        }
                        slot.PreviewedReach = true;
                        followup = await slot.Client.ReactAsync(new JsonObject { ["type"] = "reach", ["actor"] = seat, ["can_act"] = true }, cancellationToken);
                    }
                    MatchCommand command = MjaiActionMapper.Match(reach ? followup : reaction, engine.GetPlayerView(seat), choices, reach);
                    if (command == null)
                    {
                        if (reach) { await Replay(slot, seat, current, cancellationToken); }
                        return new(seat, "ModelActionNotLegalLocally", reaction: reaction, followup: followup);
                    }
                    slot.Prepared = new(seat, "Ok", command, reaction, followup);
                    return slot.Prepared;
                }
                catch (Exception exception) when (exception is FormatException || exception is JsonException || exception is InvalidOperationException)
                {
                    if (slot.PreviewedReach) { await TryReplay(slot, seat, current, cancellationToken); }
                    return new(seat, "MalformedModelAction: " + exception.Message, reaction: reaction, followup: followup);
                }
                catch (Exception exception)
                {
                    slot.Error = exception.Message;
                    if (exception is OperationCanceledException) { throw; }
                    return new(seat, "ModelResetRequired: " + exception.Message, reaction: reaction);
                }
            }
            finally { gate.Release(); }
        }

        /// <summary>执行已校验的模型建议；窗口过期或内容不合法时拒绝，不触发核心默认舍牌。</summary>
        public async Task<MortalStepResult> SubmitModelAsync(MortalDecision decision, CancellationToken cancellationToken = default)
        {
            if (decision == null || !decision.Available) { throw new ArgumentException("An available model decision is required.", nameof(decision)); }
            await gate.WaitAsync(cancellationToken);
            try
            {
                MatchState current = Verify(); MatchCommand command = decision.Command;
                if (!CurrentLegal(current, decision.Seat, command)) { throw new InvalidOperationException("Model decision is stale or illegal; the game was not changed."); }
                return await SubmitInner(decision.Seat, command, current, cancellationToken);
            }
            finally { gate.Release(); }
        }

        /// <summary>宿主/人工输入沿用核心反馈语义；真实执行结果会回灌所有已连接模型。</summary>
        public async Task<MortalStepResult> SubmitAsync(int seat, MatchCommand command, CancellationToken cancellationToken = default)
        {
            CheckSeat(seat); await gate.WaitAsync(cancellationToken);
            try { return await SubmitInner(seat, command, Verify(), cancellationToken); }
            finally { gate.Release(); }
        }

        private async Task<MortalStepResult> SubmitInner(int seat, MatchCommand command, MatchState before, CancellationToken cancellationToken)
        {
            int previewed = -1;
            foreach (var pair in models)
            {
                ModelSlot slot = pair.Value;
                if (!slot.PreviewedReach) { continue; }
                bool matching = pair.Key == seat && command?.Action?.PlayerAction == PlayerAction.Riichi && CurrentLegal(before, seat, command) &&
                    slot.Prepared != null && MatchActionRules.Matches(slot.Prepared.Command.Action, command.Action);
                if (matching) { previewed = seat; }
                else { await TryReplay(slot, pair.Key, before, cancellationToken); }
            }
            cancellationToken.ThrowIfCancellationRequested();
            OperationFeedback feedback = engine.Submit(seat, command);
            MatchState after = engine.GetState(); expected = Token(after);
            bool newInput = feedback.Accepted && command != null && command.RoundId == before.RoundId && command.PhaseId == before.PhaseId && before.PendingSeats.Contains(seat);
            List<JsonObject> events = translator.Translate(before, after, feedback, newInput ? command.Action : null);
            foreach (ModelSlot slot in models.Values) { slot.PreviewedReach = false; slot.Prepared = null; }
            int start = history.Count;
            foreach (JsonObject evt in events)
            {
                MjaiObservation observation = Add(evt, after);
                if (evt["type"]?.GetValue<string>() == "reach" && evt["actor"]?.GetValue<int>() == previewed) { observation.PreviewedSeat = previewed; }
            }
            await Deliver(start, after, cancellationToken);
            return Result(feedback);
        }

        /// <summary>显式换局，同步新的 13 张起手和庄家首摸；不启动自动换局循环。</summary>
        public async Task<MortalStepResult> StartNextRoundAsync(CancellationToken cancellationToken = default)
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                Verify(); OperationFeedback feedback = engine.StartNextRound();
                MatchState after = engine.GetState(); expected = Token(after);
                if (feedback.Accepted)
                {
                    int start = history.Count;
                    foreach (JsonObject evt in translator.StartRound(after, feedback.Records)) { Add(evt, after); }
                    await Deliver(start, after, cancellationToken);
                }
                return Result(feedback);
            }
            finally { gate.Release(); }
        }

        private MjaiObservation Add(JsonObject evt, MatchState current)
        {
            MjaiObservation observation = new()
            {
                Index = history.Count, Payload = (JsonObject)evt.DeepClone(), RoundId = current.RoundId,
                PhaseId = current.PhaseId, Targets = MjaiEventTranslator.DecisionTargets(evt, current)
            };
            history.Add(observation); return observation;
        }

        private async Task Deliver(int start, MatchState current, CancellationToken cancellationToken)
        {
            foreach (var pair in models)
            {
                ModelSlot slot = pair.Value;
                if (slot.Error != null) { continue; }
                if (slot.RoundId != current.RoundId || slot.PhaseId != current.PhaseId) { slot.Reaction = null; }
                slot.RoundId = current.RoundId; slot.PhaseId = current.PhaseId;
                try
                {
                    foreach (MjaiObservation observation in history.Skip(start))
                    {
                        if (observation.PreviewedSeat == pair.Key) { continue; }
                        bool canAct = (observation.Targets & (1 << pair.Key)) != 0;
                        JsonObject response = await slot.Client.ReactAsync(observation.ForSeat(pair.Key, canAct), cancellationToken);
                        if (canAct) { slot.Reaction = response; }
                    }
                }
                catch (Exception exception) { slot.Error = exception.Message; }
            }
        }

        private async Task Replay(ModelSlot slot, int seat, MatchState current, CancellationToken cancellationToken)
        {
            slot.Reaction = null; slot.Prepared = null; slot.PreviewedReach = false;
            long trigger = current.PendingSeats.Contains(seat) ? history.LastOrDefault(evt => evt.RoundId == current.RoundId &&
                evt.PhaseId == current.PhaseId && (evt.Targets & (1 << seat)) != 0)?.Index ?? -1 : -1;
            foreach (MjaiObservation evt in history)
            {
                JsonObject response = await slot.Client.ReactAsync(evt.ForSeat(seat, evt.Index == trigger), cancellationToken);
                if (evt.Index == trigger) { slot.Reaction = response; }
            }
            slot.RoundId = current.RoundId; slot.PhaseId = current.PhaseId; slot.Error = null;
        }

        private async Task TryReplay(ModelSlot slot, int seat, MatchState current, CancellationToken cancellationToken)
        {
            try { await Replay(slot, seat, current, cancellationToken); }
            catch (Exception exception) { slot.Error = exception.Message; }
        }

        private MortalStepResult Result(OperationFeedback feedback) => new()
        {
            Feedback = feedback, ModelErrors = new Dictionary<int, string>(models.Where(pair => pair.Value.Error != null).ToDictionary(pair => pair.Key, pair => pair.Value.Error))
        };

        private bool CurrentLegal(MatchState state, int seat, MatchCommand command) => command?.Action != null &&
            command.MatchId == state.MatchId && command.RoundId == state.RoundId && command.RoundGeneration == state.RoundGeneration && command.PhaseId == state.PhaseId &&
            engine.GetAvailableCommands(seat).Any(choice => MatchActionRules.Matches(choice.Action, command.Action));

        private MatchState Verify()
        {
            if (disposed) { throw new ObjectDisposedException(nameof(MortalMatchAdapter)); }
            MatchState current = engine.GetState();
            if (Token(current) != expected) { throw new InvalidOperationException("The game was advanced outside the adapter. Do not bypass its input and next-round methods."); }
            return current;
        }

        private static string Token(MatchState state) => $"{state.MatchId}/{state.RoundId}/{state.PhaseId}/{state.Sequence}/{state.OperationStage}/{string.Join(',', state.PendingSeats)}";
        private static void CheckSeat(int seat) { if (seat < 0 || seat > 3) { throw new ArgumentOutOfRangeException(nameof(seat)); } }

        public async ValueTask DisposeAsync()
        {
            await gate.WaitAsync();
            try
            {
                if (disposed) { return; }
                disposed = true;
                foreach (ModelSlot slot in models.Values) { await slot.Client.DisposeAsync(); }
                models.Clear();
            }
            finally { gate.Release(); }
        }
    }
}
