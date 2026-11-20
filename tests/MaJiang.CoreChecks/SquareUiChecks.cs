using System;
using System.Linq;
using MaJiangLib;
using MaJiang.Presentation;

internal static partial class Program
{
    private static void HoverVisibility()
    {
        Tile five = Tile.BytesTo("5p"), red = Tile.BytesTo("0p");
        Assert(MahjongTableEffects.Highlight(five, red, true, true, false), "River red five was separated from normal five.");
        Assert(MahjongTableEffects.Highlight(five, red, true, false, true), "Meld red five was not highlighted.");
        Assert(!MahjongTableEffects.Highlight(five, Tile.BytesTo("5s"), true, true, false), "Other suit was highlighted.");
        Assert(!MahjongTableEffects.Highlight(five, Tile.BytesTo("4p"), true, false, true), "Other number was highlighted.");
        Assert(!MahjongTableEffects.Highlight(five, red, false, false, true), "Concealed kan face was highlighted.");
        Assert(!MahjongTableEffects.Highlight(five, red, true, false, false), "A hand, draw, indicator, choice or result outside public areas was highlighted.");
        Assert(!MahjongTableEffects.Highlight(null, red, true, true, false), "Cleared hover left highlights enabled.");
    }
    private static void TurnLightStages()
    {
        var match = NewMatch();
        // 保证下家可碰，避免操作机将无人可响应的窗口直接跳过。
        Tile upcoming = match.MainTileList[match.CurrentTileIndex];
        match.PlayerList[1].PlayerHandTile.HandTileList[0] = upcoming;
        match.PlayerList[1].PlayerHandTile.HandTileList[1] = upcoming;
        var machine = Machine(match);
        Assert(MahjongTableEffects.ActiveSeat(machine.Journal.GetSnapshot(0)) == 0, "Initial draw has no light.");
        Submit(machine, machine.GetActions(0).First(action => action.PlayerAction == PlayerAction.TakeCut));
        Assert(MahjongTableEffects.ActiveSeat(machine.Journal.GetSnapshot(0)) == 0, "Light went out before discard response resolved.");
        for (int guard = 0; machine.Stage == MatchOperationStage.DiscardResponse && guard < 4; guard++)
        {
            Submit(machine, Enumerable.Range(0,4).SelectMany(machine.GetActions).First(action => action.PlayerAction == PlayerAction.Skip));
        }
        Assert(MahjongTableEffects.ActiveSeat(machine.Journal.GetSnapshot(0)) == 1, "Light did not transfer to next draw.");
        var view = machine.Journal.GetSnapshot(0);
        view.State.CurrentPlayerIndex = 3;
        foreach (var stage in new[] { MatchOperationStage.DiscardAfterCall, MatchOperationStage.AddedKanResponse, MatchOperationStage.ClosedKanResponse })
        {
            view.OperationStage = stage; Assert(MahjongTableEffects.ActiveSeat(view) == 3, "Call or kan lost its active light.");
        }
        view.OperationStage = MatchOperationStage.RoundEnded;
        Assert(MahjongTableEffects.ActiveSeat(view) == -1, "Round-end light stayed enabled.");
    }
    private static void FixedPublicSeats()
    {
        for (int anchor = 0; anchor < 4; anchor++)
        {
            for (int viewed = 0; viewed < 4; viewed++)
            {
                int[] board = Enumerable.Range(0,4).Select(relative => MahjongSeatOrder.PublicSeat(viewed, relative, anchor)).ToArray();
                Assert(board.SequenceEqual(Enumerable.Range(0,4).Select(relative => (anchor + relative) % 4)), "Changing hands rotated public seats.");
            }
        }
    }
    private static void PrivateDrawAndTurnCycle()
    {
        var match = NewMatch();
        match.FirstCycleIppatsu = false;
        var machine = Machine(match);
        for (int turn = 0; turn < 4; turn++)
        {
            int seat = turn;
            Assert(match.CurrentPlayerIndex == seat, "Turn did not advance to the next counterclockwise seat.");
            var owner = machine.Journal.GetSnapshot(seat);
            var ownHand = owner.State.PlayerList[seat].PlayerHandTile;
            Assert(ownHand.HandTileList.Count == 13 && ownHand.SingleTile != null && owner.HandCounts[seat] == 14,
                "The visible hand does not preserve the separate fourteenth tile.");
            Assert(Tile.ExactlyEquals(ownHand.SingleTile, match.PlayerList[seat].PlayerHandTile.SingleTile), "Draw identity changed in synchronization.");
            var observer = machine.Journal.GetSnapshot((seat + 1) % 4);
            Assert(observer.State.PlayerList[seat].PlayerHandTile.SingleTile == null && observer.HandCounts[seat] == 14,
                "An opponent's draw identity leaked or its public count was lost.");
            string[] concealed = ownHand.HandTileList.Select(Face).ToArray();
            var draw = ownHand.SingleTile;
            Submit(machine, machine.GetActions(seat).First(action => action.PlayerAction == PlayerAction.TakeCut));
            var after = machine.Journal.GetSnapshot(seat).State.PlayerList[seat].PlayerHandTile;
            Assert(after.SingleTile == null && after.HandTileList.Select(Face).SequenceEqual(concealed), "Tsumogiri changed the thirteen concealed tiles.");
            Assert(Tile.ExactlyEquals(match.DiscardTileList[seat].Last(), draw), "Tsumogiri discarded a concealed tile.");
            for (int guard = 0; machine.Stage == MatchOperationStage.DiscardResponse && guard < 4; guard++)
            {
                var skip = Enumerable.Range(0, 4).SelectMany(machine.GetActions).First(action => action.PlayerAction == PlayerAction.Skip);
                Submit(machine, skip);
            }
        }
        Assert(match.CurrentPlayerIndex == 0 && machine.Stage == MatchOperationStage.Discard, "A full turn cycle did not return to East.");
    }

