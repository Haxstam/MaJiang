using System;
using System.Collections.Generic;
using System.Linq;

namespace MaJiangLib
{
    /// <summary>客户端菜单和服务端校验共用，查询无副作用且不访问别家暗手。</summary>
    public static class MatchActionRules
    {
        internal static bool IsDoubleRiichiDeclaration(IMatchInformation state, int seat)
            => state.FirstCycleIppatsu && state.DiscardTileList[seat].Count == 0;

        /// <summary>根据本人手牌与公开牌局生成具体选择；忽略宝牌的一番缚由HandAnalysis判定。</summary>
        public static IReadOnlyList<PlayerActionData> GetActions(IMatchInformation state, HandTile hand, MatchActionContext context)
        {
            List<PlayerActionData> actions = new();
            if (context.HasResponded || context.Stage == MatchOperationStage.RoundEnded || context.Stage == MatchOperationStage.AwaitingDraw) { return actions; }
            int seat = hand.PlayerNumber;
            if (context.Stage == MatchOperationStage.Discard || context.Stage == MatchOperationStage.DiscardAfterCall)
            {
                if (seat != state.CurrentPlayerIndex) { return actions; }
                List<Tile> legalCuts = hand.HandTileList.Where(tile => !context.ForbiddenDiscards.Any(blocked => blocked == tile)).ToList();
                if (!state.IsRiichi[seat])
                {
                    foreach (Tile tile in DistinctPhysical(legalCuts)) { actions.Add(Choice(seat, seat, PlayerAction.HandCut, tile, new() { tile })); }
                }
                if (hand.SingleTile != null) { actions.Add(Choice(seat, seat, PlayerAction.TakeCut, hand.SingleTile, new() { hand.SingleTile })); }
                if (context.Stage == MatchOperationStage.DiscardAfterCall) { return actions; }
                if (hand.SingleTile != null && HandAnalysis.HasYaku(state, hand, hand.SingleTile, true, context.Stage))
                {
                    actions.Add(Choice(seat, seat, PlayerAction.Tsumo, hand.SingleTile, new()));
                }
                if (!state.IsRiichi[seat] && hand.IsClosedHand && state.PlayerPoint[seat] >= 1000 && state.RemainTileCount >= 4)
                {
                    List<Tile> all = hand.HandTileList.Concat(hand.SingleTile == null ? Array.Empty<Tile>() : new[] { hand.SingleTile }).ToList();
                    foreach (Tile cut in DistinctPhysical(all))
                    {
                        List<Tile> remaining = RemoveCopy(all, new[] { cut });
                        HandTile beforeWin = new(seat) { HandTileList = remaining, OpenTileList = hand.OpenTileList.ToList() };
                        if (HandAnalysis.Waits(beforeWin).Count > 0) { actions.Add(Choice(seat, seat, PlayerAction.Riichi, cut, new() { cut })); }
                    }
                }
                if (state.FirstCycleIppatsu && state.DiscardTileList[seat].Count == 0
                    && hand.HandTileList.Concat(hand.SingleTile == null ? Array.Empty<Tile>() : new[] { hand.SingleTile })
                        .Select(HandAnalysis.Face).Distinct().Count(face => face >= 27 || face % 9 == 0 || face % 9 == 8) >= 9)
                {
                    actions.Add(Choice(seat, seat, PlayerAction.Draw, null, new()));
                }
                if (state.RemainTileCount > 0 && state.KangCount < 4)
                {
                    List<Tile> all = hand.HandTileList.Concat(hand.SingleTile == null ? Array.Empty<Tile>() : new[] { hand.SingleTile }).ToList();
                    foreach (IGrouping<int, Tile> group in all.GroupBy(HandAnalysis.Face).Where(group => group.Count() == 4))
                    {
                        if (!state.IsRiichi[seat] || (hand.SingleTile != null && HandAnalysis.Face(hand.SingleTile) == group.Key && HandAnalysis.RiichiKanPreservesShape(hand, hand.SingleTile)))
                        {
                            actions.Add(Choice(seat, seat, PlayerAction.ClosedKan, group.First(), group.ToList()));
                        }
                    }
                    if (!state.IsRiichi[seat])
                    {
                        foreach (Tile tile in DistinctPhysical(all).Where(tile => hand.OpenTileList.Any(group => group.GroupType == GroupType.OpenTriple && group.Tiles[0] == tile)))
                        {
                            actions.Add(Choice(seat, seat, PlayerAction.AddedKan, tile, new() { tile }));
                        }
                    }
                }
                return actions;
            }
            if (seat == context.SourceSeat || context.ResponseTile == null) { return actions; }
            actions.Add(Choice(seat, context.SourceSeat, PlayerAction.Skip, null, new()));
            bool discardedWait = HandAnalysis.Waits(hand).Any(wait => state.DiscardTileList[seat].Any(tile => tile == wait));
            if (context.FuritenFlags == 0 && !discardedWait && HandAnalysis.HasYaku(state, hand, context.ResponseTile, false, context.Stage))
            {
                actions.Add(Choice(seat, context.SourceSeat, PlayerAction.Ron, context.ResponseTile, new()));
            }
            if (context.Stage != MatchOperationStage.DiscardResponse || state.IsRiichi[seat] || state.RemainTileCount == 0 || AbortAfterDiscard(state, context.PendingRiichiSeat) != RoundOutcomeKind.None)
            {
                return actions;
            }
            foreach (List<Tile> selection in Combinations(hand.HandTileList, 2))
            {
                if (selection.All(tile => tile == context.ResponseTile)) { AddCall(PlayerAction.Peng, selection); }
                if (seat == (context.SourceSeat + 1) % 4 && context.ResponseTile.Color != Color.Honor)
                {
                    Tile[] meld = selection.Concat(new[] { context.ResponseTile }).OrderBy(tile => tile.Number).ToArray();
                    if (meld.All(tile => tile.Color == context.ResponseTile.Color) && meld[0].Number + 1 == meld[1].Number && meld[1].Number + 1 == meld[2].Number)
                    {
                        AddCall(PlayerAction.Chi, selection);
                    }
                }
            }
            if (state.KangCount < 4)
            {
                foreach (List<Tile> selection in Combinations(hand.HandTileList, 3).Where(tiles => tiles.All(tile => tile == context.ResponseTile)))
                {
                    AddCall(PlayerAction.OpenKan, selection);
                }
            }
            return actions;

            void AddCall(PlayerAction type, List<Tile> selected)
            {
                PlayerActionData choice = Choice(seat, context.SourceSeat, type, context.ResponseTile, selected);
                if (actions.Any(existing => Matches(existing, choice))) { return; }
                if (type != PlayerAction.OpenKan)
                {
                    IReadOnlyList<Tile> blocked = ForbiddenAfterCall(choice);
                    if (!RemoveCopy(hand.HandTileList, selected).Any(tile => !blocked.Any(forbidden => forbidden == tile))) { return; }
                }
                actions.Add(choice);
            }
        }

