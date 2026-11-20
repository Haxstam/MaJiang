using MaJiangLib;
using System;
using System.Linq;
using System.Threading.Tasks;

internal static partial class Program
{
    private static Task EngineEnumeratesTripletAndSequenceDecompositions()
    {
        HandTile hand = new(1) { HandTileList = Tiles("2p 2p 2p 3p 3p 3p 4p 4p 4p 5s 6s 7s 5z") };
        string before = Fingerprint(hand);
        var shapes = HandAnalysis.Shapes(hand, Tile.BytesTo("5z"));
        Assert(shapes.Count == 2 && shapes.All(shape => shape.Pair == HandAnalysis.Face(Tile.BytesTo("5z"))),
            "Expected both decompositions with the white-dragon pair.");
        Assert(shapes.Any(shape => new[] { "2p", "3p", "4p" }.All(tile => shape.Melds.Contains((HandAnalysis.Face(Tile.BytesTo(tile)), true)))),
            "DFS did not retain the three concealed-triplet decomposition.");
        Assert(shapes.Any(shape => shape.Melds.Count(meld => !meld.Triple && meld.Face == HandAnalysis.Face(Tile.BytesTo("2p"))) == 3),
            "DFS did not retain the three 234p sequences.");
        Assert(before == Fingerprint(hand), "DFS changed its input hand.");
        hand.HandTileList.Reverse();
        Assert(HandAnalysis.Shapes(hand, Tile.BytesTo("5z")).Count == 2, "Input order changed decomposition enumeration.");
        return Task.CompletedTask;
    }

    private static Task EngineEnumeratesEveryPossiblePair()
    {
        HandTile hand = new(1) { HandTileList = Tiles("1w 1w 2w 2w 3w 3w 4w 4w 5w 5w 6w 6w 7w") };
        var shapes = HandAnalysis.Shapes(hand, Tile.BytesTo("7w"));
        Assert(shapes.Count == 3 && shapes.Select(shape => shape.Pair).OrderBy(pair => pair).SequenceEqual(new[] { 0, 3, 6 }),
            "DFS stopped after the first pair or lost one of the 11/44/77 pair decompositions.");
        Assert(shapes.All(shape => shape.Melds.Count == 4 && shape.Melds.All(meld => !meld.Triple)), "Invalid four-meld decomposition.");
        return Task.CompletedTask;
    }

    private static MachineFixture ChankanOnlyFixture(bool red = false)
    {
        MachineFixture fixture = new();
        fixture.Hand(0, "1w 2w 3w 4w 5w 6w 7s 8s 9s 5z", red ? "0p" : "5p");
        fixture.Open(0, new Group(GroupType.OpenTriple, Color.Tungs, Tiles("5p 5p"), 1, Tile.BytesTo("5p")));
        // Winning 5p completes 345p: the robber holds no 5p, since the source already owns all four.
        fixture.Hand(2, "4w 5w 6w 3p 4p 6s 7s 8s 2z 2z");
        fixture.Open(2, new Group(GroupType.OpenStraight, Color.Wans, Tiles("1w 2w"), 3, Tile.BytesTo("3w")));
        fixture.Core.FirstCycleIppatsu = false;
        fixture.Core.DoraPointerList.Clear();
        return fixture;
    }

    private static Task EngineChankanOnlyYakuSettlesWithExactWinningTile()
    {
        foreach (bool red in new[] { false, true })
        {
            MachineFixture fixture = ChankanOnlyFixture(red);
            Tile tile = fixture.Core.PlayerList[0].PlayerHandTile.SingleTile;
            Assert(RiichiScoring.Evaluate(new WinContext
            {
                State = fixture.Core, Hand = fixture.Core.PlayerList[2].PlayerHandTile, WinningTile = tile
            }) == null, "The fixture already has another yaku without robbing kan.");
            MatchSession session = StartSession(fixture);
            int remaining = fixture.Core.RemainTileCount;
            long before = fixture.Journal.LatestSequence;
            fixture.Act(0, PlayerAction.AddedKan);
            Assert(fixture.Machine.Stage == MatchOperationStage.AddedKanResponse && fixture.Journal.LatestSequence == before,
                "The added kan committed or was logged before the rob-kan response.");
            Assert(fixture.Machine.GetActions(2).Any(action => action.PlayerAction == PlayerAction.Ron), "Chankan-only ron was not offered.");
            fixture.Act(2, PlayerAction.Ron); session.SettleIfEnded();
            WinnerSettlement winner = fixture.Journal.Settlement.Winners.Single();
            Assert(winner.Seat == 2 && winner.Score.Han == (red ? 2 : 1) && winner.Score.Fu == 30 &&
                winner.Score.Yaku.Any(yaku => yaku.Yaku == YakuType.Chankan) && winner.WinningTile.IsRedDora == red,
                "Rob-kan yaku, fu or red winning tile was lost in settlement.");
            Assert(fixture.Core.PlayerPoint[0] == 25000 - (red ? 2000 : 1000) && fixture.Core.PlayerPoint[2] == 25000 + (red ? 2000 : 1000),
                "Chankan-only ron payment is wrong.");
            Assert(fixture.Core.PlayerOpenSetList[0].Single().GroupType == GroupType.OpenTriple && fixture.Core.KangCount == 0 &&
                fixture.Core.PrimeTileIndex == 0 && fixture.Core.RemainTileCount == remaining && fixture.Core.DoraPointerList.Count == 0,
                "Robbed kan changed the pon, rinshan, remaining wall or indicators.");
            Assert(fixture.Core.PlayerList[0].PlayerHandTile.SingleTile == null &&
                !fixture.Core.PlayerList[0].PlayerHandTile.HandTileList.Any(held => held == tile), "Robbed fourth tile stayed in source hand.");
            MatchRecord[] records = fixture.Journal.GetRecords(fixture.Journal.RoundId, before).ToArray();
            Assert(records.Length == 2 && records[0].Action?.PlayerAction == PlayerAction.Ron &&
                Tile.ExactlyEquals(records[0].Action.TargetTile, tile) && records[1].Kind == MatchRecordKind.RoundSettled,
                "Journal recorded a tentative/robbed kan or lost the exact ron tile.");
        }
        return Task.CompletedTask;
    }

