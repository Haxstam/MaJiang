using System.Collections.Generic;
using System.Linq;
using MaJiangLib;

namespace MaJiang.Presentation
{
    public static class MahjongTableEffects
    {
        // 赤五与普通五属于相同牌面，牌背由Widget过滤。
        public static bool SameFace(Tile left, Tile right) => left != null && right != null && left.Color == right.Color && left.Number == right.Number;
        public static bool Highlight(Tile source, Tile target, bool faceVisible, bool isRiver, bool isMeld)
            => faceVisible && (isRiver || isMeld) && SameFace(source, target);
        public static bool IsVisibleDora(Tile tile, bool faceVisible, IReadOnlyList<Tile> indicators)
            => faceVisible && tile != null && (tile.IsRedDora || indicators.Any(pointer =>
                RiichiScoring.NextDora(HandAnalysis.Face(pointer)) == HandAnalysis.Face(tile)));

        public static int ActiveDiscardIndex(VisibleMatchState view, int seat)
        {
            if (view.OperationStage != MatchOperationStage.DiscardResponse || view.ResponseSourceSeat != seat || view.ResponseTile == null) { return -1; }
            var river = view.State.DiscardTileList[seat];
            int last = river.Count - 1;
            return last >= 0 && Tile.ExactlyEquals(river[last], view.ResponseTile) ? last : -1;
        }

        public static bool CanRespondToDiscard(VisibleMatchState view, IReadOnlyList<PlayerActionData> actions)
            => view.OperationStage == MatchOperationStage.DiscardResponse && !view.HasResponded
                && view.ResponseSourceSeat != view.SelfSeat && view.ResponseTile != null
                && actions.Any(action => action.SelfNumber == view.SelfSeat && action.TargetPlayerNumber == view.ResponseSourceSeat
                    && Tile.ExactlyEquals(action.TargetTile, view.ResponseTile)
                    && action.PlayerAction is PlayerAction.Chi or PlayerAction.Peng or PlayerAction.OpenKan or PlayerAction.Ron);

        public static int ActiveSeat(VisibleMatchState view) => view.OperationStage switch
        {
            MatchOperationStage.Discard or MatchOperationStage.DiscardAfterCall or MatchOperationStage.DiscardResponse
                or MatchOperationStage.AddedKanResponse or MatchOperationStage.ClosedKanResponse => view.State.CurrentPlayerIndex,
            _ => -1
        };
    }
}
