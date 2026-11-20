using System;
using System.Collections.Generic;

namespace MaJiangLib
{
    /// <summary>指定玩家的场况副本，只包含该玩家暗手及已公开信息。</summary>
    public sealed class VisibleMatchState
    {
        /// <summary>整场标识。</summary>
        public Guid MatchId;
        /// <summary>具体本局标识。</summary>
        public Guid RoundId;
        /// <summary>整场内单调递增的本局代数。</summary>
        public int RoundGeneration;
        /// <summary>当前本局已执行事实的日志位置。</summary>
        public long Sequence;
        /// <summary>命令所属操作窗口编号。</summary>
        public long PhaseId;
        /// <summary>本视图所属的玩家座位。</summary>
        public int SelfSeat;
        /// <summary>与引擎内部隔离的牌局状态副本。</summary>
        public MainMatchControl State;
        /// <summary>各座位的暗手总张数，含单独摸牌。</summary>
        public int[] HandCounts;
        /// <summary>振听位标记：1为舍牌振听，2为同巡，4为立直见逃。</summary>
        public byte FuritenFlags;
        /// <summary>本人是否已在当前窗口完成答复。</summary>
        public bool HasResponded;
        /// <summary>尚未公开的杠宝牌指示牌数量。</summary>
        public int PendingDoraCount;
        /// <summary>当前实际操作阶段。</summary>
        public MatchOperationStage OperationStage;
        /// <summary>响应来源座位，无来源时为-1。</summary>
        public int ResponseSourceSeat;
        /// <summary>本窗口可响应的公开目标牌。</summary>
        public Tile ResponseTile;
        /// <summary>立直尚待确认的座位，无待确认时为-1。</summary>
        public int PendingRiichiSeat;
        /// <summary>食替等限制下禁止舍出的牌种。</summary>
        public List<Tile> ForbiddenDiscards;
        /// <summary>当前本局的结束原因，未结束时为None。</summary>
        public RoundOutcomeKind Outcome;
        /// <summary>已成立的和牌者座位位图。</summary>
        public byte WinnerMask;
        /// <summary>最近已完成本局的公开结算；需按RoundId区分是否属于当前局。</summary>
        public RoundSettlementResult Settlement;

        /// <summary>查询不改变对局；响应已提交时不再提供该窗口的选择。</summary>
        public IReadOnlyList<PlayerActionData> GetAvailableActions() => MatchActionRules.GetActions(State, State.PlayerList[SelfSeat].PlayerHandTile,
            new MatchActionContext
            {
                Stage = OperationStage, SourceSeat = ResponseSourceSeat, ResponseTile = ResponseTile,
                HasResponded = HasResponded, FuritenFlags = FuritenFlags,
                PendingRiichiSeat = PendingRiichiSeat, ForbiddenDiscards = ForbiddenDiscards
            });
    }

    /// <summary>可信宿主的完整场况副本，包含暗手与牌山，不能直接广播给玩家。</summary>
    public sealed class MatchState
    {
        /// <summary>整场标识。</summary>
        public Guid MatchId { get; internal set; }
        /// <summary>具体本局标识。</summary>
        public Guid RoundId { get; internal set; }
        /// <summary>整场内单调递增的本局代数。</summary>
        public int RoundGeneration { get; internal set; }
        /// <summary>命令所属操作窗口编号。</summary>
        public long PhaseId { get; internal set; }
        /// <summary>当前本局已执行事实的日志位置。</summary>
        public long Sequence { get; internal set; }
        /// <summary>与引擎内部隔离的牌局状态副本。</summary>
        public MainMatchControl State { get; internal set; }
        /// <summary>当前实际操作阶段。</summary>
        public MatchOperationStage OperationStage { get; internal set; }
        /// <summary>当前仍需外部传入操作的座位，不包含已答复者。</summary>
        public int[] PendingSeats { get; internal set; }
        /// <summary>振听位标记：1为舍牌振听，2为同巡，4为立直见逃。</summary>
        public byte[] FuritenFlags { get; internal set; }
        /// <summary>响应来源座位，无来源时为-1。</summary>
        public int ResponseSourceSeat { get; internal set; }
        /// <summary>本窗口可响应的公开目标牌。</summary>
        public Tile ResponseTile { get; internal set; }
        /// <summary>立直尚待确认的座位，无待确认时为-1。</summary>
        public int PendingRiichiSeat { get; internal set; }
        /// <summary>尚未公开的杠宝牌指示牌数量。</summary>
        public int PendingDoraCount { get; internal set; }
        /// <summary>已成立的和牌者座位位图。</summary>
        public byte WinnerMask { get; internal set; }
        /// <summary>食替等限制下禁止舍出的牌种。</summary>
        public List<Tile>[] ForbiddenDiscards { get; internal set; }
        /// <summary>当前本局的结束原因，未结束时为None。</summary>
        public RoundOutcomeKind Outcome { get; internal set; }
        /// <summary>最近已完成本局的公开结算；需按RoundId区分是否属于当前局。</summary>
        public RoundSettlementResult Settlement { get; internal set; }
        /// <summary>整场是否已结束。</summary>
        public bool MatchEnded => Settlement?.MatchEnded == true;
    }