    private static MachineFixture ClosedKanRobFixture(bool kokushi)
    {
        MachineFixture fixture = new();
        fixture.Hand(0, "1w 1w 1w 2p 3p 4p 6s 7s 8s 5z 6z 7z 9p", "1w");
        fixture.Hand(1, "2w 3w 1p 2p 3p 4s 5s 6s 7z 7z 7z 5z 5z");
        if (kokushi) { fixture.Hand(2, "9w 9w 1p 9p 1s 9s 1z 2z 3z 4z 5z 6z 7z"); }
        fixture.Core.FirstCycleIppatsu = false;
        return fixture;
    }

    private static Task EngineOnlyKokushiCanRobClosedKanAndSettle()
    {
        MachineFixture fixture = ClosedKanRobFixture(true);
        Assert(RiichiScoring.Evaluate(new WinContext
        {
            State = fixture.Core, Hand = fixture.Core.PlayerList[1].PlayerHandTile, WinningTile = Tile.BytesTo("1w")
        }) != null, "Normal-hand exclusion fixture is not a legal ron hand.");
        MatchSession session = StartSession(fixture);
        int remaining = fixture.Core.RemainTileCount, indicators = fixture.Core.DoraPointerList.Count;
        long before = fixture.Journal.LatestSequence;
        fixture.Act(0, PlayerAction.ClosedKan);
        Assert(fixture.Machine.Stage == MatchOperationStage.ClosedKanResponse && fixture.Journal.LatestSequence == before,
            "Closed kan was committed before kokushi's response.");
        Assert(!fixture.Machine.GetActions(1).Any(action => action.PlayerAction == PlayerAction.Ron) &&
            fixture.Machine.GetActions(2).Any(action => action.PlayerAction == PlayerAction.Ron), "Closed-kan rob eligibility was not restricted to kokushi.");
        fixture.Act(2, PlayerAction.Ron); session.SettleIfEnded();
        WinnerSettlement winner = fixture.Journal.Settlement.Winners.Single();
        Assert(fixture.Journal.WinByRobbingKan && winner.Score.Yakuman == 1 &&
            winner.Score.Yaku.Any(yaku => yaku.Yaku == YakuType.KokushiMusou) && winner.Payments[2] == 32000 && winner.Payments[0] == -32000,
            "Kokushi rob-closed-kan did not reach the actual yakuman settlement.");
        Assert(fixture.Core.PlayerOpenSetList[0].Count == 0 && fixture.Core.KangCount == 0 && fixture.Core.PrimeTileIndex == 0 &&
            fixture.Core.RemainTileCount == remaining && fixture.Core.DoraPointerList.Count == indicators &&
            fixture.Core.PlayerList[0].PlayerHandTile.HandTileList.Count(tile => tile == Tile.BytesTo("1w")) == 3,
            "Robbed closed kan changed the wall or removed more than the winning tile.");
        Assert(!fixture.Journal.GetRecords(fixture.Journal.RoundId).Any(record => record.Action?.PlayerAction == PlayerAction.ClosedKan),
            "Robbed closed kan was recorded as successful.");
        return Task.CompletedTask;
    }

    private static Task EngineNormalWinningHandCannotRobClosedKan()
    {
        MachineFixture fixture = ClosedKanRobFixture(false);
        fixture.Start(); fixture.Act(0, PlayerAction.ClosedKan);
        Assert(fixture.Machine.Stage == MatchOperationStage.Discard && fixture.Core.KangCount == 1 && fixture.Core.PrimeTileIndex == 1 &&
            fixture.Journal.GetRecords(fixture.Journal.RoundId).Count(record => record.Action?.PlayerAction == PlayerAction.ClosedKan) == 1 &&
            !fixture.Journal.GetRecords(fixture.Journal.RoundId).Any(record => record.Action?.PlayerAction == PlayerAction.Ron),
            "A normal winning hand robbed a closed kan or prevented its resolution.");
        return Task.CompletedTask;
    }

    private static Task EngineExplicitSkipDuringRobKanCommitsTheKan()
    {
        MachineFixture fixture = ChankanOnlyFixture(); fixture.Start();
        fixture.Act(0, PlayerAction.AddedKan); long before = fixture.Journal.LatestSequence;
        fixture.Act(2, PlayerAction.Skip);
        Assert(fixture.Machine.Stage == MatchOperationStage.Discard && fixture.Core.KangCount == 1 &&
            fixture.Core.PlayerOpenSetList[0].Single().GroupType == GroupType.AddedKan && fixture.Core.PrimeTileIndex == 1 &&
            (fixture.Journal.FuritenFlags[2] & 2) != 0, "Explicit pass did not complete kan/rinshan or mark missed ron.");
        MatchRecord[] records = fixture.Journal.GetRecords(fixture.Journal.RoundId, before).ToArray();
        Assert(records.Length == 2 && records[0].Action?.PlayerAction == PlayerAction.AddedKan && records[1].Action?.PlayerAction == PlayerAction.Take,
            "Passing chankan was logged as intent, won automatically, or lost the committed kan/rinshan.");
        return Task.CompletedTask;
    }
}
