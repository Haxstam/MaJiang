using System.Collections.Generic;

namespace MaJiangLib
{
    /// <summary>具体操作窗口，独立于旧核心中的粗略StageType。</summary>
    public enum MatchOperationStage : byte
    {
        AwaitingDraw = 0,
        Discard = 1,
        DiscardAfterCall = 2,
        DiscardResponse = 3,
        AddedKanResponse = 4,
        ClosedKanResponse = 5,
        RoundEnded = 6
    }

    /// <summary>操作阶段产生的本局结束原因；番符、流满与支付在结算阶段裁定。</summary>
    public enum RoundOutcomeKind : byte
    {
        None, Ron, Tsumo, ExhaustiveDraw, NineTerminals, FourWinds, FourRiichi, FourKans
    }

    /// <summary>查询只使用公共信息、本人手牌和本人私有标记，不读取其他玩家暗手。</summary>
    public sealed class MatchActionContext
    {
        public MatchOperationStage Stage;
        public int SourceSeat = -1;
        public Tile ResponseTile;
        public bool HasResponded;
        /// <summary>位0舍牌振听，位1同巡见逃，位2立直后见逃。</summary>
        public byte FuritenFlags;
        public int PendingRiichiSeat = -1;
        public IReadOnlyList<Tile> ForbiddenDiscards = new List<Tile>();
    }
}
