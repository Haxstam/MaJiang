using MaJiangLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatchType = MaJiangLib.MatchType;

internal static partial class Program
{
    private static List<Tile> Tiles(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(tile => Tile.BytesTo(tile)).ToList();
    private const string Inert = "1w 1w 1w 2w 2w 2w 4p 4p 4p 7s 7s 5z 6z";
    private const string YakuhaiWait = "1w 2w 3w 1p 2p 3p 1s 2s 3s 7z 7z 7z 5z";
    private sealed class MachineFixture
    {
        public long Now;
        public MainMatchControl Core = SyncCore();
        public MatchJournal Journal;
        public AuthoritativeMatchMachine Machine;
        public MachineFixture()
        {
            for (int seat = 0; seat < 4; seat++) { Hand(seat, Inert); }
            Core.CurrentPlayerIndex = 0; Core.PlayerList[0].PlayerHandTile.SingleTile = Tile.BytesTo("7z");
        }
        public void Hand(int seat, string tiles, string drawn = null)
        {
            Core.PlayerList[seat].PlayerHandTile.HandTileList = Tiles(tiles);
            Core.PlayerList[seat].PlayerHandTile.SingleTile = drawn == null ? null : Tile.BytesTo(drawn);
        }
        public void Open(int seat, Group group)
        {
            Core.PlayerOpenSetList[seat].Add(group); Core.PlayerList[seat].PlayerHandTile.OpenTileList = Core.PlayerOpenSetList[seat];
        }
        public void Start(bool draw = false)
        {
            Journal = new MatchJournal(Core, () => Now);
            Machine = new AuthoritativeMatchMachine(Journal, () => Now, draw);
        }
        public MatchCommand Command(int seat, PlayerActionData action) => new()
        {
            MatchId = Journal.MatchId, RoundId = Journal.RoundId, RoundGeneration = Journal.RoundGeneration,
            PhaseId = Journal.PhaseId, CommandId = Guid.NewGuid(), Action = action
        };
        public MatchCommand Pick(int seat, PlayerAction type, Func<PlayerActionData, bool> predicate = null)
        {
            PlayerActionData action = Machine.GetActions(seat).FirstOrDefault(action => action.PlayerAction == type && (predicate == null || predicate(action)));
            Assert(action != null, $"Missing {type} for seat {seat} in {Machine.Stage}.");
            return Command(seat, action);
        }
        public MatchCommandResult Act(int seat, PlayerAction type, Func<PlayerActionData, bool> predicate = null) => Machine.Submit(seat, Pick(seat, type, predicate));
        public void PassResponses()
        {
            MatchOperationStage stage = Machine.Stage;
            if (stage != MatchOperationStage.DiscardResponse && stage != MatchOperationStage.AddedKanResponse && stage != MatchOperationStage.ClosedKanResponse) { return; }
            MatchCommand[] commands = Enumerable.Range(0, 4).Where(seat => !Journal.HasResponded[seat]).Select(seat => Pick(seat, PlayerAction.Skip)).ToArray();
            foreach (MatchCommand command in commands) { Assert(Machine.Submit(command.Action.SelfNumber, command).Accepted, "Skip was rejected."); }
        }
    }

    private static Task MachineSharedQueries()
    {
        MachineFixture fixture = new(); fixture.Start();
        AssertSharedMachineQueries(fixture);
        MachineFixture calls = CallsFixture(); AssertSharedMachineQueries(calls);
        calls.Act(1, PlayerAction.Skip); calls.Act(2, PlayerAction.Peng); AssertSharedMachineQueries(calls);
        MachineFixture kan = AddedKanFixture(); kan.Act(0, PlayerAction.AddedKan); AssertSharedMachineQueries(kan);
        return Task.CompletedTask;
    }

    private static void AssertSharedMachineQueries(MachineFixture fixture)
    {
        for (int seat = 0; seat < 4; seat++)
        {
            byte[] before = MatchSyncCodec.EncodeSnapshot(fixture.Journal.GetSnapshot(seat));
            IReadOnlyList<PlayerActionData> local = fixture.Journal.GetSnapshot(seat).GetAvailableActions();
            IReadOnlyList<PlayerActionData> authoritative = fixture.Machine.GetActions(seat);
            Assert(local.Count == authoritative.Count && local.All(action => authoritative.Any(candidate => MatchActionRules.Matches(candidate, action))), "Client/server legal choices differ.");
            Assert(before.SequenceEqual(MatchSyncCodec.EncodeSnapshot(fixture.Journal.GetSnapshot(seat))), "Query mutated authoritative state.");
        }
    }

    private static Task MachineTurnAndRedDiscard()
    {
        MachineFixture fixture = new(); fixture.Hand(0, "0w 5w 1p 2p 3p 4p 5p 6p 1s 2s 3s 5z 6z", "7z"); fixture.Start();
        Assert(fixture.Act(0, PlayerAction.HandCut, action => action.TargetTile.IsRedDora).Accepted, "Hand discard rejected.");
        HandTile hand = fixture.Core.PlayerList[0].PlayerHandTile;
        Assert(hand.HandTileList.Count == 13 && hand.HandTileList.Any(tile => tile == Tile.BytesTo("5w") && !tile.IsRedDora)
            && !hand.HandTileList.Any(tile => tile.IsRedDora) && hand.HandTileList.Any(tile => tile == Tile.BytesTo("7z")), "Wrong red tile consumed or draw lost.");
        Assert(fixture.Core.DiscardTileList[0].Single().IsRedDora, "Discard red identity lost.");
        fixture.PassResponses();
        Assert(fixture.Core.CurrentPlayerIndex == 1 && fixture.Core.RemainTileCount == 69, "Next player did not draw exactly once.");
        MachineFixture sameFace = new(); sameFace.Core.PlayerList[0].PlayerHandTile.SingleTile = Tile.BytesTo("5z"); sameFace.Start();
        Tile heldDraw = sameFace.Core.PlayerList[0].PlayerHandTile.SingleTile;
        sameFace.Act(0, PlayerAction.HandCut, action => action.TargetTile == Tile.BytesTo("5z"));
        Assert(sameFace.Core.PlayerList[0].PlayerHandTile.HandTileList.Any(tile => ReferenceEquals(tile, heldDraw)),
            "Explicit hand discard of the same face as the draw was silently treated as tsumogiri.");
        return Task.CompletedTask;
    }

    private static Task MachineDefaultAndReplay()
    {
        MachineFixture fixture = new(); fixture.Start();
        MatchCommand invalid = fixture.Command(0, new(0, 0, new(), Tile.BytesTo("5p"), PlayerAction.Peng));
        MatchCommandResult result = fixture.Machine.Submit(0, invalid);
        Assert(!result.Accepted && result.UsedDefault && fixture.Core.DiscardTileList[0].Single() == Tile.BytesTo("7z"), "Illegal self-action did not default to drawn tile.");
        long sequence = fixture.Journal.LatestSequence;
        Assert(ReferenceEquals(result, fixture.Machine.Submit(0, invalid)) && fixture.Journal.LatestSequence == sequence, "Duplicate command executed twice.");
        MatchCommand stale = fixture.Command(1, new(1, 1, new(), null, PlayerAction.Skip)); stale.PhaseId = invalid.PhaseId;
        Assert(fixture.Machine.Submit(1, stale).Reason == "StalePhase" && fixture.Journal.LatestSequence == sequence, "Stale command consumed a current opportunity.");
        invalid.Action.TargetTile = Tile.BytesTo("1w");
        Assert(fixture.Machine.Submit(0, invalid).Reason == "CommandIdReused", "Mutated duplicate id accepted.");
        return Task.CompletedTask;
    }

    private static Task MachineAuthentication()
    {
        MachineFixture fixture = new(); fixture.Start(); long sequence = fixture.Journal.LatestSequence;
        MatchCommand spoof = fixture.Pick(0, PlayerAction.TakeCut);
        Assert(fixture.Machine.Submit(1, spoof).Reason == "ActorMismatch", "Client actor was trusted.");
        spoof.RoundId = Guid.NewGuid(); Assert(fixture.Machine.Submit(0, spoof).Reason == "StaleRound", "Wrong round accepted.");
        Assert(fixture.Journal.LatestSequence == sequence, "Rejected identity changed state.");
        return Task.CompletedTask;
    }

    private static Task MachineClocks()
    {
        MachineFixture fixture = new(); fixture.Start(); fixture.Now = 6000;
        fixture.Act(0, PlayerAction.TakeCut);
        Assert(fixture.Journal.GetSnapshot(0).ThinkingMilliseconds == 19000, "Base time/long-think debit incorrect.");
        for (int seat = 1; seat < 4; seat++)
        {
            fixture.Core.PlayerList[seat].PlayerHandTile.SingleTile = Tile.BytesTo("7z");
            fixture.Act(seat, PlayerAction.TakeCut); fixture.PassResponses();
        }
        Assert(fixture.Core.CurrentPlayerIndex == 0 && fixture.Journal.PlayerDeadlines[0] == 30000, "Long-think bank reset within the round.");
        MachineFixture timeout = new(); timeout.Start(); MatchCommand late = timeout.Pick(0, PlayerAction.TakeCut);
        timeout.Now = 25000; timeout.Machine.Tick();
        Assert(timeout.Journal.GetSnapshot(0).ThinkingMilliseconds == 0 && timeout.Core.DiscardTileList[0].Count == 1, "Deadline did not force exactly one discard.");
        long sequence = timeout.Journal.LatestSequence;
        Assert(timeout.Machine.Submit(0, late).Reason == "StalePhase" && timeout.Journal.LatestSequence == sequence, "Deadline-edge request was applied twice.");
        return Task.CompletedTask;
    }

    private static MachineFixture CallsFixture()
    {
        MachineFixture fixture = new(); fixture.Core.PlayerList[0].PlayerHandTile.SingleTile = Tile.BytesTo("3w");
        fixture.Hand(1, "1w 2w 4w 6w 8w 1p 2p 4p 5p 7s 8s 5z 6z");
        fixture.Hand(2, "3w 3w 4w 6w 8w 1p 2p 4p 5p 7s 8s 5z 6z");
        fixture.Start(); fixture.Act(0, PlayerAction.TakeCut); return fixture;
    }

    private static Task MachineCallPriority()
    {
        MachineFixture fixture = CallsFixture();
        MatchCommand chi = fixture.Pick(1, PlayerAction.Chi); MatchCommand pon = fixture.Pick(2, PlayerAction.Peng);
        fixture.Machine.Submit(1, chi);
        Assert(fixture.Machine.Stage == MatchOperationStage.DiscardResponse && fixture.Core.PlayerOpenSetList[1].Count == 0, "First packet won the claim prematurely.");
        fixture.Machine.Submit(2, pon);
        Assert(fixture.Machine.Stage == MatchOperationStage.DiscardAfterCall && fixture.Core.CurrentPlayerIndex == 2
            && fixture.Core.PlayerOpenSetList[2].Single().GroupType == GroupType.OpenTriple && fixture.Core.PlayerOpenSetList[1].Count == 0
            && fixture.Core.RemainTileCount == 70, "Pon did not beat chi or caused an extra draw.");
        Assert(!fixture.Machine.GetActions(2).Any(action => action.TargetTile == Tile.BytesTo("3w")), "Kuikae discard offered.");
        Tile expected = fixture.Machine.GetActions(2).Where(action => action.PlayerAction == PlayerAction.HandCut).OrderBy(action => action.TargetTile).Last().TargetTile;
        MatchCommand invalid = fixture.Command(2, new(2, 2, new(), Tile.BytesTo("3w"), PlayerAction.HandCut));
        MatchCommandResult result = fixture.Machine.Submit(2, invalid);
        Assert(result.UsedDefault && fixture.Core.DiscardTileList[2].Last() == expected && fixture.Core.PlayerList[2].PlayerHandTile.HandTileList.Count == 10, "Post-call default ignored legality or lost hand count.");
        return Task.CompletedTask;
    }

    private static Task MachineInvalidResponse()
    {
        MachineFixture fixture = CallsFixture();
        MatchCommand wrong = fixture.Command(2, new(2, 0, Tiles("1w 2w"), Tile.BytesTo("3w"), PlayerAction.Chi));
        MatchCommandResult result = fixture.Machine.Submit(2, wrong);
        Assert(!result.Accepted && result.UsedDefault && fixture.Journal.HasResponded[2], "Illegal response did not become skip.");
        fixture.Act(1, PlayerAction.Skip);
        Assert(fixture.Core.PlayerOpenSetList.All(groups => groups.Count == 0), "An illegal claim changed melds.");
        return Task.CompletedTask;
    }

    private static Task MachineChiRestriction()
    {
        MachineFixture fixture = new(); fixture.Core.PlayerList[0].PlayerHandTile.SingleTile = Tile.BytesTo("3w");
        fixture.Hand(1, "4w 5w 6w 7w 8w 1p 2p 4p 5p 7s 8s 5z 6z"); fixture.Start(); fixture.Act(0, PlayerAction.TakeCut);
        fixture.Act(1, PlayerAction.Chi, action => action.Tiles.Any(tile => tile.Number == 4) && action.Tiles.Any(tile => tile.Number == 5));
        Assert(fixture.Machine.Stage == MatchOperationStage.DiscardAfterCall && !fixture.Machine.GetActions(1).Any(action => action.TargetTile == Tile.BytesTo("6w")), "Suji kuikae offered.");
        return Task.CompletedTask;
    }

    private static Task MachineRedPon()
    {
        MachineFixture fixture = new(); fixture.Core.PlayerList[0].PlayerHandTile.SingleTile = Tile.BytesTo("5w");
        fixture.Hand(2, "0w 5w 5w 4w 8w 1p 2p 4p 5p 7s 8s 5z 6z"); fixture.Start(); fixture.Act(0, PlayerAction.TakeCut);
        fixture.Act(2, PlayerAction.Peng, action => action.Tiles.All(tile => !tile.IsRedDora));
        Assert(fixture.Core.PlayerList[2].PlayerHandTile.HandTileList.Any(tile => tile.IsRedDora)
            && fixture.Core.PlayerOpenSetList[2].Single().Tiles.All(tile => !tile.IsRedDora), "Pon consumed the wrong physical five.");
        return Task.CompletedTask;
    }

    private static Task MachineMultiRon()
    {
        foreach (int winners in new[] { 2, 3 })
        {
            MachineFixture fixture = new(); fixture.Core.PlayerList[0].PlayerHandTile.SingleTile = Tile.BytesTo("5z");
            for (int seat = 1; seat <= winners; seat++) { fixture.Hand(seat, YakuhaiWait); }
            fixture.Start(); fixture.Act(0, PlayerAction.TakeCut);
            MatchCommand[] ron = Enumerable.Range(1, winners).Select(seat => fixture.Pick(seat, PlayerAction.Ron)).ToArray();
            foreach (MatchCommand command in ron.Reverse()) { Assert(fixture.Machine.Submit(command.Action.SelfNumber, command).Accepted, "Ron rejected."); }
            Assert(fixture.Journal.Outcome == RoundOutcomeKind.Ron && fixture.Journal.WinnerMask == (winners == 2 ? 6 : 14), "Multiple ron was lost or turned into triple-ron draw.");
        }
        return Task.CompletedTask;
    }

    private static Task MachineNoYakuAndFuriten()
    {
        MachineFixture fixture = new(); fixture.Core.PlayerList[0].PlayerHandTile.SingleTile = Tile.BytesTo("5p");
        fixture.Hand(2, "4w 5w 6w 2p 3p 4p 6s 7s 8s 5p");
        fixture.Open(2, new Group(GroupType.OpenStraight, Color.Wans, Tiles("1w 2w"), 3, Tile.BytesTo("3w")));
        fixture.Core.DoraPointerList.Clear(); fixture.Core.DoraPointerList.Add(Tile.BytesTo("4p")); fixture.Start();
        Assert(HandAnalysis.HasWinningShape(fixture.Core.PlayerList[2].PlayerHandTile, Tile.BytesTo("5p"))
            && !HandAnalysis.HasYaku(fixture.Core, fixture.Core.PlayerList[2].PlayerHandTile, Tile.BytesTo("5p"), false, MatchOperationStage.DiscardResponse), "Dora was treated as yaku.");
        fixture.Act(0, PlayerAction.TakeCut);
        Assert((fixture.Journal.FuritenFlags[2] & 2) != 0, "No-yaku completed shape did not cause temporary furiten.");
        return Task.CompletedTask;
    }

    private static Task MachineFuritenExpiry()
    {
        foreach (bool riichi in new[] { false, true })
        {
            MachineFixture fixture = new(); fixture.Core.PlayerList[0].PlayerHandTile.SingleTile = Tile.BytesTo("5z");
            fixture.Hand(2, YakuhaiWait); fixture.Core.IsRiichi[2] = riichi; fixture.Start(); fixture.Act(0, PlayerAction.TakeCut); fixture.Act(2, PlayerAction.Skip);
            Assert((fixture.Journal.FuritenFlags[2] & (riichi ? 4 : 2)) != 0, "Missed ron furiten missing.");
            fixture.Core.PlayerList[1].PlayerHandTile.SingleTile = Tile.BytesTo("7z"); fixture.Act(1, PlayerAction.TakeCut); fixture.PassResponses();
            Assert(fixture.Core.CurrentPlayerIndex == 2 && (riichi ? (fixture.Journal.FuritenFlags[2] & 4) != 0 : (fixture.Journal.FuritenFlags[2] & 2) == 0), "Furiten cleared at the wrong time.");
        }
        return Task.CompletedTask;
    }

    private static Task MachineRiichiConfirmation()
    {
        MachineFixture fixture = new(); fixture.Hand(0, YakuhaiWait, "9p");
        fixture.Hand(1, "9p 9p 4w 6w 8w 1p 2p 4p 5p 7s 8s 5z 6z"); fixture.Start();
        fixture.Act(0, PlayerAction.Riichi, action => action.TargetTile == Tile.BytesTo("9p"));
        Assert(!fixture.Core.IsRiichi[0] && fixture.Core.PlayerPoint[0] == 25000 && fixture.Core.RiichiStickCount == 0 && fixture.Journal.PendingRiichiSeat == 0, "Riichi charged before its discard survived ron.");
        fixture.Act(1, PlayerAction.Peng);
        Assert(fixture.Core.IsRiichi[0] && fixture.Core.IsDoubleRiichi[0] && fixture.Core.PlayerPoint[0] == 24000 && fixture.Core.RiichiStickCount == 1 && !fixture.Core.HaveIppatsu[0], "Called declaration failed to confirm or kept ippatsu.");
        return Task.CompletedTask;
    }

    private static Task MachineRiichiRonned()
    {
        MachineFixture fixture = new(); fixture.Hand(0, YakuhaiWait, "9p");
        fixture.Hand(2, "1w 2w 3w 1p 2p 3p 1s 2s 3s 7z 7z 7z 9p"); fixture.Start();
        fixture.Act(0, PlayerAction.Riichi, action => action.TargetTile == Tile.BytesTo("9p")); fixture.Act(2, PlayerAction.Ron);
        Assert(fixture.Journal.Outcome == RoundOutcomeKind.Ron && !fixture.Core.IsRiichi[0] && fixture.Core.RiichiStickCount == 0 && fixture.Core.PlayerPoint[0] == 25000, "Ronned riichi declaration charged supply.");
        return Task.CompletedTask;
    }

    private static Task MachineClosedKan()
    {
        MachineFixture fixture = new(); fixture.Hand(0, "1w 1w 1w 1w 2p 3p 4p 6s 7s 8s 5z 6z 7z", "9p"); fixture.Core.PrimeTileList[10] = Tile.BytesTo("7p"); fixture.Start();
        fixture.Act(0, PlayerAction.ClosedKan);
        Assert(fixture.Core.KangCount == 1 && fixture.Core.PrimeTileIndex == 1 && fixture.Core.RemainTileCount == 69
            && fixture.Core.DoraPointerList.Count == 2 && fixture.Journal.PendingDoraCount == 0 && fixture.Core.IsKan[0], "Closed kan/rinshan/dora order failed.");
        Assert(fixture.Core.PlayerList[0].PlayerHandTile.HandTileList.Count == 10 && fixture.Core.PlayerList[0].PlayerHandTile.HandTileList.Any(tile => tile == Tile.BytesTo("9p")), "Kan lost the previously held draw.");
        return Task.CompletedTask;
    }

    private static MachineFixture AddedKanFixture()
    {
        MachineFixture fixture = new(); fixture.Hand(0, "1w 2w 3w 1p 2p 3p 1s 2s 3s 5z", "7z");
        fixture.Open(0, new Group(GroupType.OpenTriple, Color.Honor, Tiles("7z 7z"), 1, Tile.BytesTo("7z")));
        fixture.Core.PrimeTileList[10] = Tile.BytesTo("5z"); fixture.Start(); return fixture;
    }

    private static Task MachineAddedKanDora()
    {
        MachineFixture fixture = AddedKanFixture(); fixture.Act(0, PlayerAction.AddedKan);
        Assert(fixture.Core.KangCount == 1 && fixture.Core.DoraPointerList.Count == 1 && fixture.Journal.PendingDoraCount == 1
            && fixture.Machine.GetActions(0).Any(action => action.PlayerAction == PlayerAction.Tsumo), "Added kan dora opened before rinshan win.");
        fixture.Act(0, PlayerAction.Tsumo);
        Assert(fixture.Journal.Outcome == RoundOutcomeKind.Tsumo && fixture.Core.DoraPointerList.Count == 1, "Rinshan win incorrectly revealed added-kan dora.");
        MachineFixture discard = AddedKanFixture(); discard.Act(0, PlayerAction.AddedKan); discard.Act(0, PlayerAction.TakeCut);
        Assert(discard.Core.DoraPointerList.Count == 2 && discard.Journal.PendingDoraCount == 0, "Added-kan dora did not open on discard.");
        return Task.CompletedTask;
    }

    private static Task MachineRobAddedKan()
    {
        MachineFixture fixture = new(); fixture.Hand(0, "1w 2w 3w 4w 5w 6w 7s 8s 9s 5z", "5p");
        fixture.Open(0, new Group(GroupType.OpenTriple, Color.Tungs, Tiles("5p 5p"), 1, Tile.BytesTo("5p")));
        fixture.Hand(2, "1w 2w 3w 6p 7p 1s 2s 3s 7z 7z 7z 2z 2z"); fixture.Start();
        fixture.Act(0, PlayerAction.AddedKan);
        Assert(fixture.Machine.Stage == MatchOperationStage.AddedKanResponse && fixture.Core.KangCount == 0, "Added kan committed before rob response.");
        fixture.Act(2, PlayerAction.Ron);
        Assert(fixture.Journal.Outcome == RoundOutcomeKind.Ron && fixture.Core.PlayerOpenSetList[0].Single().GroupType == GroupType.OpenTriple
            && fixture.Core.PrimeTileIndex == 0 && fixture.Core.RemainTileCount == 70 && fixture.Core.DoraPointerList.Count == 1, "Robbed kan changed wall/meld/dora.");
        Assert(fixture.Core.PlayerList[0].PlayerHandTile.SingleTile == null && fixture.Core.PlayerList[0].PlayerHandTile.HandTileList.Count == 10,
            "Robbed added tile remained in source hand.");
        return Task.CompletedTask;
    }

    private static Task MachineContinuousKanRob()
    {
        MachineFixture fixture = new(); fixture.Hand(0, "1w 1w 1w 1w 2p 3p 4p 6s 7s 8s", "7z");
        fixture.Open(0, new Group(GroupType.OpenTriple, Color.Honor, Tiles("7z 7z"), 1, Tile.BytesTo("7z")));
        fixture.Hand(2, "9w 9w 1p 9p 1s 9s 1z 2z 3z 4z 5z 6z 7z"); fixture.Core.PrimeTileList[10] = Tile.BytesTo("9p"); fixture.Start();
        fixture.Act(0, PlayerAction.AddedKan); fixture.Act(0, PlayerAction.ClosedKan);
        Assert(fixture.Core.DoraPointerList.Count == 2 && fixture.Core.KangCount == 1 && fixture.Machine.Stage == MatchOperationStage.ClosedKanResponse, "Previous pending dora not revealed on next kan declaration.");
        fixture.Act(2, PlayerAction.Ron);
        Assert(fixture.Journal.Outcome == RoundOutcomeKind.Ron && fixture.Core.KangCount == 1 && fixture.Core.PrimeTileIndex == 1
            && fixture.Core.PlayerOpenSetList[0].Count == 1, "Robbed closed kan was committed.");
        Assert(fixture.Core.PlayerList[0].PlayerHandTile.SingleTile == null && fixture.Core.PlayerList[0].PlayerHandTile.HandTileList.Count == 10,
            "Robbed closed-kan tile remained in source hand.");
        return Task.CompletedTask;
    }

    private static Task MachineRiichiKan()
    {
        MachineFixture fixture = new(); fixture.Hand(0, "1w 1w 1w 2p 3p 4p 4s 5s 6s 7z 7z 7z 5z", "1w"); fixture.Core.IsRiichi[0] = true; fixture.Start();
        Assert(fixture.Machine.GetActions(0).Any(action => action.PlayerAction == PlayerAction.ClosedKan), "Unchanged riichi triplet kan rejected.");
        MachineFixture changed = new(); changed.Hand(0, "1w 1w 1w 2w 3w 4w 5w 6w 7w 8w 9w 9w 9w", "1w"); changed.Core.IsRiichi[0] = true; changed.Start();
        Assert(!changed.Machine.GetActions(0).Any(action => action.PlayerAction == PlayerAction.ClosedKan), "Riichi kan changed shape/waits.");
        return Task.CompletedTask;
    }

    private static Task MachineExhaustiveWall()
    {
        MainMatchControl core = SyncCore(); long now = 0; MatchJournal journal = new(core, () => now); AuthoritativeMatchMachine machine = new(journal, () => now);
        int steps = 0;
        while (machine.Stage != MatchOperationStage.RoundEnded && steps++ < 500)
        {
            now += 25000; machine.Tick();
        }
        Assert(machine.Stage == MatchOperationStage.RoundEnded && journal.Outcome == RoundOutcomeKind.ExhaustiveDraw
            && core.RemainTileCount == 0 && core.DiscardTileList.Sum(river => river.Count) == 70, "Offline/default game did not exhaust one full wall.");
        Assert(core.PlayerList.All(player => player.PlayerHandTile.SingleTile == null && player.PlayerHandTile.HandTileList.Count == 13), "Exhaustive hand counts are wrong.");
        return Task.CompletedTask;
    }

    private static Task MachineOpenKan()
    {
        MachineFixture fixture = new(); fixture.Core.PlayerList[0].PlayerHandTile.SingleTile = Tile.BytesTo("3w");
        fixture.Hand(2, "3w 3w 3w 2p 3p 4p 6s 7s 8s 5z 5z 6z 7z"); fixture.Start();
        fixture.Act(0, PlayerAction.TakeCut); fixture.Act(2, PlayerAction.OpenKan); fixture.PassResponses();
        Assert(fixture.Core.CurrentPlayerIndex == 2 && fixture.Core.PlayerOpenSetList[2].Single().Tiles.Count == 4
            && fixture.Core.PlayerList[2].PlayerHandTile.HandTileList.Count == 10 && fixture.Core.PlayerList[2].PlayerHandTile.SingleTile != null
            && fixture.Core.RemainTileCount == 69 && fixture.Core.DoraPointerList.Count == 1 && fixture.Journal.PendingDoraCount == 1,
            "Open kan hand/draw/dora state is wrong.");
        fixture.Act(2, PlayerAction.TakeCut);
        Assert(fixture.Core.DoraPointerList.Count == 2 && fixture.Journal.PendingDoraCount == 0, "Open-kan dora not revealed on discard.");
        return Task.CompletedTask;
    }

    private static Task MachineLastRinshanDiscard()
    {
        MachineFixture fixture = new(); fixture.Hand(0, "1z 1z 1z 1z 2p 3p 4p 6s 7s 8s 5z 6z 7z", "9p");
        fixture.Hand(2, "4w 5w 6w 2p 3p 4p 6s 7s 8s 5p");
        fixture.Open(2, new Group(GroupType.OpenStraight, Color.Wans, Tiles("1w 2w"), 3, Tile.BytesTo("3w")));
        fixture.Core.RemainTileCount = 1; fixture.Core.PrimeTileList[10] = Tile.BytesTo("5p"); fixture.Start();
        Assert(!HandAnalysis.HasYaku(fixture.Core, fixture.Core.PlayerList[2].PlayerHandTile, Tile.BytesTo("5p"), false,
            MatchOperationStage.DiscardResponse), "Fixture already has yaku before last tile.");
        fixture.Act(0, PlayerAction.ClosedKan); fixture.Act(0, PlayerAction.TakeCut);
        Assert(fixture.Core.RemainTileCount == 0 && fixture.Machine.GetActions(2).Any(action => action.PlayerAction == PlayerAction.Ron)
            && fixture.Machine.GetActions(2).All(action => action.PlayerAction == PlayerAction.Ron || action.PlayerAction == PlayerAction.Skip),
            "Final rinshan discard did not offer houtei-only ron or allowed a call.");
        fixture.Act(2, PlayerAction.Ron);
        Assert(fixture.Journal.Outcome == RoundOutcomeKind.Ron, "Exhaustive draw preempted houtei ron.");
        return Task.CompletedTask;
    }

    private static Task MachineResponseTimeout()
    {
        MachineFixture fixture = new(); fixture.Core.PlayerList[0].PlayerHandTile.SingleTile = Tile.BytesTo("5z");
        fixture.Hand(2, YakuhaiWait); fixture.Start(); fixture.Act(0, PlayerAction.TakeCut);
        fixture.Now = 25000; fixture.Machine.Tick();
        Assert(fixture.Journal.Outcome == RoundOutcomeKind.None && fixture.Core.CurrentPlayerIndex == 1
            && (fixture.Journal.FuritenFlags[2] & 2) != 0 && fixture.Journal.GetSnapshot(2).ThinkingMilliseconds == 0,
            "Response timeout auto-won, failed to advance or lost missed-win furiten.");
        return Task.CompletedTask;
    }

    private static Task MachineAbortiveDraws()
    {
        MachineFixture winds = new(); winds.Core.CurrentPlayerIndex = 3;
        winds.Core.PlayerList[0].PlayerHandTile.SingleTile = null; winds.Core.PlayerList[3].PlayerHandTile.SingleTile = Tile.BytesTo("1z");
        for (int seat = 0; seat < 3; seat++) { winds.Core.DiscardTileList[seat].Add(Tile.BytesTo("1z")); }
        winds.Start(); winds.Act(3, PlayerAction.TakeCut);
        Assert(winds.Journal.Outcome == RoundOutcomeKind.FourWinds, "Four-winds draw missing.");

        MachineFixture riichi = new(); riichi.Core.CurrentPlayerIndex = 3; riichi.Core.PlayerList[0].PlayerHandTile.SingleTile = null;
        for (int seat = 0; seat < 3; seat++) { riichi.Core.IsRiichi[seat] = true; }
        riichi.Hand(3, YakuhaiWait, "9p"); riichi.Start(); riichi.Act(3, PlayerAction.Riichi, action => action.TargetTile == Tile.BytesTo("9p"));
        Assert(riichi.Journal.Outcome == RoundOutcomeKind.FourRiichi && riichi.Core.PlayerPoint[3] == 24000
            && riichi.Core.RiichiStickCount == 1, "Fourth riichi did not commit before draw.");

        MachineFixture nine = new(); nine.Hand(0, "1w 9w 1p 9p 1s 9s 1z 2z 3z 4z 5z 6z 7z", "2w"); nine.Start(); nine.Act(0, PlayerAction.Draw);
        Assert(nine.Journal.Outcome == RoundOutcomeKind.NineTerminals, "Nine-terminals declaration missing.");
        return Task.CompletedTask;
    }

    private static Task MachineFourthKan()
    {
        foreach (bool ron in new[] { false, true })
        {
            MachineFixture fixture = new(); fixture.Hand(0, "1w 1w 1w 1w 2p 3p 4p 6s 7s 8s 5z 6z 7z", "9p");
            fixture.Core.KangCount = 3; fixture.Core.KangMark = 5; fixture.Core.PrimeTileIndex = 3;
            fixture.Core.PrimeTileList[13] = Tile.BytesTo("5p");
            if (ron) { fixture.Hand(2, YakuhaiWait.Replace("5z", "5p")); }
            fixture.Start(); fixture.Act(0, PlayerAction.ClosedKan);
            Assert(fixture.Machine.Stage == MatchOperationStage.Discard && fixture.Core.KangCount == 4, "Fourth kan ended before rinshan/discard.");
            Assert(!fixture.Machine.GetActions(0).Any(action => action.PlayerAction == PlayerAction.ClosedKan || action.PlayerAction == PlayerAction.AddedKan), "Fifth kan offered.");
            fixture.Act(0, PlayerAction.TakeCut);
            if (ron) { fixture.Act(2, PlayerAction.Ron); }
            Assert(fixture.Journal.Outcome == (ron ? RoundOutcomeKind.Ron : RoundOutcomeKind.FourKans), "Four-kan draw preempted ron or failed after pass.");
        }
        return Task.CompletedTask;
    }

    private static Task MachineMeldYaku()
    {
        MachineFixture fixture = new(); fixture.Hand(2, "4w 5w 6w 7w 8w 9w 2p 3p 4p 5s");
        fixture.Open(2, new Group(GroupType.OpenStraight, Color.Wans, Tiles("2w 3w"), 3, Tile.BytesTo("1w")));
        Assert(HandAnalysis.HasYaku(fixture.Core, fixture.Core.PlayerList[2].PlayerHandTile, Tile.BytesTo("5s"), false,
            MatchOperationStage.DiscardResponse), "Ittsuu depended on called tile storage order.");
        return Task.CompletedTask;
    }

    private static async Task MachineTcpCommands()
    {
        Fixture fixture = new();
        try
        {
            await SyncStart(fixture);
            LanRoomClient actor = fixture.Clients.Single(client => client.Match.Current.SelfSeat == 0);
            MatchCommand command = actor.AvailableCommands.First(choice => choice.Action.PlayerAction == PlayerAction.TakeCut);
            actor.SendCommand(command);
            await Until(() => actor.LastCommandResult?.CommandId == command.CommandId && fixture.Clients.All(client => client.Match.Current.Sequence == fixture.Server.Match.LatestSequence), fixture.Pump);
            Assert(actor.LastCommandResult.Accepted, "TCP command rejected.");
            long sequence = fixture.Server.Match.LatestSequence;
            actor.SendCommand(command); await Task.Delay(10); fixture.Pump();
            Assert(fixture.Server.Match.LatestSequence == sequence, "TCP duplicate replayed the discard.");
            foreach (LanRoomClient client in fixture.Clients) { SyncSameState(client.Match.Current, fixture.Server.Match.GetSnapshot(client.Match.Current.SelfSeat)); }
        }
        finally { await fixture.Cleanup(); }
    }

    private static async Task MachineTcpFullHand()
    {
        Fixture fixture = new();
        try
        {
            await SyncStart(fixture);
            int steps = 0;
            while (fixture.Server.Machine.Stage != MatchOperationStage.RoundEnded && steps++ < 400)
            {
                await Until(() => fixture.Clients.All(client => client.Match.Current.Sequence == fixture.Server.Match.LatestSequence), fixture.Pump);
                LanRoomClient actor = fixture.Clients.First(client => client.AvailableCommands.Count > 0);
                MatchCommand command = actor.AvailableCommands.FirstOrDefault(choice => choice.Action.PlayerAction == PlayerAction.TakeCut)
                    ?? actor.AvailableCommands.First(choice => choice.Action.PlayerAction == PlayerAction.Skip);
                actor.SendCommand(command);
                await Until(() => actor.LastCommandResult?.CommandId == command.CommandId, fixture.Pump);
                Assert(actor.LastCommandResult.Accepted, "Client's own shared query produced an illegal command.");
            }
            await Until(() => fixture.Clients.All(client => client.Match.Current.Sequence == fixture.Server.Match.LatestSequence), fixture.Pump);
            Assert(fixture.Server.Match.Outcome == RoundOutcomeKind.ExhaustiveDraw && fixture.Server.Match.AuthoritativeState.DiscardTileList.Sum(river => river.Count) == 70,
                "Four TCP clients failed to finish one exhaustive hand.");
            foreach (LanRoomClient client in fixture.Clients)
            {
                Assert(client.AvailableCommands.Count == 0, "Ended hand still offers commands.");
                SyncSameState(client.Match.Current, fixture.Server.Match.GetSnapshot(client.Match.Current.SelfSeat));
            }
        }
        finally { await fixture.Cleanup(); }
    }
}