        /// <summary>比较动作、来源及精确赤牌；鸣牌中的手牌顺序不影响匹配。</summary>
        public static bool Matches(PlayerActionData expected, PlayerActionData selected)
        {
            if (expected is null || selected is null || expected.PlayerAction != selected.PlayerAction || expected.SelfNumber != selected.SelfNumber
                || expected.TargetPlayerNumber != selected.TargetPlayerNumber || !Tile.ExactlyEquals(expected.TargetTile, selected.TargetTile)) { return false; }
            if (selected.PlayerAction == PlayerAction.HandCut || selected.PlayerAction == PlayerAction.TakeCut || selected.PlayerAction == PlayerAction.Riichi) { return true; }
            List<Tile> left = (expected.Tiles ?? new()).OrderBy(tile => tile).ToList();
            List<Tile> right = (selected.Tiles ?? new()).OrderBy(tile => tile).ToList();
            return left.Count == right.Count && left.Zip(right, Tile.ExactlyEquals).All(equal => equal);
        }

        /// <summary>现物食替及吃牌替换同一顺子的筋食替限制。</summary>
        public static IReadOnlyList<Tile> ForbiddenAfterCall(PlayerActionData call)
        {
            List<Tile> forbidden = new() { new(call.TargetTile.Color, call.TargetTile.Number) };
            if (call.PlayerAction != PlayerAction.Chi) { return forbidden; }
            int start = call.Tiles.Concat(new[] { call.TargetTile }).Min(tile => tile.Number);
            int alternative = call.TargetTile.Number == start ? start + 3 : call.TargetTile.Number == start + 2 ? start - 1 : 0;
            if (alternative >= 1 && alternative <= 9) { forbidden.Add(new(call.TargetTile.Color, alternative)); }
            return forbidden;
        }

        /// <summary>只能在荣和窗口之后实际结束；查询时也用于抑制会被流局取代的鸣牌。</summary>
        public static RoundOutcomeKind AbortAfterDiscard(IMatchInformation state, int pendingRiichiSeat)
        {
            if (state.KangCount == 4 && state.KangMark == 5) { return RoundOutcomeKind.FourKans; }
            if (Enumerable.Range(0, 4).All(seat => state.IsRiichi[seat] || seat == pendingRiichiSeat)) { return RoundOutcomeKind.FourRiichi; }
            if (state.FirstCycleIppatsu && state.DiscardTileList.All(river => river.Count == 1))
            {
                Tile first = state.DiscardTileList[0][0];
                if (first.Color == Color.Honor && first.Number <= 4 && state.DiscardTileList.All(river => river[0] == first)) { return RoundOutcomeKind.FourWinds; }
            }
            return RoundOutcomeKind.None;
        }

        internal static List<Tile> RemoveCopy(IEnumerable<Tile> source, IEnumerable<Tile> selected)
        {
            List<Tile> result = source.ToList();
            foreach (Tile tile in selected)
            {
                int index = result.FindIndex(candidate => Tile.ExactlyEquals(candidate, tile));
                if (index < 0) { throw new ArgumentException("Selected tile is absent."); }
                result.RemoveAt(index);
            }
            return result;
        }
        private static IEnumerable<Tile> DistinctPhysical(IEnumerable<Tile> tiles) => tiles.GroupBy(tile => (tile.Color, tile.Number, tile.IsRedDora)).Select(group => group.First());
        private static PlayerActionData Choice(int seat, int source, PlayerAction type, Tile target, List<Tile> tiles) => new(seat, source, tiles, target, type);
        private static IEnumerable<List<Tile>> Combinations(List<Tile> tiles, int count)
        {
            for (int first = 0; first < tiles.Count; first++)
            {
                for (int second = first + 1; second < tiles.Count; second++)
                {
                    if (count == 2) { yield return new() { tiles[first], tiles[second] }; }
                    else { for (int third = second + 1; third < tiles.Count; third++) { yield return new() { tiles[first], tiles[second], tiles[third] }; } }
                }
            }
        }
    }
}