    private static void CounterclockwiseSeats()
    {
        for (int dealer = 0; dealer < 4; dealer++)
        {
            for (int self = 0; self < 4; self++)
            {
                int selfWind = MahjongSeatOrder.WindIndex(self, dealer);
                for (int relative = 0; relative < 4; relative++)
                {
                    int seat = MahjongSeatOrder.AbsoluteSeat(self, relative);
                    Assert(MahjongSeatOrder.WindIndex(seat, dealer) == (selfWind + relative) % 4,
                        "Observer rotation swapped South and North.");
                }
            }
            Assert(MahjongSeatOrder.WindIndex(MahjongSeatOrder.AbsoluteSeat(dealer, 1), dealer) == 1, "East's right neighbor is not South.");
            Assert(MahjongSeatOrder.WindIndex(MahjongSeatOrder.AbsoluteSeat(dealer, 2), dealer) == 2, "East's opposite is not West.");
            Assert(MahjongSeatOrder.WindIndex(MahjongSeatOrder.AbsoluteSeat(dealer, 3), dealer) == 3, "East's left neighbor is not North.");
        }
    }

    private static void HandStripEdges()
    {
        for (int melds = 0; melds <= 4; melds++)
        {
            float hand = (13 - 3 * melds) * 40 + (12 - 3 * melds);
            float meld = melds * (100 * 40f / 30);
            var layout = new MahjongHandStripMetrics(580, hand, 40, meld, 10);
            RiverNear(layout.DrawX - hand * layout.Scale, 10);
            RiverNear(layout.MeldX + meld * layout.Scale, 580);
            Assert(layout.DrawX + 40 * layout.Scale <= layout.MeldX + 0.001f, "Drawn tile overlaps melds.");
            Assert(layout.Scale > 0 && layout.Scale <= 1, "Invalid strip scale.");
        }
        var noDraw = new MahjongHandStripMetrics(580, 400, 0, 130, 10);
        RiverNear(noDraw.Scale, 1); RiverNear(noDraw.DrawX, 400);
        var empty = new MahjongHandStripMetrics(580, 0, 0, 0, 10);
        RiverNear(empty.Scale, 1); RiverNear(empty.MeldX, 580);
    }

