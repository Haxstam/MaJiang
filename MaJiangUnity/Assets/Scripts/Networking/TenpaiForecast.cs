using System;
using System.Collections.Generic;
using System.Linq;

namespace MaJiangLib
{
    public readonly struct TenpaiWait
    {
        public Tile Tile { get; }
        public ScoredHand RonScore { get; }
        public int Remaining { get; }
        public bool Furiten { get; }

        public TenpaiWait(Tile tile, ScoredHand ronScore, int remaining, bool furiten)
        {
            Tile = tile;
            RonScore = ronScore;
            Remaining = remaining;
            Furiten = furiten;
        }
    }

    /// <summary>以本人暗手和公开牌计算听牌提示，舍牌预估不修改可见状态。</summary>
    public static class TenpaiForecast
    {
        public static IReadOnlyList<TenpaiWait> GetWaits(VisibleMatchState view, PlayerActionData discard = null)
        {
            if (view.OperationStage == MatchOperationStage.RoundEnded) { return Array.Empty<TenpaiWait>(); }
            MainMatchControl state = view.State;
            int seat = view.SelfSeat;
            HandTile current = state.PlayerList[seat].PlayerHandTile;
            HandTile waiting = current;
            if (discard != null)
            {
                waiting = (HandTile)current.Clone();
                if (discard.PlayerAction == PlayerAction.HandCut || !Tile.ExactlyEquals(current.SingleTile, discard.TargetTile))
                {
                    waiting.HandTileList = MatchActionRules.RemoveCopy(current.HandTileList, new[] { discard.TargetTile });
                    if (current.SingleTile != null) { waiting.HandTileList.Add(current.SingleTile); }
                }
                waiting.SingleTile = null;
            }
            IReadOnlyList<Tile> waits = HandAnalysis.Waits(waiting);
            if (waits.Count == 0) { return Array.Empty<TenpaiWait>(); }

            // 舍牌振听随新等待重算。同巡和立直后见逃在舍牌时仍有效。
            bool furiten = (discard == null ? view.FuritenFlags : view.FuritenFlags & 0b110) != 0
                || waits.Any(tile => tile == discard?.TargetTile || state.DiscardTileList[seat].Any(discarded => discarded == tile));
            bool declaresRiichi = discard?.PlayerAction == PlayerAction.Riichi;
            int[] known = CountKnownTiles(state, current);
            return waits.Select(tile => new TenpaiWait(tile, RiichiScoring.EstimateRon(state, waiting, tile, declaresRiichi),
                Math.Max(0, 4 - known[HandAnalysis.Face(tile)]), furiten)).ToArray();
        }

        private static int[] CountKnownTiles(IMatchInformation state, HandTile current)
        {
            // 从舍牌到牌河的移动不改变已知数；当前暗手和摸牌各扣除一次。
            int[] known = new int[34];
            void Count(Tile tile) { known[HandAnalysis.Face(tile)]++; }
            foreach (Tile tile in current.HandTileList) { Count(tile); }
            if (current.SingleTile != null) { Count(current.SingleTile); }
            foreach (Tile tile in state.DoraPointerList) { Count(tile); }
            List<Tile>[] rivers = state.DiscardTileList.Select(river => river.ToList()).ToArray();
            foreach (Group group in state.PlayerOpenSetList.SelectMany(groups => groups))
            {
                foreach (Tile tile in group.Tiles) { Count(tile); }
                if (!group.IsOpen || group.SingleTile == null) { continue; }
                List<Tile> source = rivers[group.FuluSource];
                int called = source.FindIndex(tile => Tile.ExactlyEquals(tile, group.SingleTile));
                if (called >= 0) { source.RemoveAt(called); }
            }
            foreach (Tile tile in rivers.SelectMany(river => river)) { Count(tile); }
            return known;
        }
    }
}