    /// <summary>日志只包含已经发生的领域事实。</summary>
    public enum MatchRecordKind
    {
        /// <summary>本局发牌初始化已经完成。</summary>
        RoundStarted,
        /// <summary>确定的摸、切、鸣牌、立直、和牌或流局已经执行。</summary>
        ActionApplied,
        /// <summary>一张宝牌指示牌已经公开。</summary>
        DoraRevealed,
        /// <summary>本局支付与结算已经提交。</summary>
        RoundSettled
    }

    /// <summary>已确定的操作/事件。每次读取都复制可变载荷，不含响应意向或请求内容。</summary>
    public sealed class MatchRecord
    {
        private readonly PlayerActionData action;
        private readonly RoundSettlementResult settlement;
        /// <summary>整场标识。</summary>
        public Guid MatchId { get; }
        /// <summary>具体本局标识。</summary>
        public Guid RoundId { get; }
        /// <summary>整场内单调递增的本局代数。</summary>
        public int RoundGeneration { get; }
        /// <summary>当前本局已执行事实的日志位置。</summary>
        public long Sequence { get; }
        /// <summary>已执行事实的类型。</summary>
        public MatchRecordKind Kind { get; }
        /// <summary>已成立的实际动作；每次读取返回副本。</summary>
        public PlayerActionData Action => StateCopy.Action(action);
        /// <summary>本条公开的宝牌指示牌。</summary>
        public Tile Indicator { get; }
        /// <summary>当前本局的结束原因，未结束时为None。</summary>
        public RoundOutcomeKind Outcome { get; }
        /// <summary>最近已完成本局的公开结算；需按RoundId区分是否属于当前局。</summary>
        public RoundSettlementResult Settlement => StateCopy.Settlement(settlement);

        internal MatchRecord(Guid matchId, Guid roundId, int generation, long sequence, MatchRecordKind kind,
            PlayerActionData action = null, Tile indicator = null, RoundOutcomeKind outcome = RoundOutcomeKind.None,
            RoundSettlementResult settlement = null)
        {
            MatchId = matchId; RoundId = roundId; RoundGeneration = generation; Sequence = sequence;
            Kind = kind; this.action = StateCopy.Action(action); Indicator = indicator;
            Outcome = outcome; this.settlement = StateCopy.Settlement(settlement);
        }

        internal MatchRecord ForSeat(int seat)
        {
            PlayerActionData visible = Action;
            if (visible?.PlayerAction == PlayerAction.Take && visible.SelfNumber != seat)
            {
                visible.TargetTile = null; visible.Tiles.Clear();
            }
            return new(MatchId, RoundId, RoundGeneration, Sequence, Kind, visible, Indicator, Outcome, settlement);
        }
    }

    /// <summary>Accepted只表示处理选择；竞争中的鸣牌是否成立，读取Records。</summary>
    public sealed class OperationFeedback
    {
        /// <summary>对应输入命令的幂等标识。</summary>
        public Guid CommandId { get; internal set; }
        /// <summary>输入选择是否被正常接收；不表示竞争中的鸣牌已成立。</summary>
        public bool Accepted { get; internal set; }
        /// <summary>本次规则非法输入是否已按既定规则执行默认动作。</summary>
        public bool UsedDefault { get; internal set; }
        /// <summary>处理结果代码，成功时为Ok。</summary>
        public string Reason { get; internal set; }
        /// <summary>处理后是否仍处于响应收集窗口。</summary>
        public bool AwaitingResponses { get; internal set; }
        /// <summary>命令所属操作窗口编号。</summary>
        public long PhaseId { get; internal set; }
        /// <summary>当前本局已执行事实的日志位置。</summary>
        public long Sequence { get; internal set; }
        /// <summary>本次调用新产生的完整记录；包含摸牌信息，只能交给可信宿主。</summary>
        public IReadOnlyList<MatchRecord> Records { get; internal set; }
    }
}