    private static void FiveExactChiChoices()
    {
        var match = NewMatch();
        var hand = match.PlayerList[1].PlayerHandTile;
        hand.HandTileList = "2p 3p 4p 0p 5p 6p 1w 2w 3w 1s 2s 5z 6z".Split(' ').Select(value => Tile.BytesTo(value)).ToList();
        hand.SingleTile = null;
        var before = hand.HandTileList.ToArray();
        var actions = MatchActionRules.GetActions(match, hand, new MatchActionContext
        {
            Stage = MatchOperationStage.DiscardResponse, SourceSeat = 0, ResponseTile = Tile.BytesTo("4p")
        }).Where(action => action.PlayerAction == PlayerAction.Chi).ToArray();
        string[] pairs = actions.Select(action => string.Join("", action.Tiles.OrderBy(tile => tile).Select(tile => tile.IsRedDora ? "0" : tile.Number.ToString()))).OrderBy(pair => pair).ToArray();
        Assert(pairs.SequenceEqual(new[] { "06", "23", "30", "35", "56" }), "The five exact red/normal chi choices were not preserved.");
        Assert(hand.HandTileList.SequenceEqual(before), "Generating choices modified the concealed hand.");
        Assert(actions.All(action => action.Tiles.Count == 2 && action.TargetTile.Number == 4), "Choice includes the discarded tile among the two own tiles.");
    }

    private static Tile[] TenpaiTiles(string tiles) => tiles.Split(' ').Select(value => Tile.BytesTo(value)).ToArray();

    private static VisibleMatchState TenpaiView(string tiles, string drawn = null)
    {
        MainMatchControl state = NewMatch();
        state.FirstCycleIppatsu = false;
        state.DoraPointerList.Clear();
        HandTile hand = state.PlayerList[0].PlayerHandTile;
        hand.HandTileList = TenpaiTiles(tiles).ToList();
        hand.SingleTile = drawn == null ? null : Tile.BytesTo(drawn);
        return new VisibleMatchState { State = state, SelfSeat = 0, OperationStage = MatchOperationStage.Discard };
    }

    private static PlayerActionData ForecastCut(string tile, PlayerAction action)
    {
        Tile selected = Tile.BytesTo(tile);
        return new PlayerActionData(0, 0, new() { selected }, selected, action);
    }

    private static void TenpaiRonHan()
    {
        var view = TenpaiView("2w 3w 4w 6p 7p 7p 8p 8p 3s 4s 0s 7s 7s");
        view.State.IsRiichi[0] = true;
        view.State.HaveIppatsu[0] = true;
        view.State.IsKan[0] = true;
        view.State.RemainTileCount = 0;
        var waits = TenpaiForecast.GetWaits(view);
        Assert(waits.Count == 2 && waits[0].Tile == Tile.BytesTo("6p") && waits[0].RonScore.Han == 4
            && waits[1].Tile == Tile.BytesTo("9p") && waits[1].RonScore.Han == 2,
            "Ron forecast included an accidental yaku or used the wrong decomposition.");
        Assert(waits[1].RonScore.Yaku.Select(yaku => yaku.Yaku).SequenceEqual(new[] { YakuType.Riichi, YakuType.Pinfu }),
            "The nine-pin wait should be riichi and pinfu.");
        view.State.DoraPointerList.Add(Tile.BytesTo("5p"));
        waits = TenpaiForecast.GetWaits(view);
        Assert(waits[0].RonScore.Han == 6 && waits[1].RonScore.Han == 3, "Visible dora was not counted for each winning tile.");

        view = TenpaiView("1w 2w 3w 1p 2p 3p 7p 7p 7p 2s 3s 8s 8s", "3z");
        var choices = view.GetAvailableActions();
        Tile draw = Tile.BytesTo("3z");
        var cut = choices.Single(action => action.PlayerAction == PlayerAction.TakeCut);
        var declaration = choices.Single(action => action.PlayerAction == PlayerAction.Riichi && action.TargetTile == draw);
        Tile fourSou = Tile.BytesTo("4s");
        Assert(TenpaiForecast.GetWaits(view, cut).Single(wait => wait.Tile == fourSou).RonScore == null,
            "Selecting the normal cut should preview a no-yaku wait.");
        Assert(TenpaiForecast.GetWaits(view, declaration).Single(wait => wait.Tile == fourSou).RonScore.Han == 1,
            "Selecting the same tile as a riichi declaration should preview one han.");
        view.State.DoraPointerList.Add(Tile.BytesTo("3s"));
        waits = TenpaiForecast.GetWaits(view);
        Assert(waits.Count == 2 && waits[0].Tile == Tile.BytesTo("1s") && waits[0].RonScore.Han == 2
            && waits[1].Tile == Tile.BytesTo("4s") && waits[1].RonScore == null, "Dora turned a no-yaku wait into ron.");
        Assert(TenpaiForecast.GetWaits(view, declaration)[1].RonScore.Han == 2, "Proposed riichi did not unlock the dora wait.");
        view.State.FirstCycleIppatsu = true;
        Assert(TenpaiForecast.GetWaits(view, declaration)[1].RonScore.Han == 3, "Double riichi was not counted.");
        Assert(!view.State.IsRiichi[0] && view.State.DiscardTileList[0].Count == 0, "Forecast modified the actual declaration state.");
    }

