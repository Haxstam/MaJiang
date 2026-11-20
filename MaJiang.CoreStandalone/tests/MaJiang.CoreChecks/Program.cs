using MaJiangLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

internal static partial class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true };
    private static string Fingerprint<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    private static void Assert(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }

    private static MainMatchControl SyncCore()
    {
        MainMatchControl core = new();
        core.ServerFourMatchInit(new(MatchType.FourMahjongSouth), Profiles(), new Xoshiro256StarStar(Seed()));
        core.DoraPointerList.Add(core.PrimeTileList[0]);
        return core;
    }

    private static byte[] Seed() => Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
    private static UserProfile[] Profiles() => new[] { new UserProfile("A"), new UserProfile("B"), new UserProfile("C"), new UserProfile("D") };
    private static MahjongEngine Engine() => new(new(MatchType.FourMahjongSouth), Profiles(), Seed());

    private static async Task<int> Main()
    {
        int failures = DomainRegression.RunAll();
        MethodInfo[] checks = typeof(Program).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(method => method.ReturnType == typeof(Task) && (method.Name.StartsWith("Machine") || method.Name.StartsWith("Engine")))
            .OrderBy(method => method.Name).ToArray();
        int passed = 0;
        foreach (MethodInfo check in checks)
        {
            try
            {
                await (Task)check.Invoke(null, null);
                Console.WriteLine("PASS " + check.Name); passed++;
            }
            catch (Exception exception)
            {
                failures++; Console.Error.WriteLine("FAIL " + check.Name + ": " + (exception.InnerException ?? exception));
            }
        }
        Console.WriteLine($"{passed}/{checks.Length} action/engine checks passed (plus domain checks above).");
        return failures == 0 ? 0 : 1;
    }

    private static Task EngineQueriesStayStaticAndDetached()
    {
        MatchSettingData settings = new(MatchType.FourMahjongSouth);
        UserProfile[] profiles = Profiles(); byte[] seed = Seed();
        MahjongEngine engine = new(settings, profiles, seed);
        string before = Fingerprint(engine.GetState());
        settings.BasePoint = 1; profiles[0].Name = "changed"; seed[0] = 0;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            MatchState state = engine.GetState();
            state.State.MainTileList.Clear(); state.State.PlayerPoint[0] = -100;
            state.State.PlayerList[0].PlayerHandTile.HandTileList.Clear(); state.PendingSeats[0] = 3;
            VisibleMatchState view = engine.GetPlayerView(0);
            view.State.PlayerList[0].PlayerHandTile.HandTileList.Clear(); view.ForbiddenDiscards.Add(Tile.BytesTo("1w"));
            MatchCommand command = engine.GetAvailableCommands(0)[0]; command.Action.Tiles.Clear();
            engine.GetRecords(); engine.GetPlayerRecords(1);
        }
        Assert(before == Fingerprint(engine.GetState()), "Queries or returned aliases advanced/modified the engine.");
        return Task.CompletedTask;
    }

    private static Task EngineOnlyCommittedClaimsAreRecorded()
    {
        MachineFixture fixture = CallsFixture(); long before = fixture.Journal.LatestSequence;
        fixture.Act(1, PlayerAction.Chi);
        Assert(fixture.Journal.LatestSequence == before && fixture.Journal.GetRecords(fixture.Journal.RoundId, before).Count == 0,
            "An unconfirmed chi entered the journal.");
        Assert(fixture.Journal.GetSnapshot(1).HasResponded && !fixture.Journal.GetSnapshot(2).HasResponded,
            "Private response status was not retained.");
        fixture.Act(2, PlayerAction.Peng);
        MatchRecord[] applied = fixture.Journal.GetRecords(fixture.Journal.RoundId, before).ToArray();
        Assert(applied.Length == 1 && applied[0].Action.PlayerAction == PlayerAction.Peng && applied[0].Action.SelfNumber == 2,
            "The losing chi or intermediate intent entered the journal.");
        return Task.CompletedTask;
    }

    private static Task EngineRonCancelsPendingPonWithoutRecordingIt()
    {
        MachineFixture fixture = new(); fixture.Core.PlayerList[0].PlayerHandTile.SingleTile = Tile.BytesTo("3w");
        fixture.Hand(1, "1w 2w 4w 6w 8w 1p 2p 4p 5p 7s 8s 5z 6z");
        fixture.Hand(2, "3w 3w 4w 6w 8w 1p 2p 4p 5p 7s 8s 5z 6z");
        fixture.Hand(3, "1p 2p 3p 1s 2s 3s 7z 7z 7z 9p 9p 9p 3w");
        fixture.Start(); fixture.Act(0, PlayerAction.TakeCut); long before = fixture.Journal.LatestSequence;
        fixture.Act(2, PlayerAction.Peng); fixture.Act(3, PlayerAction.Ron);
        Assert(fixture.Journal.LatestSequence == before, "Pending responses were recorded before collection finished.");
        fixture.Act(1, PlayerAction.Skip);
        Assert(fixture.Journal.Outcome == RoundOutcomeKind.Ron && fixture.Core.PlayerOpenSetList[2].Count == 0,
            "Ron did not override pon.");
        Assert(fixture.Journal.GetRecords(fixture.Journal.RoundId, before).All(record => record.Action?.PlayerAction == PlayerAction.Ron),
            "An unsuccessful pon/skip entered the journal.");
        return Task.CompletedTask;
    }

    private static Task EngineRobbedKanDoesNotEnterJournal()
    {
        MachineFixture fixture = new(); fixture.Hand(0, "1w 2w 3w 4w 5w 6w 7s 8s 9s 5z", "5p");
        fixture.Open(0, new Group(GroupType.OpenTriple, Color.Tungs, Tiles("5p 5p"), 1, Tile.BytesTo("5p")));
        fixture.Hand(2, "1w 2w 3w 6p 7p 1s 2s 3s 7z 7z 7z 2z 2z"); fixture.Start();
        long before = fixture.Journal.LatestSequence; fixture.Act(0, PlayerAction.AddedKan);
        Assert(fixture.Journal.LatestSequence == before, "Tentative added kan entered the journal.");
        Assert(fixture.Journal.GetSnapshot(2).State.CurrentTile == Tile.BytesTo("5p"), "Rob-kan window lost public target.");
        fixture.Act(2, PlayerAction.Ron);
        Assert(!fixture.Journal.GetRecords(fixture.Journal.RoundId).Any(record => record.Action?.PlayerAction == PlayerAction.AddedKan),
            "Robbed kan was recorded as successful.");
        return Task.CompletedTask;
    }

    private static Task EngineRiichiOnlyRecordedWhenEstablished()
    {
        MachineFixture fixture = new(); fixture.Hand(0, YakuhaiWait, "9p");
        fixture.Hand(2, "1w 2w 3w 1p 2p 3p 1s 2s 3s 7z 7z 7z 9p"); fixture.Start();
        fixture.Act(0, PlayerAction.Riichi, action => action.TargetTile == Tile.BytesTo("9p"));
        Assert(!fixture.Journal.GetRecords(fixture.Journal.RoundId).Any(record => record.Action?.PlayerAction == PlayerAction.Riichi),
            "Unconfirmed riichi entered journal.");
        fixture.Act(2, PlayerAction.Ron);
        Assert(fixture.Journal.GetRecords(fixture.Journal.RoundId).Any(record => record.Action?.PlayerAction == PlayerAction.TakeCut) &&
            !fixture.Journal.GetRecords(fixture.Journal.RoundId).Any(record => record.Action?.PlayerAction == PlayerAction.Riichi),
            "Ronned declaration lost its discard or recorded an unsuccessful riichi.");
        MachineFixture success = new(); success.Hand(0, YakuhaiWait, "9p");
        success.Hand(1, "9p 9p 4w 6w 8w 1p 2p 4p 5p 7s 8s 5z 6z"); success.Start();
        success.Act(0, PlayerAction.Riichi, action => action.TargetTile == Tile.BytesTo("9p")); success.Act(1, PlayerAction.Peng);
        Assert(success.Journal.GetRecords(success.Journal.RoundId).Count(record => record.Action?.PlayerAction == PlayerAction.Riichi) == 1,
            "Successful riichi missing or duplicated.");
        return Task.CompletedTask;
    }

    private static Task EnginePrivacyAndRecordIsolation()
    {
        MahjongEngine engine = Engine();
        for (int seat = 0; seat < 4; seat++)
        {
            VisibleMatchState view = engine.GetPlayerView(seat);
            Assert(view.State.MainTileList == null && view.State.PrimeTileList == null && view.State.Random == null, "Hidden wall leaked.");
            for (int other = 0; other < 4; other++)
            {
                if (other == seat) { continue; }
                Assert(view.State.PlayerList[other].PlayerHandTile.HandTileList.Count == 0 &&
                    view.State.PlayerList[other].PlayerHandTile.SingleTile == null, "Other concealed hand leaked.");
            }
            MatchRecord draw = engine.GetPlayerRecords(seat).Single(record => record.Action?.PlayerAction == PlayerAction.Take);
            Assert((draw.Action.TargetTile != null) == (seat == 0), "Draw record leaked to another seat or hid own draw.");
            IReadOnlyList<PlayerActionData> visibleChoices = view.GetAvailableActions();
            Assert(visibleChoices.Count == engine.GetAvailableCommands(seat).Count, "Player menu differs from authority.");
        }
        MatchRecord original = engine.GetRecords().Single(record => record.Action?.PlayerAction == PlayerAction.Take);
        PlayerActionData payload = original.Action; payload.TargetTile = null; payload.Tiles.Add(Tile.BytesTo("1w"));
        Assert(original.Action.TargetTile != null && original.Action.Tiles.Count == 0, "Record was modified through its payload.");
        return Task.CompletedTask;
    }

    private static Task EngineFeedbackAndIdempotence()
    {
        MahjongEngine engine = Engine(); MatchCommand command = engine.GetAvailableCommands(0).First(item => item.Action.PlayerAction == PlayerAction.TakeCut);
        OperationFeedback feedback = engine.Submit(0, command);
        Assert(feedback.Accepted && feedback.Records.Any(record => record.Action?.PlayerAction == PlayerAction.TakeCut), "Applied operation missing in feedback.");
        string after = Fingerprint(engine.GetState());
        Assert(engine.Submit(0, command).Records.Count == 0 && after == Fingerprint(engine.GetState()), "Retry reexecuted an operation.");
        command.Action.TargetTile = Tile.BytesTo("1w");
        Assert(engine.Submit(0, command).Reason == "CommandIdReused" && after == Fingerprint(engine.GetState()), "Mutated retry changed state.");
        MatchCommand stale = engine.GetAvailableCommands(engine.GetState().PendingSeats[0])[0]; stale.PhaseId--;
        Assert(engine.Submit(stale.Action.SelfNumber, stale).Reason == "StalePhase" && after == Fingerprint(engine.GetState()), "Stale command advanced state.");
        Assert(!engine.StartNextRound().Accepted && after == Fingerprint(engine.GetState()), "Early next-round call advanced state.");
        return Task.CompletedTask;
    }

    private static OperationFeedback Step(MahjongEngine engine)
    {
        MatchState state = engine.GetState(); int seat = state.PendingSeats[0];
        IReadOnlyList<MatchCommand> choices = engine.GetAvailableCommands(seat);
        MatchCommand command = choices.FirstOrDefault(item => item.Action.PlayerAction == PlayerAction.Skip)
            ?? choices.FirstOrDefault(item => item.Action.PlayerAction == PlayerAction.TakeCut)
            ?? choices.First(item => item.Action.PlayerAction == PlayerAction.HandCut);
        OperationFeedback result = engine.Submit(seat, command);
        Assert(result.Accepted, "Explicit default-play choice rejected: " + result.Reason);
        return result;
    }

    private static Task EngineExplicitWholeMatchAndHistory()
    {
        MahjongEngine engine = Engine(); int commands = 0, rounds = 0;
        while (rounds++ < 100)
        {
            Guid roundId = engine.GetState().RoundId; MatchCommand previousCommand = null;
            while (engine.GetState().OperationStage != MatchOperationStage.RoundEnded && commands++ < 20000)
            {
                int seat = engine.GetState().PendingSeats[0]; previousCommand = engine.GetAvailableCommands(seat)[0];
                Step(engine);
            }
            MatchState ended = engine.GetState();
            Assert(ended.OperationStage == MatchOperationStage.RoundEnded && ended.Settlement?.RoundId == roundId, "No settled round after explicit play.");
            Assert(engine.GetRecords().Count(record => record.Kind == MatchRecordKind.RoundSettled) == 1, "Settlement missing or duplicated.");
            string before = Fingerprint(ended);
            for (int query = 0; query < 10; query++) { engine.GetPlayerView(query % 4); engine.GetAvailableCommands(query % 4); }
            Assert(before == Fingerprint(engine.GetState()), "Queries advanced the settled round.");
            if (previousCommand != null)
            {
                engine.Submit(previousCommand.Action.SelfNumber, previousCommand);
                Assert(before == Fingerprint(engine.GetState()), "Rejected/retried command repeated settlement.");
            }
            int recordCount = engine.GetRecords().Count;
            int[] points = ended.State.PlayerPoint.ToArray();
            if (ended.MatchEnded)
            {
                Assert(engine.StartNextRound().Reason == "MatchEnded" && before == Fingerprint(engine.GetState()), "Ended match restarted.");
                Assert(engine.RoundIds.Count == rounds && ended.State.PlayerPoint.Sum() + ended.State.RiichiStickCount * 1000 == 100000, "Final ledger/history mismatch.");
                return Task.CompletedTask;
            }
            Assert(engine.StartNextRound().Accepted, "Explicit next-round input rejected.");
            MatchState next = engine.GetState();
            Assert(next.RoundId != roundId && next.RoundGeneration == ended.RoundGeneration + 1 && points.SequenceEqual(next.State.PlayerPoint), "Next-round identity or points incorrect.");
            Assert(engine.GetRoundRecords(roundId).Count == recordCount, "Next round discarded old records.");
            if (previousCommand != null)
            {
                string fresh = Fingerprint(next);
                Assert(engine.Submit(previousCommand.Action.SelfNumber, previousCommand).Reason == "StaleRound" && fresh == Fingerprint(engine.GetState()), "Old-round command advanced next round.");
            }
        }
        throw new InvalidOperationException("Explicit match did not finish within bounds.");
    }

    private static Task EngineInvalidMetadataHasNoSideEffects()
    {
        MahjongEngine engine = Engine(); string before = Fingerprint(engine.GetState());
        MatchCommand invalid = engine.GetAvailableCommands(0)[0]; invalid.Action.TargetTile = new(Color.Honor, 9);
        Assert(engine.Submit(0, invalid).Reason == "InvalidCommand" && before == Fingerprint(engine.GetState()), "Malformed tile changed state.");
        Assert(engine.Submit(-1, invalid).Reason == "InvalidCommand" && before == Fingerprint(engine.GetState()), "Invalid seat changed state.");
        Assert(engine.Submit(0, null).Reason == "InvalidCommand" && before == Fingerprint(engine.GetState()), "Null input changed state.");
        return Task.CompletedTask;
    }

    private static MatchSession StartSession(MachineFixture fixture)
    {
        MainMatchControl core = fixture.Core;
        core.MainTileList[core.CurrentTileIndex] = core.PlayerList[core.CurrentPlayerIndex].PlayerHandTile.SingleTile;
        core.PlayerList[core.CurrentPlayerIndex].PlayerHandTile.SingleTile = null;
        fixture.Journal = new MatchJournal(core);
        MatchSession session = new(fixture.Journal);
        fixture.Machine = session.Machine;
        return session;
    }

    private static Task EngineWinningSettlementIsOnceAndNextRoundIsExplicit()
    {
        MachineFixture fixture = new(); fixture.Core.PlayerList[0].PlayerHandTile.SingleTile = Tile.BytesTo("5z");
        fixture.Hand(1, YakuhaiWait); fixture.Hand(2, YakuhaiWait);
        MatchSession session = StartSession(fixture);
        fixture.Act(0, PlayerAction.TakeCut); fixture.Act(1, PlayerAction.Ron); fixture.Act(2, PlayerAction.Ron);
        session.SettleIfEnded();
        MatchJournal journal = fixture.Journal;
        Assert(journal.Settlement.Winners.Count == 2 && journal.AuthoritativeState.PlayerPoint[0] < 25000,
            "Multi-ron did not enter actual session payments.");
        Assert(journal.AuthoritativeState.PlayerPoint.Sum() + journal.AuthoritativeState.RiichiStickCount * 1000 == 100000,
            "Winning session ledger changed total points.");
        string before = Fingerprint(journal.GetSnapshot(0));
        for (int call = 0; call < 10; call++) { session.SettleIfEnded(); journal.GetSnapshot(call % 4); }
        Assert(before == Fingerprint(journal.GetSnapshot(0)) && journal.GetRecords(journal.RoundId).Count(record => record.Kind == MatchRecordKind.RoundSettled) == 1,
            "Settlement repeated or advanced the round.");
        RoundSettlementResult result = journal.Settlement;
        long oldCount = journal.LatestSequence; Guid oldRound = journal.RoundId;
        AuthoritativeMatchMachine oldMachine = fixture.Machine;
        MatchCommand old = fixture.Command(0, new(0, 0, new(), null, PlayerAction.Skip));
        Assert(session.StartNextRound() == "Ok" && journal.RoundId != oldRound && journal.RoundGeneration == 2,
            "Explicit next round did not initialize.");
        Assert(journal.AuthoritativeState.PlayerPoint.SequenceEqual(result.PointsAfter) && journal.FuritenFlags.All(flag => flag == 0) &&
            journal.AuthoritativeState.IsRiichi.All(riichi => !riichi), "Next round lost points or kept private markers.");
        Assert(journal.GetRecords(oldRound).Count == oldCount + 1 && oldMachine.Submit(0, old).Reason == "RetiredMachine",
            "Old records disappeared or retired machine could still act.");
        return Task.CompletedTask;
    }

    private static Task EngineFinalWinningResultDoesNotRestart()
    {
        MachineFixture fixture = new(); fixture.Hand(0, YakuhaiWait, "5z");
        fixture.Core.Wind = WindType.South; fixture.Core.Round = 4;
        fixture.Core.PlayerPoint = new() { 40000, 20000, 20000, 20000 };
        MatchSession session = StartSession(fixture);
        fixture.Act(0, PlayerAction.Tsumo); session.SettleIfEnded();
        Assert(fixture.Journal.Settlement.MatchEnded, "Final dealer win did not end match.");
        string before = Fingerprint(fixture.Journal.GetSnapshot(0));
        Assert(session.StartNextRound() == "MatchEnded" && before == Fingerprint(fixture.Journal.GetSnapshot(0)),
            "Final winning result started another round.");
        return Task.CompletedTask;
    }

    private static Task EngineDoraFactsFollowCommittedActions()
    {
        MachineFixture fixture = AddedKanFixture(); fixture.Act(0, PlayerAction.AddedKan);
        long before = fixture.Journal.LatestSequence;
        fixture.Act(0, PlayerAction.TakeCut);
        MatchRecord[] facts = fixture.Journal.GetRecords(fixture.Journal.RoundId, before).ToArray();
        Assert(facts[0].Action?.PlayerAction == PlayerAction.TakeCut && facts[1].Kind == MatchRecordKind.DoraRevealed,
            "Delayed kan dora was logged before its triggering discard.");
        return Task.CompletedTask;
    }
}
