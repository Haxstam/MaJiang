using System;
using System.Collections.Generic;
using System.Linq;

namespace MaJiangLib
{
    /// <summary>纯同步麻将引擎。调用者串行输入操作；查询和时间流逝均不会推动牌局。</summary>
    public sealed class MahjongEngine
    {
        private readonly MatchJournal journal;
        private readonly MatchSession session;

        /// <summary>供同程序集协作与固定集成牌例接管已初始化的领域状态；不公开给外部调用者。</summary>
        internal MahjongEngine(MainMatchControl initializedState)
        {
            journal = new MatchJournal(initializedState);
            session = new MatchSession(journal);
        }

        /// <summary>显式开局，复制规则和座位信息。相同配置、32字节种子及操作可复现牌山。</summary>
        public MahjongEngine(MatchSettingData settings, IReadOnlyList<UserProfile> playersBySeat, byte[] seed = null, DemonCurseOptions curses = null)
        {
            if (settings is null) { throw new ArgumentNullException(nameof(settings)); }
            if (settings.MinimumYakuFan != 1 || settings.HaveLocalYaku)
            {
                throw new NotSupportedException("Current rules support one-yaku minimum and no local yaku.");
            }
            if (settings.BasePoint < 0 || settings.FinishPoint <= 0) { throw new ArgumentException("Invalid point settings.", nameof(settings)); }
            if (seed != null && seed.Length != 32) { throw new ArgumentException("Seed must have exactly 32 bytes.", nameof(seed)); }
            MainMatchControl core = new();
            core.ServerFourMatchInit(StateCopy.Settings(settings), playersBySeat,
                seed == null ? new Xoshiro256StarStar() : new Xoshiro256StarStar((byte[])seed.Clone()), curses);
            core.RevealDoraIndicator();
            journal = new MatchJournal(core); session = new MatchSession(journal);
        }

        /// <summary>可信宿主完整视图；返回独立副本，不允许直接修改内部状态。</summary>
        public MatchState GetState() => new()
        {
            MatchId = journal.MatchId, RoundId = journal.RoundId, RoundGeneration = journal.RoundGeneration,
            PhaseId = journal.PhaseId, Sequence = journal.LatestSequence, State = StateCopy.Core(journal.AuthoritativeState),
            OperationStage = journal.OperationStage,
            PendingSeats = Enumerable.Range(0, 4).Where(seat => !journal.HasResponded[seat]).ToArray(),
            FuritenFlags = journal.FuritenFlags.ToArray(), Outcome = journal.Outcome, Settlement = StateCopy.Settlement(journal.Settlement),
            ResponseSourceSeat = journal.ResponseSourceSeat, ResponseTile = journal.ResponseTile,
            PendingRiichiSeat = journal.PendingRiichiSeat, PendingDoraCount = journal.PendingDoraCount, WinnerMask = journal.WinnerMask,
            ForbiddenDiscards = journal.ForbiddenDiscards.Select(tiles => tiles.ToList()).ToArray()
        };

        /// <summary>玩家视图不包含牌山、其他玩家暗手、随机源或其他玩家的响应意向。</summary>
        public VisibleMatchState GetPlayerView(int seat) => journal.GetSnapshot(seat);

        /// <summary>生成绑定当前局和窗口的选择；取菜单本身不会推进牌局。</summary>
        public IReadOnlyList<MatchCommand> GetAvailableCommands(int seat)
        {
            MatchJournal.CheckSeat(seat);
            return Array.AsReadOnly(session.Machine.GetActions(seat).Select(action => new MatchCommand
            {
                MatchId = journal.MatchId, RoundId = journal.RoundId, RoundGeneration = journal.RoundGeneration,
                PhaseId = journal.PhaseId, CommandId = Guid.NewGuid(), Action = StateCopy.Action(action)
            }).ToArray());
        }

        /// <summary>认证由宿主完成，seat是宿主认定的座位。返回新执行事实，不记录输入意向。</summary>
        public OperationFeedback Submit(int seat, MatchCommand command)
        {
            long before = journal.LatestSequence;
            MatchCommandResult result = session.Machine.Submit(seat, command);
            session.SettleIfEnded();
            return Feedback(result.CommandId, result.Accepted, result.UsedDefault, result.Reason, before);
        }

        /// <summary>显式换局；未结算或整场结束时只返回拒绝反馈。</summary>
        public OperationFeedback StartNextRound()
        {
            string reason = session.StartNextRound();
            return Feedback(Guid.Empty, reason == "Ok", false, reason, reason == "Ok" ? -1 : journal.LatestSequence);
        }

        /// <summary>完整本局记录含摸牌牌面，供可信宿主使用。</summary>
        public IReadOnlyList<MatchRecord> GetRecords(long afterSequence = -1) => journal.GetRecords(journal.RoundId, afterSequence);

        /// <summary>可信实验宿主的隐藏调牌审计；不得广播给玩家或模型。</summary>
        public IReadOnlyList<CurseAuditRecord> GetCurseAudit(int startIndex = 0) => journal.AuthoritativeState.CurseController?.GetAudit(startIndex) ??
            (startIndex == 0 ? Array.Empty<CurseAuditRecord>() : throw new ArgumentOutOfRangeException(nameof(startIndex)));

        /// <summary>逐座位的诅咒触发、调换与不可避免失效统计副本。</summary>
        public IReadOnlyList<CurseStatistics> GetCurseStatistics() => journal.AuthoritativeState.CurseController?.Statistics ?? Array.Empty<CurseStatistics>();

        /// <summary>按玩家遮蔽其他座位的摸牌牌面，保持同一序号。</summary>
        public IReadOnlyList<MatchRecord> GetPlayerRecords(int seat, long afterSequence = -1) => journal.GetRecords(journal.RoundId, afterSequence, seat);

        /// <summary>本实例已开始的全部本局标识；换局后保留旧局记录。</summary>
        public IReadOnlyList<Guid> RoundIds => journal.RoundIds;

        /// <summary>查询指定本局记录；seat为空时返回完整记录。</summary>
        public IReadOnlyList<MatchRecord> GetRoundRecords(Guid roundId, int? seat = null, long afterSequence = -1)
            => journal.GetRecords(roundId, afterSequence, seat);

        private OperationFeedback Feedback(Guid commandId, bool accepted, bool usedDefault, string reason, long afterSequence) => new()
        {
            CommandId = commandId, Accepted = accepted, UsedDefault = usedDefault, Reason = reason,
            AwaitingResponses = journal.OperationStage == MatchOperationStage.DiscardResponse ||
                journal.OperationStage == MatchOperationStage.AddedKanResponse || journal.OperationStage == MatchOperationStage.ClosedKanResponse,
            PhaseId = journal.PhaseId, Sequence = journal.LatestSequence, Records = GetRecords(afterSequence)
        };
    }
}