    private static void TenpaiDiscardFuriten()
    {
        var view = TenpaiView("1p 2p 3p 4p 5p 6p 7p 2s 3s 4s 4z 4z 4z", "4s");
        view.State.DiscardTileList[0].Add(Tile.BytesTo("1p"));
        view.FuritenFlags = 1;
        var current = TenpaiForecast.GetWaits(view);
        Assert(current.Count == 3 && current.All(wait => wait.Furiten), "Current discarded wait did not mark the whole wait list.");
        var discard = ForecastCut("1p", PlayerAction.HandCut);
        var changed = TenpaiForecast.GetWaits(view, discard);
        Assert(changed.Select(wait => Face(wait.Tile)).SequenceEqual(TenpaiTiles("1s 4s").Select(Face))
            && changed.All(wait => !wait.Furiten), "Changed waits kept old discard furiten.");
        foreach (byte flag in new byte[] { 2, 4 })
        {
            view.FuritenFlags = (byte)(1 | flag);
            Assert(TenpaiForecast.GetWaits(view, discard).All(wait => wait.Furiten), "Discard improperly cleared missed-win furiten.");
        }
        view = TenpaiView("2w 3w 4w 6p 7p 7p 8p 8p 3s 4s 5s 7s 7s", "9p");
        var waits = TenpaiForecast.GetWaits(view, ForecastCut("9p", PlayerAction.TakeCut));
        Assert(waits.Count == 2 && waits.All(wait => wait.Furiten), "A hypothetical tsumogiri of a wait did not mark every row.");
        Assert(view.State.PlayerList[0].PlayerHandTile.SingleTile == Tile.BytesTo("9p") && view.State.DiscardTileList[0].Count == 0,
            "A hypothetical discard changed the live hand or river.");
    }

    private static void TenpaiRemainingTiles()
    {
        var view = TenpaiView("1w 2w 3w 3s 4s 5s 8s 8s 8s 5z 5z 8p 9p");
        Tile seven = Tile.BytesTo("7p");
        view.State.PlayerOpenSetList[2].Add(new(GroupType.OpenStraight, seven.Color, TenpaiTiles("8p 9p").ToList(), 1, seven));
        view.State.DiscardTileList[1].Add(seven);
        view.State.DoraPointerList.Add(seven);
        var wait = TenpaiForecast.GetWaits(view).Single();
        Assert(wait.Tile == seven && wait.Remaining == 2, "A called seven was counted both in the river and meld.");

        view = TenpaiView("1w 2w 3w 3s 4s 5s 8s 8s 8s 5z 5z 3p 4p", "5p");
        Tile red = Tile.BytesTo("0p");
        view.State.PlayerOpenSetList[2].Add(new(GroupType.OpenStraight, red.Color, TenpaiTiles("3p 4p").ToList(), 1, red));
        view.State.DiscardTileList[1].AddRange(TenpaiTiles("5p 0p"));
        view.State.DoraPointerList.Add(Tile.BytesTo("5p"));
        wait = TenpaiForecast.GetWaits(view).Single(row => row.Tile == red);
        Assert(wait.Remaining == 0 && !wait.Tile.IsRedDora, "Red and normal five were counted separately, or the draw was omitted.");
    }

    private static void TenpaiRiichiChoices()
    {
        var view = TenpaiView("1p 2p 3p 4p 5p 6p 7p 2s 3s 4s 4z 4z 4z", "4s");
        var declarations = view.GetAvailableActions().Where(action => action.PlayerAction == PlayerAction.Riichi).ToArray();
        Assert(declarations.Select(action => Face(action.TargetTile)).OrderBy(face => face)
            .SequenceEqual(TenpaiTiles("1p 4p 7p 4s").Select(Face).OrderBy(face => face)), "Riichi mask would expose the wrong faces.");
        Assert(declarations.All(action => TenpaiForecast.GetWaits(view, action).Count > 0), "An allowed declaration had no previewable waits.");
    }

    private static void DoraSweepTiles()
    {
        Tile[] indicators = TenpaiTiles("9p 4z 7z");
        foreach (Tile tile in TenpaiTiles("1p 1z 5z 0s"))
        {
            Assert(MahjongTableEffects.IsVisibleDora(tile, true, indicators), "Face-up dora missed the sweep.");
            Assert(!MahjongTableEffects.IsVisibleDora(tile, false, indicators), "A tile back revealed dora.");
        }
        Assert(!MahjongTableEffects.IsVisibleDora(null, true, indicators), "An empty tile slot was treated as dora.");
        Assert(TenpaiTiles("9p 4z 7z 5s").All(tile => !MahjongTableEffects.IsVisibleDora(tile, true, indicators)),
            "An indicator or ordinary five was incorrectly treated as dora.");
    }

    private static void DiscardFrames()
    {
        MainMatchControl match = NewMatch();
        Tile discarded = match.MainTileList[match.CurrentTileIndex];
        Tile matchFace = HandAnalysis.TileOf(HandAnalysis.Face(discarded));
        match.PlayerList[1].PlayerHandTile.HandTileList[0] = matchFace;
        match.PlayerList[1].PlayerHandTile.HandTileList[1] = matchFace;
        var machine = Machine(match);
        Submit(machine, machine.GetActions(0).Single(action => action.PlayerAction == PlayerAction.TakeCut));

        VisibleMatchState observer = machine.Journal.GetSnapshot(1);
        var choices = observer.GetAvailableActions();
        Assert(MahjongTableEffects.ActiveDiscardIndex(observer, 0) == match.DiscardTileList[0].Count - 1
            && MahjongTableEffects.ActiveDiscardIndex(observer, 1) == -1, "The frame picked the wrong river or discard.");
        Assert(MahjongTableEffects.CanRespondToDiscard(observer, choices), "An eligible seat did not get the extra frame.");
        Assert(!MahjongTableEffects.CanRespondToDiscard(observer, choices.Where(action => action.PlayerAction == PlayerAction.Skip).ToArray()),
            "Skip alone showed the response frame.");
        observer.HasResponded = true;
        Assert(!MahjongTableEffects.CanRespondToDiscard(observer, choices) && MahjongTableEffects.ActiveDiscardIndex(observer, 0) >= 0,
            "Replying did not clear only the observer's response frame.");
        for (int guard = 0; machine.Stage == MatchOperationStage.DiscardResponse && guard < 4; guard++)
        {
            Submit(machine, Enumerable.Range(0, 4).SelectMany(machine.GetActions).First(action => action.PlayerAction == PlayerAction.Skip));
        }
        observer = machine.Journal.GetSnapshot(1);
        Assert(MahjongTableEffects.ActiveDiscardIndex(observer, 0) == -1 && !MahjongTableEffects.CanRespondToDiscard(observer, choices),
            "The discard frames persisted after the response window.");
    }
}
